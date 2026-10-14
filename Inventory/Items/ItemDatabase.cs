using System;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>The database in Resources/ItemDatabase (null if there is none).</summary>
    public static ItemDatabase Instance
    {
        get
        {
            if (instance == null && !searched)
            {
                searched = true;
                instance = Resources.Load<ItemDatabase>(ResourcesName);
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

    /// <summary>The id of an item from the default database (null without one, or when the item is not in it).</summary>
    public static string Id(ItemSO item) => Instance != null ? Instance.IdOf(item) : null;

    /// <summary>The item of an id from the default database (null without one).</summary>
    public static ItemSO Item(string id) => Instance != null ? Instance.Find(id) : null;
}
