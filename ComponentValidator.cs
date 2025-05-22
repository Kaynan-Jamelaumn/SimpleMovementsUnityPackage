using UnityEngine;
using System.Collections.Generic;

public static class ComponentValidator
{
    public static T CheckComponent<T>(this MonoBehaviour script, T component, string variableName, bool isCritical = true, bool searchChildren = false, bool searchSiblings = false, bool searchParent = false) where T : Component
    {
        if (component == null)
        {
            component = searchChildren ?
                script.GetComponentInChildren<T>() :
                script.GetComponent<T>();

            // Search siblings if enabled
            if (component == null && searchSiblings)
            {
                Transform parentTransform = script.transform.parent;
                if (parentTransform != null)
                {
                    foreach (Transform sibling in parentTransform)
                    {
                        if (sibling != script.transform)
                        {
                            component = sibling.GetComponent<T>();
                            if (component != null)
                                break;
                        }
                    }
                }
            }

            // Search parent if enabled
            if (component == null && searchParent)
            {
                component = script.GetComponentInParent<T>();
            }

            if (component == null)
            {
                string className = script.GetType().Name;
                string errorMsg = $"{typeof(T).Name} component '{variableName}' is missing on {className} (GameObject: {script.gameObject.name}). Please add the component.";
                HandleValidationError(script, errorMsg, isCritical);
            }
            else
            {
                string className = script.GetType().Name;
                Debug.Log($"Auto-assigned {typeof(T).Name} component '{variableName}' on {className} (GameObject: {script.gameObject.name})", script.gameObject);
            }
        }
        return component;
    }

    public static void ValidateField<T>(this MonoBehaviour script, T field, string fieldName, bool isCritical = true)
    {
        if (field == null || field.Equals(null))
        {
            string className = script.GetType().Name;
            string errorMsg = $"{fieldName} reference missing on {className} (GameObject: {script.gameObject.name})";
            HandleValidationError(script, errorMsg, isCritical);
        }
    }

    public static void ValidateList<T>(this MonoBehaviour script, List<T> list, string listName, bool allowEmpty = false)
    {
        if (list == null)
        {
            string className = script.GetType().Name;
            string errorMsg = $"{listName} list not initialized on {className} (GameObject: {script.gameObject.name})";
            HandleValidationError(script, errorMsg, true);
            return;
        }

        if (!allowEmpty && list.Count == 0)
        {
            string className = script.GetType().Name;
            string errorMsg = $"{listName} list is empty on {className} (GameObject: {script.gameObject.name})";
            HandleValidationError(script, errorMsg, true);
        }
    }

    public static float ValidateValue(this MonoBehaviour script, float value, float minValue, float maxValue, string valueName)
    {
        if (value < minValue || value > maxValue)
        {
            string className = script.GetType().Name;
            float clampedValue = Mathf.Clamp(value, minValue, maxValue);
            string warningMsg = $"{valueName} value out of range on {className} (GameObject: {script.gameObject.name}). Clamped to {clampedValue}";
            Debug.LogWarning(warningMsg, script.gameObject);
            return clampedValue;
        }
        return value;
    }

    public static void ValidateDict<TKey, TValue>(this MonoBehaviour script, Dictionary<TKey, TValue> dictionary,
        string dictName, bool allowEmpty = false)
    {
        if (dictionary == null)
        {
            string className = script.GetType().Name;
            string errorMsg = $"{dictName} dictionary not initialized on {className} (GameObject: {script.gameObject.name})";
            HandleValidationError(script, errorMsg, true);
            return;
        }

        if (!allowEmpty && dictionary.Count == 0)
        {
            string className = script.GetType().Name;
            string errorMsg = $"{dictName} dictionary is empty on {className} (GameObject: {script.gameObject.name})";
            HandleValidationError(script, errorMsg, true);
        }
    }

    public static void ValidateString(this MonoBehaviour script, string value, string fieldName,
        bool isCritical = true, bool allowEmpty = false)
    {
        if (string.IsNullOrEmpty(value) || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
        {
            string className = script.GetType().Name;
            string state = value == null ? "null" : (allowEmpty ? "empty" : "empty/invalid");
            string errorMsg = $"{fieldName} string is {state} on {className} (GameObject: {script.gameObject.name})";
            HandleValidationError(script, errorMsg, isCritical);
        }
    }

    private static void HandleValidationError(MonoBehaviour script, string message, bool isCritical)
    {
        if (isCritical)
        {
            Debug.LogError(message, script.gameObject);
            script.enabled = false;
        }
        else
        {
            Debug.LogWarning(message, script.gameObject);
        }
    }
}