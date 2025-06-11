#if UNITY_EDITOR

// Property drawer for ArmorSetEffect to make it look better in inspector
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ArmorSetEffect))]
public class ArmorSetEffectDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var piecesRequired = property.FindPropertyRelative("piecesRequired");
        var effectName = property.FindPropertyRelative("effectName");

        // Ensure we have valid values
        int pieces = piecesRequired != null ? piecesRequired.intValue : 0;
        string name = effectName != null ? effectName.stringValue : "";

        // Create better display label
        string displayLabel;
        if (pieces < 1)
        {
            displayLabel = "⚠️ Invalid Effect (< 1 piece)";
        }
        else if (string.IsNullOrEmpty(name) || name == "Set Bonus")
        {
            displayLabel = $"{pieces} pieces: Unnamed Effect";
        }
        else
        {
            displayLabel = $"{pieces} pieces: {name}";
        }

        property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, displayLabel, true);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif