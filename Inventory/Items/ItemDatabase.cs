using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An item made at runtime that knows its own id (a crafted sword, a half-forged blade): the database names it by
/// that id, and a resolver registered with <see cref="ItemDatabase.AddResolver"/> finds it again from the id.
/// </summary>
public interface IRuntimeItem
{
    string RuntimeId { get; }
}

/// <summary>
/// A system that makes items at runtime (crafting) and can describe them for the network. Its items exist first on the
/// server; a client that meets an id it does not know (in its inventory, on another player, on the ground, in a trade)
/// asks the server, which <see cref="Export"/>s the item's definition, and the client <see cref="Import"/>s it - so a
/// sword forged on the server is the same sword, with the same numbers and the same look, on every machine.
/// Register one with <see cref="ItemDatabase.AddSource"/>.
/// </summary>
public interface IRuntimeItemSource
{
    /// <summary>Is <paramref name="id"/> one of this source's ids (by its prefix)?</summary>
    bool Owns(string id);

    /// <summary>The item, when this machine knows it (null otherwise: a client then asks the server).</summary>
    ItemSO Find(string id);

    /// <summary>The definition of an item this machine knows, as text (the server sends it). Null when unknown or when every machine can make it itself.</summary>
    string Export(string id);

    /// <summary>The item described by <paramref name="definition"/> (from the server); null when it is not valid. Not saved.</summary>
    ItemSO Import(string id, string definition);
}

/// <summary>
/// Every item of the game with a stable id, so items can be named over the network (and in saves) instead of by asset
/// reference. Network sessions need it: put one in a Resources folder named exactly "ItemDatabase" and fill it with
/// Tools ▸ SimpleMovements ▸ Project Setup ▸ Build Item Database (ids are the assets' GUIDs, identical in every build).
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "SimpleMovements/Items/Item Database", order = 10)]
public class ItemDatabase : ScriptableObject
{
    public const string ResourcesName = "ItemDatabase";

    [Serializable]
    public struct Entry
    {
        [Tooltip("Stable id (the asset's GUID).")]
        public string id;
        public ItemSO item;
    }

    [Tooltip("Every item that can be sent over the network or saved.")]
    public List<Entry> items = new List<Entry>();

    private Dictionary<string, ItemSO> byId;
    private Dictionary<ItemSO, string> idOf;
    private static ItemDatabase instance;
    private static bool searched;
    private static readonly List<Func<string, ItemSO>> resolvers = new List<Func<string, ItemSO>>();
    private static readonly List<IRuntimeItemSource> sources = new List<IRuntimeItemSource>();

    /// <summary>
    /// An id a source owns could not be found on this machine (a client: its definition is on the server). The network
    /// layer asks the server for it; raised every time it is looked up, so listeners deduplicate.
    /// </summary>
    public static event Action<string> RuntimeItemMissing;

    /// <summary>A runtime item's definition arrived and its id now resolves (inventories, world items and windows show it again).</summary>
    public static event Action<string> RuntimeItemDefined;

    /// <summary>A runtime item changed on this machine (the server: it was tried, renamed): clients that have it need its definition again.</summary>
    public static event Action<string> RuntimeItemChanged;

    /// <summary>The database in Resources/SimpleMovements/Databases/ItemDatabase (null if there is none).</summary>
    public static ItemDatabase Instance
    {
        get
        {
            if (instance == null && !searched)
            {
                searched = true;
                instance = SMAssets.Load<ItemDatabase>(SMAssets.Databases, ResourcesName);
            }
            return instance;
        }
    }

    /// <summary>Uses <paramref name="db"/> instead of the one in Resources (tests, custom loading).</summary>
    public static void Override(ItemDatabase db)
    {
        instance = db;
        searched = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        searched = false;
    }

    private void OnEnable()
    {
        byId = null;
        idOf = null;
    }

