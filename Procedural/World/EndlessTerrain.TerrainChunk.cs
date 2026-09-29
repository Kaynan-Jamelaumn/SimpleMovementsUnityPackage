using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Jobs;
using static DataStructure;

// EndlessTerrain, part 2 of 2: one terrain chunk - its meshes, water, objects, NavMesh and visibility, and
// freeing all of it when it is unloaded (see EndlessTerrain.cs).
public partial class EndlessTerrain : MonoBehaviour
{
    /// <summary>
    /// Represents a single terrain chunk in the endless terrain system.
    /// Manages its mesh, texture, collision, navigation mesh, objects and spawner systems.
    ///
    /// Its life: generated on a worker thread (heights, water, mesh data, biomes, splat pixels) -> applied on the
    /// main thread (material, mesh, water; the collider is cooked on a job thread) -> objects decided on a worker
    /// thread and created a few per frame -> NavMesh built in the background once it is near the viewer -> mob and
    /// portal spawners started. <see cref="Unload"/> cancels whatever is still pending and destroys everything it made.
    /// </summary>
    public class TerrainChunk
    {
        bool shouldUseHDRPShaders = false;
        bool enableDebugging = false;

        readonly EndlessTerrain owner;
        readonly Vector2 coord;
        GameObject meshObject;
        Vector2 position;
        // The area the chunk covers (its mesh spans position .. position + size), for distances and visibility.
        Vector2 boundsMin, boundsMax;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        NavMeshSurface navMeshSurface;
        private TerrainGenerator terrainGenerator;
        public float[,] heightmap;

        /// <summary>
        /// Debug-only: per-cell erosion delta captured alongside <see cref="heightmap"/> when
        /// <see cref="TerrainGenerator.VisualizeErosionDebug"/> is enabled. Positive = erosion removed
        /// material at that cell, negative = erosion deposited material. Null otherwise.
        /// </summary>
        public float[,] erosionDeltaMap;

        /// <summary>Per-cell water (surface, type, shoreline level). Null when <see cref="TerrainGenerator.EnableWater"/> is off. See <see cref="WaterGenerator"/>.</summary>
        public WaterMapData waterData;

        // Distance LOD (see TerrainGenerator.LodForDistance): this chunk's mesh at each level of detail
        // (0-6) once built, and which one is showing. The full-detail mesh is also the collider's.
        Mesh baseMesh;
        readonly Mesh[] lodMeshes = new Mesh[7];
        readonly bool[] lodRequested = new bool[7];
        int currentLod = -1;

        GameObject waterObject;
        MeshFilter waterMeshFilter;
        MeshRenderer waterMeshRenderer;
        BoxCollider waterCollider;
        Mesh waterMesh;

        // Built-in fallback water materials (one per water type), shared by every chunk - unlike the
        // terrain material (which bakes per-chunk texture data into a unique Material instance), these
        // have nothing chunk-specific in them, so one shared instance each is fine.
        private static readonly Material[] fallbackWaterMaterials = new Material[WaterMeshData.SubmeshCount];
        private static readonly bool[] fallbackWaterMaterialAttempted = new bool[WaterMeshData.SubmeshCount];
        // The missing "Ground" tag is reported once, not per chunk.
        private static bool missingGroundTagReported;

        Vector2 globalOffset;
        float maxViewDistance;
        float scaleFactor = 1f;

        // Lifecycle: pending work is cancelled through the token when the chunk is unloaded.
        readonly WorkToken token = new WorkToken();
        bool unloaded;
        DataStructure.TerrainData generatedData;
        bool hasGeneratedData;
        PlacementResult placements;
        GameObject[] objects;
        PlacementInstantiator.Batch objectBatch;
        FarObjectSwitcher.Chunk farObjects;
        Dictionary<GameObject, int> objectIndices;
        bool objectsReady;
        Material terrainMaterial;
        JobHandle colliderBake;
        bool colliderBaking;
        // Generation Stats timestamps (see GenerationStats): when the chunk was requested, its collider scheduled, its NavMesh started.
        long requestedAt, colliderScheduledAt, navMeshStartedAt;
        NavMeshData navMeshData;
        AsyncOperation navMeshBuild;
        bool navMeshBuilt;
        bool spawnersStarted;
        PortalSpawner portalSpawner;
        MobSpawner mobSpawner;
        LoadedTerrain.Chunk loaded;

        public Vector2 Position { get { return position; } }
        /// <summary>The chunk's coordinate in the chunk grid.</summary>
        public Vector2 Coord { get { return coord; } }
        /// <summary>True once its objects exist (see <see cref="Objects"/>).</summary>
        public bool ObjectsReady { get { return objectsReady; } }
        /// <summary>The created objects by placement index (null until created, and where removed).</summary>
        public IReadOnlyList<GameObject> Objects { get { return objects; } }
        /// <summary>The chunk's GameObject (the parent of its water and objects).</summary>
        public GameObject GameObject { get { return meshObject; } }

