using System.Collections.Generic;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// Makes the borders of the world's rings visible to this machine's player: near a border (Show Distance) a glowing
    /// line is drawn on the ground along it and a translucent wall rises from it, fading upwards and towards both ends.
    /// Nothing blocks the way - it has no collider, players walk straight through. By default only the borders of the
    /// safe zones are drawn (where PvP turns on or off: safe zone ↔ PvP ring, and the city walls when the first ring is
    /// PvP); set <see cref="borders"/> to All Rings to see every ring.
    /// <para>Only the piece of the border around the player is built (Arc Length, following the ground), so it costs the
    /// same whatever the ring's size. Added next to the <see cref="HubWorldDirector"/> automatically (its Show Zone
    /// Borders), never on a dedicated server; only shown while the player is out in the World.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneBorders : MonoBehaviour
    {
        /// <summary>Which ring borders are drawn.</summary>
        public enum Which
        {
            /// <summary>Only where PvP changes: the edges of the safe zones.</summary>
            SafeZoneEdges,
            /// <summary>Every border between two rings (and the city walls).</summary>
            AllRings,
        }

        [Tooltip("Safe Zone Edges: only where PvP turns on or off. All Rings: every ring border.")]
        public Which borders = Which.SafeZoneEdges;
        [Tooltip("A border is drawn while the player is closer to it than this (metres). It gets stronger as they come near.")]
        [Min(5f)] public float showDistance = 70f;
        [Tooltip("Length of the piece of border drawn around the player (metres).")]
        [Min(10f)] public float arcLength = 160f;
        [Tooltip("Distance between two points of the border (metres): shorter follows the ground more closely.")]
        [Range(0.5f, 10f)] public float segmentLength = 2f;

        [Header("Look")]
        [Tooltip("Draw a line on the ground along the border.")]
        public bool groundLine = true;
        [Tooltip("Width of the ground line (metres).")]
        [Min(0.05f)] public float lineWidth = 0.6f;
        [Tooltip("Draw a translucent wall along the border (it can be walked through).")]
        public bool wall = true;
        [Tooltip("Height of the wall (metres).")]
        [Min(0.2f)] public float wallHeight = 4f;
        [Tooltip("Colour of a safe zone's edge (one side PvP, the other not).")]
        public Color safeEdgeColor = new Color(1f, 0.42f, 0.22f, 1f);
        [Tooltip("Colour of the other ring borders (All Rings).")]
        public Color ringColor = new Color(0.85f, 0.9f, 1f, 1f);
        [Tooltip("Strength of the ground line.")]
        [Range(0f, 1f)] public float lineAlpha = 0.85f;
        [Tooltip("Strength of the wall at its foot (it fades to nothing at the top).")]
        [Range(0f, 1f)] public float wallAlpha = 0.35f;
        [Tooltip("How much the border breathes (0 = steady).")]
        [Range(0f, 1f)] public float pulseAmount = 0.25f;
        [Min(0f)] public float pulseSpeed = 1.6f;
        [Tooltip("OPTIONAL. Material of the border: transparent, showing vertex colours, both faces. Empty = a copy of the Combat Settings telegraph material (Sprites/Default).")]
        public Material material;

        private struct Border
        {
            public float radius;
            public bool safeEdge;
        }

        private sealed class Strip
        {
            public GameObject go;
            public Mesh mesh;
            public MeshRenderer renderer;
        }

        private HubWorldDirector director;
        private Material runtimeMaterial;
        private GameObject root;
        private readonly List<Border> list = new List<Border>();
        private readonly List<Strip> strips = new List<Strip>();
        private readonly Dictionary<long, float> heights = new Dictionary<long, float>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private ZoneMap builtFor;
        private Vector3 lastBuiltAt = new Vector3(float.MaxValue, 0f, 0f);
        private float nextBuild, nextCacheClear;
        private bool shown;

        private void Awake()
        {
            director = GetComponent<HubWorldDirector>();
        }

        private void OnEnable()
        {
            if (director != null)
                director.CycleJumped += OnCycleJumped;
        }

        private void OnDisable()
        {
            if (director != null)
                director.CycleJumped -= OnCycleJumped;
            Hide();
        }

        private void OnDestroy()
        {
            if (root != null)
                Destroy(root);
            foreach (Strip s in strips)
                if (s.mesh != null)
                    Destroy(s.mesh);
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        private void OnValidate() => lastBuiltAt = new Vector3(float.MaxValue, 0f, 0f);

        // A new world: the ground under the borders is different.
        private void OnCycleJumped(int cycle) => ClearHeights();

        private void ClearHeights()
        {
            heights.Clear();
            lastBuiltAt = new Vector3(float.MaxValue, 0f, 0f);
        }

        // ------------------------------------------------------------------ every frame

        private void LateUpdate()
        {
            if (GameNet.NetAuthority.IsHeadless || director == null || director.Zones == null || (!groundLine && !wall))
            {
                Hide();
                return;
            }
            CombatEntity me = HubWorldDirector.LocalPlayer;
            PlayerRealmState state = me != null ? PlayerRealmState.Of(me) : null;
            if (state == null || !state.InWorld)
            {
                Hide();
                return;
            }
            if (Time.unscaledTime >= nextCacheClear)
            {
                // Terrain chunks stream in and change: the ground is sampled again now and then.
                nextCacheClear = Time.unscaledTime + 20f;
                heights.Clear();
            }
            if (builtFor != director.Zones)
                CollectBorders(director.Zones);
            Vector3 at = me.Position;
            if (Time.unscaledTime >= nextBuild && (at - lastBuiltAt).sqrMagnitude > 0.09f)
            {
                nextBuild = Time.unscaledTime + 0.1f;
                lastBuiltAt = at;
                Build(director.Zones, at);
            }
            shown = true;
            if (runtimeMaterial != null && material == null)
            {
                float pulse = 1f - pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI));
                runtimeMaterial.color = new Color(1f, 1f, 1f, pulse);
            }
        }

        private void Hide()
        {
            if (!shown)
                return;
            shown = false;
            foreach (Strip s in strips)
                if (s.go != null)
                    s.go.SetActive(false);
            lastBuiltAt = new Vector3(float.MaxValue, 0f, 0f);
        }

        /// <summary>The borders to draw: between the city and the first ring, and between each two rings.</summary>
        private void CollectBorders(ZoneMap zones)
        {
            builtFor = zones;
            list.Clear();
            ClearHeights();
            IReadOnlyList<ZoneBand> bands = zones.Bands;
            if (bands.Count == 0)
                return;
            // The city walls: the city is never PvP.
            if (zones.CityRadius > 0f && (bands[0].pvp || borders == Which.AllRings))
                list.Add(new Border { radius = zones.CityRadius, safeEdge = bands[0].pvp });
            // The last ring goes on forever: no border after it.
            for (int i = 0; i < bands.Count - 1; i++)
            {
                bool edge = bands[i].pvp != bands[i + 1].pvp;
                if ((edge || borders == Which.AllRings) && bands[i].outerRadius > zones.CityRadius)
                    list.Add(new Border { radius = bands[i].outerRadius, safeEdge = edge });
            }
        }

        // ------------------------------------------------------------------ building

        private void Build(ZoneMap zones, Vector3 at)
        {
            float distance = zones.DistanceFromCity(at);
            int used = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Border b = list[i];
                float away = Mathf.Abs(distance - b.radius);
                if (away > showDistance || b.radius < 1f)
                    continue;
                Strip strip = StripAt(used++);
                BuildArc(strip, zones.Center, b, i, at, away);
                strip.go.SetActive(true);
            }
            for (int i = used; i < strips.Count; i++)
                strips[i].go.SetActive(false);
        }

        private void BuildArc(Strip strip, Vector2 center, Border border, int borderIndex, Vector3 at, float away)
        {
            float r = border.radius;
            // Points at fixed angles around the ring, so the same ground heights are reused as the player moves.
            int count = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * r / segmentLength), 16, 1 << 20);
            float step = 2f * Mathf.PI / count;
            float playerAngle = Mathf.Atan2(at.z - center.y, at.x - center.x);
            float half = Mathf.Min(Mathf.PI, arcLength * 0.5f / r);
            int k0 = Mathf.FloorToInt((playerAngle - half) / step);
            int k1 = Mathf.CeilToInt((playerAngle + half) / step);
            if (k1 - k0 + 1 > count)
                k1 = k0 + count;
            Color baseColor = border.safeEdge ? safeEdgeColor : ringColor;
            // Stronger as the player comes near; never quite gone while shown.
            float near = Mathf.Lerp(0.25f, 1f, 1f - Mathf.Clamp01(away / showDistance));

            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            int perColumn = (groundLine ? 2 : 0) + (wall ? 2 : 0);
            for (int k = k0; k <= k1; k++)
            {
                float angle = k * step;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float x = center.x + dir.x * r, z = center.y + dir.y * r;
                float y = GroundAt(borderIndex, k, count, x, z, at.y);
                float along = Mathf.Abs(Mathf.DeltaAngle(playerAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad) / Mathf.Max(0.0001f, half);
                float t = Mathf.InverseLerp(0.55f, 1f, along); // fades out over the outer part of the piece
                float fade = (1f - t * t * (3f - 2f * t)) * near;
                if (groundLine)
                {
                    float w = lineWidth * 0.5f;
                    vertices.Add(new Vector3(center.x + dir.x * (r - w), y + 0.08f, center.y + dir.y * (r - w)));
                    vertices.Add(new Vector3(center.x + dir.x * (r + w), y + 0.08f, center.y + dir.y * (r + w)));
                    Color c = baseColor;
                    c.a = baseColor.a * lineAlpha * fade;
                    colors.Add(c);
                    colors.Add(c);
                }
                if (wall)
                {
                    vertices.Add(new Vector3(x, y - 0.25f, z));
                    vertices.Add(new Vector3(x, y + wallHeight, z));
                    Color bottom = baseColor, top = baseColor;
                    bottom.a = baseColor.a * wallAlpha * fade;
                    top.a = 0f;
                    colors.Add(bottom);
                    colors.Add(top);
                }
            }
            int columns = k1 - k0 + 1;
            for (int j = 0; j < columns - 1; j++)
            {
                int a = j * perColumn, b = (j + 1) * perColumn;
                for (int q = 0; q < perColumn; q += 2)
                {
                    triangles.Add(a + q); triangles.Add(a + q + 1); triangles.Add(b + q + 1);
                    triangles.Add(a + q); triangles.Add(b + q + 1); triangles.Add(b + q);
                }
            }
            Mesh mesh = strip.mesh;
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Ground height of one border point (cached; the player's height when nothing is found).</summary>
        private float GroundAt(int borderIndex, int k, int count, float x, float z, float fallback)
        {
            int index = ((k % count) + count) % count;
            long key = ((long)borderIndex << 32) | (uint)index;
            if (heights.TryGetValue(key, out float y))
                return y;
            if (CombatQuery.GroundHeight(new Vector3(x, fallback, z), out y, 80f, 200f))
            {
                heights[key] = y;
                return y;
            }
            return fallback;
        }

        private Strip StripAt(int index)
        {
            while (strips.Count <= index)
            {
                if (root == null)
                {
                    root = new GameObject("Zone Borders");
                    DontDestroyOnLoad(root);
                }
                var go = new GameObject("Zone Border");
                go.transform.SetParent(root.transform, false);
                var mesh = new Mesh { name = "Zone Border" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = BorderMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                strips.Add(new Strip { go = go, mesh = mesh, renderer = renderer });
            }
            Strip s = strips[index];
            Material m = BorderMaterial();
            if (s.renderer.sharedMaterial != m)
                s.renderer.sharedMaterial = m;
            return s;
        }

        private Material BorderMaterial()
        {
            if (material != null)
                return material;
            if (runtimeMaterial == null)
            {
                Material telegraph = CombatSettings.Instance != null ? CombatSettings.Instance.TelegraphMaterial : null;
                if (telegraph != null)
                    runtimeMaterial = new Material(telegraph) { name = "Zone Border", hideFlags = HideFlags.DontSave };
            }
            return runtimeMaterial;
        }
    }
}
