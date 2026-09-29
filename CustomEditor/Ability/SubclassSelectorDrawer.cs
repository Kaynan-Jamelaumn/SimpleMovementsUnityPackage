#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws [SerializeReference, SubclassSelector] fields (ability actions and effects) with a type dropdown grouped
/// by the types' AbilityMenu paths, so designers pick "Hit/Area Hit" or "Control/Stun" instead of writing code.
/// </summary>
[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    private static readonly Dictionary<Type, List<Type>> typeCache = new Dictionary<Type, List<Type>>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
            return EditorGUIUtility.singleLineHeight;
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.LabelField(position, label.text, "[SubclassSelector] needs [SerializeReference]");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);
        Type current = GetCurrentType(property);
        Type baseType = GetFieldType(property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        float labelWidth = EditorGUIUtility.labelWidth;
        Rect button = new Rect(line.x + labelWidth + 2f, line.y, Mathf.Max(60f, line.width - labelWidth - 2f), line.height);

        string typeName = current != null ? AbilityTypeNames.Nice(current) : "None (choose a type)";
        GUIContent buttonContent = new GUIContent(typeName + "  ▾", TooltipOf(current));
        Color old = GUI.backgroundColor;
        if (current == null)
            GUI.backgroundColor = new Color(1f, 0.6f, 0.5f);
        if (EditorGUI.DropdownButton(button, buttonContent, FocusType.Keyboard))
            ShowMenu(property, baseType, current);
        GUI.backgroundColor = old;

        // Element labels ("Element 0") become the type name, easier to scan in long lists.
        GUIContent shown = label;
        if (current != null && label.text.StartsWith("Element "))
            shown = new GUIContent($"{label.text.Substring(8)}. {AbilityTypeNames.Nice(current)}", label.tooltip);

        if (current == null)
            EditorGUI.LabelField(new Rect(line.x, line.y, labelWidth, line.height), shown);
        else
            EditorGUI.PropertyField(position, property, shown, true);
        EditorGUI.EndProperty();
    }

    private static string TooltipOf(Type t)
    {
        if (t == null)
            return "Pick what this entry does.";
        var menu = (AbilityMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AbilityMenuAttribute), false);
        return menu != null ? menu.tooltip : t.Name;
    }

    private static void ShowMenu(SerializedProperty property, Type baseType, Type current)
    {
        SerializedObject so = property.serializedObject;
        string path = property.propertyPath;
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("None"), current == null, () => Assign(so, path, null));
        menu.AddSeparator("");
        foreach (Type t in GetTypes(baseType))
        {
            var attr = (AbilityMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AbilityMenuAttribute), false);
            string item = attr != null && !string.IsNullOrEmpty(attr.path) ? attr.path : AbilityTypeNames.Nice(t);
            Type captured = t;
            menu.AddItem(new GUIContent(item), current == t, () => Assign(so, path, captured));
        }
        menu.ShowAsContext();
    }

    private static void Assign(SerializedObject so, string path, Type type)
    {
        so.Update();
        SerializedProperty p = so.FindProperty(path);
        if (p == null)
            return;
        Undo.RecordObjects(so.targetObjects, "Change Type");
        p.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
        p.isExpanded = type != null;
        so.ApplyModifiedProperties();
    }

    public static List<Type> GetTypes(Type baseType)
    {
        if (baseType == null)
            return new List<Type>();
        if (typeCache.TryGetValue(baseType, out List<Type> list))
            return list;
        list = TypeCache.GetTypesDerivedFrom(baseType)
            .Where(t => !t.IsAbstract && !t.IsGenericType && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                        && t.IsDefined(typeof(SerializableAttribute), false) && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t =>
            {
                var a = (AbilityMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AbilityMenuAttribute), false);
                string group = a != null && a.path.Contains("/") ? a.path.Substring(0, a.path.LastIndexOf('/')) : "~";
                return group;
            })
            .ThenBy(t =>
            {
                var a = (AbilityMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AbilityMenuAttribute), false);
                return a != null ? a.order : 100;
            })
            .ThenBy(t => t.Name)
            .ToList();
        if (!baseType.IsAbstract && baseType.GetConstructor(Type.EmptyTypes) != null)
            list.Insert(0, baseType);
        typeCache[baseType] = list;
        return list;
    }

    public static Type GetCurrentType(SerializedProperty property) => ParseType(property.managedReferenceFullTypename);

    public static Type GetFieldType(SerializedProperty property) => ParseType(property.managedReferenceFieldTypename);

    private static Type ParseType(string typename)
    {
        if (string.IsNullOrEmpty(typename))
            return null;
        int split = typename.IndexOf(' ');
        if (split < 0)
            return Type.GetType(typename);
        string assembly = typename.Substring(0, split);
        string name = typename.Substring(split + 1);
        return Type.GetType($"{name}, {assembly}");
    }
}

/// <summary>Draws [LayerIndex] ints as a layer dropdown.</summary>
[CustomPropertyDrawer(typeof(LayerIndexAttribute))]
public class LayerIndexDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }
        EditorGUI.BeginProperty(position, label, property);
        property.intValue = EditorGUI.LayerField(position, label, property.intValue);
        EditorGUI.EndProperty();
    }
}
#endif