        /// <param name="cachedData">The chunk's data from a previous visit (skips generating it again); null to generate it.</param>
        /// <param name="cachedPlacements">Its object placements from that visit.</param>
        public TerrainChunk(EndlessTerrain owner, Vector2 coord, int size, float scaleFactor, Transform parent, PortalSettings portalSettings, MobSettings mobSettings, int count,
            bool shouldUseHDRPShaders, bool enableDebugging, float maxViewDistance, DataStructure.TerrainData? cachedData = null, PlacementResult cachedPlacements = null)
        {
            this.owner = owner;
            this.coord = coord;
            this.shouldUseHDRPShaders = shouldUseHDRPShaders;
            this.enableDebugging = enableDebugging;
            this.maxViewDistance = maxViewDistance;
            this.scaleFactor = scaleFactor;

            position = coord * size;
            boundsMin = position;
            boundsMax = position + Vector2.one * (size * scaleFactor);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            // Fix: Use position directly for globalOffset, not position * scaleFactor
            globalOffset = position;

            if (enableDebugging)
                Debug.Log($"Creating TerrainChunk {count} at coord: {coord}, position: {position}, size: {size}, scaleFactor: {scaleFactor}, globalOffset: {globalOffset}");

            meshObject = new GameObject("Terrain Chunk" + count);
            // Used by TerrainMonitor and gameplay code to recognise the ground. An undefined tag throws, which would
            // abort the chunk: warn once instead (add "Ground" under Project Settings > Tags and Layers).
            try
            {
                meshObject.tag = "Ground";
            }
            catch (UnityException)
            {
                if (!missingGroundTagReported)
                {
                    missingGroundTagReported = true;
                    Debug.LogWarning("EndlessTerrain: the 'Ground' tag isn't defined - terrain chunks stay Untagged. Add it under Project Settings > Tags and Layers.");
                }
            }
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            navMeshSurface = meshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;

            // The chunk's portals and mobs (started by StartSpawners once the chunk is ready, stopped by Unload).
            portalSpawner = meshObject.AddComponent<PortalSpawner>();
            portalSpawner.SetSettings(portalSettings);
            mobSpawner = meshObject.AddComponent<MobSpawner>();
            mobSpawner.SetSettings(mobSettings);

            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);

            requestedAt = GenerationStats.Start();
            if (cachedData.HasValue)
            {
                // Back from the data cache: only the main-thread part is left (still within the per-frame budget).
                DataStructure.TerrainData data = cachedData.Value;
                mapGenerator.RunOnMainThread(() =>
                {
                    if (!unloaded)
                        OnTerrainDataReceived(data, cachedPlacements);
                });
            }
            else
            {
                mapGenerator.RequestChunkData(data => OnTerrainDataReceived(data, null), globalOffset, DistanceToViewer, token);
            }
        }

