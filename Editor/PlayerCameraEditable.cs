#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Custom PropertyDrawer for CameraEntry struct/class
[CustomPropertyDrawer(typeof(CameraEntry))]
public class CameraEntryDrawer : PropertyDrawer
{
    // Main method that draws the property in the Inspector
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Start property drawing context (handles undo/redo and prefab overrides)
        EditorGUI.BeginProperty(position, label, property);

        // Setup layout measurements
        var singleLineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        var currentY = position.y;
        var fullWidth = position.width;

        // Draw foldout arrow and manage expand/collapse state
        var foldoutRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        currentY += singleLineHeight + spacing;

        // Only show child properties when expanded
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++; // Increase visual indentation

            // Draw camera reference field
            var cameraRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
            EditorGUI.PropertyField(cameraRect, property.FindPropertyRelative("camera"));
            currentY += singleLineHeight + spacing;

            // Draw first person toggle
            var isFirstPersonRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
            EditorGUI.PropertyField(isFirstPersonRect, property.FindPropertyRelative("isFirstPerson"));
            currentY += singleLineHeight + spacing;

            // Draw camera name text field
            var cameraNameRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
            EditorGUI.PropertyField(cameraNameRect, property.FindPropertyRelative("cameraName"));
            currentY += singleLineHeight + spacing;

            // Draw nested transition settings (using its own PropertyDrawer)
            var transitionSettingsRect = new Rect(position.x, currentY, fullWidth,
                EditorGUI.GetPropertyHeight(property.FindPropertyRelative("transitionSettings")));
            EditorGUI.PropertyField(transitionSettingsRect,
                property.FindPropertyRelative("transitionSettings"), true);

            EditorGUI.indentLevel--; // Reset indentation
        }

        EditorGUI.EndProperty(); // End property drawing context
    }

    // Calculates total height needed for the property display
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var singleLineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
        {
            return singleLineHeight; // Collapsed height
        }

        // Expanded height calculation
        var height = singleLineHeight + spacing; // Foldout
        height += 3 * (singleLineHeight + spacing); // Three regular fields
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("transitionSettings"), true); // Nested properties

        return height;
    }
}

// Custom PropertyDrawer for CameraTransitionSettings struct/class
[CustomPropertyDrawer(typeof(CameraTransitionSettings))]
public class CameraTransitionSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var singleLineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        var currentY = position.y;
        var fullWidth = position.width;

        // Foldout for transition settings group
        var foldoutRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        currentY += singleLineHeight + spacing;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            // Draw smoothing toggle
            var useSmoothingRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
            EditorGUI.PropertyField(useSmoothingRect, property.FindPropertyRelative("useSmoothing"));
            currentY += singleLineHeight + spacing;

            // Only show transition parameters if smoothing is enabled
            var useSmoothing = property.FindPropertyRelative("useSmoothing").boolValue;
            if (useSmoothing)
            {
                // Draw duration field
                var durationRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
                EditorGUI.PropertyField(durationRect, property.FindPropertyRelative("transitionDuration"));
                currentY += singleLineHeight + spacing;

                // Draw animation curve field
                var curveRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
                EditorGUI.PropertyField(curveRect, property.FindPropertyRelative("transitionCurve"));
                currentY += singleLineHeight + spacing;

                // Draw velocity toggle
                var maintainVelocityRect = new Rect(position.x, currentY, fullWidth, singleLineHeight);
                EditorGUI.PropertyField(maintainVelocityRect, property.FindPropertyRelative("maintainVelocity"));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var singleLineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
        {
            return singleLineHeight;
        }

        var height = singleLineHeight + spacing; // Foldout
        height += singleLineHeight + spacing; // UseSmoothing

        // Add height for conditional fields
        if (property.FindPropertyRelative("useSmoothing").boolValue)
        {
            height += 3 * (singleLineHeight + spacing); // Three additional fields
        }

        return height;
    }
}
#endif