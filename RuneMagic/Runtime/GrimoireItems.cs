using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Grimoire items made at runtime, by id: "grimoire:&lt;serial&gt;:&lt;author&gt;". Every machine makes the same item from the same id,
    /// so saves, trades, drops and the network resolve it through the Item Database (as rune items are). The pages are not in the item:
    /// they are in the world's ledger (<see cref="GrimoireStore"/>), under the serial.
    /// </summary>
    public static class GrimoireItems
    {
        private static readonly Dictionary<string, GrimoireItemSO> made = new Dictionary<string, GrimoireItemSO>();
        private static readonly Source source = new Source();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => made.Clear();

        // Registered when anything first uses grimoire items (tools and Edit Mode too) and again before each play session.
        static GrimoireItems() => ItemDatabase.AddSource(source);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSource() => ItemDatabase.AddSource(source);

        private sealed class Source : IRuntimeItemSource
        {
            public bool Owns(string id) => GrimoireIds.IsId(id);
            public ItemSO Find(string id) => Resolve(id);
            // Every machine makes the item from the id alone.
            public string Export(string id) => null;
            public ItemSO Import(string id, string definition) => Resolve(id);
        }

        /// <summary>The item id of a grimoire.</summary>
        public static string IdOf(GrimoireData book) => book == null ? "" : GrimoireIds.Make(book.serial, book.authorName);

        /// <summary>The item of a well-formed grimoire id (null otherwise).</summary>
        public static GrimoireItemSO Resolve(string id)
        {
            if (!GrimoireIds.TryParse(id, out string serial, out string tag)) return null;
            if (made.TryGetValue(id, out GrimoireItemSO cached) && cached != null) return cached;
            var item = ScriptableObject.CreateInstance<GrimoireItemSO>();
            item.Setup(id, serial, tag);
            item.hideFlags = HideFlags.DontSave;
            ((Object)item).name = "grimoire_" + serial;
            made[id] = item;
            return item;
        }

        /// <summary>The item of <paramref name="book"/>.</summary>
        public static GrimoireItemSO For(GrimoireData book) => Resolve(IdOf(book));
    }

    /// <summary>The look of grimoires: a leather book icon with a gold glyph, and a flat book model (made once).</summary>
    public static class GrimoireArt
    {
        private static Sprite icon;
        private static GameObject model, root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            icon = null;
            model = null;
            root = null;
        }

        public static Sprite Icon()
        {
            if (icon != null) return icon;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Grimoire Icon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            Color leather = new Color(0.30f, 0.14f, 0.12f, 1f), edge = new Color(0.16f, 0.07f, 0.06f, 1f), gold = new Color(0.95f, 0.78f, 0.3f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool book = x >= 10 && x < 54 && y >= 6 && y < 58;
                    bool spine = x >= 10 && x < 17;
                    float dx = x + 0.5f - 35.5f, dy = y + 0.5f - 32f;
                    float glyph = Mathf.Clamp01(9f - (Mathf.Abs(dx) + Mathf.Abs(dy)) + 0.5f);
                    bool border = book && (x < 12 || x >= 52 || y < 8 || y >= 56);
                    Color col = !book ? Color.clear : spine || border ? edge : Color.Lerp(leather, gold, glyph);
                    px[y * size + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            icon = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            icon.name = tex.name;
            icon.hideFlags = HideFlags.DontSave;
            return icon;
        }

        /// <summary>The model of a grimoire lying on the ground (an inactive template; the item system instantiates it).</summary>
        public static GameObject Model()
        {
            if (model != null) return model;
            if (root == null)
            {
                root = new GameObject("[Grimoire Models]") { hideFlags = HideFlags.DontSave };
                root.SetActive(false);
                if (Application.isPlaying) Object.DontDestroyOnLoad(root);
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Grimoire";
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = new Vector3(0.28f, 0.05f, 0.38f);
            Material mat = AlchemyArt.MaterialFor(new Color(0.30f, 0.14f, 0.12f));
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            model = go;
            return model;
        }
    }
}
