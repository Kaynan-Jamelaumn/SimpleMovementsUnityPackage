#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Draws a HitShape showing only the fields its shape type uses, with a one-line description.</summary>
[CustomPropertyDrawer(typeof(HitShape))]
public class HitShapeDrawer : PropertyDrawer
{
    private static readonly List<string> fields = new List<string>(12);

    private static void Fields(HitShapeType type, List<string> list)
    {
        list.Clear();
        switch (type)
        {
            case HitShapeType.Circle: list.AddRange(new[] { "radius", "height", "baseOffset", "offset" }); break;
            case HitShapeType.Sphere: list.AddRange(new[] { "radius", "offset" }); break;
            case HitShapeType.Cylinder: list.AddRange(new[] { "radius", "height", "baseOffset", "offset" }); break;
            case HitShapeType.Cone: list.AddRange(new[] { "radius", "angle", "innerRadius", "height", "baseOffset", "offset", "yaw" }); break;
            case HitShapeType.Triangle: list.AddRange(new[] { "width", "length", "startAtOrigin", "height", "baseOffset", "offset", "yaw" }); break;
            case HitShapeType.Square: list.AddRange(new[] { "width", "startAtOrigin", "height", "baseOffset", "offset", "yaw" }); break;
            case HitShapeType.Rectangle: list.AddRange(new[] { "width", "length", "startAtOrigin", "height", "baseOffset", "offset", "yaw" }); break;
            case HitShapeType.Trapezoid: list.AddRange(new[] { "nearWidth", "width", "length", "startAtOrigin", "height", "baseOffset", "offset", "yaw" }); break;
            case HitShapeType.Line: list.AddRange(new[] { "length", "width", "startAtOrigin", "height", "baseOffset", "offset", "yaw" }); break;
            case HitShapeType.Ring: list.AddRange(new[] { "radius", "innerRadius", "height", "baseOffset", "offset" }); break;
        }
    }

    private static string Summary(SerializedProperty p)
    {
        var s = new HitShape
        {
            type = (HitShapeType)p.FindPropertyRelative("type").enumValueIndex,
            radius = p.FindPropertyRelative("radius").floatValue,
            innerRadius = p.FindPropertyRelative("innerRadius").floatValue,
            angle = p.FindPropertyRelative("angle").floatValue,
            width = p.FindPropertyRelative("width").floatValue,
            nearWidth = p.FindPropertyRelative("nearWidth").floatValue,
            length = p.FindPropertyRelative("length").floatValue,
        };
        return s.Describe();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return h;
        float sp = EditorGUIUtility.standardVerticalSpacing;
        h += sp + EditorGUIUtility.singleLineHeight; // type
        Fields((HitShapeType)property.FindPropertyRelative("type").enumValueIndex, fields);
        foreach (string f in fields)
            h += sp + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(f), true);
        return h + sp;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float line = EditorGUIUtility.singleLineHeight;
        float sp = EditorGUIUtility.standardVerticalSpacing;
        Rect r = new Rect(position.x, position.y, position.width, line);
        property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, new GUIContent($"{label.text}   ({Summary(property)})", label.tooltip), true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            r.y += line + sp;
            SerializedProperty type = property.FindPropertyRelative("type");
            EditorGUI.PropertyField(r, type);
            Fields((HitShapeType)type.enumValueIndex, fields);
            foreach (string f in fields)
            {
                SerializedProperty p = property.FindPropertyRelative(f);
                float h = EditorGUI.GetPropertyHeight(p, true);
                r.y += r.height + sp;
                r.height = h;
                EditorGUI.PropertyField(r, p, true);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
#endif
