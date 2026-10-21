#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a [ComponentTypeName] string as a script picker: drag a script file onto it or pick one of the project's
/// scripts from the list (Unity's own scripts are left out). Stores the type's assembly-qualified name.
/// </summary>
[CustomPropertyDrawer(typeof(ComponentTypeNameAttribute))]
public class ComponentTypeNameDrawer : PropertyDrawer
{
    private static List<Type> scripts;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }
        EditorGUI.BeginProperty(position, label, property);
        if (!string.IsNullOrEmpty(property.stringValue) && Type.GetType(property.stringValue, false) == null)
            label = new GUIContent(label.text + " (missing!)", "The script '" + property.stringValue + "' no longer exists.");
        Rect field = EditorGUI.PrefixLabel(position, label);
        Rect objRect = new Rect(field.x, field.y, field.width - 24f, field.height);
        Rect listRect = new Rect(field.xMax - 22f, field.y, 22f, field.height);

        Type current = string.IsNullOrEmpty(property.stringValue) ? null : Type.GetType(property.stringValue, false);
        MonoScript currentScript = current != null ? FindScript(current) : null;
        EditorGUI.BeginChangeCheck();
        var picked = (MonoScript)EditorGUI.ObjectField(objRect, currentScript, typeof(MonoScript), false);
        if (EditorGUI.EndChangeCheck())
        {
            Type t = picked != null ? picked.GetClass() : null;
            if (picked == null)
                property.stringValue = "";
            else if (t != null && typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract)
                property.stringValue = t.AssemblyQualifiedName;
            else
                Debug.LogWarning($"{picked.name} is not a MonoBehaviour that can be added to an object.");
        }

        if (GUI.Button(listRect, EditorGUIUtility.IconContent("_Popup"), EditorStyles.miniButton))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), current == null, () => Set(property, ""));
            foreach (Type t in Scripts())
            {
                string path = string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace.Replace('.', '/') + "/" + t.Name;
                menu.AddItem(new GUIContent(path), t == current, () => Set(property, t.AssemblyQualifiedName));
            }
            menu.DropDown(listRect);
        }
        EditorGUI.EndProperty();
    }

    private static void Set(SerializedProperty property, string value)
    {
        property.serializedObject.Update();
        property.stringValue = value;
        property.serializedObject.ApplyModifiedProperties();
    }

    private static readonly Dictionary<Type, MonoScript> scriptFiles = new Dictionary<Type, MonoScript>();

    private static MonoScript FindScript(Type t)
    {
        if (scriptFiles.TryGetValue(t, out MonoScript cached) && cached != null)
            return cached;
        MonoScript found = Search(t);
        scriptFiles[t] = found;
        return found;
    }

    private static MonoScript Search(Type t)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:MonoScript {t.Name}"))
        {
            var s = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null && s.GetClass() == t)
                return s;
        }
        return null;
    }

    private static List<Type> Scripts()
    {
        if (scripts != null)
            return scripts;
        scripts = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
            .Where(t => !t.IsAbstract && !t.IsGenericType && t.IsPublic)
            .Where(t => t.Namespace == null || !(t.Namespace.StartsWith("Unity") || t.Namespace.StartsWith("TMPro") || t.Namespace.StartsWith("FishNet")))
            .OrderBy(t => t.Namespace ?? "").ThenBy(t => t.Name)
            .ToList();
        return scripts;
    }
}
#endif
