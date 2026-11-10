using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The items of animal husbandry, by id: feed of every food that has a feed item ("animal:feed/hay"), a livestock crate of
/// every breed ("animal:livestock/cow/jersey") and the care tools of its own ("animal:tool/soft_brush": the brushes, the
/// hoof pick, the pitchfork, the shears, the milking pail) and what living animals give ("animal:product/egg": eggs and
/// milk are food, wool a material). An asset bound in the Animal Database is used when there is one; otherwise the item is made
/// at runtime from the id (every machine makes the same item from the same id, so saves, merchants, trades and the
/// network resolve it through the Item Database - nothing but the id is ever sent).
/// <para>The other systems' items animal husbandry uses (soap, straw, a shovel, medicines) are theirs: they are matched
/// by their keys (see <see cref="AnimalToolkit.KeyOf"/>), never made here.</para>
/// </summary>
public static class AnimalItems
{
    private static readonly Dictionary<string, ItemSO> made = new Dictionary<string, ItemSO>();
    private static readonly Source source = new Source();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => made.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSource() => ItemDatabase.AddSource(source);

    private sealed class Source : IRuntimeItemSource
    {
        public bool Owns(string id) => AnimalIds.IsAnimalId(id);
        public ItemSO Find(string id) => Resolve(id);
        public string Export(string id) => null;
        public ItemSO Import(string id, string definition) => Resolve(id);
    }

    /// <summary>Forget the items made (tests; after changing content from code).</summary>
    public static void ClearCaches() => made.Clear();

    public static ItemSO Feed(string food) => Resolve(AnimalIds.Feed(food));
    public static ItemSO Livestock(string species, string breed) => Resolve(AnimalIds.Livestock(species, breed));
    /// <summary>A care tool of animal husbandry's own ("soft_brush"), or null.</summary>
    public static ItemSO Tool(string tool) => Resolve(AnimalIds.Tool(tool));
    /// <summary>A product's item ("egg", "milk", "wool"), or null.</summary>
    public static ItemSO Product(string product) => Resolve(AnimalIds.Product(product));

    /// <summary>The item of an animal id: the bound asset, else one made at runtime (null for an unknown id).</summary>
    public static ItemSO Resolve(string id)
    {
        if (!AnimalIds.IsAnimalId(id)) return null;
        AnimalDatabase db = AnimalDatabase.Instance;
        ItemSO bound = db.BoundItem(id);
        if (bound != null) return bound;
        if (made.TryGetValue(id, out ItemSO cached) && cached != null) return cached;
        if (!AnimalIds.Parse(id, out string kind, out string key)) return null;
        ItemSO item = null;
        switch (kind)
        {
            case AnimalIds.FeedKind:
            {
                AnimalFoodData f = db.Food(key);
                // Only foods that are a feed item of their own (not the farm's produce, which is its own item).
                if (f == null || !f.IsItem(id)) return null;
                var s = ScriptableObject.CreateInstance<AnimalFeedSO>();
                s.Setup(f, AnimalArt.FeedIcon(f), AnimalArt.Template("feed:" + f.id, PrimitiveType.Cube, AnimalArt.FeedColour(f), new Vector3(0.35f, 0.25f, 0.25f)), id);
                item = s;
                break;
            }
            case AnimalIds.LivestockKind:
            {
                if (!AnimalIds.ParseLivestock(id, out string speciesId, out string breedId)) return null;
                AnimalSpeciesData sp = db.Species(speciesId);
                if (sp == null) return null;
                AnimalBreedData b = string.IsNullOrEmpty(breedId) ? sp.BreedOrDefault(null) : sp.Breed(breedId);
                if (!string.IsNullOrEmpty(breedId) && b == null) return null;
                var s = ScriptableObject.CreateInstance<LivestockItemSO>();
                s.Setup(sp, b, AnimalArt.CrateIcon(sp, b), AnimalArt.Template("crate", PrimitiveType.Cube, new Color(0.6f, 0.45f, 0.28f), new Vector3(0.6f, 0.45f, 0.5f)), id);
                item = s;
                break;
            }
            case AnimalIds.ProductKind:
            {
                // What a living animal gives: food (eggs, milk) or a material (wool).
                AnimalProductSpec p = AnimalProducts.Spec(key);
                if (p == null) return null;
                if (p.edible)
                {
                    var s = ScriptableObject.CreateInstance<AnimalProductFoodSO>();
                    s.Setup(p, AnimalArt.ProductIcon(p), AnimalArt.ProductModel(p), id);
                    item = s;
                }
                else
                {
                    var s = ScriptableObject.CreateInstance<AnimalProductMaterialSO>();
                    s.Setup(p, AnimalArt.ProductIcon(p), AnimalArt.ProductModel(p), id);
                    item = s;
                }
                break;
            }
            case AnimalIds.ToolKind:
            {
                AnimalToolData t = db.Tool(key);
                // Only the tools whose items are this very id (animal husbandry's own): soap, straw and the shovel are other systems' items.
                if (t == null || !t.IsItem(id)) return null;
                var s = ScriptableObject.CreateInstance<AnimalToolSO>();
                s.Setup(t, AnimalArt.ToolIcon(t.id), AnimalArt.ToolModel(t), id);
                item = s;
                break;
            }
        }
        if (item == null) return null;
        item.hideFlags = HideFlags.DontSave;
        ((Object)item).name = id.Substring(AnimalIds.Prefix.Length).Replace('/', '_');
        made[id] = item;
        return item;
    }

    /// <summary>
    /// Every built-in animal item, for merchants, chests and testing: the feed items, a crate of every breed, and the care
    /// tools of animal husbandry's own.
    /// </summary>
    public static List<ItemSO> All(bool feed = true, bool livestock = true, bool tools = true, bool products = false)
    {
        var list = new List<ItemSO>();
        AnimalDatabase db = AnimalDatabase.Instance;
        if (feed)
            foreach (AnimalFoodData f in db.AllFoods)
            {
                ItemSO i = f.IsItem(AnimalIds.Feed(f.id)) ? Feed(f.id) : null;
                if (i != null) list.Add(i);
            }
        if (livestock)
            foreach (AnimalSpeciesData s in db.AllSpecies)
            {
                if (s.breeds == null || s.breeds.Count == 0)
                {
                    ItemSO i = Livestock(s.id, "");
                    if (i != null) list.Add(i);
                    continue;
                }
                foreach (AnimalBreedData b in s.breeds)
                {
                    ItemSO i = b != null ? Livestock(s.id, b.id) : null;
                    if (i != null) list.Add(i);
                }
            }
        if (tools)
            foreach (AnimalToolData t in db.AllTools)
            {
                ItemSO i = t != null && t.IsItem(AnimalIds.Tool(t.id)) ? Tool(t.id) : null;
                if (i != null) list.Add(i);
            }
        if (products)
        {
            var seen = new HashSet<string>();
            foreach (AnimalSpeciesData s in db.AllSpecies)
                if (s?.products != null)
                    foreach (AnimalProductSpec p in s.products)
                    {
                        if (p == null || !seen.Add(p.id)) continue;
                        ItemSO i = Product(p.id);
                        if (i != null) list.Add(i);
                    }
        }
        return list;
    }
}
