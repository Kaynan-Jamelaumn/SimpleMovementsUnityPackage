using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Rune items made at runtime, by id: "rune:&lt;rune&gt;:&lt;rank&gt;:&lt;quality&gt;:&lt;energy&gt;". Every machine
    /// makes the same item from the same id and its Rune Magic Database, so saves, merchants, trades and the network resolve
    /// it through the Item Database. Identical runes stack.
    /// </summary>
    public static class RuneItems
    {
        public const string Prefix = "rune:";

        private static readonly Dictionary<string, RuneItemSO> made = new Dictionary<string, RuneItemSO>();
        private static readonly Source source = new Source();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => made.Clear();

        // Registered when anything first uses rune items (tools and Edit Mode too) and again before each play session.
        static RuneItems() => ItemDatabase.AddSource(source);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSource() => ItemDatabase.AddSource(source);

        private sealed class Source : IRuntimeItemSource
        {
            public bool Owns(string id) => IsRuneId(id);
            public ItemSO Find(string id) => Resolve(id);
            // Every machine makes the item from the id and its own Rune Magic Database.
            public string Export(string id) => null;
            public ItemSO Import(string id, string definition) => Resolve(id);
        }

        public static bool IsRuneId(string id) => id != null && id.StartsWith(Prefix, System.StringComparison.Ordinal);

        /// <summary>The id of the rune item holding <paramref name="d"/> (quality and energy as whole numbers).</summary>
        public static string IdOf(RuneDrop d)
        {
            int q = Mathf.Clamp(Mathf.RoundToInt(d.quality), 0, 100);
            long e = (long)System.Math.Max(0, System.Math.Round(d.energy));
            return Prefix + d.runeId + ":" + Mathf.Max(1, d.rank).ToString(CultureInfo.InvariantCulture) + ":" +
                   q.ToString(CultureInfo.InvariantCulture) + ":" + e.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a rune item id (false when it is not one).</summary>
        public static bool TryParse(string id, out RuneDrop d)
        {
            d = default;
            if (!IsRuneId(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length != 4 || parts[0].Length == 0) return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rank) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int quality) ||
                !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long energy))
                return false;
            if (rank < 1 || rank > 1000 || quality < 0 || quality > 100 || energy < 0) return false;
            d = new RuneDrop { runeId = parts[0], rank = rank, quality = quality, energy = energy };
            return true;
        }

        /// <summary>The item holding <paramref name="d"/> (null when the rune is unknown).</summary>
        public static RuneItemSO For(RuneDrop d) => Resolve(IdOf(d));

        /// <summary>The rune item of an id (null when it is not a valid id of a known rune).</summary>
        public static RuneItemSO Resolve(string id)
        {
            if (!TryParse(id, out RuneDrop d)) return null;
            if (made.TryGetValue(id, out RuneItemSO cached) && cached != null) return cached;
            if (RuneMagicDatabase.Instance.Catalog.Rune(d.runeId) == null) return null;
            var item = ScriptableObject.CreateInstance<RuneItemSO>();
            item.Setup(d, id);
            item.hideFlags = HideFlags.DontSave;
            ((Object)item).name = id.Substring(Prefix.Length).Replace(':', '_');
            made[id] = item;
            return item;
        }

        /// <summary>Drops a rune item into the world at <paramref name="position"/> (where the rules run). Null when it cannot.</summary>
        public static GameObject Spawn(RuneDrop d, Vector3 position, GameObject source)
        {
            RuneItemSO item = For(d);
            if (item == null || !GameNet.NetSpawn.ShouldSpawnHere) return null;
            return ItemHandler.SpawnWorldItem(item, 1, null, position, source);
        }
    }

    /// <summary>The look of rune items: an icon and a small glowing model in the rune's colour (made once per rune).</summary>
    public static class RuneArt
    {
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        private static GameObject root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            icons.Clear();
            models.Clear();
            root = null;
        }

        private static Color ColorOf(RuneDef def) => def != null ? def.color : new Color(0.7f, 0.75f, 0.85f);

        /// <summary>A rune stone: a dark disc, a ring and a diamond glyph in the rune's colour.</summary>
        public static Sprite Icon(RuneDef def)
        {
            string key = def != null ? def.id : "";
            if (icons.TryGetValue(key, out Sprite s) && s != null) return s;
            const int size = 64;
            Color c = ColorOf(def);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Rune Icon " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f, mid = size * 0.5f;
            Color stone = new Color(0.12f, 0.13f, 0.17f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - mid, dy = y + 0.5f - mid;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float disc = Mathf.Clamp01(r - d + 0.5f);
                    float ring = Mathf.Clamp01(1.5f - Mathf.Abs(d - r * 0.82f));
                    float glyph = Mathf.Clamp01(r * 0.42f - (Mathf.Abs(dx) + Mathf.Abs(dy) * 0.7f) + 0.5f);
                    Color col = Color.Lerp(stone, c, Mathf.Max(ring, glyph));
                    col.a = disc;
                    px[y * size + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = tex.name;
            s.hideFlags = HideFlags.DontSave;
            icons[key] = s;
            return s;
        }

        /// <summary>The model of a rune lying on the ground (an inactive template; the item system instantiates it).</summary>
        public static GameObject Model(RuneDef def)
        {
            string key = def != null ? def.id : "";
            if (models.TryGetValue(key, out GameObject m) && m != null) return m;
            if (root == null)
            {
                root = new GameObject("[Rune Item Models]") { hideFlags = HideFlags.DontSave };
                root.SetActive(false);
                if (Application.isPlaying) Object.DontDestroyOnLoad(root);
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Rune " + key;
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = new Vector3(0.22f, 0.22f, 0.08f);
            Material mat = AlchemyArt.MaterialFor(ColorOf(def));
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            models[key] = go;
            return go;
        }
    }
}
