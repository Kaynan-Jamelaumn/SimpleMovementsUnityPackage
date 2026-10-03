#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Draws an attack effect like an <see cref="AttackEffect"/> without its Attack Cast list (weapon attacks do their own hit detection).</summary>
[CustomPropertyDrawer(typeof(AttackActionEffect))]
public class AttackActionEffectDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return h;
        SerializedProperty it = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        if (it.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(it, end)) break;
                if (it.name == "attackCast") continue;
                h += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            while (it.NextVisible(false));
        }
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var type = property.FindPropertyRelative("effectType");
        var amount = property.FindPropertyRelative("amount");
        var enemy = property.FindPropertyRelative("enemyEffect");
        string summary = type != null && amount != null
            ? $"{label.text}: {type.enumDisplayNames[type.enumValueIndex]} {amount.floatValue:+0.##;-0.##} ({(enemy != null && enemy.boolValue ? "target" : "self")})"
            : label.text;
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, summary, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty it = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            if (it.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(it, end)) break;
                    if (it.name == "attackCast") continue;
                    float h = EditorGUI.GetPropertyHeight(it, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), it, true);
                    y += h + EditorGUIUtility.standardVerticalSpacing;
                }
                while (it.NextVisible(false));
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}

/// <summary>An armor set bonus: "(2) Warrior's Vigor [group]" header, the fields when expanded.</summary>
[CustomPropertyDrawer(typeof(ArmorSetEffect))]
public class ArmorSetEffectDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int pieces = property.FindPropertyRelative("piecesRequired")?.intValue ?? 0;
        string name = property.FindPropertyRelative("effectName")?.stringValue ?? "";
        string group = property.FindPropertyRelative("upgradeGroup")?.stringValue ?? "";
        string header = pieces < 1 ? "⚠ Invalid bonus (needs at least 1 piece)"
            : $"({pieces} pieces) {(string.IsNullOrEmpty(name) ? "Unnamed bonus" : name)}{(string.IsNullOrEmpty(group) ? "" : $"   [{group}]")}";
        // The default drawing (foldout + children) with a readable header; it used to draw only the header.
        EditorGUI.PropertyField(position, property, new GUIContent(header, label.tooltip), true);
        EditorGUI.EndProperty();
    }
}

/// <summary>A classic equipment stat on one line (type, amount, units in the tooltip); the rarely used options when expanded.</summary>
[CustomPropertyDrawer(typeof(EquippableEffect))]
public class EquippableEffectDrawer : PropertyDrawer
{
    private static readonly string[] Advanced = { "effectDescription", "duration", "isTemporary", "canStack", "maxStacks", "applicationChance", "minimumLevel", "effectPrefab", "effectSound" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
            foreach (string n in Advanced)
            {
                SerializedProperty p = property.FindPropertyRelative(n);
                if (p != null) h += EditorGUI.GetPropertyHeight(p, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var type = property.FindPropertyRelative("effectType");
        var amount = property.FindPropertyRelative("amount");
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Rect fold = new Rect(line.x, line.y, 16f, line.height);
        property.isExpanded = EditorGUI.Foldout(fold, property.isExpanded, GUIContent.none, true);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float x = line.x + 16f;
        float w = line.width - 16f;
        var t = (EquippableEffectType)type.enumValueIndex;
        string units = LegacyEquipmentStats.Explain(t);
        EditorGUI.PropertyField(new Rect(x, line.y, w * 0.45f - 4f, line.height), type, new GUIContent("", units));
        EditorGUI.PropertyField(new Rect(x + w * 0.45f, line.y, w * 0.2f - 4f, line.height), amount, new GUIContent("", units));
        EditorGUI.LabelField(new Rect(x + w * 0.65f, line.y, w * 0.35f, line.height),
            new GUIContent(LegacyEquipmentStats.Describe(t, amount.floatValue), units), EditorStyles.miniLabel);
        EditorGUI.indentLevel = indent;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
            foreach (string n in Advanced)
            {
                SerializedProperty p = property.FindPropertyRelative(n);
                if (p == null) continue;
                float h = EditorGUI.GetPropertyHeight(p, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), p, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}

/// <summary>
/// A combat stat modifier on one line: stat, value (units in the tooltip), and the filter the stat uses - the element
/// for element-aware stats (Poison Damage = Elemental Damage limited to Poison), the kind of ability for cooldown / mana
/// cost stats, otherwise the weapon category.
/// </summary>
[CustomPropertyDrawer(typeof(CombatStatModifier))]
public class CombatStatModifierDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var stat = property.FindPropertyRelative("stat");
        var value = property.FindPropertyRelative("value");
        var weapon = property.FindPropertyRelative("onlyWithWeapon");
        var element = property.FindPropertyRelative("onlyElement");
        var scope = property.FindPropertyRelative("scope");
        var type = (CombatStatType)stat.enumValueIndex;
        CombatStatInfo info = CombatStatInfo.Get(type);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float w = position.width;
        EditorGUI.PropertyField(new Rect(position.x, position.y, w * 0.42f - 4f, position.height), stat, new GUIContent("", info.description));
        EditorGUI.PropertyField(new Rect(position.x + w * 0.42f, position.y, w * 0.2f - 16f, position.height), value, new GUIContent("", info.description));
        EditorGUI.LabelField(new Rect(position.x + w * 0.62f - 14f, position.y, 14f, position.height), info.isPercent ? "%" : "", EditorStyles.miniLabel);
        var last = new Rect(position.x + w * 0.62f, position.y, w * 0.38f, position.height);
        if (CombatStatInfo.UsesElement(type) && element != null)
            EditorGUI.PropertyField(last, element, new GUIContent("", "Only for this element (None = every element). Poison here = poison damage / strength / chance / resistance."));
        else if (CombatStatInfo.UsesScope(type) && scope != null)
            EditorGUI.PropertyField(last, scope, new GUIContent("", "Which abilities: skills, innate (race / class / trait abilities), items (granted abilities and consumables), or everything."));
        else
            EditorGUI.PropertyField(last, weapon, new GUIContent("", "Only while wielding a weapon of this category (None = always)."));
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}

/// <summary>A scaling rule on one line: "[Agility] gives [0.3] [Critical Chance] per point".</summary>
[CustomPropertyDrawer(typeof(StatScalingRule))]
public class StatScalingRuleDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float w = position.width;
        const string tip = "Each point of the first stat gives this much of the second (e.g. Agility gives 0.3 Critical Chance). Uses the raw totals, so rules never chain.";
        EditorGUI.PropertyField(new Rect(position.x, position.y, w * 0.32f, position.height), property.FindPropertyRelative("from"), new GUIContent("", tip));
        EditorGUI.LabelField(new Rect(position.x + w * 0.32f + 2f, position.y, 34f, position.height), "gives", EditorStyles.miniLabel);
        EditorGUI.PropertyField(new Rect(position.x + w * 0.32f + 36f, position.y, w * 0.18f - 38f, position.height), property.FindPropertyRelative("perPoint"), new GUIContent("", tip));
        EditorGUI.PropertyField(new Rect(position.x + w * 0.5f, position.y, w * 0.34f, position.height), property.FindPropertyRelative("to"), new GUIContent("", tip));
        EditorGUI.LabelField(new Rect(position.x + w * 0.84f + 4f, position.y, w * 0.16f - 4f, position.height), "per point", EditorStyles.miniLabel);
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}

/// <summary>An elemental resistance on one line: element, percent.</summary>
[CustomPropertyDrawer(typeof(ElementalResistance))]
public class ElementalResistanceDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float w = position.width;
        EditorGUI.PropertyField(new Rect(position.x, position.y, w * 0.4f - 4f, position.height), property.FindPropertyRelative("element"), GUIContent.none);
        EditorGUI.PropertyField(new Rect(position.x + w * 0.4f, position.y, w * 0.6f - 70f, position.height), property.FindPropertyRelative("percent"),
            new GUIContent("", "Percent less damage of this element. Negative = a weakness."));
        EditorGUI.LabelField(new Rect(position.xMax - 66f, position.y, 66f, position.height), "% resist", EditorStyles.miniLabel);
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}

/// <summary>A combo condition showing only the fields its type uses.</summary>
[CustomPropertyDrawer(typeof(ComboCondition))]
public class ComboConditionDrawer : PropertyDrawer
{
    private static string[] Fields(ComboCondition.ConditionType t)
    {
        switch (t)
        {
            case ComboCondition.ConditionType.TraitRequired:
            case ComboCondition.ConditionType.WeaponTraitRequired: return new[] { "requiredTrait" };
            case ComboCondition.ConditionType.StatusEffect: return new[] { "requiredStatusEffect" };
            case ComboCondition.ConditionType.PreviousAttack: return new[] { "requiredInput" };
            case ComboCondition.ConditionType.InAir: return new string[0];
            default: return new[] { "threshold" };
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var type = (ComboCondition.ConditionType)property.FindPropertyRelative("type").enumValueIndex;
        int lines = 2 + Fields(type).Length; // type + fields + inverse
        return lines * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float lh = EditorGUIUtility.singleLineHeight;
        float step = lh + EditorGUIUtility.standardVerticalSpacing;
        Rect r = new Rect(position.x, position.y, position.width, lh);
        var typeProp = property.FindPropertyRelative("type");
        EditorGUI.PropertyField(r, typeProp, new GUIContent(label.text, "What is checked."));
        var type = (ComboCondition.ConditionType)typeProp.enumValueIndex;
        EditorGUI.indentLevel++;
        foreach (string f in Fields(type))
        {
            r.y += step;
            EditorGUI.PropertyField(r, property.FindPropertyRelative(f));
        }
        r.y += step;
        EditorGUI.PropertyField(r, property.FindPropertyRelative("inverse"), new GUIContent("Not (inverse)"));
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
#endif
    