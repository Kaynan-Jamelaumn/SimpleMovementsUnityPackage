using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.AI.Navigation;
using System.Drawing;
using static DataStructure;

// EndlessTerrain, part 2 of 2: one terrain chunk - its meshes, water, objects and visibility (see EndlessTerrain.cs).
public partial class EndlessTerrain : MonoBehaviour
{
    /// <summary>
    /// Represents a single terrain chunk in the endless terrain system.
    /// Manages its mesh, texture, collision, navigation mesh, and spawner systems dynamically.
    /// </summary>
    public class TerrainChunk
    {
        bool shouldUseHDRPShaders = false;
        bool enableDebugging = false;

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

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
        // The area the mesh actually covers (bounds above is centered on the chunk's corner), for LOD distances.
        Bounds meshBounds;
        readonly Mesh[] lodMeshes = new Mesh[7];
        readonly bool[] lodRequested = new bool[7];
        int currentLod = -1;

        GameObject waterObject;
        MeshFilter waterMeshFilter;
        MeshRenderer waterMeshRenderer;
        BoxCollider waterCollider;

        // Built-in fallback water materials (one per water type), shared by every chunk - unlike the
        // terrain material (which bakes per-chunk texture data into a unique Material instance), these
        // have nothing chunk-specific in them, so one shared instance each is fine.
        private static readonly Material[] fallbackWaterMaterials = new Material[WaterMeshData.SubmeshCount];
        private static readonly bool[] fallbackWaterMaterialAttempted = new bool[WaterMeshData.SubmeshCount];

        Vector2 globalOffset;
        int maxMobs;
        float maxViewDistance;
        float scaleFactor = 1f;
        public Vector2 Position { get { return position; } }

        public TerrainChunk(Vector2 coord, int size, float scaleFactor, Transform parent, PortalSettings portalSettings, MobSettings mobSettings, int count, bool shouldUseHDRPShaders, bool enableDebugging, float maxViewDistance)
        {
            this.shouldUseHDRPShaders |= shouldUseHDRPShaders;
            this.enableDebugging = enableDebugging;
            this.maxViewDistance = maxViewDistance;
            this.scaleFactor = scaleFactor;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            meshBounds = new Bounds(position + Vector2.one * (size * scaleFactor * 0.5f), Vector2.one * (size * scaleFactor));
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            // Fix: Use position directly for globalOffset, not position * scaleFactor
            globalOffset = position;

            if (enableDebugging)
                Debug.Log($"Creating TerrainChunk {count} at coord: {coord}, position: {position}, size: {size}, scaleFactor: {scaleFactor}, globalOffset: {globalOffset}");

            meshObject = new GameObject("Terrain Chunk" + count);
            meshObject.tag = "Ground";
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();


            navMeshSurface = meshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;


            PortalSpawner portalSpawner = meshObject.AddComponent<PortalSpawner>();
            portalSpawner.SetSettings(portalSettings); // Pass the entire settings object
            portalSpawner.spawnablePrefabs = portalSettings.prefabs;
            portalSpawner.globalMaxInstances = portalSettings.maxNumberOfPortals;
            portalSpawner.shouldWaitToStartSpawning = portalSettings.shouldWaitToStartSpawning;
            portalSpawner.waitingTime = portalSettings.waitingTime;
            portalSpawner.minWaitingTime = portalSettings.minWaitingTime;
            portalSpawner.maxWaitingTime = portalSettings.maxWaitingTime;
            portalSpawner.shouldHaveRandomWaitingTime = portalSettings.shouldHaveRandomWaitingTime;
            portalSpawner.retryingSpawnTime = portalSettings.retryingSpawnTime;

            MobSpawner mobSpawner = meshObject.AddComponent<MobSpawner>();
            mobSpawner.SetSettings(mobSettings); // Pass the entire settings object
            mobSpawner.spawnablePrefabs = mobSettings.prefabs;
            mobSpawner.globalMaxInstances = mobSettings.maxNumberOfMobs;
            mobSpawner.shouldWaitToStartSpawning = mobSettings.shouldWaitToStartSpawning;
            mobSpawner.waitingTime = mobSettings.waitingTime;
            mobSpawner.minWaitingTime = mobSettings.minWaitingTime;
            mobSpawner.maxWaitingTime = mobSettings.maxWaitingTime;
            mobSpawner.shouldHaveRandomWaitingTime = mobSettings.shouldHaveRandomWaitingTime;
            mobSpawner.retryingSpawnTime = mobSettings.retryingSpawnTime;


            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);


            mapGenerator.RequestMapData(OnMapDataReceived, globalOffset, enableDebugging);
            this.shouldUseHDRPShaders = shouldUseHDRPShaders;
        }

        void OnMapDataReceived(MapData mapData)
        {
            if (enableDebugging)
                Debug.Log($"OnMapDataReceived for chunk at {globalOffset}");
            mapGenerator.RequestTerrainData(mapData, OnTerrainDataReceived, globalOffset, enableDebugging);
        }
        /// <summary>
        /// Receives biome object data, initializes the biome spawner, and bakes the NavMesh for AI navigation.
        /// </summary>
        /// <param name="biomeObjectData">The data containing biome object information, including biome map and height map.</param>

