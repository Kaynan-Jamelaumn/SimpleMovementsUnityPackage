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
        {
            DrawQuickSetup();
            DrawGridFootprint();
        }
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

    private static readonly Vector2Int[] CommonGridSizes =
    {
        new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 2),
    };

    /// <summary>
    /// The item's size in the grid inventory (Inventory Manager ▸ Use Grid Inventory): a drawing of the cells it takes,
    /// common sizes and the usual size of its kind. The classic slots ignore it.
    /// </summary>
    private void DrawGridFootprint()
    {
        SerializedProperty size = serializedObject.FindProperty("gridSize");
        SerializedProperty rotate = serializedObject.FindProperty("canRotateInGrid");
        SerializedProperty angleProp = serializedObject.FindProperty("gridIconAngle");
        SerializedProperty gridIconProp = serializedObject.FindProperty("gridIcon");
        if (size == null)
            return;
        Vector2Int s = size.vector2IntValue;
        s = new Vector2Int(Mathf.Clamp(s.x, 1, 10), Mathf.Clamp(s.y, 1, 10));
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // How it looks in the grid: its cells and its icon, laid out as the grid draws it.
        const float box = 76f;
        Rect area = GUILayoutUtility.GetRect(box, box, GUILayout.Width(box), GUILayout.Height(box));
        float cell = Mathf.Min(22f, (box - 2f) / Mathf.Max(s.x, s.y));
        var block = new Rect(area.x + (box - s.x * cell) * 0.5f, area.y + (box - s.y * cell) * 0.5f, s.x * cell, s.y * cell);
        EditorGUI.DrawRect(area, new Color(0f, 0f, 0f, 0.15f));
        for (int y = 0; y < s.y; y++)
            for (int x = 0; x < s.x; x++)
                EditorGUI.DrawRect(new Rect(block.x + x * cell + 0.5f, block.y + y * cell + 0.5f, cell - 1f, cell - 1f), new Color(0.35f, 0.6f, 0.95f, 0.45f));
        Sprite sprite = Item.GridIcon;
        if (sprite != null && sprite.texture != null)
        {
            float angle = Item.HasGridIcon || angleProp == null ? 0f : angleProp.floatValue;
            Vector2 sz = IconSize(new Vector2(block.width - 2f, block.height - 2f), sprite, angle, Item.HasGridIcon);
            Rect r = new Rect(block.center - sz * 0.5f, sz);
            Rect uv = sprite.textureRect;
            uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height, uv.width / sprite.texture.width, uv.height / sprite.texture.height);
            Matrix4x4 old = GUI.matrix;
            GUI.BeginClip(area);
            r.position -= area.position;
            GUIUtility.RotateAroundPivot(angle, r.center);
            GUI.DrawTextureWithTexCoords(r, sprite.texture, uv, true);
            GUI.matrix = old;
            GUI.EndClip();
        }

        EditorGUILayout.BeginVertical();
        bool turns = rotate != null && rotate.boolValue && s.x != s.y;
        EditorGUILayout.LabelField(new GUIContent($"Grid inventory: {s.x} × {s.y} cells" + (turns ? $" (or {s.y} × {s.x} turned)" : ""),
            "Cells the item takes when the Inventory Manager uses the grid inventory; the classic slots ignore it. " +
            "Can Rotate In Grid lets players turn it (R while dragging or hovering)."), EditorStyles.miniBoldLabel);
        Vector2Int suggested = ItemPresets.SuggestGridSize(Item);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < CommonGridSizes.Length; i++)
        {
            Vector2Int c = CommonGridSizes[i];
            GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft : i == CommonGridSizes.Length - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
            if (GUILayout.Toggle(s == c, $"{c.x}×{c.y}", style, GUILayout.MinWidth(30f)) && s != c)
                size.vector2IntValue = c;
        }
        using (new EditorGUI.DisabledScope(suggested == s))
        {
            if (GUILayout.Button(new GUIContent($"Usual: {suggested.x}×{suggested.y}", "The usual size of this kind of item (weapon category, armor slot)."),
                    EditorStyles.miniButton, GUILayout.MinWidth(70f)))
                size.vector2IntValue = suggested;
        }
        EditorGUILayout.EndHorizontal();

        if (angleProp != null && gridIconProp != null)
        {
            if (gridIconProp.objectReferenceValue != null)
            {
                EditorGUILayout.LabelField("Grid Icon set: drawn in the item's shape, it fills the cells as drawn.", EditorStyles.wordWrappedMiniLabel);
            }
            else if (s.x != s.y)
            {
                EditorGUILayout.LabelField("A square icon cannot fill a long item without stretching. Diagonal icons (most swords, spears, " +
                                           "staves) can be stood up; or give the item a Grid Icon drawn in its shape.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Turn icon", GUILayout.Width(60f));
                foreach (float a in new[] { 0f, 45f, -45f, 90f })
                {
                    bool on = Mathf.Approximately(angleProp.floatValue, a);
                    if (GUILayout.Toggle(on, a == 0f ? "as drawn" : $"{a:+0;-0}°", EditorStyles.miniButton, GUILayout.MinWidth(46f)) && !on)
                        angleProp.floatValue = a;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>The icon's size in an area, as the grid draws it (see GridInventoryView.Style).</summary>
    private static Vector2 IconSize(Vector2 area, Sprite sp, float angle, bool madeForShape)
    {
        float aspect = sp.rect.height > 0f ? sp.rect.width / sp.rect.height : 1f;
        if (Mathf.Abs(angle) > 0.01f)
        {
            float rad = angle * Mathf.Deg2Rad, cos = Mathf.Abs(Mathf.Cos(rad)), sin = Mathf.Abs(Mathf.Sin(rad));
            float h = area.y >= area.x ? area.y / (aspect * sin + cos) : area.x / (aspect * cos + sin);
            h = Mathf.Min(h, Mathf.Max(area.x, area.y));
            return new Vector2(h * aspect, h);
        }
        // Proportions kept: as big as fits.
        float scale = Mathf.Min(area.x / Mathf.Max(1f, sp.rect.width), area.y / Mathf.Max(1f, sp.rect.height));
        return new Vector2(sp.rect.width * scale, sp.rect.height * scale);
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
        if (armor.IsShield)
        {
            AddShieldPreset(menu, armor, "Buckler (small, quick, great parries)", ShieldDefense.Buckler);
            AddShieldPreset(menu, armor, "Round Shield (all-rounder)", ShieldDefense.Round);
            AddShieldPreset(menu, armor, "Kite Shield (wide, solid)", ShieldDefense.Kite);
            AddShieldPreset(menu, armor, "Tower Shield (a wall: slow, no parry)", ShieldDefense.Tower);
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Shield Defense/(only for armor in the Shield slot)"));
        }
    }

    private void AddShieldPreset(GenericMenu menu, ArmorSO armor, string label, System.Func<ShieldDefense> make)
    {
        menu.AddItem(new GUIContent("Shield Defense/" + label), false, () => ApplyPreset(label, () =>
        {
            Undo.RecordObject(armor, "Shield preset");
            armor.SetShieldDefense(make());
            EditorUtility.SetDirty(armor);
        }));
    }

    /// <summary>Every field; Shield Defense only for shields (it does nothing on other armor).</summary>
    protected override void DrawFields()
    {
        if (serializedObject.isEditingMultipleObjects || ((ArmorSO)target).IsShield)
            DrawPropertiesExcluding(serializedObject, "m_Script");
        else
            DrawPropertiesExcluding(serializedObject, "m_Script", "shieldDefense");
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
        if (armor.IsShield)
        {
            EditorGUILayout.Space(2);
            ShieldDefense defense = armor.ShieldDefense;
            if (defense == null)
            {
                EditorGUILayout.LabelField("Shield: blocking is off (Shield Defense ▸ Enabled) - only its Defense counts.", EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                var lines = new List<string>();
                defense.Describe(lines);
                foreach (string l in lines)
                    EditorGUILayout.LabelField("Shield: " + l, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.LabelField("Worn in the Off Hand slot; stowed (no defense, no blocking) while a two-handed weapon is in the main hand.",
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();
    }
}

/// <summary>Ammo inspector: presets and the weapons that fire it.</summary>
[CustomEditor(typeof(AmmoSO), true)]
[CanEditMultipleObjects]
public class AmmoSOEditor : ItemSOEditor
{
    private double nextScan;
    private readonly List<WeaponSO> firedBy = new List<WeaponSO>();

    protected override void AddPresets(GenericMenu menu)
    {
        var ammo = (AmmoSO)target;
        foreach (ItemPresets.Ammo a in System.Enum.GetValues(typeof(ItemPresets.Ammo)))
        {
            ItemPresets.Ammo captured = a;
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(a.ToString())), false, () =>
                ApplyPreset(captured.ToString(), () => ItemPresets.ApplyAmmo(ammo, captured)));
        }
    }

    protected override void DrawToolsTop()
    {
        if (serializedObject.isEditingMultipleObjects)
            return;
        var ammo = (AmmoSO)target;
        if (EditorApplication.timeSinceStartup >= nextScan)
        {
            nextScan = EditorApplication.timeSinceStartup + 3.0;
            firedBy.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponSO"))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null && w.Ranged != null && w.Ranged.ammo != null && w.Ranged.ammo.Accepts(ammo))
                    firedBy.Add(w);
            }
        }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Ammo type '{ammo.AmmoType}'", EditorStyles.boldLabel);
        if (firedBy.Count == 0)
            EditorGUILayout.LabelField("No weapon fires it yet: a ranged weapon's Ammo ▸ Ammo Type must be the same word.", EditorStyles.wordWrappedMiniLabel);
        else
            EditorGUILayout.LabelField("Fired by: " + string.Join(", ", firedBy.Select(w => w.Name)), EditorStyles.wordWrappedMiniLabel);
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
