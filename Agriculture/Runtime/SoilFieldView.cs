using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a soil field, cheaply:
/// <list type="bullet">
/// <item><b>The soil</b>: one mesh over the field's cells (following the ground), with a texture of one pixel per cell -
/// the colour of its soil type, darker when wet, crumbly when worked, crusted, stony, mulched, a material lying on top,
/// a white salt crust. A few hundred cells are refreshed a frame, only while the camera is near.</item>
/// <item><b>The plants</b>: no GameObject per plant. Every plant is a few instances of shared low-poly meshes (stems,
/// leaves, crowns, blades, fruit, flowers, pests, stakes), coloured from a shared palette by how the plant looks, drawn
/// with GPU instancing in batches of up to 1023. The lists are rebuilt only when a plant looks different.</item>
/// <item>Nothing is drawn beyond the field's Draw Distance; fruit and pests only within its Detail Distance; nothing while
/// the field's realm is hidden (the hub city while the local player is out in the world).</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public class SoilFieldView : MonoBehaviour
{
    private sealed class Batch
    {
        public Mesh mesh;
        public Material material;
        public bool detail;
        public Matrix4x4[] matrices = new Matrix4x4[32];
        public int count;

        public void Add(Matrix4x4 m)
        {
            if (count == matrices.Length) System.Array.Resize(ref matrices, count * 2);
            matrices[count++] = m;
        }
    }

    private SoilField field;
    private MeshFilter filter;
    private MeshRenderer soilRenderer;
    private Mesh mesh;
    private Texture2D texture;
    private Color32[] pixels;
    private Material soilMaterial;
    private int cursor;
    private bool textureDirty;
    private float applyTimer, plantTimer;
    private int plantsSeen = -1, soilSeen = -1;
    private readonly Dictionary<long, Batch> batches = new Dictionary<long, Batch>();
    private readonly List<Batch> order = new List<Batch>();
    private static readonly Matrix4x4[] chunk = new Matrix4x4[1023];
    private bool instancing = true;

    public void Bind(SoilField f)
    {
        field = f;
        Rebuild();
    }

    public void Rebuild()
    {
        if (field == null) return;
        BuildSoil();
        plantsSeen = -1;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (texture != null) Destroy(texture);
        if (soilMaterial != null) Destroy(soilMaterial);
    }

    // ---------------------------------------------------------------- the soil
    private void BuildSoil()
    {
        SoilGrid g = field.State.Grid;
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        if (soilRenderer == null)
        {
            soilRenderer = gameObject.AddComponent<MeshRenderer>();
            soilRenderer.shadowCastingMode = ShadowCastingMode.Off;
            soilRenderer.receiveShadows = true;
        }
        if (mesh != null) Destroy(mesh);
        mesh = new Mesh { name = "Soil Field", hideFlags = HideFlags.DontSave };
        var v = new List<Vector3>(g.InsideCount * 4);
        var uv = new List<Vector2>(g.InsideCount * 4);
        var t = new List<int>(g.InsideCount * 6);
        float cs = g.CellSize;
        for (int i = 0; i < g.Count; i++)
        {
            if (!g.Inside[i]) continue;
            int x = i % g.Width, y = i / g.Width;
            int start = v.Count;
            for (int k = 0; k < 4; k++)
            {
                int cx = x + (k == 1 || k == 2 ? 1 : 0), cy = y + (k >= 2 ? 1 : 0);
                var l = new Vector2(g.Origin.x + cx * cs, g.Origin.y + cy * cs);
                v.Add(new Vector3(l.x, field.GroundHeight(l) + field.groundOffset, l.y));
                uv.Add(new Vector2((float)cx / g.Width, (float)cy / g.Height));
            }
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }
        if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;

        if (texture != null) Destroy(texture);
        texture = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false) { name = "Soil Field", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        pixels = new Color32[g.Count];
        for (int i = 0; i < g.Count; i++) pixels[i] = Look(g, i);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        if (soilMaterial != null) Destroy(soilMaterial);
        soilMaterial = PlantArt.SoilMaterial(texture);
        soilRenderer.sharedMaterial = soilMaterial;
        soilRenderer.enabled = field.showSoil;
        soilSeen = g.Version;
    }

    /// <summary>The colour of a cell as it looks: type, wetness, working, crust, stones, mulch, what lies on it, salt, a hole.</summary>
    private Color32 Look(SoilGrid g, int i)
    {
        if (!g.Inside[i]) return new Color32(0, 0, 0, 0);
        int o = i * SoilGrid.Stride;
        float[] v = g.Values;
        SoilType type = g.TypeAt(i);
        float m = v[o + (int)SoilChannel.Moisture];
        float fc = SoilChemistry.FieldCapacity(v[o + (int)SoilChannel.WaterRetention]);
        float wet = AgriMath.Smooth(0.05f, fc + 0.05f, m);
        Color c = Color.Lerp(SoilTypes.DryColor(type), SoilTypes.WetColor(type), wet);
        float tilth = v[o + (int)SoilChannel.Tilth], comp = v[o + (int)SoilChannel.Compaction];
        c *= 1f - 0.12f * tilth;
        if (tilth < 0.3f && comp > 0.45f) c = Color.Lerp(c, new Color(c.grayscale, c.grayscale, c.grayscale) * 1.15f, (comp - 0.45f) * 0.6f);
        float stones = v[o + (int)SoilChannel.Stones];
        if (stones > 0.05f && AgriMath.Noise(i, 13) < stones) c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.56f), 0.55f);
        float mulch = v[o + (int)SoilChannel.Mulch];
        if (mulch > 0.02f) c = Color.Lerp(c, new Color(0.8f, 0.7f, 0.45f), Mathf.Clamp01(mulch * 1.4f));
        int top = g.PendingTop(i, out float amount);
        if (top >= 0)
        {
            SoilAmendmentData a = field.State.Catalog?.Amendment(top);
            if (a != null) c = Color.Lerp(c, a.color, Mathf.Clamp01(amount * 0.5f));
        }
        if (v[o + (int)SoilChannel.Salinity] > 4f) c = Color.Lerp(c, new Color(0.92f, 0.92f, 0.88f), Mathf.Clamp01((v[o + (int)SoilChannel.Salinity] - 4f) * 0.08f));
        if (m > 0.93f) c = Color.Lerp(c, new Color(0.25f, 0.3f, 0.32f), 0.35f);
        if (g.Holes[i] > 0) c *= 0.55f;
        c.a = 1f;
        return c;
    }

    private void RefreshSoil(bool near)
    {
        SoilGrid g = field.State.Grid;
        bool changed = g.Version != soilSeen;
        if (!near && !changed) return;
        soilSeen = g.Version;
        int n = g.Count;
        int slice = changed ? Mathf.Min(n, 2048) : Mathf.Min(n, 256);
        double now = FarmClock.Now;
        bool advance = near && field.RulesHere && !field.Mirror;
        AgriSettings s = field.Settings;
        for (int k = 0; k < slice; k++)
        {
            if (cursor >= n) cursor = 0;
            int i = cursor++;
            if (!g.Inside[i]) continue;
            if (advance) g.Advance(i, now, s);
            Color32 c = Look(g, i);
            if (c.r != pixels[i].r || c.g != pixels[i].g || c.b != pixels[i].b)
            {
                pixels[i] = c;
                textureDirty = true;
            }
        }
        applyTimer += Time.unscaledDeltaTime;
        if (textureDirty && applyTimer > 0.3f)
        {
            applyTimer = 0f;
            textureDirty = false;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
    }

    // ---------------------------------------------------------------- frame
    private void LateUpdate()
    {
        if (field == null || field.State == null) return;
        if (mesh == null) BuildSoil();
        // Hidden with its realm (the city while the local player is away): RealmScene switches renderers off.
        if (soilRenderer != null && !soilRenderer.enabled && field.showSoil) return;
        Camera cam = Camera.main;
        float distance = cam != null ? Mathf.Sqrt(field.WorldBounds().SqrDistance(cam.transform.position)) : 0f;
        if (distance > field.drawDistance) return;
        bool near = distance < field.detailDistance;
        RefreshSoil(near);
        plantTimer += Time.unscaledDeltaTime;
        if (field.State.PlantVersion != plantsSeen || (near && plantTimer > 5f))
        {
            plantsSeen = field.State.PlantVersion;
            plantTimer = 0f;
            BuildPlants();
        }
        Draw(near);
    }

    private void Draw(bool near)
    {
        ShadowCastingMode shadows = field.plantShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        Matrix4x4 root = transform.localToWorldMatrix;
        int layer = gameObject.layer;
        foreach (Batch b in order)
        {
            if (b.count == 0 || b.material == null || (b.detail && !near)) continue;
            if (instancing && SystemInfo.supportsInstancing && b.material.enableInstancing)
            {
                for (int start = 0; start < b.count; start += chunk.Length)
                {
                    int n = Mathf.Min(chunk.Length, b.count - start);
                    for (int k = 0; k < n; k++) chunk[k] = root * b.matrices[start + k];
                    Graphics.DrawMeshInstanced(b.mesh, 0, b.material, chunk, n, null, shadows, true, layer);
                }
            }
            else
            {
                int n = Mathf.Min(b.count, 1500);
                for (int k = 0; k < n; k++)
                    Graphics.DrawMesh(b.mesh, root * b.matrices[k], b.material, layer, null, 0, null, shadows, true);
            }
        }
    }

    // ---------------------------------------------------------------- the plants
    private void BuildPlants()
    {
        foreach (Batch b in order) b.count = 0;
        FieldState st = field.State;
        foreach (PlantState p in st.Plants)
        {
            if (p == null || p.data == null) continue;
            if (!p.Alive && p.deadHours > 1e5f) continue; // a seed that never came up
            AddPlant(p, p.data);
        }
    }

    private void Add(Mesh m, Color c, Vector3 pos, Quaternion rot, Vector3 scale, bool detail = false)
    {
        int key = PlantArt.Key(c);
        long id = ((long)m.GetHashCode() << 20) ^ ((long)key << 1) ^ (detail ? 1 : 0);
        if (!batches.TryGetValue(id, out Batch b))
        {
            b = new Batch { mesh = m, material = PlantArt.Palette(key), detail = detail };
            batches[id] = b;
            order.Add(b);
        }
        b.Add(Matrix4x4.TRS(pos, rot, scale));
    }

    private static readonly Color DeadColor = new Color(0.42f, 0.32f, 0.18f);

    /// <summary>The colour of the leaves as they look: symptoms, diseases, age, health.</summary>
    public static Color Foliage(PlantState p, PlantSpeciesData d)
    {
        if (!p.Alive) return DeadColor;
        Color c = d.Look.leaf;
        c = Color.Lerp(c, new Color(0.78f, 0.75f, 0.35f), p.Symptom(PlantSymptom.PaleOldLeaves) * 0.6f);
        c = Color.Lerp(c, new Color(0.42f, 0.25f, 0.42f), p.Symptom(PlantSymptom.PurpleLeaves) * 0.5f);
        c = Color.Lerp(c, new Color(0.55f, 0.42f, 0.22f), p.Symptom(PlantSymptom.ScorchedEdges) * 0.45f);
        c = Color.Lerp(c, new Color(0.72f, 0.72f, 0.32f), p.Symptom(PlantSymptom.YellowBetweenVeins) * 0.5f);
        c = Color.Lerp(c, new Color(0.82f, 0.85f, 0.55f), p.Symptom(PlantSymptom.PaleNewLeaves) * 0.45f);
        c = Color.Lerp(c, new Color(0.5f, 0.35f, 0.2f), p.Symptom(PlantSymptom.LeafBurn) * 0.5f);
        float g = c.grayscale;
        c = Color.Lerp(c, new Color(g, g, g), p.Symptom(PlantSymptom.Wilting) * 0.3f);
        c = Color.Lerp(c, new Color(0.7f, 0.68f, 0.3f), p.Symptom(PlantSymptom.Waterlogged) * 0.5f);
        c = Color.Lerp(c, new Color(0.12f, 0.16f, 0.12f), p.Symptom(PlantSymptom.FrostBitten) * 0.7f);
        c = Color.Lerp(c, new Color(0.85f, 0.85f, 0.7f), p.Symptom(PlantSymptom.Sunscald) * 0.4f);
        c = Color.Lerp(c, c * 1.25f, p.Symptom(PlantSymptom.Leggy) * 0.3f);
        c = Color.Lerp(c, new Color(0.85f, 0.85f, 0.82f), p.Disease(DiseaseKind.Fungal) * 0.45f);
        c = Color.Lerp(c, new Color(0.45f, 0.35f, 0.2f), p.Disease(DiseaseKind.LeafSpot) * 0.4f);
        c = Color.Lerp(c, new Color(0.75f, 0.8f, 0.4f), p.Disease(DiseaseKind.Viral) * 0.35f);
        c = Color.Lerp(c, c * 0.6f, p.Disease(DiseaseKind.Bacterial) * 0.4f);
        if (p.stage == PlantStage.Senescence) c = Color.Lerp(c, new Color(0.7f, 0.6f, 0.25f), Mathf.Clamp01(p.progress) * 0.8f);
        c = Color.Lerp(c, new Color(0.55f, 0.5f, 0.3f), (1f - p.health) * 0.4f);
        if (d.Look.glow.a > 0.01f) c = Color.Lerp(c, d.Look.glow, d.Look.glow.a * 0.25f);
        c.a = 1f;
        return c;
    }

    private void AddPlant(PlantState p, PlantSpeciesData d)
    {
        PlantLook look = d.Look;
        Vector2 lp = p.position;
        Vector3 basePos = new Vector3(lp.x, field.GroundHeight(lp) + field.groundOffset, lp.y);
        Quaternion yaw = Quaternion.Euler(0f, AgriMath.Noise(p.id, 5) * 360f, 0f);
        if (p.IsSeed)
        {
            if (!p.Alive) return;
            if (p.covered) Add(PlantArt.Disc, new Color(0.22f, 0.16f, 0.1f), basePos, yaw, new Vector3(0.07f, 0.004f, 0.07f), true);
            else Add(PlantArt.Sphere, new Color(0.55f, 0.45f, 0.3f), basePos, yaw, Vector3.one * 0.012f, true);
            return;
        }
        float s = Mathf.Max(0.04f, p.size);
        float h = look.height * s, w = look.width * s;
        float droop = p.Alive ? 1f - 0.35f * p.Symptom(PlantSymptom.Wilting) : 0.45f;
        Color leaf = Foliage(p, d);
        Color stem = p.Alive ? look.stem : DeadColor;
        if (p.dormant)
        {
            leaf = Color.Lerp(look.stem, new Color(0.35f, 0.28f, 0.2f), 0.5f);
            if (look.shape == PlantShape.Herb || look.shape == PlantShape.Flower || look.shape == PlantShape.RootCrop || look.shape == PlantShape.Rosette)
                h *= 0.3f;
        }
        bool supported = p.supported;
        switch (look.shape)
        {
            case PlantShape.Herb:
                Add(PlantArt.Cylinder, stem, basePos, yaw, new Vector3(0.012f + 0.02f * s, h * droop, 0.012f + 0.02f * s));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 off = yaw * new Vector3(Mathf.Cos(k * 2.1f) * w * 0.22f, h * (0.25f + 0.25f * k) * droop, Mathf.Sin(k * 2.1f) * w * 0.22f);
                    Add(PlantArt.Sphere, leaf, basePos + off, yaw, new Vector3(w * 0.55f, h * 0.35f * droop, w * 0.55f));
                }
                break;
            case PlantShape.Bush:
                Add(PlantArt.Sphere, leaf, basePos, yaw, new Vector3(w, h * droop, w));
                Add(PlantArt.Sphere, leaf, basePos + yaw * new Vector3(w * 0.3f, 0f, 0f), yaw, new Vector3(w * 0.6f, h * 0.7f * droop, w * 0.6f));
                break;
            case PlantShape.Vine:
                if (supported)
                {
                    Add(PlantArt.Box, new Color(0.5f, 0.38f, 0.22f), basePos + yaw * new Vector3(0.05f, 0f, 0f), yaw, new Vector3(0.025f, look.height * 1.05f, 0.025f));
                    Add(PlantArt.Cylinder, stem, basePos, yaw, new Vector3(0.015f + 0.02f * s, h, 0.015f + 0.02f * s));
                    for (int k = 0; k < 4; k++)
                        Add(PlantArt.Sphere, leaf, basePos + yaw * new Vector3((k % 2 == 0 ? 1f : -1f) * w * 0.15f, h * (0.15f + 0.22f * k) * droop, 0f), yaw, new Vector3(w * 0.6f, h * 0.28f, w * 0.6f));
                }
                else
                {
                    Add(PlantArt.Sphere, leaf, basePos, yaw, new Vector3(w * 1.3f, Mathf.Min(h, look.height * 0.3f) * droop, w * 1.3f));
                }
                break;
            case PlantShape.Tree:
            {
                float trunk = Mathf.Max(0.03f, w * 0.08f);
                Add(PlantArt.Cylinder, stem, basePos, yaw, new Vector3(trunk, h * 0.55f, trunk));
                float crown = p.dormant ? 0.55f : 1f;
                Add(PlantArt.Sphere, leaf, basePos + Vector3.up * h * 0.38f, yaw, new Vector3(w * crown, h * 0.62f * droop * crown, w * crown));
                if (p.Alive && p.deadWood > 0.3f)
                    Add(PlantArt.Cylinder, new Color(0.35f, 0.3f, 0.25f), basePos + Vector3.up * h * 0.6f + yaw * new Vector3(w * 0.4f, 0f, 0f), yaw * Quaternion.Euler(0f, 0f, 60f), new Vector3(trunk * 0.4f, w * 0.4f, trunk * 0.4f));
                break;
            }
            case PlantShape.Grain:
                for (int k = 0; k < 5; k++)
                {
                    Quaternion tilt = yaw * Quaternion.Euler(Mathf.Sin(k * 1.7f) * 12f * droop + (1f - droop) * 30f, k * 72f, 0f);
                    Add(PlantArt.Blade, leaf, basePos + yaw * new Vector3(Mathf.Cos(k * 1.3f) * w * 0.15f, 0f, Mathf.Sin(k * 1.3f) * w * 0.15f), tilt, new Vector3(0.025f, h, 0.012f));
                }
                break;
            case PlantShape.RootCrop:
                for (int k = 0; k < 4; k++)
                    Add(PlantArt.Blade, leaf, basePos, yaw * Quaternion.Euler(18f + (1f - droop) * 30f, k * 90f, 0f), new Vector3(w * 0.3f, h, w * 0.12f));
                break;
            case PlantShape.Rosette:
                Add(PlantArt.Sphere, leaf, basePos, yaw, new Vector3(w * 1.1f, h * 0.35f * droop, w * 1.1f));
                break;
            default: // Flower
                Add(PlantArt.Cylinder, stem, basePos, yaw, new Vector3(0.01f + 0.012f * s, h * droop, 0.01f + 0.012f * s));
                Add(PlantArt.Sphere, leaf, basePos, yaw, new Vector3(w * 0.8f, h * 0.3f, w * 0.8f));
                break;
        }
        AddPieces(p, d, basePos, yaw, h, w, droop);
        AddPests(p, basePos, h, w);
    }

    private void AddPieces(PlantState p, PlantSpeciesData d, Vector3 basePos, Quaternion yaw, float h, float w, float droop)
    {
        ProduceSpec pr = d.Produce;
        PlantLook look = d.Look;
        bool fruitPart = pr.part == ProducePart.Fruit || pr.part == ProducePart.Grain;
        foreach (FruitState f in p.fruits)
        {
            Color c;
            if (f.stage == ProduceStage.Bud && fruitPart) c = f.pollinated ? Color.Lerp(look.flower, pr.colorYoung, 0.6f) : look.flower;
            else c = pr.ColorAt(f.stage, f.progress);
            float size = pr.pieceSize * Mathf.Max(0.3f, f.size) * (f.stage == ProduceStage.Bud ? 0.6f : 1f);
            if (pr.part == ProducePart.Flower) size = pr.pieceSize * (f.stage < ProduceStage.Early ? 0.5f : f.stage >= ProduceStage.Overripe ? 0.75f : 1f);
            float angle = (f.slot * 360f * 0.618f + p.id * 37f) % 360f;
            float frac = 0.35f + 0.55f * ((f.slot * 7.31f) % 1f);
            Vector3 pos;
            switch (look.shape)
            {
                case PlantShape.Tree:
                    pos = basePos + Vector3.up * h * (0.3f + 0.45f * frac) + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * w * 0.48f;
                    break;
                case PlantShape.Grain:
                case PlantShape.Flower:
                    pos = basePos + Vector3.up * h * (0.85f + 0.12f * frac) * droop + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * w * 0.18f * frac;
                    break;
                case PlantShape.RootCrop:
                    if (f.stage < ProduceStage.Developing) continue;
                    pos = basePos;
                    size = pr.pieceSize * 0.6f * Mathf.Max(0.4f, f.size);
                    break;
                case PlantShape.Rosette:
                    pos = basePos;
                    Add(PlantArt.Sphere, c, pos, yaw, new Vector3(w * 0.62f * Mathf.Max(0.3f, f.size), h * 0.75f * droop, w * 0.62f * Mathf.Max(0.3f, f.size)));
                    continue;
                default:
                    pos = basePos + Vector3.up * h * frac * droop + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * w * (0.3f + 0.15f * frac);
                    break;
            }
            Vector3 scale = pr.part == ProducePart.Grain ? new Vector3(size * 0.5f, size * 1.6f, size * 0.5f) : Vector3.one * size;
            if (f.stage == ProduceStage.Rotten) scale.y *= 0.7f;
            Add(PlantArt.Sphere, c, pos, yaw, scale, true);
        }
    }

    private static readonly Color[] PestColors =
    {
        new Color(0.55f, 0.8f, 0.3f), new Color(0.4f, 0.65f, 0.2f), new Color(0.8f, 0.2f, 0.1f), new Color(0.12f, 0.16f, 0.1f),
        new Color(0.95f, 0.95f, 0.92f), new Color(0.5f, 0.4f, 0.3f), new Color(0.45f, 0.35f, 0.25f), new Color(0.6f, 0.5f, 0.35f),
    };

    private void AddPests(PlantState p, Vector3 basePos, float h, float w)
    {
        for (int k = 0; k < 8; k++)
        {
            float load = p.pests[k];
            if (load < 0.12f || k == (int)PestKind.RootParasites) continue;
            int n = Mathf.Min(8, Mathf.CeilToInt(load * 7f));
            float size = k == (int)PestKind.Caterpillars || k == (int)PestKind.Slugs ? 0.03f : k == (int)PestKind.Snails || k == (int)PestKind.Beetles ? 0.022f : 0.009f;
            for (int i = 0; i < n; i++)
            {
                float a = AgriMath.Noise(p.id, k, i) * 360f, y = AgriMath.Noise(p.id, k + 9, i);
                bool low = k == (int)PestKind.Slugs || k == (int)PestKind.Snails;
                Vector3 pos = basePos + Vector3.up * (low ? 0.02f : h * (0.2f + 0.7f * y)) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * w * (low ? 0.6f : 0.4f);
                Vector3 scale = k == (int)PestKind.Caterpillars || k == (int)PestKind.Slugs ? new Vector3(size * 0.6f, size * 0.6f, size * 1.8f) : Vector3.one * size;
                Add(PlantArt.Sphere, PestColors[k], pos, Quaternion.Euler(0f, a, 0f), scale, true);
            }
        }
    }
}
