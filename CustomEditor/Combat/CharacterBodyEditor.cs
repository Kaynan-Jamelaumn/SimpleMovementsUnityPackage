using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector of <see cref="CharacterBody"/>: shows where the height comes from (the body itself or a Character Identity
/// and its race), the scale and final height it gives, which model and CharacterController it will resize, and why
/// nothing changes in Edit mode.
/// </summary>
[CustomEditor(typeof(CharacterBody))]
public class CharacterBodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var body = (CharacterBody)target;
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

        Transform root = body.GetComponentInParent<CharacterController>() != null
            ? body.GetComponentInParent<CharacterController>().transform
            : body.transform;
        CharacterIdentity identity = body.GetComponentInParent<CharacterIdentity>();
        if (identity == null) identity = body.GetComponentInChildren<CharacterIdentity>();
        float identityHeight = identity != null ? identity.Height : 0f;
        float usedHeight = identityHeight > 0f ? identityHeight : body.height;

        if (identityHeight > 0f)
            EditorGUILayout.HelpBox($"The Character Identity sets the height: {identityHeight:0.00} m (its Height field, else its race's default, " +
                                    "clamped to the race's Height Range). This body's own Height field is overwritten with it when the game starts.", MessageType.Info);

        float scale = Application.isPlaying ? body.Scale : body.ScaleFor(usedHeight);
        float raw = usedHeight / Mathf.Max(0.1f, body.modelHeight);
        EditorGUILayout.LabelField(Application.isPlaying ? "Scale (now)" : "Scale (when the game runs)", $"{scale:0.###}  (height {usedHeight:0.00} / model height {body.modelHeight:0.00}{(Application.isPlaying ? " × Height stat" : "")})");
        EditorGUILayout.LabelField("Final height", $"{body.modelHeight * scale:0.00} m");
        if (!Application.isPlaying && (raw < body.scaleLimits.x - 0.0001f || raw > body.scaleLimits.y + 0.0001f))
            EditorGUILayout.HelpBox($"Scale Limits ({body.scaleLimits.x:0.##}-{body.scaleLimits.y:0.##}) cut the scale {raw:0.###} to {scale:0.###}.", MessageType.Warning);

        Transform model = body.model != null ? body.model : CharacterBody.FindModel(root);
        CharacterController cc = body.GetComponent<CharacterController>() ?? root.GetComponent<CharacterController>();
        EditorGUILayout.LabelField("Model scaled", model != null ? model.name + (body.model == null ? "  (found automatically)" : "") : "none");
        EditorGUILayout.LabelField("Controller resized", cc != null && body.resizeColliders ? $"{cc.name} (height {cc.height:0.00} → {cc.height * scale:0.00})" : "none");
        if (model == root)
            EditorGUILayout.HelpBox("The model must be a child holding the meshes, not the character's root.", MessageType.Error);
        if (model == null)
            EditorGUILayout.HelpBox("No model to scale: assign Model (the child holding the meshes, e.g. PlayerShellObject).", MessageType.Warning);
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("The size is applied when the game runs (Play): the prefab keeps its authored size in Edit mode. " +
                                    "While playing, changing Height here or on the Character Identity updates the character at once.", MessageType.None);
    }
}
