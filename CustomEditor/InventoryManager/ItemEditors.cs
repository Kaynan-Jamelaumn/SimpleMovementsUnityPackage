#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector of every item (consumables, equippables, armor, weapons): its problems (with explanations), all its
/// fields, and a preview of the tooltip - what the item does in game. Derived editors add tools.
/// </summary>
[CustomEditor(typeof(ItemSO), true)]
[CanEditMultipleObjects]
public class ItemSOEditor : Editor
{
    protected readonly List<string> errors = new List<string>();
    protected readonly List<string> warnings = new List<string>();
    private double nextValidation;
    private bool showPreview = true;

    protected ItemSO Item => (ItemSO)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (!serializedObject.isEditingMultipleObjects && EditorApplication.timeSinceStartup >= nextValidation)
        {
            nextValidation = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            try { Item.ValidateItem(errors, warnings); }
            catch (System.Exception e) { errors.Add("Validation failed: " + e.Message); }
        }

        DrawProblems();
        if (!serializedObject.isEditingMultipleObjects)
            DrawQuickSetup();
        DrawToolsTop();
        EditorGUILayout.Space(2);
        DrawFields();
        serializedObject.ApplyModifiedProperties();
        DrawToolsBottom();
        if (!serializedObject.isEditingMultipleObjects)
            DrawPreview();
    }

    protected void DrawProblems()
    {
        if (serializedObject.isEditingMultipleObjects)
            return;
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
    }

    protected virtual void DrawToolsTop() { }

    /// <summary>The item's fields (all of them by default; the weapon inspector groups them in sections).</summary>
    protected virtual void DrawFields() => DrawPropertiesExcluding(serializedObject, "m_Script");

    /// <summary>Presets offered by the Presets menu of this kind of item (none by default).</summary>
    protected virtual void AddPresets(GenericMenu menu) { }

    /// <summary>Icon, one-line summary, Auto-Fill, Icon From Prefab and the Presets menu.</summary>
    private void DrawQuickSetup()
    {
        ItemSO item = Item;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        Rect iconRect = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));
        if (item.Icon != null)
        {
            Texture2D tex = AssetPreview.GetAssetPreview(item.Icon);
            GUI.DrawTexture(iconRect, tex != null ? tex : item.Icon.texture, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(iconRect, new Color(0f, 0f, 0f, 0.2f));
            GUI.Label(iconRect, "no\nicon", EditorStyles.centeredGreyMiniLabel);
        }
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(string.IsNullOrEmpty(item.Name) ? item.name : item.Name, EditorStyles.boldLabel);
        string durability = item.MaxDurability > 0 ? $"durability {item.MaxDurability}" : "unbreakable";
        EditorGUILayout.LabelField($"{ObjectNames.NicifyVariableName(item.ItemType.ToString())} · stack {item.StackMax} · weight {item.Weight:0.##} · value {item.Price:0.##} · {durability}",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Auto-Fill", "Fills what is missing: display name, an icon and a prefab with the item's name, " +
                                                        "armor slot / weapon category / food-or-potion from the name, stack size and durability.")))
        {
            List<string> changes = ItemPresets.AutoFill(item);
            serializedObject.Update();
            Debug.Log(changes.Count > 0 ? $"[Items] Auto-Fill '{item.name}': {string.Join(", ", changes)}." : $"[Items] Auto-Fill '{item.name}': nothing to fill.", item);
        }
        using (new EditorGUI.DisabledScope(item.Prefab == null))
        {
            if (GUILayout.Button(new GUIContent("Icon From Prefab", item.Prefab == null ? "Assign a Prefab first." :
                    "Renders the prefab and saves it next to the item as its icon sprite (replaces the current icon).")))
                ItemPresets.GenerateIcon(item);
        }
        var presets = new GenericMenu();
        AddPresets(presets);
        using (new EditorGUI.DisabledScope(presets.GetItemCount() == 0))
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Presets…", "Ready-made values for this kind of item (can be undone)."), FocusType.Keyboard))
                presets.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    /// <summary>Runs <paramref name="apply"/> on the item and refreshes the inspector.</summary>
    protected void ApplyPreset(string label, System.Action apply)
    {
        apply();
        serializedObject.Update();
        Debug.Log($"[Items] Applied preset '{label}' to '{target.name}'.", target);
    }
    protected virtual void DrawToolsBottom() { }

    private void DrawPreview()
    {
        EditorGUILayout.Space(6);
        showPreview = EditorGUILayout.Foldout(showPreview, "Tooltip Preview (what the item does)", true, EditorStyles.foldoutHeader);
        if (!showPreview)
            return;
        var lines = new List<string>();
        try { Item.AppendTooltip(lines); }
        catch (System.Exception e) { lines.Add("(preview failed: " + e.Message + ")"); }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(Item.Name, EditorStyles.boldLabel);
        if (lines.Count == 0)
            EditorGUILayout.LabelField("(no effects)", EditorStyles.miniLabel);
        foreach (string l in lines)
            EditorGUILayout.LabelField(l, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
    }
}

