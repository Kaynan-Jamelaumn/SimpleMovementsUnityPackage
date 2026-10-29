using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The items harvesting gives: for each material of each creature, the item bound to it in the Harvest Database, else an
/// item made at runtime - a <see cref="HarvestedFoodSO"/> for what can be eaten, a <see cref="HarvestedMaterialSO"/> for
/// the rest - named "harvest:creature/material", so every machine makes the same item from the same id (saves, the
/// network and other players' inventories name it by that id). Hides with a tannery hide become worked hide pieces of
/// the crafting system (<see cref="HarvestHides"/>).
/// </summary>
public static class HarvestItems
{
    /// <summary>Prefix of the ids of harvested items made at runtime.</summary>
    public const string IdPrefix = "harvest:";

    private static readonly Dictionary<string, ItemSO> made = new Dictionary<string, ItemSO>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => made.Clear();

    /// <summary>
    /// Forgets the items made so far (made again on demand from the creatures as they are then); outside Play Mode they
    /// are destroyed. For tests and tools that change the creatures between uses.
    /// </summary>
    public static void ClearCaches()
    {
        if (!Application.isPlaying)
            foreach (ItemSO i in made.Values)
                if (i != null) UnityEngine.Object.DestroyImmediate(i);
        made.Clear();
    }

    private static readonly Source source = new Source();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSource() => ItemDatabase.AddSource(source);

    private sealed class Source : IRuntimeItemSource
    {
        public bool Owns(string id) => IsHarvestId(id);
        public ItemSO Find(string id) => Resolve(id);
        // Every machine makes the item from the id and its own Harvest Database.
        public string Export(string id) => null;
        public ItemSO Import(string id, string definition) => Resolve(id);
    }

    public static bool IsHarvestId(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(IdPrefix, StringComparison.Ordinal);

    public static string IdOf(string anatomy, string material) => IdPrefix + anatomy + "/" + material;

    /// <summary>The item of a runtime id ("harvest:bear/claws"), or null when the database has no such material.</summary>
    public static ItemSO Resolve(string id)
    {
        if (!IsHarvestId(id))
            return null;
        string rest = id.Substring(IdPrefix.Length);
        int slash = rest.IndexOf('/');
        if (slash <= 0)
            return null;
        HarvestAnatomyData a = HarvestDatabase.Instance.Anatomy(rest.Substring(0, slash));
        HarvestMaterial m = a?.Material(rest.Substring(slash + 1));
        return m != null ? Made(a, m) : null;
    }

    /// <summary>
    /// The item given for <paramref name="m"/> of <paramref name="a"/>: the Harvest Database's asset for it, else the item
    /// made at runtime.
    /// </summary>
    public static ItemSO ItemFor(HarvestAnatomyData a, HarvestMaterial m)
    {
        if (a == null || m == null)
            return null;
        ItemSO bound = HarvestDatabase.Instance.BoundItem(a.id, m.id);
        return bound != null ? bound : Made(a, m);
    }

    private static ItemSO Made(HarvestAnatomyData a, HarvestMaterial m)
    {
        string id = IdOf(a.id, m.id);
        if (made.TryGetValue(id, out ItemSO cached) && cached != null)
            return cached;
        ItemSO item;
        if (m.IsFood)
        {
            var food = ScriptableObject.CreateInstance<HarvestedFoodSO>();
            food.Setup(a, m, HarvestArt.Icon(a, m), HarvestArt.Template(a, m), id);
            item = food;
        }
        else
        {
            var mat = ScriptableObject.CreateInstance<HarvestedMaterialSO>();
            mat.Setup(a, m, HarvestArt.Icon(a, m), HarvestArt.Template(a, m), id);
            item = mat;
        }
        item.hideFlags = HideFlags.DontSave;
        ((UnityEngine.Object)item).name = $"{a.id}_{m.id}";
        made[id] = item;
        return item;
    }

    /// <summary>Does <paramref name="item"/> keep a value per copy (its quality and marks)?</summary>
    public static bool KeepsQuality(ItemSO item) => item is IHarvestedItem;

    /// <summary>The values of <paramref name="count"/> copies of a piece (one per copy), for an item that keeps them; else null.</summary>
    public static List<int> Codes(ItemSO item, int count, double quality, HarvestMarks marks)
    {
        if (!KeepsQuality(item) || count <= 0)
            return null;
        int code = HarvestItemCode.Encode(quality, marks);
        var list = new List<int>(count);
        for (int i = 0; i < count; i++) list.Add(code);
        return list;
    }
}
