using UnityEngine;

/// <summary>
/// Unity calls whose overloads differ between Unity 6 minor versions, behind one call that compiles on every 6000.x
/// and does on each version exactly what the project already did on Unity 6.4.
/// <list type="bullet">
/// <item><see cref="FindObjectsByType{T}(FindObjectsInactive)"/>: Unity 6.4 (6000.4) added
/// <c>Object.FindObjectsByType&lt;T&gt;(FindObjectsInactive)</c> and deprecated the overloads taking a
/// <see cref="FindObjectsSortMode"/> (the sort order relied on InstanceID). Unity 6000.0 - 6000.3 only have the
/// sort-mode overloads; there the same search runs with <see cref="FindObjectsSortMode.None"/>, which is what the new
/// overload does (unsorted).</item>
/// <item><see cref="HasObjectReferenceId"/> (editor): Unity 6.4 replaced
/// <c>SerializedProperty.objectReferenceInstanceIDValue</c> with <c>objectReferenceEntityIdValue</c>.</item>
/// </list>
/// </summary>
public static class UnityVersionCompat
{
    /// <summary>
    /// Every loaded object of type <typeparamref name="T"/> (no assets), unsorted; objects on inactive GameObjects only
    /// with <see cref="FindObjectsInactive.Include"/>. The same as <c>Object.FindObjectsByType&lt;T&gt;(findObjectsInactive)</c>
    /// on Unity 6.4 and later.
    /// </summary>
    public static T[] FindObjectsByType<T>(FindObjectsInactive findObjectsInactive) where T : Object
    {
#if UNITY_6000_4_OR_NEWER
        return Object.FindObjectsByType<T>(findObjectsInactive);
#else
        return Object.FindObjectsByType<T>(findObjectsInactive, FindObjectsSortMode.None);
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// True when an object reference property holds a reference id - also when the object it names is gone (a missing
    /// reference: <c>objectReferenceValue</c> is null but the id is not). The same as
    /// <c>property.objectReferenceEntityIdValue != default</c> on Unity 6.4 and later.
    /// </summary>
    public static bool HasObjectReferenceId(UnityEditor.SerializedProperty property)
    {
        if (property == null)
            return false;
#if UNITY_6000_4_OR_NEWER
        return property.objectReferenceEntityIdValue != default;
#else
        return property.objectReferenceInstanceIDValue != 0;
#endif
    }
#endif
}