/// <summary>Armor inspector: set membership kept consistent on both sides, and the set's tiers.</summary>
[CustomEditor(typeof(ArmorSO), true)]
[CanEditMultipleObjects]
public class ArmorSOEditor : ItemSOEditor
{
    protected override void AddPresets(GenericMenu menu)
    {
        var armor = (ArmorSO)target;
        foreach (ItemPresets.ArmorMaterial m in System.Enum.GetValues(typeof(ItemPresets.ArmorMaterial)))
        {
            ItemPresets.ArmorMaterial captured = m;
            menu.AddItem(new GUIContent($"Material/{m}"), false, () =>
                ApplyPreset($"{captured} armor", () => ItemPresets.ApplyArmor(armor, captured)));
        }
    }

    protected override void DrawToolsTop()
    {
        if (serializedObject.isEditingMultipleObjects)
            return;
        var armor = (ArmorSO)target;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{SlotTypeHelper.GetDisplayName(armor.ArmorSlotType)} · Defense {armor.GetEffectiveDefense():0.#} · Magic Resistance {armor.GetEffectiveMagicDefense():0.#}", EditorStyles.boldLabel);
        ArmorSet set = armor.BelongsToSet;
        if (set != null)
        {
            bool listed = set.ContainsPiece(armor);
            EditorGUILayout.LabelField($"Set: {set.SetName} ({set.SetPieces.Count(p => p != null)} pieces)" + (listed ? "" : "  ⚠ not listed by the set"));
            EditorGUILayout.BeginHorizontal();
            if (!listed && GUILayout.Button("Add to the set's pieces"))
            {
                Undo.RecordObject(set, "Add set piece");
                set.AddPiece(armor);
                EditorUtility.SetDirty(set);
            }
            if (GUILayout.Button("Open set"))
                Selection.activeObject = set;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("Not part of an armor set.", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }
}

// The weapon inspector (WeaponSOEditor) is in WeaponSOEditor.cs.

/// <summary>Consumable inspector: potion and food presets.</summary>
[CustomEditor(typeof(ConsumableSO), true)]
[CanEditMultipleObjects]
public class ConsumableSOEditor : ItemSOEditor
{
    protected override void AddPresets(GenericMenu menu)
    {
        var item = (ConsumableSO)target;
        foreach (ItemPresets.Consumable c in System.Enum.GetValues(typeof(ItemPresets.Consumable)))
        {
            ItemPresets.Consumable captured = c;
            bool food = c >= ItemPresets.Consumable.Bread;
            menu.AddItem(new GUIContent($"{(food ? "Food & Drink" : "Potions")}/{ObjectNames.NicifyVariableName(c.ToString())}"), false, () =>
                ApplyPreset(captured.ToString(), () => ItemPresets.ApplyConsumable(item, captured)));
        }
    }
}

/// <summary>Combo tree inspector: its problems and a branch overview.</summary>
[CustomEditor(typeof(ComboTree))]
public class ComboTreeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var tree = (ComboTree)target;
        var errors = new List<string>();
        var warnings = new List<string>();
        tree.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);
        foreach (ComboBranch b in tree.branches)
        {
            if (b == null) continue;
            string conds = b.conditions == null || b.conditions.Count == 0 ? "always" : string.Join(" and ", b.conditions.Where(c => c != null).Select(c => c.Describe()));
            EditorGUILayout.LabelField($"{b.triggerInput} → {(string.IsNullOrEmpty(b.branchName) ? "branch" : b.branchName)}{(b.isFinisher ? " (finisher)" : "")}",
                $"when {conds}" + (b.damageBonus != 0f ? $", {b.damageBonus * 100f:+0;-0}% damage" : ""), EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();

        DrawDefaultInspector();
    }
}
#endif
