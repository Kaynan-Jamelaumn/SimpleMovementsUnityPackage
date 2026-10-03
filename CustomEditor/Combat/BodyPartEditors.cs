#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspectors of the body-part system: a drawing of the character's parts (heights, sides, damage, armour), one-click
/// setup (an editable profile, hitboxes on a Humanoid rig) and, while playing, test hits and the parts' state.
/// </summary>
public static class BodyPartDiagram
{
    /// <summary>Draws the parts of <paramref name="p"/> as a body: centre parts in the middle, side parts on both sides.</summary>
    public static void Draw(Rect r, BodyPartProfile p)
    {
        EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.18f));
        if (p == null || p.parts == null || p.parts.Count == 0)
        {
            GUI.Label(r, "No parts", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        float sideW = r.width * 0.22f;
        var centre = new Rect(r.x + sideW, r.y, r.width - sideW * 2f, r.height);
        var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        for (int i = 0; i < p.parts.Count; i++)
        {
            BodyPartDefinition part = p.parts[i];
            if (part == null)
                continue;
            Color c = BodyPartController.PartColor(i);
            float lo = Mathf.Clamp01(Mathf.Min(part.heightRange.x, part.heightRange.y));
            float hi = Mathf.Clamp01(Mathf.Max(part.heightRange.x, part.heightRange.y));
            float y0 = r.yMax - hi * r.height, y1 = r.yMax - lo * r.height;
            string text = $"{part.name}\n×{part.damageMultiplier:0.##}";
            if (part.side == BodySide.Any)
            {
                var box = new Rect(centre.x + 1f, y0 + 1f, centre.width - 2f, Mathf.Max(2f, y1 - y0 - 2f));
                EditorGUI.DrawRect(box, new Color(c.r, c.g, c.b, 0.55f));
                GUI.Label(box, text, style);
            }
            else
            {
                bool left = part.side != BodySide.Right, right = part.side != BodySide.Left;
                if (left) Side(new Rect(r.x + 1f, y0 + 1f, sideW - 2f, Mathf.Max(2f, y1 - y0 - 2f)), c, text, style);
                if (right) Side(new Rect(r.xMax - sideW + 1f, y0 + 1f, sideW - 2f, Mathf.Max(2f, y1 - y0 - 2f)), c, text, style);
            }
        }
        GUI.Label(new Rect(r.x + 2f, r.y, 40f, 14f), "top", EditorStyles.miniLabel);
        GUI.Label(new Rect(r.x + 2f, r.yMax - 14f, 40f, 14f), "feet", EditorStyles.miniLabel);
    }

    private static void Side(Rect box, Color c, string text, GUIStyle style)
    {
        EditorGUI.DrawRect(box, new Color(c.r, c.g, c.b, 0.45f));
        GUI.Label(box, text, style);
    }

    /// <summary>One line per part: what protects it and what it does when hit.</summary>
    public static void DrawTable(BodyPartProfile p)
    {
        if (p == null || p.parts == null)
            return;
        for (int i = 0; i < p.parts.Count; i++)
        {
            BodyPartDefinition part = p.parts[i];
            if (part == null)
                continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect sw = GUILayoutUtility.GetRect(10f, 14f, GUILayout.Width(10f));
                EditorGUI.DrawRect(new Rect(sw.x, sw.y + 2f, 10f, 10f), BodyPartController.PartColor(i));
                EditorGUILayout.LabelField(part.Describe(), EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}

[CustomEditor(typeof(BodyPartProfile))]
public class BodyPartProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var p = (BodyPartProfile)target;
        var errors = new List<string>();
        var warnings = new List<string>();
        p.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Parts (height 0 = feet, 1 = top of the character)", EditorStyles.boldLabel);
        Rect r = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
        BodyPartDiagram.Draw(r, p);
        BodyPartDiagram.DrawTable(p);
        if (GUILayout.Button(new GUIContent("Reset To Humanoid", "Head, Torso, Arms, Legs with the default multipliers, armour and effects (Undo restores).")))
        {
            Undo.RecordObject(p, "Reset body parts");
            p.FillHumanoid();
            EditorUtility.SetDirty(p);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(BodyPartController))]
public class BodyPartControllerEditor : Editor
{
    private float testDamage = 10f;

    public override void OnInspectorGUI()
    {
        var c = (BodyPartController)target;
        serializedObject.Update();
        SerializedProperty assigned = serializedObject.FindProperty("profile");
        BodyPartProfile used = c.Profile;
        string source = assigned.objectReferenceValue != null ? "the assigned profile"
            : CombatSettings.Instance.defaultBodyPartProfile != null ? "Combat Settings ▸ Default Body Part Profile"
            : "the built-in humanoid (not editable: create a profile to change it)";

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Body parts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Using {source}.", EditorStyles.wordWrappedMiniLabel);
        Rect r = GUILayoutUtility.GetRect(10f, 200f, GUILayout.ExpandWidth(true));
        BodyPartDiagram.Draw(r, used);
        BodyPartDiagram.DrawTable(used);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (assigned.objectReferenceValue == null)
            {
                if (GUILayout.Button(new GUIContent("Create Editable Profile", "Saves a copy of the profile in use as an asset and assigns it, so you can change parts, multipliers, armour and effects.")))
                    CreateProfile(c, used);
            }
            else if (GUILayout.Button(new GUIContent("Edit Profile", "Selects the assigned profile.")))
            {
                Selection.activeObject = assigned.objectReferenceValue;
            }
        }
        EditorGUILayout.EndVertical();

        // Hitboxes
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        BodyPartHitbox[] boxes = c.GetComponentsInChildren<BodyPartHitbox>(true);
        EditorGUILayout.LabelField($"Hitboxes ({boxes.Length})", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(boxes.Length == 0
                ? "None: parts come from where a hit lands (height and side). Hitboxes on the bones make hits exact and follow the animation."
                : "Hits that touch a hitbox count for its part; other hits use the height and side.",
            EditorStyles.wordWrappedMiniLabel);
        Animator animator = c.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.isHuman && animator.avatar != null;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!human || Application.isPlaying))
            {
                if (GUILayout.Button(new GUIContent(boxes.Length == 0 ? "Add Hitboxes (Humanoid)" : "Rebuild Hitboxes (Humanoid)",
                        human ? "A sphere on the head, a capsule on the torso, and capsules on the upper and lower arms and legs, named after the profile's parts."
                              : "Needs a Humanoid Animator with an Avatar. On other rigs add BodyPartHitbox components to colliders yourself.")))
                    HumanoidHitboxes.Build(c, used, animator);
            }
            using (new EditorGUI.DisabledScope(boxes.Length == 0 || Application.isPlaying))
            {
                if (GUILayout.Button("Remove Hitboxes"))
                    HumanoidHitboxes.Remove(c);
            }
        }
        if (!human)
            EditorGUILayout.LabelField("Not a Humanoid rig: add a collider and a Body Part Hitbox to each bone yourself (Part = a part's name).", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        if (Application.isPlaying)
            DrawLive(c, used);
        else
            EditorGUILayout.HelpBox("Select the character to see its parts in the Scene view (coloured bands, hitboxes). While playing, hits show where they " +
                                    "landed with the part and the damage, and you can test each part below.", MessageType.None);

        EditorGUILayout.Space(4);
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying)
            Repaint();
    }

    private void DrawLive(BodyPartController c, BodyPartProfile used)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
        testDamage = EditorGUILayout.Slider(new GUIContent("Test Damage", "Damage of a test hit (before the part's multiplier and armour)."), testDamage, 1f, 200f);
        CombatEntity e = c.Entity;
        for (int i = 0; used != null && used.parts != null && i < used.parts.Count; i++)
        {
            BodyPartDefinition part = used.parts[i];
            if (part == null)
                continue;
            BodyPartController.PartState st = c.GetState(part);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect sw = GUILayoutUtility.GetRect(10f, 16f, GUILayout.Width(10f));
                EditorGUI.DrawRect(new Rect(sw.x, sw.y + 3f, 10f, 10f), BodyPartController.PartColor(i));
                EditorGUILayout.LabelField(part.name, $"{st.hits} hit(s), {st.damageTaken:0.#} damage{(st.severed ? ", SEVERED" : "")}");
                using (new EditorGUI.DisabledScope(e == null || e.IsDead))
                {
                    if (GUILayout.Button(new GUIContent("Hit", $"Deals {testDamage:0} damage aimed at the {part.name} (unblockable), to check multipliers, armour and effects."), GUILayout.Width(40f)))
                        TestHit(c, e, part);
                }
            }
        }
        IReadOnlyList<BodyPartController.RecentHit> hits = c.RecentHits;
        if (hits.Count > 0)
        {
            BodyPartController.RecentHit last = hits[hits.Count - 1];
            EditorGUILayout.LabelField($"Last hit: {last.part}, {last.damage:0.#} damage, {Time.time - last.time:0.0}s ago", EditorStyles.miniLabel);
        }
        if (GUILayout.Button("Reset Parts"))
            c.ResetParts();
        EditorGUILayout.EndVertical();
    }

    private void TestHit(BodyPartController c, CombatEntity e, BodyPartDefinition part)
    {
        float mid = (part.heightRange.x + part.heightRange.y) * 0.5f;
        Vector3 point = e.BasePosition + Vector3.up * mid * e.Height;
        if (part.side == BodySide.Left) point -= c.transform.right * e.Radius;
        else if (part.side != BodySide.Any) point += c.transform.right * e.Radius;
        e.ApplyDamage(new DamageInfo
        {
            amount = testDamage,
            target = e,
            point = point,
            direction = -c.transform.forward,
            type = DamageType.Physical,
            delivery = DamageDelivery.Melee,
            aimedBodyPart = part.name,
            unblockable = true,
        });
    }

    private void CreateProfile(BodyPartController c, BodyPartProfile from)
    {
        var copy = from != null ? Object.Instantiate(from) : BodyPartProfile.CreateHumanoid();
        copy.name = c.gameObject.name + " Body Parts";
        string folder = "Assets";
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject);
        if (!string.IsNullOrEmpty(prefabPath))
            folder = System.IO.Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{copy.name}.asset");
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        serializedObject.FindProperty("profile").objectReferenceValue = copy;
        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(copy);
        Debug.Log($"[Body Parts] Created '{path}' and assigned it to '{c.name}'.", copy);
    }
}

