#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a <see cref="TargetFilter"/> as toggle buttons (Self, Allies, Enemies, Neutral, Party) instead of Unity's flags
/// popup, which listed None / Nothing and All / Everything side by side (they are the same).
/// </summary>
[CustomPropertyDrawer(typeof(TargetFilter))]
public class TargetFilterDrawer : PropertyDrawer
{
    private static readonly (TargetFilter flag, string label, string tip)[] Options =
    {
        (TargetFilter.Self, "Self", "The attacker / caster itself (a self-heal, a buff on yourself)."),
        (TargetFilter.Allies, "Allies", "Characters of the same team (Combat Entity ▸ Team; empty = 'Player' for players, the mob type for mobs)."),
        (TargetFilter.Enemies, "Enemies", "Hostile characters. For the player every character of another team is an enemy (mobs, animals); " +
                                          "for a mob, whoever its AI is hostile to."),
        (TargetFilter.Neutral, "Neutral", "Characters that are neither allies nor enemies (neutral factions, a mob another mob is not hostile to)."),
        (TargetFilter.Party, "Party", "Only members of the attacker's party (a party heal that skips other allies). Party members are " +
                                      "allies too: with Allies on, they are already included."),
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect field = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var value = (TargetFilter)property.intValue;
        if (property.hasMultipleDifferentValues)
            value = TargetFilter.None;
        float w = field.width / Options.Length;
        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < Options.Length; i++)
        {
            GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft : i == Options.Length - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
            var r = new Rect(field.x + w * i, field.y, w, EditorGUIUtility.singleLineHeight);
            bool on = (value & Options[i].flag) != 0;
            bool now = GUI.Toggle(r, on, new GUIContent(Options[i].label, Options[i].tip), style);
            if (now != on)
                value = now ? value | Options[i].flag : value & ~Options[i].flag;
        }
        if (EditorGUI.EndChangeCheck())
            property.intValue = (int)value;

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
#endif