        /// <summary>
        /// The chunk's generated data arrived (main thread): material, mesh, collider, water - then its objects
        /// are decided (or taken from <paramref name="cachedPlacements"/>).
        /// </summary>
        void OnTerrainDataReceived(DataStructure.TerrainData terrainData, PlacementResult cachedPlacements)
        {
            if (unloaded)
                return;
            if (enableDebugging)
                Debug.Log($"OnTerrainDataReceived for chunk at {globalOffset}");
            long applyStart = GenerationStats.Start();
            long stage = applyStart;

            terrainGenerator = terrainData.terrainGenerator;
            heightmap = terrainData.heightMap;
            erosionDeltaMap = terrainData.erosionDeltaMap;
            waterData = terrainData.waterData;

            // Material: the biome textures every chunk shares, plus this chunk's splat maps and wetness map.
            if (terrainData.splatPixels != null)
            {
                terrainMaterial = TextureGenerator.CreateChunkMaterial(terrainData.splatPixels, terrainGenerator.ChunkSize, terrainGenerator, shouldUseHDRPShaders);
                if (terrainMaterial != null)
                {
                    meshRenderer.sharedMaterial = terrainMaterial;
                    ApplyWetnessMap(terrainData.waterData);
                }
                GenerationStats.Record(GenerationStats.ApplyMaterial, stage);
            }

            stage = GenerationStats.Start();
            Mesh mesh = terrainData.meshData.UpdateMesh();
            GenerationStats.Record(GenerationStats.ApplyMesh, stage);

            if (enableDebugging)
                Debug.Log($"Mesh stats - Vertices: {mesh.vertexCount}, Bounds: {mesh.bounds}");

            if (mesh != null && mesh.vertexCount > 0)
            {
                meshFilter.sharedMesh = mesh;
                baseMesh = mesh;
                currentLod = Mathf.Clamp(terrainGenerator.LevelOfDetail, 0, lodMeshes.Length - 1);
                lodMeshes[currentLod] = mesh;
                lodRequested[currentLod] = true;

                // Cooking the collider is the slowest part of applying a chunk, so it runs on a job thread, with the
                // same cooking options the collider is given; the collider gets the mesh once it is done (see
                // UpdateTerrainChunk), so it finds the pre-baked data and nothing is cooked on the main thread.
                colliderBake = MeshColliderBaker.Schedule(mesh);
                colliderBaking = true;
                colliderScheduledAt = GenerationStats.Start();
            }
            else
            {
                Debug.LogError($"Invalid mesh generated for chunk at {globalOffset}");
            }

            stage = GenerationStats.Start();
            UpdateWaterMesh(terrainData);
            if (terrainData.waterMeshData != null)
                GenerationStats.Record(GenerationStats.ApplyWater, stage);
            if (IsVisible())
                UpdateLod(LodDistance());

            // Kept for the data cache (see EndlessTerrain) - without what is only needed once.
            generatedData = terrainData;
            generatedData.splatBlend = null;
            hasGeneratedData = true;

            loaded = new LoadedTerrain.Chunk
            {
                Coord = new Vector2Int((int)coord.x, (int)coord.y),
                OriginX = Mathf.RoundToInt(globalOffset.x),
                OriginZ = Mathf.RoundToInt(globalOffset.y),
                Span = terrainGenerator.ChunkSize - 1,
                LodFactor = terrainGenerator.LevelOfDetail > 0 ? terrainGenerator.LevelOfDetail * 2 : 1,
                Heights = heightmap,
                Water = waterData,
                Biomes = terrainData.biomeMap,
                Root = meshObject.transform,
            };
            loaded.IsRemoved = index => owner.IsRemoved(coord, index);
            LoadedTerrain.Register(loaded);

            if (cachedPlacements != null)
                OnPlacementsReady(cachedPlacements);
            else
                mapGenerator.RequestObjectPlacement(OnPlacementsReady, terrainData, DistanceToViewer, token);
            // The placement job has what it needs; the environment it reads is dropped with it.
            generatedData.placementFields = null;

            GenerationStats.Record(GenerationStats.ApplyTotal, applyStart);
            GenerationStats.Record(GenerationStats.ChunkVisible, requestedAt);
            GenerationStats.Count(GenerationStats.ChunksApplied);
            GenerationStats.MarkChunkVisible();
        }

        /// <summary>The chunk's objects were decided: create them a few per frame, nearest chunks first.</summary>
        void OnPlacementsReady(PlacementResult result)
        {
            if (unloaded)
                return;
            placements = result;
            if (loaded != null)
            {
                loaded.Placements = result;
                loaded.Plan = result.Plan;
            }
            objectBatch = mapGenerator.ObjectInstantiator.Enqueue(meshObject.transform, result.Plan, result, DistanceToViewer, token,
                index => owner.IsRemoved(coord, index), OnObjectsCreated);
        }

        void OnObjectsCreated(PlacementInstantiator.Batch batch)
        {
            if (unloaded)
                return;
            objects = batch.Created;
            objectIndices = new Dictionary<GameObject, int>(objects.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objectIndices[objects[i]] = i;
            }
            if (loaded != null)
                loaded.Objects = objects;
            objectsReady = true;
            GenerationStats.Record(GenerationStats.ChunkObjects, requestedAt);
            GenerationStats.MarkObjectsCreated();
            // Only chunks near the viewer keep their full objects (see Far Objects).
            farObjects = mapGenerator.FarObjects.Track(meshObject.transform, objects, DistanceToViewer);
            if (enableDebugging)
                Debug.Log($"Chunk at {globalOffset}: {objectIndices.Count} objects created");
        }

        /// <summary>The generated data of a fully generated chunk (for the data cache).</summary>
        public bool TryGetGeneratedData(out DataStructure.TerrainData data, out PlacementResult placementResult)
        {
            data = generatedData;
            placementResult = placements;
            return hasGeneratedData && placements != null;
        }

        /// <summary>The placement index of one of this chunk's objects.</summary>
        public bool TryGetObjectIndex(GameObject placedObject, out int index)
        {
            index = -1;
            return objectIndices != null && objectIndices.TryGetValue(placedObject, out index);
        }

        /// <summary>Forgets an object that is being destroyed (see <see cref="EndlessTerrain.RemovePlacedObject"/>).</summary>
        public void ForgetObject(int index)
        {
            if (objects == null || index < 0 || index >= objects.Length)
                return;
            if (objects[index] != null)
                objectIndices.Remove(objects[index]);
            objects[index] = null;
        }