/// <summary>Creates body-part hitboxes on a Humanoid rig's bones.</summary>
public static class HumanoidHitboxes
{
    private const string Prefix = "Hitbox_";

    public static void Build(BodyPartController c, BodyPartProfile profile, Animator a)
    {
        Remove(c);
        CombatEntity e = c.GetComponent<CombatEntity>();
        float h = e != null && e.Height > 0.1f ? e.Height : 1.8f;
        int layer = c.gameObject.layer;
        string head = Pick(profile, "Head"), torso = Pick(profile, "Torso"), arms = Pick(profile, "Arms"), legs = Pick(profile, "Legs");
        int made = 0;

        Transform headBone = a.GetBoneTransform(HumanBodyBones.Head);
        if (headBone != null)
        {
            var go = Make(headBone, Prefix + head, layer);
            var sc = Undo.AddComponent<SphereCollider>(go);
            sc.radius = 0.065f * h / Mathf.Max(0.0001f, headBone.lossyScale.y);
            sc.center = headBone.InverseTransformVector(Vector3.up * 0.05f * h);
            Tag(go, head);
            made++;
        }
        made += Capsule(a, HumanBodyBones.Hips, HumanBodyBones.Neck, torso, 0.12f * h, layer);
        made += Capsule(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, arms, 0.04f * h, layer);
        made += Capsule(a, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, arms, 0.035f * h, layer);
        made += Capsule(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, arms, 0.04f * h, layer);
        made += Capsule(a, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, arms, 0.035f * h, layer);
        made += Capsule(a, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, legs, 0.06f * h, layer);
        made += Capsule(a, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, legs, 0.045f * h, layer);
        made += Capsule(a, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, legs, 0.06f * h, layer);
        made += Capsule(a, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, legs, 0.045f * h, layer);
        EditorUtility.SetDirty(c.gameObject);
        Debug.Log($"[Body Parts] Added {made} hitbox(es) to '{c.name}' (triggers on layer '{LayerMask.LayerToName(layer)}'). Adjust their radius on each Hitbox_ object.", c);
    }

