#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a trait modifier on one line: stat, mode (locked when the stat supports only one), value, and a readable
/// result ("+20% Max Health"). The stat's description is the tooltip.
/// </summary>
[CustomPropertyDrawer(typeof(TraitModifier))]
public class TraitModifierDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty statP = property.FindPropertyRelative("stat");
        SerializedProperty modeP = property.FindPropertyRelative("mode");
        SerializedProperty valueP = property.FindPropertyRelative("value");
        var stat = (TraitStat)statP.enumValueIndex;
        TraitStats.StatInfo info = TraitStats.Get(stat);

        EditorGUI.BeginProperty(position, label, property);
        Rect r = new Rect(position.x, position.y + 1f, position.width, EditorGUIUtility.singleLineHeight);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float w = r.width;
        Rect statR = new Rect(r.x, r.y, w * 0.34f, r.height);
        Rect modeR = new Rect(statR.xMax + 3f, r.y, w * 0.17f, r.height);
        Rect valR = new Rect(modeR.xMax + 3f, r.y, w * 0.14f, r.height);
        Rect sumR = new Rect(valR.xMax + 6f, r.y, r.xMax - valR.xMax - 6f, r.height);

        EditorGUI.PropertyField(statR, statP, new GUIContent("", info.description));
        bool locked = info.modes != TraitStats.Modes.Both;
        using (new EditorGUI.DisabledScope(locked))
        {
            if (locked)
                EditorGUI.Popup(modeR, info.modes == TraitStats.Modes.FlatOnly ? 0 : 1, new[] { "Flat", "Percent" });
            else
                EditorGUI.PropertyField(modeR, modeP, new GUIContent("", "Flat: add the value in the stat's units. Percent: +20 = 20% more."));
        }
        EditorGUI.PropertyField(valR, valueP, new GUIContent("", info.description));

        var preview = new TraitModifier(stat, (TraitModifierMode)modeP.enumValueIndex, valueP.floatValue);
        Color old = GUI.contentColor;
        GUI.contentColor = preview.IsBeneficial ? new Color(0.45f, 0.85f, 0.45f) : (Mathf.Approximately(preview.value, 0f) ? old : new Color(0.95f, 0.5f, 0.45f));
        EditorGUI.LabelField(sumR, new GUIContent(preview.Describe(), info.description + (preview.IsBeneficial ? "\n(benefit)" : "\n(drawback)")), EditorStyles.miniLabel);
        GUI.contentColor = old;

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
#endif