        /// <summary>Distance from the viewer to the nearest point of the chunk (thread-safe: plain arithmetic, used as the chunk's work priority).</summary>
        public float DistanceToViewer()
        {
            Vector2 viewer = viewerPosition;
            float dx = Mathf.Max(0f, Mathf.Max(boundsMin.x - viewer.x, viewer.x - boundsMax.x));
            float dy = Mathf.Max(0f, Mathf.Max(boundsMin.y - viewer.y, viewer.y - boundsMax.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance from the viewer to the nearest point of the chunk's mesh, which decides its level of detail.</summary>
        float LodDistance()
        {
            return DistanceToViewer();
        }

        /// <summary>
        /// Shows the mesh for the level of detail this distance calls for, requesting it (built in the
        /// background) the first time it is needed; the current mesh stays until the new one is ready.
        /// </summary>
        void UpdateLod(float distance)
        {
            if (baseMesh == null || terrainGenerator == null)
                return;

            int lod = Mathf.Clamp(terrainGenerator.LodForDistance(distance), 0, lodMeshes.Length - 1);
            if (lod == currentLod)
                return;

            if (lodMeshes[lod] != null)
            {
                meshFilter.sharedMesh = lodMeshes[lod];
                currentLod = lod;
            }
            else if (!lodRequested[lod])
            {
                lodRequested[lod] = true;
                terrainGenerator.RequestLodMesh(meshData => OnLodMeshReceived(lod, meshData), heightmap, waterData, lod, globalOffset, LodDistance, token);
            }
        }

        void OnLodMeshReceived(int lod, MeshData meshData)
        {
            if (unloaded)
                return;
            Mesh mesh = meshData.UpdateMesh();
            mesh.name = "Terrain Mesh LOD " + lod;
            lodMeshes[lod] = mesh;
            if (IsVisible())
                UpdateLod(LodDistance());
        }

        /// <summary>
        /// Gives the terrain material a "_WetnessMap" texture (one texel per height map cell, laid out like the
        /// splat maps - sample it with the same UV) from the chunk's ground wetness near water, for a terrain shader
        /// that darkens and adds gloss to wet ground (the package shader does). The same value is also in the
        /// terrain mesh's vertex color (red). Only created when the material's shader has _WetnessMap.
        /// </summary>
        void ApplyWetnessMap(WaterMapData water)
        {
            Material material = meshRenderer.sharedMaterial;
            if (material == null || water == null || !material.HasProperty("_WetnessMap"))
                return;

            int size = water.Size;
            bool r8 = SystemInfo.SupportsTextureFormat(TextureFormat.R8);
            var texture = new Texture2D(size, size, r8 ? TextureFormat.R8 : TextureFormat.RGBA32, false, true)
            {
                name = "Wetness",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if (r8)
            {
                var values = new byte[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        values[y * size + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(water.Wetness[x, y]) * 255f);
                texture.SetPixelData(values, 0);
            }
            else
            {
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(water.Wetness[x, y]) * 255f);
                        pixels[y * size + x] = new Color32(value, value, value, 255);
                    }
                }
                texture.SetPixels32(pixels);
            }
            texture.Apply(false, true);
            // Freed with the material (see TextureGenerator.ReleaseChunkMaterial).
            material.SetTexture("_WetnessMap", texture);
        }

        /// <summary>
        /// Builds (or updates) this chunk's water GameObject from the water map computed alongside its
        /// heightmap - see <see cref="HeightGenerator"/>/<see cref="WaterGenerator"/>. One mesh, one
        /// submesh (and material) per water type. A no-op if water is disabled or this chunk is dry.
        /// </summary>
        void UpdateWaterMesh(DataStructure.TerrainData terrainData)
        {
            if (!terrainGenerator.EnableWater || terrainData.waterData == null)
                return;

            // Normally built on the worker thread with the rest of the chunk.
            WaterMeshData waterMeshData = terrainData.waterMeshData ?? MeshGenerator.GenerateWaterMesh(terrainGenerator, terrainData.heightMap, terrainData.waterData, terrainGenerator.LevelOfDetail, globalOffset);
            if (waterMeshData == null)
                return;

            waterMesh = waterMeshData.BuildMesh();

            if (waterObject == null)
            {
                waterObject = new GameObject("Water");
                waterObject.transform.parent = meshObject.transform;
                waterObject.transform.localPosition = Vector3.zero;
                waterObject.transform.localRotation = Quaternion.identity;

                waterMeshFilter = waterObject.AddComponent<MeshFilter>();
                waterMeshRenderer = waterObject.AddComponent<MeshRenderer>();
                waterMeshRenderer.sharedMaterials = GetWaterMaterials(terrainGenerator);

                if (terrainGenerator.EnableSwimDetection)
                {
                    waterCollider = waterObject.AddComponent<BoxCollider>();
                    waterCollider.isTrigger = true;
                    waterObject.AddComponent<WaterVolume>();
                }

                // Best-effort: the consuming project may not have defined a "Water" tag, and an
                // undefined tag throws rather than silently no-opping - water still works fine
                // untagged, so this is guarded rather than allowed to abort chunk setup entirely.
                try
                {
                    waterObject.tag = "Water";
                }
                catch (UnityException)
                {
                    if (enableDebugging)
                        Debug.LogWarning("TerrainChunk: no 'Water' tag defined in this project - water objects will stay 'Untagged'. Add a 'Water' tag under Project Settings > Tags and Layers if you want to filter/query by it.");
                }
            }

            waterMeshFilter.sharedMesh = waterMesh;

            if (waterCollider != null)
            {
                UpdateWaterColliderBounds(waterCollider, terrainData.heightMap, terrainData.waterData);
            }
        }

        /// <summary>
        /// Sizes this chunk's water trigger volume as a single axis-aligned box spanning the chunk's
        /// full XZ footprint and just tall enough (from the lowest water bed to the highest water
        /// surface actually present) to contain every wet cell. This is a coarse approximation of the
        /// water body's true shape, not an exact fit - see <see cref="WaterVolume"/>'s remarks for why
        /// that's an acceptable tradeoff here (a universally trigger-compatible primitive collider
        /// rather than a non-convex mesh collider, whose trigger support varies by Unity version).
        /// </summary>
        void UpdateWaterColliderBounds(BoxCollider collider, float[,] heightMap, WaterMapData water)
        {
            int width = water.Size;
            int depth = water.Size;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            bool anyWater = false;

            for (int y = 0; y < depth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!water.IsWet(x, y))
                        continue;

                    anyWater = true;
                    float surface = water.Surface[x, y];
                    if (surface > maxY) maxY = surface;

                    // A wet cell's terrain is always below its water surface - it's that cell's floor.
                    float bed = heightMap[x, y];
                    if (bed < minY) minY = bed;
                }
            }

            if (!anyWater)
            {
                collider.enabled = false;
                return;
            }

            collider.enabled = true;

            float chunkWorldSize = (width - 1) * scaleFactor;
            float centerY = (minY + maxY) * 0.5f;

            collider.center = new Vector3(chunkWorldSize * 0.5f, centerY, chunkWorldSize * 0.5f);
            collider.size = new Vector3(chunkWorldSize, Mathf.Max(0.1f, maxY - minY), chunkWorldSize);
        }

