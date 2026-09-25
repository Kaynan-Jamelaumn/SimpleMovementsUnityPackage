#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class InventoryEditorStyles
{
    // Styles
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle buttonStyle;
    private GUIStyle successButtonStyle;
    private GUIStyle warningButtonStyle;
    private GUIStyle errorButtonStyle;
    private GUIStyle boxStyle;
    private GUIStyle centeredStyle;

    // Colors
    private readonly Color successColor = new Color(0.2f, 0.8f, 0.2f);
    private readonly Color warningColor = new Color(1f, 0.8f, 0.2f);
    private readonly Color errorColor = new Color(0.8f, 0.2f, 0.2f);
    private readonly Color infoColor = new Color(0.2f, 0.6f, 1f);

    // Properties
    public GUIStyle HeaderStyle => headerStyle;
    public GUIStyle SubHeaderStyle => subHeaderStyle;
    public GUIStyle ButtonStyle => buttonStyle;
    public GUIStyle SuccessButtonStyle => successButtonStyle;
    public GUIStyle WarningButtonStyle => warningButtonStyle;
    public GUIStyle ErrorButtonStyle => errorButtonStyle;
    public GUIStyle BoxStyle => boxStyle;
    public GUIStyle CenteredStyle => centeredStyle;

    public Color SuccessColor => successColor;
    public Color WarningColor => warningColor;
    public Color ErrorColor => errorColor;
    public Color InfoColor => infoColor;

    public void InitializeStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };
        }

        if (subHeaderStyle == null)
        {
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.cyan : Color.blue }
            };
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(2, 2, 2, 2)
            };
        }

        if (successButtonStyle == null)
        {
            successButtonStyle = new GUIStyle(buttonStyle);
            successButtonStyle.normal.textColor = successColor;
            successButtonStyle.hover.textColor = Color.white;
        }

        if (warningButtonStyle == null)
        {
            warningButtonStyle = new GUIStyle(buttonStyle);
            warningButtonStyle.normal.textColor = warningColor;
            warningButtonStyle.hover.textColor = Color.white;
        }

        if (errorButtonStyle == null)
        {
            errorButtonStyle = new GUIStyle(buttonStyle);
            errorButtonStyle.normal.textColor = errorColor;
            errorButtonStyle.hover.textColor = Color.white;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(5, 5, 5, 5)
            };
        }

        if (centeredStyle == null)
        {
            centeredStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
        }
    }

    public void DrawStatusIndicator(string label, bool isValid)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label);

        GUI.color = isValid ? successColor : errorColor;
        EditorGUILayout.LabelField(isValid ? "✓" : "✗", GUILayout.Width(20));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    public void DrawProgressBar(string label, float progress)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(120));

        Rect progressRect = EditorGUILayout.GetControlRect();
        EditorGUI.ProgressBar(progressRect, progress, $"{progress:P0}");

        EditorGUILayout.EndHorizontal();
    }
}
#endif