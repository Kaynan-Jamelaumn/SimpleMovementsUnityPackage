using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The items of agriculture, by id: seeds and produce of every species, every soil material, tool, instrument and water
/// container. An asset bound in the Agriculture Database is used when there is one; otherwise the item is made at
/// runtime from the id (every machine makes the same item from the same id, so saves, merchants and the network resolve
/// it through the Item Database).
/// </summary>
public static class AgriItems
{
    private static readonly Dictionary<string, ItemSO> made = new Dictionary<string, ItemSO>();
    private static readonly Source source = new Source();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => made.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSource() => ItemDatabase.AddSource(source);

    private sealed class Source : IRuntimeItemSource
    {
        public bool Owns(string id) => IsAgriId(id);
        public ItemSO Find(string id) => Resolve(id);
        // Every machine makes the item from the id and its own Agriculture Database.
        public string Export(string id) => null;
        public ItemSO Import(string id, string definition) => Resolve(id);
    }

    public static bool IsAgriId(string id) => id != null && id.StartsWith(AgriIds.Prefix, System.StringComparison.Ordinal);

    public static ItemSO Seeds(string species) => Resolve(AgriIds.Seed(species));
    public static ItemSO Produce(string species) => Resolve(AgriIds.Produce(species));
    public static ItemSO Amendment(string id) => Resolve(AgriIds.Amendment(id));
    public static ItemSO Tool(string id) => Resolve(AgriIds.Tool(id));
    public static ItemSO Instrument(string id) => Resolve(AgriIds.Instrument(id));
    public static ItemSO Container(string id) => Resolve(AgriIds.Container(id));

    /// <summary>The item of an agricultural id: the bound asset, else one made at runtime (null for an unknown id).</summary>
    public static ItemSO Resolve(string id)
    {
        if (!IsAgriId(id)) return null;
        AgricultureDatabase db = AgricultureDatabase.Instance;
        ItemSO bound = db.BoundItem(id);
        if (bound != null) return bound;
        if (made.TryGetValue(id, out ItemSO cached) && cached != null) return cached;
        if (!AgriIds.Parse(id, out string kind, out string key)) return null;
        ItemSO item = null;
        switch (kind)
        {
            case "seed":
            {
                PlantSpeciesData d = db.Species(key);
                if (d == null) return null;
                var s = ScriptableObject.CreateInstance<SeedItemSO>();
                s.Setup(d, PlantArt.SeedIcon(d), PlantArt.Template("seed", PrimitiveType.Cube, new Color(0.72f, 0.6f, 0.42f), new Vector3(0.12f, 0.08f, 0.08f)), id);
                item = s;
                break;
            }
            case "produce":
            {
                PlantSpeciesData d = db.Species(key);
                if (d == null) return null;
                ProduceSpec p = d.Produce;
                GameObject model = PlantArt.Template("produce:" + d.id, p.part == ProducePart.Root ? PrimitiveType.Capsule : PrimitiveType.Sphere, p.colorRipe,
                    Vector3.one * Mathf.Clamp(p.pieceSize * 1.6f, 0.05f, 0.35f));
                bool food = (p.uses & PlantUses.Food) != 0 && p.nutrition > 0f;
                if (food)
                {
                    var f = ScriptableObject.CreateInstance<ProduceFoodSO>();
                    f.Setup(d, PlantArt.ProduceIcon(d), model, id);
                    item = f;
                }
                else
                {
                    var m = ScriptableObject.CreateInstance<ProduceMaterialSO>();
                    m.Setup(d, PlantArt.ProduceIcon(d), model, id);
                    item = m;
                }
                break;
            }
            case "amend":
            {
                SoilAmendmentData a = db.Amendment(key);
                if (a == null) return null;
                var s = ScriptableObject.CreateInstance<SoilAmendmentSO>();
                s.Setup(a, PlantArt.AmendmentIcon(a), PlantArt.Template("amend:" + a.id, PrimitiveType.Cube, a.color, new Vector3(0.25f, 0.3f, 0.18f)), id);
                item = s;
                break;
            }
            case "tool":
            {
                FarmToolPreset p = AgriDefaults.Tools().Find(t => t.id == key);
                if (p == null) return null;
                var s = ScriptableObject.CreateInstance<FarmToolSO>();
                s.Setup(p, PlantArt.ToolIcon(p.kind), PlantArt.Template("tool:" + p.kind, PrimitiveType.Cylinder, new Color(0.55f, 0.4f, 0.25f), new Vector3(0.05f, 0.5f, 0.05f)), id);
                item = s;
                break;
            }
            case "instr":
            {
                InstrumentPreset p = AgriDefaults.Instruments().Find(t => t.id == key);
                if (p == null) return null;
                var s = ScriptableObject.CreateInstance<SoilInstrumentSO>();
                s.Setup(p, PlantArt.InstrumentIcon(p.kind), PlantArt.Template("instr:" + p.kind, PrimitiveType.Cube, new Color(0.75f, 0.6f, 0.3f), new Vector3(0.15f, 0.08f, 0.2f)), id);
                item = s;
                break;
            }
            case "water":
            {
                ContainerPreset p = AgriDefaults.Containers().Find(t => t.id == key);
                if (p == null) return null;
                var s = ScriptableObject.CreateInstance<WaterContainerSO>();
                s.Setup(p, PlantArt.ContainerIcon(p), PlantArt.Template("water:" + p.id, PrimitiveType.Cylinder, new Color(0.5f, 0.6f, 0.55f), new Vector3(0.25f, 0.18f, 0.25f)), id);
                item = s;
                break;
            }
        }
        if (item == null) return null;
        item.hideFlags = HideFlags.DontSave;
        ((Object)item).name = id.Substring(AgriIds.Prefix.Length).Replace('/', '_');
        made[id] = item;
        return item;
    }

    /// <summary>The alchemy ingredient produce of a species gives its substances as (by its produce's Alchemy Ingredient name).</summary>
    public static AlchemyIngredientProfile AlchemyProfileOf(PlantSpeciesData d)
    {
        string name = d?.Produce.alchemyIngredient;
        if (string.IsNullOrWhiteSpace(name)) return null;
        AlchemyIngredientSO ing = AlchemyItems.RuntimeIngredient(name);
        return ing != null ? ing.AlchemyProfile : null;
    }

    /// <summary>Every built-in agricultural item, for merchants, chests and testing (seeds of every species, every material, tool, instrument, container).</summary>
    public static List<ItemSO> All(bool seeds = true, bool materials = true, bool equipment = true)
    {
        var list = new List<ItemSO>();
        AgricultureDatabase db = AgricultureDatabase.Instance;
        if (seeds)
            foreach (PlantSpeciesData s in db.AllSpecies)
            {
                ItemSO i = Seeds(s.id);
                if (i != null) list.Add(i);
            }
        if (materials)
            foreach (SoilAmendmentData a in db.AllAmendments)
            {
                ItemSO i = Amendment(a.id);
                if (i != null) list.Add(i);
            }
        if (equipment)
        {
            foreach (FarmToolPreset p in AgriDefaults.Tools()) { ItemSO i = Tool(p.id); if (i != null) list.Add(i); }
            foreach (InstrumentPreset p in AgriDefaults.Instruments()) { ItemSO i = Instrument(p.id); if (i != null) list.Add(i); }
            foreach (ContainerPreset p in AgriDefaults.Containers()) { ItemSO i = Container(p.id); if (i != null) list.Add(i); }
        }
        return list;
    }
}