        /// <summary>
        /// One material per water mesh submesh (ocean, lake, pond, river - see <see cref="WaterMeshData.SubmeshIndex"/>):
        /// whatever the TerrainGenerator assigns for that type, otherwise a simple built-in transparent
        /// fallback tinted per type (created once and shared by every chunk).
        /// </summary>
        static Material[] GetWaterMaterials(TerrainGenerator terrainGenerator)
        {
            Material[] materials = new Material[WaterMeshData.SubmeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                WaterBodyType type = WaterMeshData.SubmeshType(i);
                Material assigned = terrainGenerator.GetWaterMaterial(type);
                materials[i] = assigned != null ? assigned : GetFallbackWaterMaterial(i);
            }
            return materials;
        }

        static Material GetFallbackWaterMaterial(int submesh)
        {
            if (fallbackWaterMaterials[submesh] == null && !fallbackWaterMaterialAttempted[submesh])
            {
                fallbackWaterMaterialAttempted[submesh] = true;
                fallbackWaterMaterials[submesh] = CreatePackageWaterMaterial(submesh) ?? CreateFallbackWaterMaterial(FallbackWaterColor(submesh));
            }

            return fallbackWaterMaterials[submesh];
        }

        // Fully qualified: this file also has a stray `using System.Drawing;`, whose Color type would
        // otherwise make the bare `Color` identifier ambiguous with UnityEngine.Color (see the same fix in
        // DrawErosionGizmos below).
        static UnityEngine.Color FallbackWaterColor(int submesh)
        {
            switch (WaterMeshData.SubmeshType(submesh))
            {
                case WaterBodyType.Ocean: return new UnityEngine.Color(0.06f, 0.24f, 0.42f, 0.78f); // deep blue, most opaque
                case WaterBodyType.Lake: return new UnityEngine.Color(0.10f, 0.34f, 0.42f, 0.62f);  // clear blue-teal
                case WaterBodyType.Pond: return new UnityEngine.Color(0.20f, 0.34f, 0.24f, 0.66f);  // murky green
                case WaterBodyType.Waterfall: return new UnityEngine.Color(0.78f, 0.86f, 0.90f, 0.72f); // white water
                default: return new UnityEngine.Color(0.22f, 0.44f, 0.50f, 0.55f);                  // river: lighter, shallower
            }
        }