    private void BuildIndex()
    {
        byId = new Dictionary<string, ItemSO>(items.Count);
        idOf = new Dictionary<ItemSO, string>(items.Count, ReferenceComparer<ItemSO>.Instance);
        for (int i = 0; i < items.Count; i++)
        {
            Entry e = items[i];
            if (e.item == null || string.IsNullOrEmpty(e.id))
                continue;
            if (!byId.ContainsKey(e.id))
                byId.Add(e.id, e.item);
            if (!idOf.ContainsKey(e.item))
                idOf.Add(e.item, e.id);
        }
    }

    /// <summary>The item with <paramref name="id"/>, or null.</summary>
    public ItemSO Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (byId == null)
            BuildIndex();
        return byId.TryGetValue(id, out ItemSO item) ? item : null;
    }

    /// <summary>The id of <paramref name="item"/>, or null when it is not in the database.</summary>
    public string IdOf(ItemSO item)
    {
        if (item == null)
            return null;
        if (idOf == null)
            BuildIndex();
        return idOf.TryGetValue(item, out string id) ? id : null;
    }

    /// <summary>Adds (or re-ids) an item; used by the editor tool and by code that creates items at runtime.</summary>
    public void Register(string id, ItemSO item)
    {
        if (item == null || string.IsNullOrEmpty(id))
            return;
        items.RemoveAll(e => e.item == item || e.id == id);
        items.Add(new Entry { id = id, item = item });
        byId = null;
        idOf = null;
    }

    /// <summary>
    /// Finds items made at runtime by their id (<see cref="IRuntimeItem"/>) when the database does not hold them. The
    /// systems that make such items register one (crafting does, for worked pieces and finished weapons and armour).
    /// </summary>
    public static void AddResolver(Func<string, ItemSO> resolver)
    {
        if (resolver != null && !resolvers.Contains(resolver))
            resolvers.Add(resolver);
    }

    public static void RemoveResolver(Func<string, ItemSO> resolver) => resolvers.Remove(resolver);

    /// <summary>Registers a system that makes items at runtime and can export and import their definitions.</summary>
    public static void AddSource(IRuntimeItemSource source)
    {
        if (source != null && !sources.Contains(source))
            sources.Add(source);
    }

    public static void RemoveSource(IRuntimeItemSource source) => sources.Remove(source);

    private static IRuntimeItemSource SourceOf(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null && sources[i].Owns(id))
                return sources[i];
        return null;
    }

    /// <summary>Is <paramref name="id"/> the id of an item made at runtime (one a source owns)?</summary>
    public static bool IsRuntimeId(string id) => SourceOf(id) != null;

    /// <summary>The definition of a runtime item this machine knows (the server sends it to clients), or null.</summary>
    public static string ExportRuntime(string id) => SourceOf(id)?.Export(id);

    /// <summary>A runtime item from the server's definition; raises <see cref="RuntimeItemDefined"/>. Null when it is not valid.</summary>
    public static ItemSO ImportRuntime(string id, string definition)
    {
        IRuntimeItemSource source = SourceOf(id);
        ItemSO item = source?.Import(id, definition);
        if (item != null)
            RuntimeItemDefined?.Invoke(id);
        return item;
    }

    /// <summary>Tells the clients' copies that a runtime item changed (see <see cref="RuntimeItemChanged"/>).</summary>
    public static void NotifyRuntimeItemChanged(string id)
    {
        if (!string.IsNullOrEmpty(id))
            RuntimeItemChanged?.Invoke(id);
    }

    /// <summary>The id of an item from the default database (or its own runtime id; null when it has none).</summary>
    public static string Id(ItemSO item) => (Instance != null ? Instance.IdOf(item) : null) ?? (item as IRuntimeItem)?.RuntimeId;

    /// <summary>The item of an id from the default database (or a runtime item a resolver knows; null when none does).</summary>
    public static ItemSO Item(string id)
    {
        ItemSO item = Instance != null ? Instance.Find(id) : null;
        if (item != null || string.IsNullOrEmpty(id))
            return item;
        for (int i = 0; i < resolvers.Count; i++)
        {
            item = resolvers[i]?.Invoke(id);
            if (item != null)
                return item;
        }
        IRuntimeItemSource source = SourceOf(id);
        if (source == null)
            return null;
        item = source.Find(id);
        if (item == null)
            RuntimeItemMissing?.Invoke(id);
        return item;
    }
}