    public static void Remove(BodyPartController c)
    {
        foreach (BodyPartHitbox hb in c.GetComponentsInChildren<BodyPartHitbox>(true))
            if (hb != null && hb.gameObject.name.StartsWith(Prefix))
                Undo.DestroyObjectImmediate(hb.gameObject);
    }

    /// <summary>A part name of the profile (the wanted one, else an alias match, else the wanted name).</summary>
    private static string Pick(BodyPartProfile p, string wanted)
    {
        BodyPartDefinition d = p != null ? p.Find(wanted) : null;
        return d != null ? d.name : wanted;
    }

    private static int Capsule(Animator a, HumanBodyBones from, HumanBodyBones to, string part, float radius, int layer)
    {
        Transform f = a.GetBoneTransform(from), t = a.GetBoneTransform(to);
        if (f == null || t == null)
            return 0;
        Vector3 axis = t.position - f.position;
        float length = axis.magnitude;
        if (length < 0.01f)
            return 0;
        var go = Make(f, $"{Prefix}{part}_{from}", layer);
        go.transform.position = f.position + axis * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis / length);
        var cc = Undo.AddComponent<CapsuleCollider>(go);
        float s = Mathf.Max(0.0001f, go.transform.lossyScale.y);
        cc.direction = 1;
        cc.radius = radius / s;
        cc.height = (length + radius) / s;
        Tag(go, part);
        return 1;
    }

    private static GameObject Make(Transform bone, string name, int layer)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Add body part hitbox");
        go.transform.SetParent(bone, false);
        go.layer = layer;
        return go;
    }

    private static void Tag(GameObject go, string part)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        var hb = Undo.AddComponent<BodyPartHitbox>(go);
        hb.part = part;
    }
}
#endif