        /// <summary>
        /// The package's own water shader ("SimpleMovements/Water", in Water/Resources): shallow-to-deep colour,
        /// waves, ripples that follow the river flow and foam on shores, rapids and waterfalls, all read from the
        /// water mesh. It supports URP and the Built-in pipeline; null under HDRP (or if the shader is missing),
        /// so the plain fallback below is used instead.
        /// </summary>
        static Material CreatePackageWaterMaterial(int submesh)
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && !pipeline.GetType().Name.Contains("Universal"))
                return null;

            Shader shader = Shader.Find("SimpleMovements/Water");
            if (shader == null || !shader.isSupported)
                return null;

            UnityEngine.Color deep = FallbackWaterColor(submesh);
            UnityEngine.Color shallow = UnityEngine.Color.Lerp(deep, new UnityEngine.Color(0.45f, 0.75f, 0.72f, 0.4f), 0.55f);
            Material material = new Material(shader) { name = "Water (" + WaterMeshData.SubmeshType(submesh) + ")" };
            material.SetColor("_DeepColor", deep);
            material.SetColor("_ShallowColor", shallow);
            switch (WaterMeshData.SubmeshType(submesh))
            {
                case WaterBodyType.Ocean:
                    material.SetFloat("_DepthRange", 0.8f);
                    break;
                case WaterBodyType.River:
                    material.SetFloat("_DepthRange", 0.3f);
                    break;
                case WaterBodyType.Waterfall:
                    material.SetFloat("_ForceFoam", 1f);
                    material.SetFloat("_FlowSpeed", 1.4f);
                    break;
            }
            return material;
        }

        /// <summary>
        /// Tries a small list of shaders, in order, that are likely to exist depending on the host
        /// project's render pipeline (URP, Built-in/Standard, or an ancient/stripped project that only
        /// has the truly universal legacy/unlit ones), so this works out of the box without knowing
        /// which pipeline the consuming project uses. Used when the package's own water shader (see
        /// <see cref="CreatePackageWaterMaterial"/>) can't be: under HDRP, or if it was removed. Assign
        /// materials on the TerrainGenerator for full control over the look (reflections, refraction, etc).
        /// </summary>
        static Material CreateFallbackWaterMaterial(UnityEngine.Color waterColor)
        {
            string[] candidateShaders =
            {
                "Universal Render Pipeline/Lit",
                "Standard",
                "Legacy Shaders/Transparent/Diffuse",
                "Unlit/Transparent",
                "Sprites/Default",
            };

            foreach (string shaderName in candidateShaders)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                    continue;

                Material material = new Material(shader) { color = waterColor };

                // Property names vary per shader/pipeline, so each is set only if actually present
                // rather than assumed - these are best-effort transparency hints, not required for the
                // material to work at all.
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", waterColor);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); // URP: 1 = Transparent
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f); // Standard shader: 3 = Transparent
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                return material;
            }

            Debug.LogWarning("TerrainChunk: none of the fallback shaders (URP/Standard/legacy/unlit/sprite) were found - water will render with Unity's default material. Assign water materials on the TerrainGenerator to fix this.");
            return null;
        }

        /// <summary>
        /// Builds the chunk's NavMesh in the background once its objects exist and it is near the viewer (see
        /// EndlessTerrain's NavMesh settings), then starts its mob and portal spawners.
        /// </summary>
        void UpdateNavMesh()
        {
            if (!objectsReady || spawnersStarted)
                return;
            if (!owner.bakeNavMesh || navMeshSurface == null)
            {
                StartSpawners();
                return;
            }

            if (navMeshBuild != null)
            {
                if (!navMeshBuild.isDone)
                    return;
                GenerationStats.Record(GenerationStats.NavMesh, navMeshStartedAt);
                navMeshBuild = null;
                navMeshBuilt = true;
                if (loaded != null)
                    loaded.HasNavMesh = true;
                StartSpawners();
                return;
            }

            float limit = owner.navMeshDistance > 0f ? owner.navMeshDistance : maxViewDistance;
            if (navMeshBuilt || !IsVisible() || DistanceToViewer() > limit)
                return;

            if (navMeshSurface.navMeshData == null)
            {
                navMeshData = new NavMeshData(navMeshSurface.agentTypeID)
                {
                    name = meshObject.name + " NavMesh",
                    position = meshObject.transform.position,
                    rotation = meshObject.transform.rotation,
                };
                navMeshSurface.navMeshData = navMeshData;
                navMeshSurface.AddData();
            }

            // Always from the full-detail mesh, whatever level distance LOD is showing (the sources are gathered
            // right here; the build itself runs in the background).
            Mesh shown = meshFilter.sharedMesh;
            if (baseMesh != null)
                meshFilter.sharedMesh = baseMesh;
            // A NavMesh built from colliders must see the objects' colliders, even while the chunk is far.
            bool fromColliders = farObjects != null && navMeshSurface.useGeometry == NavMeshCollectGeometry.PhysicsColliders;
            if (fromColliders)
                mapGenerator.FarObjects.SetSwitchedCollidersEnabled(farObjects, true);
            navMeshStartedAt = GenerationStats.Start();
            navMeshBuild = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            if (fromColliders)
                mapGenerator.FarObjects.SetSwitchedCollidersEnabled(farObjects, false);
            meshFilter.sharedMesh = shown;
        }

        void StartSpawners()
        {
            spawnersStarted = true;
            if (enableDebugging)
                Debug.Log($"Starting spawners for chunk at {globalOffset}");

            // The chunk's data, objects and NavMesh exist: its spawners can judge the ground (see LoadedTerrain).
            var chunkCoord = new Vector2Int((int)coord.x, (int)coord.y);
            float span = terrainGenerator.ChunkSize - 1;
            if (portalSpawner != null)
                portalSpawner.Begin(owner, terrainGenerator, chunkCoord, globalOffset, span);
            if (mobSpawner != null)
                mobSpawner.Begin(owner, terrainGenerator, chunkCoord, globalOffset, span);
        }

        /// <summary>
        /// Updates the visibility of this chunk based on its distance to the viewer, and moves along its pending
        /// work (level of detail, collider, NavMesh).
        /// </summary>
        public void UpdateTerrainChunk()
        {
            if (unloaded)
                return;

            float distance = DistanceToViewer();
            bool visible = distance <= maxViewDistance;
            SetVisible(visible);
            if (visible)
                UpdateLod(LodDistance());
            // Hidden chunks' objects are inactive anyway: they are switched when the chunk shows again.
            if (visible && farObjects != null && mapGenerator != null)
                mapGenerator.FarObjects.Refresh(farObjects, distance);

            if (colliderBaking && colliderBake.IsCompleted)
            {
                colliderBake.Complete();
                colliderBaking = false;
                if (baseMesh != null)
                    MeshColliderBaker.Assign(meshCollider, baseMesh);   // already cooked: no cooking on the main thread
                GenerationStats.Record(GenerationStats.ColliderReady, colliderScheduledAt);
            }

            UpdateNavMesh();
        }

        public void SetVisible(bool visible)
        {
            if (meshObject != null && meshObject.activeSelf != visible)
                meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject != null && meshObject.activeSelf;
        }

        /// <summary>
        /// Destroys the chunk and frees everything it made: its GameObject with its objects, water and spawners,
        /// every mesh (all levels of detail and the water), its material with its splat and wetness maps, and
        /// its NavMesh. Pending background work for it is cancelled and its results are dropped.
        /// </summary>
        public void Unload()
        {
            if (unloaded)
                return;
            unloaded = true;
            token.Cancel();
            // Spawners first: they remove their portals and mobs without counting them as killed or used.
            if (portalSpawner != null)
                portalSpawner.End();
            if (mobSpawner != null)
                mobSpawner.End();
            LoadedTerrain.Unregister(new Vector2Int((int)coord.x, (int)coord.y));

            if (colliderBaking)
            {
                colliderBake.Complete();
                colliderBaking = false;
            }

            if (navMeshSurface != null)
            {
                if (navMeshBuild != null && !navMeshBuild.isDone && navMeshSurface.navMeshData != null)
                    NavMeshBuilder.Cancel(navMeshSurface.navMeshData);
                navMeshSurface.RemoveData();
                navMeshSurface.navMeshData = null;
            }
            DestroyAsset(navMeshData);
            navMeshData = null;
            navMeshBuild = null;

            if (meshCollider != null)
                meshCollider.sharedMesh = null;
            for (int i = 0; i < lodMeshes.Length; i++)
            {
                if (lodMeshes[i] != null && lodMeshes[i] != baseMesh)
                    DestroyAsset(lodMeshes[i]);
                lodMeshes[i] = null;
            }
            DestroyAsset(baseMesh);
            baseMesh = null;
            DestroyAsset(waterMesh);
            waterMesh = null;

            TextureGenerator.ReleaseChunkMaterial(terrainMaterial);
            terrainMaterial = null;

            ReleaseObjects();
            if (meshObject != null)
                Object.Destroy(meshObject);
            meshObject = null;

            objects = null;
            objectIndices = null;
            heightmap = null;
            erosionDeltaMap = null;
            waterData = null;
            generatedData = default;
            placements = null;
            loaded = null;
        }

        /// <summary>Hands the chunk's objects (also those of a batch still being created) back for reuse (see Object Pooling).</summary>
        void ReleaseObjects()
        {
            // Far objects get back what was switched off, after they are pooled (inactive) and with the chunk switched
            // off first, so restoring doesn't wake any script up (the chunk is destroyed right after anyway).
            FarObjectSwitcher.Chunk far = farObjects;
            farObjects = null;
            bool restore = far != null && far.HasChanges;
            if (restore && meshObject != null)
                meshObject.SetActive(false);

            GameObject[] created = objects ?? objectBatch?.Created;
            objectBatch = null;
            if (created != null && placements != null && placements.Plan != null && mapGenerator != null)
            {
                PlacementInstantiator instantiator = mapGenerator.ObjectInstantiator;
                Transform chunk = meshObject != null ? meshObject.transform : null;
                for (int i = 0; i < created.Length && i < placements.Objects.Count; i++)
                {
                    GameObject instance = created[i];
                    // Objects the game moved out of the chunk (picked up, re-parented) are no longer the chunk's to reuse.
                    if (instance == null || instance.transform.parent != chunk)
                        continue;
                    int type = placements.Objects[i].Type;
                    instantiator.Release(type < placements.Plan.Types.Length ? placements.Plan.Types[type].Prefab : null, instance);
                    created[i] = null;
                }
            }

            if (far != null && mapGenerator != null)
                mapGenerator.FarObjects.Remove(far, restore);
        }

        static void DestroyAsset(Object asset)
        {
            if (asset != null)
                Object.Destroy(asset);
        }

        /// <summary>
        /// Draws one Scene-view gizmo cube per sampled cell where erosion changed this chunk's height
        /// by more than <see cref="TerrainGenerator.ErosionDebugMinDelta"/>: red/orange where erosion
        /// removed material, blue/cyan where it deposited material, with color intensity and cube size
        /// both scaling toward <see cref="TerrainGenerator.ErosionDebugMaxDelta"/>. Only called when
        /// <see cref="TerrainGenerator.VisualizeErosionDebug"/> is enabled (see <see cref="EndlessTerrain.OnDrawGizmos"/>).
        /// </summary>
        public void DrawErosionGizmos(TerrainGenerator generator)
        {
            if (erosionDeltaMap == null || heightmap == null || meshObject == null || !meshObject.activeSelf)
                return;

            int width = erosionDeltaMap.GetLength(0);
            int depth = erosionDeltaMap.GetLength(1);
            int stride = Mathf.Max(1, generator.ErosionDebugStride);
            float minDelta = Mathf.Max(0f, generator.ErosionDebugMinDelta);
            float maxDelta = Mathf.Max(0.0001f, generator.ErosionDebugMaxDelta);
            float baseSize = Mathf.Max(0.01f, generator.ErosionDebugGizmoSize);
            float heightOffset = generator.ErosionDebugHeightOffset;
            int maxGizmos = Mathf.Max(0, generator.ErosionDebugMaxGizmosPerChunk);

            Vector3 origin = meshObject.transform.position;
            int drawn = 0;

            for (int y = 0; y < depth && drawn < maxGizmos; y += stride)
            {
                for (int x = 0; x < width && drawn < maxGizmos; x += stride)
                {
                    float delta = erosionDeltaMap[x, y];
                    float magnitude = Mathf.Abs(delta);
                    if (magnitude < minDelta)
                        continue;

                    float t = Mathf.Clamp01(magnitude / maxDelta);

                    // Erosion (material removed): orange -> red. Deposition (material added): cyan -> blue.
                    // Fully qualified: this file also has a stray `using System.Drawing;`, whose Color type
                    // would otherwise make the bare `Color` identifier ambiguous with UnityEngine.Color.
                    Gizmos.color = delta > 0f
                        ? UnityEngine.Color.Lerp(new UnityEngine.Color(1f, 0.75f, 0f, 0.55f), new UnityEngine.Color(1f, 0f, 0f, 0.95f), t)
                        : UnityEngine.Color.Lerp(new UnityEngine.Color(0f, 0.85f, 1f, 0.55f), new UnityEngine.Color(0.1f, 0.1f, 1f, 0.95f), t);

                    float worldHeight = heightmap[x, y];
                    Vector3 worldPos = origin + new Vector3(x * scaleFactor, worldHeight + heightOffset, y * scaleFactor);
                    float cubeSize = Mathf.Max(0.02f, Mathf.Lerp(baseSize * 0.35f, baseSize, t) * scaleFactor);

                    Gizmos.DrawCube(worldPos, Vector3.one * cubeSize);
                    drawn++;
                }
            }
        }
    }
}
