#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>"1 - 3" on one line.</summary>
[CustomPropertyDrawer(typeof(DropAmount))]
public class DropAmountDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect r = EditorGUI.PrefixLabel(position, label);
        float w = (r.width - 20f) * 0.5f;
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        SerializedProperty min = property.FindPropertyRelative("min"), max = property.FindPropertyRelative("max");
        min.intValue = Mathf.Max(0, EditorGUI.IntField(new Rect(r.x, r.y, w, r.height), min.intValue));
        EditorGUI.LabelField(new Rect(r.x + w + 4f, r.y, 12f, r.height), "-");
        max.intValue = Mathf.Max(min.intValue, EditorGUI.IntField(new Rect(r.x + w + 20f, r.y, w, r.height), max.intValue));
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}

/// <summary>Drop table inspector: setup checks, simulated drops at a tier, and adding the items selected in the Project.</summary>
[CustomEditor(typeof(ItemDropTable))]
public class ItemDropTableEditor : Editor
{
    private static int simTier;
    private static int simRuns = 1000;
    private string simResult = "";

    public override void OnInspectorGUI()
    {
        var t = (ItemDropTable)target;
        var errors = new List<string>();
        var warnings = new List<string>();
        t.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        ItemDropManager m = ItemDropManager.Instance;
        if (!m.AllTables().Contains(t))
        {
            EditorGUILayout.HelpBox("This table is not registered in the Item Drop Manager (it still works where it is assigned).", MessageType.Info);
            if (AssetDatabase.Contains(m) && GUILayout.Button("Register In The Item Drop Manager"))
            {
                Undo.RecordObject(m, "Register drop table");
                m.tables.Add(t);
                EditorUtility.SetDirty(m);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Add Selected Items To Pool", "Adds the item assets selected in the Project window to the random pool.")))
                AddSelected(t, false);
            if (GUILayout.Button(new GUIContent("Add Selected As Guaranteed", "Adds the selected item assets as guaranteed drops.")))
                AddSelected(t, true);
        }

        EditorGUILayout.Space();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Simulate", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            simTier = EditorGUILayout.IntSlider("Tier", simTier, 0, 10);
            simRuns = EditorGUILayout.IntField(simRuns, GUILayout.Width(60));
            if (GUILayout.Button("Roll", GUILayout.Width(60)))
                simResult = Simulate(t, simTier, Mathf.Clamp(simRuns, 1, 100000));
        }
        if (!string.IsNullOrEmpty(simResult))
            EditorGUILayout.HelpBox(simResult, MessageType.None);
    }

    private static void AddSelected(ItemDropTable t, bool guaranteed)
    {
        ItemSO[] items = Selection.objects.OfType<ItemSO>().ToArray();
        if (items.Length == 0)
        {
            EditorUtility.DisplayDialog("Item Drop Table", "Select item assets in the Project window first (this table stays in the Inspector when you lock it).", "OK");
            return;
        }
        Undo.RecordObject(t, "Add drop items");
        List<ItemDropEntry> list = guaranteed ? t.guaranteed : t.pool;
        foreach (ItemSO i in items)
            if (!list.Exists(d => d != null && d.item == i))
                list.Add(new ItemDropEntry { item = i });
        EditorUtility.SetDirty(t);
    }

    private static string Simulate(ItemDropTable t, int tier, int runs)
    {
        var rng = new System.Random(12345);
        var totals = new Dictionary<ItemSO, long>();
        var hits = new Dictionary<ItemSO, int>();
        int empty = 0;
        var buffer = new List<ItemStack>();
        ItemDropManager m = ItemDropManager.Instance;
        for (int r = 0; r < runs; r++)
        {
            buffer.Clear();
            t.Roll(tier, rng, buffer, m.dropChanceMultiplier, m.quantityMultiplier);
            if (buffer.Count == 0) empty++;
            foreach (ItemStack s in buffer)
            {
                totals[s.item] = (totals.TryGetValue(s.item, out long v) ? v : 0) + s.quantity;
                hits[s.item] = (hits.TryGetValue(s.item, out int h) ? h : 0) + 1;
            }
        }
        var lines = new List<string> { $"{runs} drops at tier {tier}: {empty * 100f / runs:0.#}% gave nothing." };
        foreach (var kv in totals.OrderByDescending(k => hits[k.Key]))
            lines.Add($"{kv.Key.Name}: in {hits[kv.Key] * 100f / runs:0.#}% of drops, {kv.Value / (float)runs:0.##} on average");
        return string.Join("\n", lines);
    }
}

/// <summary>Item Drop Manager inspector: the registry, finding tables, and every item that can drop and from where.</summary>
[CustomEditor(typeof(ItemDropManager))]
public class ItemDropManagerEditor : Editor
{
    private bool showItems = true;

    public override void OnInspectorGUI()
    {
        var m = (ItemDropManager)target;
        EditorGUILayout.HelpBox("Every drop table of the game and the defaults for chests, breakables (urns, barrels, crates), mobs, " +
                                "elites and bosses. Dungeon loot and prop entries, chests, breakables and Item Drop On Death " +
                                "components can use their own table; everything else falls back to these.", MessageType.None);
        if (GUILayout.Button(new GUIContent("Find All Drop Tables In The Project", "Registers every Item Drop Table asset.")))
        {
            Undo.RecordObject(m, "Find drop tables");
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDropTable"))
            {
                var t = AssetDatabase.LoadAssetAtPath<ItemDropTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && !m.tables.Contains(t)) m.tables.Add(t);
            }
            EditorUtility.SetDirty(m);
        }
        if (GUILayout.Button("Create New Drop Table"))
            ItemDropMenu.CreateTable(m);
        EditorGUILayout.Space();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        showItems = EditorGUILayout.Foldout(showItems, "Items that can drop (from every table)", true);
        if (showItems)
        {
            var sources = new Dictionary<ItemSO, List<string>>();
            foreach (ItemDropTable t in m.AllTables())
            {
                var set = new HashSet<ItemSO>();
                t.CollectItems(set);
                foreach (ItemSO i in set)
                {
                    if (!sources.TryGetValue(i, out List<string> l)) sources[i] = l = new List<string>();
                    l.Add(t.Name);
                }
            }
            if (sources.Count == 0)
                EditorGUILayout.LabelField("Nothing yet: add items to drop tables.", EditorStyles.miniLabel);
            foreach (var kv in sources.OrderBy(k => k.Key.Name))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(kv.Key, typeof(ItemSO), false, GUILayout.Width(200));
                    EditorGUILayout.LabelField((m.IsBlocked(kv.Key) ? "BLOCKED - " : "") + string.Join(", ", kv.Value), EditorStyles.miniLabel);
                }
            }
        }
    }
}

public static class ItemDropMenu
{
    [MenuItem(SMMenu.Root + "Inventory/Item Drops/Create Item Drop Manager", false, SMMenu.Inventory + 30)]
    public static void CreateManager()
    {
        if (SMAssetsEditor.Find<ItemDropManager>(SMAssets.ItemDrops, ItemDropManager.ResourcesName) != null)
        {
            SMAssetsEditor.FindOrCreate<ItemDropManager>(SMAssets.ItemDrops, ItemDropManager.ResourcesName);
            return;
        }
        ItemDropManager m = SMAssetsEditor.FindOrCreate<ItemDropManager>(SMAssets.ItemDrops, ItemDropManager.ResourcesName);
        // A starting point: chest and breakable tables to fill.
        m.chestTable = CreateTable(m, "Chest Drops", false);
        m.breakableTable = CreateTable(m, "Breakable Drops", false);
        if (m.breakableTable != null)
        {
            m.breakableTable.rolls = new DropAmount(1, 1);
            m.breakableTable.nothingChance = 0.5f;
            m.breakableTable.extraRollsPerTier = 0.2f;
            EditorUtility.SetDirty(m.breakableTable);
        }
        ItemDropManager.Override(m);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Selection.activeObject = m;
        Debug.Log($"[Item Drops] Created the Item Drop Manager at {AssetDatabase.GetAssetPath(m)} with empty Chest and Breakable tables (in {SMAssets.FolderPath(SMAssets.DropTables)}): add items to them.", m);
    }

    [MenuItem(SMMenu.Root + "Inventory/Item Drops/Open Item Drop Manager", false, SMMenu.Inventory + 31)]
    private static void Open()
    {
        var m = SMAssetsEditor.Find<ItemDropManager>(SMAssets.ItemDrops, ItemDropManager.ResourcesName);
        if (m == null) CreateManager();
        else Selection.activeObject = m;
    }

    /// <summary>Creates a drop table next to the manager (registered in it).</summary>
    public static ItemDropTable CreateTable(ItemDropManager m, string name = "New Drop Table", bool select = true)
    {
        var t = ScriptableObject.CreateInstance<ItemDropTable>();
        t.displayName = name;
        AssetDatabase.CreateAsset(t, SMAssetsEditor.UniquePath(SMAssets.DropTables, name + ".asset"));
        if (m != null && AssetDatabase.Contains(m))
        {
            Undo.RecordObject(m, "Create drop table");
            m.tables.Add(t);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        if (select) Selection.activeObject = t;
        return t;
    }
}
#endif