        void OnBiomeObjectDataReceived(BiomeObjectData biomeObjectData)
        {
            if (enableDebugging)
                Debug.Log($"OnBiomeObjectDataReceived for chunk at {globalOffset}");

            BakeNavMesh();

            MobSpawner mobSpawner = meshObject.GetComponent<MobSpawner>();
            mobSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, terrainGenerator.ChunkSize, meshObject.transform, biomeObjectData.biomeMap);

            PortalSpawner portalSpawner = meshObject.GetComponent<PortalSpawner>();
            portalSpawner.InitializeSpawner(globalOffset, biomeObjectData.heightMap, mapGenerator.ChunkSize, meshObject.transform, biomeObjectData.biomeMap);
        }

        void OnTerrainDataReceived(DataStructure.TerrainData terrainData)
        {
            if (enableDebugging)
                Debug.Log($"OnTerrainDataReceived for chunk at {globalOffset}");

            terrainGenerator = terrainData.terrainGenerator;
            TextureGenerator textureGenerator = new TextureGenerator();
            textureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer, shouldUseHDRPShaders);
            ApplyWetnessMap(terrainData.waterData);
            heightmap = terrainData.heightMap;
            erosionDeltaMap = terrainData.erosionDeltaMap;
            waterData = terrainData.waterData;

            Mesh mesh = terrainData.meshData.UpdateMesh();

            if (enableDebugging)
                Debug.Log($"Mesh stats - Vertices: {mesh.vertexCount}, Triangles: {mesh.triangles.Length / 3}, Bounds: {mesh.bounds}");

            if (mesh != null && mesh.vertexCount > 0 && mesh.triangles.Length > 0)
            {
                meshFilter.mesh = mesh;
                baseMesh = mesh;
                currentLod = Mathf.Clamp(terrainGenerator.LevelOfDetail, 0, lodMeshes.Length - 1);
                lodMeshes[currentLod] = mesh;
                lodRequested[currentLod] = true;

                if (enableDebugging)
                    Debug.Log("Setting MeshCollider...");
                meshCollider.sharedMesh = mesh;
                if (enableDebugging)
                    Debug.Log("MeshCollider set successfully");
            }
            else
            {
                Debug.LogError($"Invalid mesh generated for chunk at {globalOffset}");
            }

            UpdateWaterMesh(terrainData);
            if (IsVisible())
                UpdateLod(LodDistance());

            mapGenerator.RequestBiomeObjectData(OnBiomeObjectDataReceived, terrainData, globalOffset, meshObject.transform);
        }

        float DistanceToViewer()
        {
            return Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
        }

        /// <summary>Distance from the viewer to the nearest point of the chunk's mesh, which decides its level of detail.</summary>
        float LodDistance()
        {
            return Mathf.Sqrt(meshBounds.SqrDistance(viewerPosition));
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
                terrainGenerator.RequestLodMesh(meshData => OnLodMeshReceived(lod, meshData), heightmap, waterData, lod, globalOffset);
            }
        }

        void OnLodMeshReceived(int lod, MeshData meshData)
        {
            Mesh mesh = meshData.UpdateMesh();
            mesh.name = "Terrain Mesh LOD " + lod;
            lodMeshes[lod] = mesh;
            if (IsVisible())
                UpdateLod(LodDistance());
        }

        /// <summary>
        /// Gives the terrain material a "_WetnessMap" texture (grayscale, one texel per height map cell, laid out
        /// like the splat maps - sample it with the same UV) from the chunk's ground wetness near water, for a
        /// terrain shader that darkens and adds gloss to wet ground. The same value is also in the terrain mesh's
        /// vertex color (red). Shaders that don't declare _WetnessMap simply ignore it.
        /// </summary>
        void ApplyWetnessMap(WaterMapData water)
        {
            Material material = meshRenderer.sharedMaterial;
            if (material == null || water == null)
                return;

            int size = water.Size;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(water.Wetness[x, y]) * 255f);
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Wetness",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
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

            WaterMeshData waterMeshData = MeshGenerator.GenerateWaterMesh(terrainGenerator, terrainData.heightMap, terrainData.waterData, terrainGenerator.LevelOfDetail, globalOffset);
            if (waterMeshData == null)
                return;

            Mesh waterMesh = waterMeshData.BuildMesh();

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

        void BakeNavMesh()
        {
            // Always from the full-detail mesh, whatever level distance LOD is showing right now.
            Mesh shown = meshFilter.sharedMesh;
            if (baseMesh != null)
                meshFilter.sharedMesh = baseMesh;
            navMeshSurface.BuildNavMesh();
            meshFilter.sharedMesh = shown;
        }
        /// <summary>
        /// Updates the visibility of this chunk based on its distance to the viewer.
        /// </summary>
        /// /// <remarks>
        /// Chunks are dynamically created or re-used to reduce overhead. 
        /// Visible chunks are determined by checking their distance from the viewer.
        /// Chunks outside the viewing distance are hidden but not destroyed for faster reactivation.
        /// </remarks>
        public void UpdateTerrainChunk()
        {
            float viewerDstFromNearestEdge = DistanceToViewer();
            bool visible = viewerDstFromNearestEdge <= maxViewDistance;

            SetVisible(visible);
            if (visible)
                UpdateLod(LodDistance());
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
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
