using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(AttackActionEffect))]
public class AttackActionEffectDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw all properties except AttackCast
        var iterator = property.Copy();
        var endProperty = property.GetEndProperty();

        if (iterator.NextVisible(true))
        {
            do
            {
                if (iterator.name != "attackCast") // Skip AttackCast field
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
            while (iterator.NextVisible(false) && !SerializedProperty.EqualContents(iterator, endProperty));
        }

        EditorGUI.EndProperty();
    }
}
#endif

