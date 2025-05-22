#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[CustomPropertyDrawer(typeof(CameraEntry))]
public class CameraEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var cameraRect = new Rect(position.x, position.y, position.width - 60, position.height);
        var boolRect = new Rect(position.x + position.width - 50, position.y, 50, position.height);

        EditorGUI.PropertyField(cameraRect, property.FindPropertyRelative("camera"), GUIContent.none);
        EditorGUI.PropertyField(boolRect, property.FindPropertyRelative("isFirstPerson"), GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
#endif