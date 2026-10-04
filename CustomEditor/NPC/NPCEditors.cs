#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Shared drawing for the NPC inspectors: throttled validation, problem boxes, small headers.</summary>
public static class NPCEditorGUI
{
    public static void Problems(List<string> errors, List<string> warnings)
    {
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
    }

    public static void Section(string title)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    public static void Icon(Sprite sprite, float size)
    {
        Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        if (sprite != null)
        {
            Texture2D tex = AssetPreview.GetAssetPreview(sprite);
            GUI.DrawTexture(r, tex != null ? tex : sprite.texture, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.2f));
        }
    }

    /// <summary>The nearest player to <paramref name="at"/> (play mode tools).</summary>
    public static GameObject NearestPlayer(Vector3 at)
    {
        GameObject best = null;
        float bestSq = float.MaxValue;
        foreach (PlayerStatusController p in UnityEngine.Object.FindObjectsByType<PlayerStatusController>(FindObjectsInactive.Exclude))
        {
            float d = (p.transform.position - at).sqrMagnitude;
            if (d < bestSq) { bestSq = d; best = p.gameObject; }
        }
        return best;
    }
}

// ====================================================================== NPC
[CustomEditor(typeof(NPC))]
public class NPCEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private double nextValidation;

    public override void OnInspectorGUI()
    {
        var npc = (NPC)target;
        serializedObject.Update();
        if (EditorApplication.timeSinceStartup >= nextValidation)
        {
            nextValidation = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            npc.Validate(errors, warnings);
            NPCSetupMenu.CheckInteractorReach(npc, warnings);
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        NPCEditorGUI.Icon(npc.Portrait, 40f);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(npc.DisplayName + (string.IsNullOrWhiteSpace(npc.Title) ? "" : $"  ·  {npc.Title}"), EditorStyles.boldLabel);
        var options = new List<string>();
        foreach (NPCBehaviour b in npc.GetComponents<NPCBehaviour>())
            if (b != null) options.Add($"{b.OptionLabel} ({ObjectNames.NicifyVariableName(b.GetType().Name)}){(b.enabled ? "" : " - off")}");
        EditorGUILayout.LabelField(options.Count > 0 ? "Options: " + string.Join(", ", options) : "No options yet: add a Merchant or an NPC Dialogue.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("+ Merchant", "Adds a shop (Trade option).")))
            Undo.AddComponent<Merchant>(npc.gameObject);
        if (GUILayout.Button(new GUIContent("+ Dialogue", "Adds a Talk option with lines.")))
            Undo.AddComponent<NPCDialogue>(npc.gameObject);
        using (new EditorGUI.DisabledScope(npc.GetComponentInChildren<Collider>(true) != null))
        {
            if (GUILayout.Button(new GUIContent("+ Collider", "A capsule the Player Interactor can find.")))
            {
                var c = Undo.AddComponent<CapsuleCollider>(npc.gameObject);
                c.center = new Vector3(0f, 1f, 0f);
                c.height = 2f;
                c.radius = 0.4f;
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        NPCEditorGUI.Problems(errors, warnings);
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
        {
            NPCEditorGUI.Section("Live");
            if (npc.Sessions.Count == 0)
                EditorGUILayout.LabelField("No conversation.", EditorStyles.miniLabel);
            foreach (NPCInteractionSession s in npc.Sessions)
                EditorGUILayout.LabelField($"With {(s.Player != null ? s.Player.name : "?")} · {(s.ActiveBehaviour != null ? s.ActiveBehaviour.OptionLabel : "menu")} · {Time.time - s.StartTime:0}s");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open For Nearest Player"))
            {
                GameObject p = NPCEditorGUI.NearestPlayer(npc.transform.position);
                if (p != null) npc.StartSession(p);
                else Debug.LogWarning("[NPC] No player in the scene.", npc);
            }
            using (new EditorGUI.DisabledScope(npc.Sessions.Count == 0))
                if (GUILayout.Button("End Conversations"))
                    npc.EndAllSessions();
            EditorGUILayout.EndHorizontal();
            Repaint();
        }
    }
}

// ====================================================================== Merchant
[CustomEditor(typeof(Merchant))]
public class MerchantEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private double nextValidation;
    private bool showPreview = true;

    public override void OnInspectorGUI()
    {
        var merchant = (Merchant)target;
        serializedObject.Update();
        if (EditorApplication.timeSinceStartup >= nextValidation)
        {
            nextValidation = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            try { merchant.Validate(errors, warnings); }
            catch (Exception e) { errors.Add("Validation failed: " + e.Message); }
        }
        NPCEditorGUI.Problems(errors, warnings);

        // Goods tools.
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Goods", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        var selected = new List<ItemSO>();
        foreach (UnityEngine.Object o in Selection.objects)
            if (o is ItemSO i) selected.Add(i);
        using (new EditorGUI.DisabledScope(selected.Count == 0))
            if (GUILayout.Button(new GUIContent($"Add Selected Items ({selected.Count})", "Adds the item assets selected in the Project window (lock this inspector first).")))
                AddItems(merchant, selected);
        if (GUILayout.Button(new GUIContent("Add Items From Folder…", "Adds every item asset of a folder (and its subfolders).")))
        {
            string abs = EditorUtility.OpenFolderPanel("Folder with items", Application.dataPath, "");
            if (!string.IsNullOrEmpty(abs))
            {
                string rel = abs.StartsWith(Application.dataPath) ? "Assets" + abs.Substring(Application.dataPath.Length) : null;
                if (rel == null) EditorUtility.DisplayDialog("Merchant", "Pick a folder inside the project's Assets folder.", "OK");
                else AddItems(merchant, NPCSetupMenu.ItemsIn(rel));
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Sort By Category", "Orders the goods as their tabs: category, subcategory, then name.")))
            SortByCategory(merchant);
        if (GUILayout.Button(new GUIContent("Remove Empty", "Removes entries without an item.")))
        {
            Undo.RecordObject(merchant, "Remove empty goods");
            merchant.StockEntries.RemoveAll(e => e == null || e.item == null);
            Changed(merchant);
        }
        using (new EditorGUI.DisabledScope(merchant.StockEntries.Count == 0))
            if (GUILayout.Button(new GUIContent("Save As Stock Asset…", "Moves the goods into a Merchant Stock asset other merchants can share.")))
                SaveAsStockAsset(merchant);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        DrawPreview(merchant);
        if (Application.isPlaying)
            DrawLive(merchant);
    }

    private static void AddItems(Merchant merchant, List<ItemSO> items)
    {
        Undo.RecordObject(merchant, "Add goods");
        int added = 0;
        foreach (ItemSO item in items)
        {
            if (item == null || merchant.StockEntries.Exists(e => e != null && e.item == item))
                continue;
            int qty = item.StackMax > 1 ? Mathf.Min(item.StackMax, 20) : 3;
            merchant.StockEntries.Add(new MerchantStockEntry(item, qty));
            added++;
        }
        Changed(merchant);
        Debug.Log($"[Merchant] Added {added} item(s) to {merchant.ShopName}{(added < items.Count ? $" ({items.Count - added} already sold)" : "")}.", merchant);
    }

    private static void SortByCategory(Merchant merchant)
    {
        Undo.RecordObject(merchant, "Sort goods");
        ItemCategoryDatabase db = merchant.Categories;
        merchant.StockEntries.Sort((a, b) =>
        {
            if (a?.item == null) return 1;
            if (b?.item == null) return -1;
            ItemCategoryMatch ca = merchant.CategoryOf(a.item, a), cb = merchant.CategoryOf(b.item, b);
            int c = ItemCategoryDatabase.Compare(ca.Top, cb.Top);
            if (c == 0) c = ItemCategoryDatabase.Compare(ca.Sub, cb.Sub);
            return c != 0 ? c : string.Compare(a.item.Name, b.item.Name, StringComparison.OrdinalIgnoreCase);
        });
        Changed(merchant);
    }

    private static void SaveAsStockAsset(Merchant merchant)
    {
        string path = EditorUtility.SaveFilePanelInProject("Save goods", $"{merchant.ShopName} Stock", "asset", "Where to save the Merchant Stock asset.");
        if (string.IsNullOrEmpty(path))
            return;
        var asset = ScriptableObject.CreateInstance<MerchantStock>();
        foreach (MerchantStockEntry e in merchant.StockEntries)
            if (e != null)
                asset.Entries.Add(JsonUtility.FromJson<MerchantStockEntry>(JsonUtility.ToJson(e)));
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        var so = new SerializedObject(merchant);
        so.FindProperty("stockAsset").objectReferenceValue = asset;
        so.FindProperty("stock").ClearArray();
        so.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(asset);
    }

    private static void Changed(Merchant merchant)
    {
        EditorUtility.SetDirty(merchant);
        PrefabUtility.RecordPrefabInstancePropertyModifications(merchant);
    }

    private void DrawPreview(Merchant merchant)
    {
        EditorGUILayout.Space(6);
        showPreview = EditorGUILayout.Foldout(showPreview, "Shop preview (tabs and prices)", true);
        if (!showPreview)
            return;
        List<MerchantStockEntry> entries = merchant.AllEntries();
        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("No goods yet.", MessageType.None);
            return;
        }
        var tabs = new Dictionary<ItemCategory, int>();
        CurrencyDefinition currency = merchant.CurrencyFor(null);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(24f);
        EditorGUILayout.LabelField("Item", EditorStyles.miniBoldLabel, GUILayout.MinWidth(90f));
        EditorGUILayout.LabelField("Listed under", EditorStyles.miniBoldLabel, GUILayout.MinWidth(90f));
        EditorGUILayout.LabelField("Buy", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
        EditorGUILayout.LabelField("Sell", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
        EditorGUILayout.EndHorizontal();
        foreach (MerchantStockEntry e in entries)
        {
            if (e?.item == null)
                continue;
            ItemCategoryMatch cat = merchant.CategoryOf(e.item, e);
            bool shown = merchant.IsCategoryShown(cat.Top);
            if (cat.Top != null && shown)
                tabs[cat.Top] = tabs.TryGetValue(cat.Top, out int n) ? n + 1 : 1;
            EditorGUILayout.BeginHorizontal();
            NPCEditorGUI.Icon(e.item.Icon, 20f);
            EditorGUILayout.LabelField(new GUIContent($"{e.item.Name}{(e.unlimited ? "  ∞" : $"  ×{e.quantity}")}", e.item.name), GUILayout.MinWidth(90f));
            EditorGUILayout.LabelField(shown ? cat.Path : $"{cat.Path} (tab hidden)", EditorStyles.miniLabel, GUILayout.MinWidth(90f));
            EditorGUILayout.LabelField(currency.FormatNumber(merchant.PreviewBuyPrice(e)), GUILayout.Width(56f));
            string sell = merchant.WillBuy(e.item, out _) ? currency.FormatNumber(merchant.PreviewSellPrice(e.item)) : "-";
            EditorGUILayout.LabelField(sell, GUILayout.Width(56f));
            EditorGUILayout.EndHorizontal();
        }
        var order = new List<ItemCategory>(tabs.Keys);
        order.Sort(ItemCategoryDatabase.Compare);
        var labels = new List<string>();
        if (merchant.ShowAllTab) labels.Add("All");
        foreach (ItemCategory c in order) labels.Add($"{c.DisplayName} ({tabs[c]})");
        EditorGUILayout.LabelField($"Tabs: {string.Join(" · ", labels)}   (prices in {currency.DisplayName}; sell = an undamaged item)", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawLive(Merchant merchant)
    {
        NPCEditorGUI.Section("Live stock");
        foreach (MerchantStockSlot slot in merchant.Inventory.Slots)
        {
            EditorGUILayout.BeginHorizontal();
            NPCEditorGUI.Icon(slot.Item != null ? slot.Item.Icon : null, 18f);
            EditorGUILayout.LabelField($"{(slot.Item != null ? slot.Item.Name : "?")}{(slot.IsBuyback ? "  (sold by the player)" : "")}");
            EditorGUILayout.LabelField(slot.Unlimited ? "∞" : slot.Quantity.ToString(), GUILayout.Width(50f));
            EditorGUILayout.EndHorizontal();
        }
        float next = merchant.SecondsUntilRestock;
        EditorGUILayout.LabelField(next >= 0f ? $"Next restock in {next / 60f:0.0} min" : "No restocking", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Restock Now")) merchant.Restock();
        if (GUILayout.Button("Reset Stock")) merchant.RebuildStock();
        if (GUILayout.Button("Open Shop For Nearest Player"))
        {
            GameObject p = NPCEditorGUI.NearestPlayer(merchant.transform.position);
            NPC npc = merchant.Npc;
            if (p != null && npc != null)
            {
                NPCInteractionSession s = npc.StartSession(p);
                if (s != null && s.ActiveBehaviour != merchant) npc.OpenBehaviour(s, merchant, false);
            }
        }
        EditorGUILayout.EndHorizontal();
        Repaint();
    }
}

// ====================================================================== categories
[CustomEditor(typeof(ItemCategoryDatabase))]
public class ItemCategoryDatabaseEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private double nextValidation;
    private bool showPreview;
    private List<(ItemSO item, string path, bool fallback)> preview;

    public override void OnInspectorGUI()
    {
        var db = (ItemCategoryDatabase)target;
        serializedObject.Update();
        if (EditorApplication.timeSinceStartup >= nextValidation)
        {
            nextValidation = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            db.Validate(errors, warnings);
        }
        bool isAsset = EditorUtility.IsPersistent(db);
        if (db.Categories.Count == 0)
        {
            EditorGUILayout.HelpBox("No categories yet. Start from the standard ones (Weapons, Armor, Accessories, Consumables, Materials, Miscellaneous with subcategories) and change them.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!isAsset))
                if (GUILayout.Button("Fill With The Standard Categories"))
                    NPCSetupMenu.FillWithDefaults(db);
        }
        NPCEditorGUI.Problems(errors, warnings);

        // The tree.
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Categories (tabs) and subcategories", EditorStyles.boldLabel);
        foreach (ItemCategory top in db.TopCategories())
        {
            DrawRow(db, top, 0, isAsset);
            foreach (ItemCategory sub in db.ChildrenOf(top))
                DrawRow(db, sub, 1, isAsset);
        }
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!isAsset))
        {
            if (GUILayout.Button("+ Category"))
                AddCategory(db, null);
            if (EditorGUILayout.DropdownButton(new GUIContent("+ Subcategory Of…"), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach (ItemCategory top in db.TopCategories())
                {
                    ItemCategory parent = top;
                    menu.AddItem(new GUIContent(top.DisplayName), false, () => AddCategory(db, parent));
                }
                menu.ShowAsContext();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        showPreview = EditorGUILayout.Foldout(showPreview, "Where the project's items go", true);
        if (showPreview)
        {
            if (preview == null || GUILayout.Button("Refresh"))
                BuildPreview(db);
            int unmatched = preview.FindAll(p => p.fallback).Count;
            EditorGUILayout.LabelField($"{preview.Count} item(s); {unmatched} only in the fallback category.", EditorStyles.miniLabel);
            foreach ((ItemSO item, string path, bool fallback) in preview)
            {
                EditorGUILayout.BeginHorizontal();
                NPCEditorGUI.Icon(item.Icon, 18f);
                if (GUILayout.Button(item.Name, EditorStyles.label, GUILayout.MinWidth(120f)))
                    EditorGUIUtility.PingObject(item);
                EditorGUILayout.LabelField(fallback ? $"{path}  (no rule matched)" : path, fallback ? EditorStyles.boldLabel : EditorStyles.label);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private static void DrawRow(ItemCategoryDatabase db, ItemCategory c, int indent, bool isAsset)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indent * 18f);
        NPCEditorGUI.Icon(c.Icon, 16f);
        if (GUILayout.Button(new GUIContent(c.DisplayName, c.Path), indent == 0 ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.MinWidth(110f)))
            Selection.activeObject = c;
        string rules = c.Rules.Count > 0 ? c.Rules[0]?.Describe() + (c.Rules.Count > 1 ? $" (+{c.Rules.Count - 1})" : "") : c.Items.Count > 0 ? $"{c.Items.Count} listed item(s)" : "no rules";
        EditorGUILayout.LabelField(rules, EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(!isAsset || AssetDatabase.GetAssetPath(c) != AssetDatabase.GetAssetPath(db)))
            if (GUILayout.Button(new GUIContent("✕", "Remove this category (and leave its subcategories without a parent)."), GUILayout.Width(22f)))
                RemoveCategory(db, c);
        EditorGUILayout.EndHorizontal();
    }

    private static void AddCategory(ItemCategoryDatabase db, ItemCategory parent)
    {
        var c = CreateInstance<ItemCategory>();
        c.Configure(parent == null ? "New Category" : $"New {parent.DisplayName} Subcategory", parent, 100, 0);
        AssetDatabase.AddObjectToAsset(c, db);
        Undo.RegisterCreatedObjectUndo(c, "Add category");
        var so = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("categories");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = c;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        db.ClearCache();
        Selection.activeObject = c;
    }

    private static void RemoveCategory(ItemCategoryDatabase db, ItemCategory c)
    {
        if (!EditorUtility.DisplayDialog("Categories", $"Remove '{c.Path}'?", "Remove", "Cancel"))
            return;
        var so = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("categories");
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == c)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }
        so.ApplyModifiedProperties();
        Undo.DestroyObjectImmediate(c);
        AssetDatabase.SaveAssets();
        db.ClearCache();
    }

    private void BuildPreview(ItemCategoryDatabase db)
    {
        preview = new List<(ItemSO, string, bool)>();
        ItemCategory fallback = db.Fallback;
        foreach (ItemSO item in NPCSetupMenu.AllItems())
        {
            ItemCategoryMatch m = db.Resolve(item);
            bool fb = m.Leaf == fallback && item.Category == null && !(fallback != null && fallback.MatchesRules(item));
            preview.Add((item, m.Path, fb));
        }
        preview.Sort((a, b) => string.Compare(a.path + a.item.Name, b.path + b.item.Name, StringComparison.OrdinalIgnoreCase));
    }
}

[CustomEditor(typeof(ItemCategory))]
public class ItemCategoryEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private List<ItemSO> matched;

    public override void OnInspectorGUI()
    {
        var c = (ItemCategory)target;
        serializedObject.Update();
        errors.Clear();
        warnings.Clear();
        c.Validate(errors, warnings);
        EditorGUILayout.LabelField(c.Path, EditorStyles.boldLabel);
        NPCEditorGUI.Problems(errors, warnings);
        DrawPropertiesExcluding(serializedObject, "m_Script");
        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed)
        {
            matched = null;
            ItemCategoryDatabase.Default.ClearCache();
        }
        EditorGUILayout.Space(6);
        if (matched == null || GUILayout.Button("Refresh Matching Items"))
        {
            matched = new List<ItemSO>();
            foreach (ItemSO item in NPCSetupMenu.AllItems())
                if (item.Category == c || c.Lists(item) || c.MatchesRules(item))
                    matched.Add(item);
        }
        EditorGUILayout.LabelField($"Items this category takes ({matched.Count}) - before other categories are considered:", EditorStyles.miniLabel);
        foreach (ItemSO item in matched)
        {
            EditorGUILayout.BeginHorizontal();
            NPCEditorGUI.Icon(item.Icon, 16f);
            if (GUILayout.Button(item.Name, EditorStyles.label))
                EditorGUIUtility.PingObject(item);
            EditorGUILayout.EndHorizontal();
        }
    }
}

// ====================================================================== wallet, interactor, skin
[CustomEditor(typeof(CurrencyWallet))]
public class CurrencyWalletEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var wallet = (CurrencyWallet)target;
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("A currency missing from Balances starts at its Starting Amount. Without a Default Currency the project's default is used " +
                                    $"('{CurrencyDefinition.Default.DisplayName}').", MessageType.None);
            return;
        }
        NPCEditorGUI.Section("Live");
        var currencies = new List<CurrencyDefinition> { wallet.DefaultCurrency };
        foreach (CurrencyBalance b in wallet.Balances)
            if (b?.currency != null && !currencies.Contains(b.currency))
                currencies.Add(b.currency);
        foreach (CurrencyDefinition c in currencies)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(c.DisplayName, c.Format(wallet.GetBalance(c)));
            foreach (int d in new[] { -100, -10, 10, 100 })
                if (GUILayout.Button(d > 0 ? $"+{d}" : d.ToString(), GUILayout.Width(44f)))
                {
                    if (d > 0) wallet.TryAdd(c, d);
                    else wallet.TrySpend(c, Mathf.Min(-d, wallet.GetBalance(c)));
                }
            EditorGUILayout.EndHorizontal();
        }
        Repaint();
    }
}

[CustomEditor(typeof(PlayerInteractor))]
public class PlayerInteractorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var interactor = (PlayerInteractor)target;
        serializedObject.Update();
        if (interactor.GetComponentInParent<PlayerStatusController>() == null)
            EditorGUILayout.HelpBox("Put the Player Interactor on the player (the object with the Player Status Controller) or one of its children.", MessageType.Warning);
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Interact: the action named above in the player's input actions (created with E / the north face button when missing). " +
                                    "Clicks: the left mouse button. NPCs and anything with an IInteractable component are found.", MessageType.None);
            return;
        }
        NPCEditorGUI.Section("Live");
        EditorGUILayout.LabelField("Interact input", interactor.InputSource);
        EditorGUILayout.LabelField("Click input", interactor.ClickSource);
        IInteractable f = interactor.Focused;
        EditorGUILayout.LabelField("Target", f == null ? "none" : $"{f.InteractionVerb} {f.InteractionName}{(interactor.FocusedAvailable ? "" : " (unavailable)")}");
        IInteractable cur = interactor.Current;
        EditorGUILayout.LabelField("Open", cur == null ? "none" : cur.InteractionName);
        Repaint();
    }
}

[CustomEditor(typeof(MerchantUISkin))]
public class MerchantUISkinEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        errors.Clear();
        warnings.Clear();
        ((MerchantUISkin)target).Validate(errors, warnings);
        EditorGUILayout.HelpBox("Every field is optional: what is empty is generated with the default look. Assign a Window Prefab for a window of " +
                                "your own (a MerchantWindow with its fields wired), or only the entry / tab / button prefabs, sprites, font and colours.", MessageType.None);
        NPCEditorGUI.Problems(errors, warnings);
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
