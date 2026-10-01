#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Draws a weapon hit volume showing only the fields its shape uses, with a one-line summary.</summary>
[CustomPropertyDrawer(typeof(WeaponBlade))]
public class WeaponBladeDrawer : PropertyDrawer
{
    private static readonly List<(string field, string label)> rows = new List<(string, string)>(10);

    private static void Rows(WeaponBlade.VolumeShape shape)
    {
        rows.Clear();
        rows.Add(("name", "Name"));
        rows.Add(("shape", "Shape"));
        switch (shape)
        {
            case WeaponBlade.VolumeShape.Sphere:
                rows.Add(("center", "Centre"));
                rows.Add(("radius", "Radius"));
                rows.Add(("startMarker", "Centre Marker"));
                break;
            case WeaponBlade.VolumeShape.Box:
                rows.Add(("center", "Centre"));
                rows.Add(("size", "Size"));
                rows.Add(("rotation", "Rotation"));
                rows.Add(("startMarker", "Centre Marker"));
                break;
            default:
                rows.Add(("axis", "Axis"));
                rows.Add(("start", "Start"));
                rows.Add(("end", "End"));
                rows.Add(("radius", "Radius"));
                rows.Add(("startMarker", "Start Marker"));
                rows.Add(("endMarker", "End Marker"));
                break;
        }
    }

    private static string Summary(SerializedProperty p)
    {
        string name = p.FindPropertyRelative("name").stringValue;
        var shape = (WeaponBlade.VolumeShape)p.FindPropertyRelative("shape").enumValueIndex;
        switch (shape)
        {
            case WeaponBlade.VolumeShape.Sphere:
                return $"{name}: sphere r{p.FindPropertyRelative("radius").floatValue:0.##} m";
            case WeaponBlade.VolumeShape.Box:
                Vector3 s = p.FindPropertyRelative("size").vector3Value;
                return $"{name}: box {s.x:0.##}×{s.y:0.##}×{s.z:0.##} m";
            default:
                return $"{name}: capsule {p.FindPropertyRelative("start").floatValue:0.##} → {p.FindPropertyRelative("end").floatValue:0.##} m, r{p.FindPropertyRelative("radius").floatValue:0.##}";
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return h;
        Rows((WeaponBlade.VolumeShape)property.FindPropertyRelative("shape").enumValueIndex);
        foreach (var r in rows)
            h += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(r.field), true);
        return h + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float sp = EditorGUIUtility.standardVerticalSpacing;
        var r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, new GUIContent(Summary(property), label.tooltip), true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            Rows((WeaponBlade.VolumeShape)property.FindPropertyRelative("shape").enumValueIndex);
            foreach (var row in rows)
            {
                SerializedProperty p = property.FindPropertyRelative(row.field);
                r.y += r.height + sp;
                r.height = EditorGUI.GetPropertyHeight(p, true);
                EditorGUI.PropertyField(r, p, new GUIContent(row.label, p.tooltip), true);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
#endif
