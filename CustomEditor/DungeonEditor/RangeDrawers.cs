using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    /// <summary>Draws a <see cref="FloatRange"/> / <see cref="IntRange"/> on one line: "Label  Min [ ]  Max [ ]" (the label keeps the field's tooltip).</summary>
    [CustomPropertyDrawer(typeof(FloatRange))]
    [CustomPropertyDrawer(typeof(IntRange))]
    public class RangeDrawer : PropertyDrawer
    {
        private static readonly GUIContent MinLabel = new GUIContent("Min", "Lowest value.");
        private static readonly GUIContent MaxLabel = new GUIContent("Max", "Highest value.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float half = field.width * 0.5f;
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 30f;
            EditorGUI.PropertyField(new Rect(field.x, field.y, half - 4f, field.height), property.FindPropertyRelative("min"), MinLabel);
            EditorGUI.PropertyField(new Rect(field.x + half, field.y, half, field.height), property.FindPropertyRelative("max"), MaxLabel);
            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
    }
}
