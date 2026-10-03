#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Previews one attack of a weapon without entering Play Mode: plays (or scrubs) its animation on a character in the
/// scene and draws, in the Scene view, the area it hits - coloured by phase (yellow wind-up, red while it hits, grey
/// recovery) and moving with the attack's lunge - plus the weapon's range and its old Attack Cast around the hand.
/// Open it with the ▶ buttons of the weapon inspector or Tools ▸ SimpleMovements ▸ Inventory ▸ Attack Preview.
/// </summary>
public class AttackPreviewWindow : EditorWindow
{
    [SerializeField] private WeaponSO weapon;
    [SerializeField] private AttackType input;
    [SerializeField] private int variation; // 0 = the attack itself, n = its n-th variation (chain hit n + 1)
    [SerializeField] private GameObject character;
    [SerializeField] private float time;
    [SerializeField] private float charge = 1f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool sampleAnimation = true;
    [SerializeField] private bool showRange = true;
    [SerializeField] private float playbackRate = 1f;
    [SerializeField] private bool showWeapon = true;
    [SerializeField] private bool showTrail = true;

    private bool playing;
    // Temporary copy of the weapon in the animated hand (HideFlags.DontSave: never saved, removed on Stop / close).
    private GameObject previewModel;
    private WeaponSO previewModelOf;
    // The swing: where each active volume was at each sampled moment of the active phase.
    private struct TrailSample { public float t; public int volume; public VolumePose pose; }
    private readonly List<TrailSample> trail = new List<TrailSample>(256);
    private readonly List<WeaponBlade> activeVolumes = new List<WeaponBlade>(4);
    private bool impactShown;
    private float impactTime;
    private Vector3 impactPoint;
    private bool impactEstimated;
    private float lastSimTime = -1f;
    private double lastTick;
    private float pauseUntil;
    private readonly List<Vector3> outer = new List<Vector3>(64);
    private readonly List<Vector3> inner = new List<Vector3>(64);

    private static readonly float[] Rates = { 0.1f, 0.25f, 0.5f, 1f };
    private static readonly string[] RateNames = { "×0.1", "×0.25", "×0.5", "×1" };

    public static void Open(WeaponSO weapon, AttackType input, int variation)
    {
        var w = GetWindow<AttackPreviewWindow>();
        w.titleContent = new GUIContent("Attack Preview");
        w.minSize = new Vector2(340f, 420f);
        if (weapon != null)
            w.weapon = weapon;
        w.input = input;
        w.variation = Mathf.Max(0, variation);
        w.time = 0f;
        w.playing = true;
        if (w.character == null)
            w.character = FindCharacter();
        w.lastTick = EditorApplication.timeSinceStartup;
        w.Show();
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/SimpleMovements/Inventory/Attack Preview")]
    private static void OpenFromMenu() => Open(Selection.activeObject as WeaponSO, AttackType.Normal, 0);

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        lastTick = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        StopSampling();
        SceneView.RepaintAll();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        playing = false;
        StopSampling();
    }

    private void ResetSimulation()
    {
        trail.Clear();
        impactShown = false;
        impactEstimated = false;
        lastSimTime = -1f;
    }

    // ------------------------------------------------------------------ data
    private AttackComponent Current() => AttackPreviewUtility.Resolve(weapon, input, variation);

    private float Duration(AttackComponent c) => AttackPreviewUtility.Duration(weapon, c);

    private float AreaScale(AttackComponent c) => c.charge != null && c.charge.enabled ? c.charge.AreaMultiplier(charge) : 1f;

    private static GameObject FindCharacter()
    {
        if (Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
        {
            Animator a = Selection.activeGameObject.GetComponentInChildren<Animator>(true);
            if (a != null) return a.gameObject;
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            Animator a = stage.prefabContentsRoot.GetComponentInChildren<Animator>(true);
            if (a != null) return a.gameObject;
        }
        PlayerStatusController ps = Object.FindAnyObjectByType<PlayerStatusController>();
        if (ps != null)
        {
            Animator a = ps.GetComponentInChildren<Animator>(true);
            return a != null ? a.gameObject : ps.gameObject;
        }
        return null;
    }

    // ------------------------------------------------------------------ playback
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min(0.1f, (float)(now - lastTick));
        lastTick = now;
        if (!playing || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        AttackComponent c = Current();
        if (c == null)
        {
            playing = false;
            return;
        }
        if (Time.realtimeSinceStartup < pauseUntil)
            return;
        float duration = Duration(c);
        time += dt * playbackRate;
        if (time >= duration)
        {
            if (loop)
            {
                time = 0f;
                pauseUntil = Time.realtimeSinceStartup + 0.35f; // a short pause between loops
            }
            else
            {
                time = duration;
                playing = false;
            }
        }
        Sample(c);
        SceneView.RepaintAll();
        Repaint();
    }

    private void Sample(AttackComponent c)
    {
        if (c == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        AnimationClip clip = c.AnimationClip;
        if (sampleAnimation && character != null && clip != null)
        {
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            // The game stretches the clip over the attack's duration.
            float duration = Mathf.Max(0.01f, Duration(c));
            float clipTime = Mathf.Clamp01(time / duration) * clip.length;
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, clipTime);
            AnimationMode.EndSampling();
        }
        else if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }
        Simulate(c);
    }

    private void StopSampling()
    {
        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
        DestroyPreviewModel();
        ResetSimulation();
    }

    // ------------------------------------------------------------------ weapon in the hand
    /// <summary>The character's weapon hand: its Weapon Controller's Hand, else a humanoid's right hand.</summary>
    private Transform FindHand()
    {
        if (character == null)
            return null;
        WeaponController wc = character.GetComponentInParent<WeaponController>();
        if (wc == null) wc = character.transform.root.GetComponentInChildren<WeaponController>(true);
        if (wc != null && wc.HandTransform != null)
            return wc.HandTransform;
        Animator animator = character.GetComponentInChildren<Animator>(true);
        return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
    }

    /// <summary>
    /// The weapon model in the hand: the one the character already holds, or a temporary copy placed with the item's
    /// hand position / rotation / scale (exactly like the game does). Null without a hand.
    /// </summary>
    private Transform HeldModel(bool create = true)
    {
        Transform hand = FindHand();
        if (hand == null || weapon == null)
            return null;
        GameObject prefab = weapon.Prefab;
        if (prefab != null)
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform t = hand.GetChild(i);
                if (t.gameObject != previewModel && t.name.StartsWith(prefab.name, System.StringComparison.Ordinal))
                    return t; // already held (e.g. in Play mode or a posed prefab)
            }
        if (!showWeapon || prefab == null)
            return hand; // volumes measured from the hand, as the game does without a model
        if (previewModel != null && (previewModelOf != weapon || previewModel.transform.parent != hand))
            DestroyPreviewModel();
        if (previewModel == null && !create)
            return null;
        if (previewModel == null)
        {
            previewModel = Object.Instantiate(prefab, hand);
            previewModel.name = $"[Attack Preview] {prefab.name}";
            previewModelOf = weapon;
            previewModel.transform.localPosition = weapon.Position;
            previewModel.transform.localRotation = Quaternion.Euler(weapon.Rotation);
            previewModel.transform.localScale = weapon.Scale;
            foreach (Collider col in previewModel.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);
            foreach (Rigidbody rb in previewModel.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(rb);
            foreach (MonoBehaviour mb in previewModel.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;
            foreach (Transform t in previewModel.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        }
        return previewModel.transform;
    }

    private void DestroyPreviewModel()
    {
        if (previewModel != null)
            Object.DestroyImmediate(previewModel);
        previewModel = null;
        previewModelOf = null;
    }

    private static VolumePose PoseOf(WeaponBlade v, Transform model) =>
        v.GetPose(model, WeaponBlade.FindMarker(model, v.startMarker), WeaponBlade.FindMarker(model, v.endMarker));

    // ------------------------------------------------------------------ simulation (trail, impact)
    /// <summary>Records the swing (volumes during the active phase) and finds where the Impact would happen.</summary>
    private void Simulate(AttackComponent c)
    {
        if (time < lastSimTime - 1e-4f)
        {
            trail.RemoveAll(sample => sample.t > time + 1e-4f); // scrubbed back
            if (impactShown && time < impactTime - 1e-4f)
                impactShown = false;
        }
        lastSimTime = time;

        float duration = Mathf.Max(0.001f, Duration(c));
        float f = Mathf.Clamp01(time / duration);
        float total = Mathf.Max(0.0001f, c.StartupFrames + c.ActiveFrames + c.RecoveryFrames);
        float startupEnd = c.StartupFrames / total, activeEnd = (c.StartupFrames + c.ActiveFrames) / total;
        bool active = f >= startupEnd && f <= activeEnd;
        Transform model = HeldModel();
        weapon.ActiveVolumes(c, activeVolumes);

        if (model != null && active && AttackPreviewUtility.Detection(weapon, c) == HitDetectionMode.WeaponBlade)
            for (int i = 0; i < activeVolumes.Count; i++)
                trail.Add(new TrailSample { t = time, volume = i, pose = PoseOf(activeVolumes[i], model) });

        ImpactSettings imp = c.impact;
        if (imp == null || !imp.enabled || imp.area == null || impactShown)
            return;
        GetFrame(SceneView.lastActiveSceneView, out Vector3 basePos, out Quaternion rot);
        Vector3 tip;
        bool estimated = model == null;
        if (!estimated)
        {
            WeaponBlade contact = weapon.FindVolume(imp.contactVolume);
            if (contact == null) contact = activeVolumes.Count > 0 ? activeVolumes[0] : weapon.Blade;
            tip = contact.LowestPoint(PoseOf(contact, model), AreaScale(c));
        }
        else
        {
            tip = basePos + rot * Vector3.forward * AttackPreviewUtility.ImpactDistance(weapon, c);
        }
        float ground = GroundY(tip, basePos.y);
        bool fire = false;
        switch (imp.trigger)
        {
            case ImpactSettings.Trigger.ActiveStart:
                fire = f >= startupEnd;
                break;
            case ImpactSettings.Trigger.ActiveEnd:
                fire = f >= activeEnd;
                break;
            default:
                fire = (!estimated && active && tip.y - ground <= imp.contactTolerance) ||
                       (imp.trigger == ImpactSettings.Trigger.GroundContactOrActiveEnd && f >= activeEnd);
                break;
        }
        if (!fire)
            return;
        impactShown = true;
        impactEstimated = estimated;
        impactTime = time;
        impactPoint = new Vector3(tip.x, ground, tip.z);
    }

    /// <summary>Ground height under <paramref name="p"/> (scene colliders on the ground layers, ignoring the character), else <paramref name="fallback"/>.</summary>
    private float GroundY(Vector3 p, float fallback)
    {
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 6f, CombatSettings.Instance.groundLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue, y = fallback;
        Transform root = character != null ? character.transform.root : null;
        foreach (RaycastHit h in hits)
        {
            if (root != null && h.collider.transform.IsChildOf(root)) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        return y;
    }

    // ------------------------------------------------------------------ window
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        weapon = (WeaponSO)EditorGUILayout.ObjectField(new GUIContent("Weapon", "The weapon whose attack is previewed."), weapon, typeof(WeaponSO), false);
        using (new EditorGUILayout.HorizontalScope())
        {
            character = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Character",
                "The character in the scene the animation is played on (the object with the Animator). Empty = the area is drawn at the " +
                "centre of the Scene view."), character, typeof(GameObject), true);
            if (GUILayout.Button(new GUIContent("Find", "Uses the selected object, the prefab being edited, or the player in the scene."), GUILayout.Width(44f)))
                character = FindCharacter();
        }
        if (EditorGUI.EndChangeCheck())
            StopSampling();

        if (weapon == null)
        {
            EditorGUILayout.HelpBox("Pick a weapon, or press ▶ on an attack in the weapon's inspector.", MessageType.Info);
            return;
        }

        // Input and chain hit
        var inputNames = new string[5];
        for (int i = 0; i < 5; i++)
        {
            AttackType t = (AttackType)i;
            inputNames[i] = AttackPreviewUtility.RawAction(weapon, t) != null && (t == AttackType.Normal || WeaponSO.IsUsable(AttackPreviewUtility.RawAction(weapon, t)))
                ? t.ToString() : t + " –";
        }
        EditorGUI.BeginChangeCheck();
        input = (AttackType)GUILayout.Toolbar((int)input, inputNames);
        AttackAction action = AttackPreviewUtility.RawAction(weapon, input);
        int count = 1 + (action?.GetVariationCount() ?? 0);
        if (count > 1)
        {
            var hits = new string[count];
            for (int i = 0; i < count; i++)
                hits[i] = $"Hit {i + 1}: {(i == 0 ? action.DisplayName : action.variations[i - 1]?.DisplayName)}";
            variation = EditorGUILayout.Popup(new GUIContent("Chain hit", "Which attack of the chain (pressing the same input again)."), Mathf.Min(variation, count - 1), hits);
        }
        else
        {
            variation = 0;
        }
        if (EditorGUI.EndChangeCheck())
        {
            time = 0f;
            StopSampling();
            Sample(Current());
        }

        AttackComponent c = Current();
        if (c == null)
        {
            EditorGUILayout.HelpBox($"The weapon has no {input} attack.", MessageType.Info);
            return;
        }

        float duration = Duration(c);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(c.DisplayName, EditorStyles.boldLabel);

        // Timeline + scrubber
        Rect bar = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
        AttackPreviewUtility.DrawTimeline(bar, c, duration, time / Mathf.Max(0.001f, duration));
        EditorGUI.BeginChangeCheck();
        float scrub = EditorGUILayout.Slider(new GUIContent("Time (s)", "Drag to scrub the attack."), time, 0f, duration);
        if (EditorGUI.EndChangeCheck())
        {
            time = scrub;
            playing = false;
            Sample(c);
            SceneView.RepaintAll();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "❚❚ Pause" : "▶ Play", GUILayout.Height(24f)))
            {
                if (!playing && time >= duration - 0.0001f) time = 0f;
                playing = !playing;
                lastTick = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("■ Stop", GUILayout.Height(24f)))
            {
                playing = false;
                time = 0f;
                StopSampling(); // restores the pose, removes the temporary weapon
                SceneView.RepaintAll();
            }
            loop = GUILayout.Toggle(loop, "Loop", "Button", GUILayout.Height(24f), GUILayout.Width(52f));
            int rate = System.Array.IndexOf(Rates, playbackRate);
            rate = EditorGUILayout.Popup(rate < 0 ? 3 : rate, RateNames, GUILayout.Width(60f));
            playbackRate = Rates[rate];
        }

        if (c.charge != null && c.charge.enabled)
            charge = EditorGUILayout.Slider(new GUIContent("Charge", "How charged the attack is: the area grows up to 'Area At Full Charge'."), charge, 0f, 1f);

        EditorGUI.BeginChangeCheck();
        sampleAnimation = EditorGUILayout.ToggleLeft(new GUIContent("Play the animation on the character",
            "Samples the attack's clip on the character (nothing is changed; Stop or closing the window restores the pose)."), sampleAnimation);
        showRange = EditorGUILayout.ToggleLeft(new GUIContent("Show the weapon's Max Range (blue circle)"), showRange);
        showWeapon = EditorGUILayout.ToggleLeft(new GUIContent("Put the weapon in the hand",
            "A temporary copy of the weapon's prefab in the character's hand, placed with the item's hand position / rotation / scale. " +
            "It is never saved and is removed when you press Stop or close this window."), showWeapon);
        showTrail = EditorGUILayout.ToggleLeft(new GUIContent("Show the swing trail", "Where the hit volumes passed during the active phase (Weapon Blade)."), showTrail);
        if (EditorGUI.EndChangeCheck())
        {
            if (!showWeapon) DestroyPreviewModel();
            ResetSimulation();
            Sample(c);
            SceneView.RepaintAll();
        }

        // What the user sees
        HitDetectionMode mode = AttackPreviewUtility.Detection(weapon, c);
        var info = new List<string>
        {
            $"Phase: {AttackPreviewUtility.PhaseAt(c, time / Mathf.Max(0.001f, duration))}   ·   {time:0.00}s / {duration:0.00}s",
            "Hits with: " + AttackPreviewUtility.DescribeDetection(weapon, c, mode, AreaScale(c)),
            $"Reach: {AttackPreviewUtility.Reach(weapon, c, AreaScale(c)):0.##} m" + (weapon.MaxRange > 0f ? $"   ·   weapon Max Range {weapon.MaxRange:0.##} m" : ""),
        };
        if (mode == HitDetectionMode.WeaponBlade || (c.impact != null && c.impact.enabled))
        {
            if (character != null && FindHand() == null)
                info.Add("No hand found: give the character a Weapon Controller with its Hand (or use a humanoid Animator).");
            else if (weapon.Prefab == null)
                info.Add("The weapon has no Prefab: the volumes are measured from the hand bone.");
        }
        if (c.impact != null && c.impact.enabled)
            info.Add(impactShown
                ? $"Impact at {impactTime:0.00}s{(impactEstimated ? " (estimated: no character / hand)" : "")} - {c.impact.area.Describe(AreaScale(c))} (orange)"
                : c.impact.trigger == ImpactSettings.Trigger.GroundContact ? "Impact: the weapon has not touched the ground yet (Ground Contact only)" : "Impact: not yet");
        if (c.AnimationClip == null)
            info.Add($"No animation clip: the game triggers '{AttackAnimationHandler.GetFallbackAttackTrigger(input)}' on the Animator, which cannot be previewed here.");
        else if (character == null)
            info.Add("No character: the area is drawn at the centre of the Scene view. Assign a character to see the animation.");
        else if (character.GetComponentInChildren<Animator>(true) == null)
            info.Add("The character has no Animator, so the clip cannot be played on it.");
        EditorGUILayout.HelpBox(string.Join("\n", info), MessageType.None);

        Rect diagram = GUILayoutUtility.GetRect(10f, 200f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float f = time / Mathf.Max(0.001f, duration);
        HitAreaDiagram.Draw(diagram, weapon, c, AreaScale(c), AttackPreviewUtility.PhaseColor(c, f), AttackPreviewUtility.LungeProgress(c, f), showRange);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Frame in Scene"))
                FrameInScene(c);
            if (GUILayout.Button("Select Weapon"))
                Selection.activeObject = weapon;
        }
    }

    private void FrameInScene(AttackComponent c)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
            return;
        GetFrame(view, out Vector3 pos, out _);
        float reach = Mathf.Max(2f, AttackPreviewUtility.Reach(weapon, c, AreaScale(c)), showRange ? weapon.MaxRange : 0f);
        view.Frame(new Bounds(pos + Vector3.up, Vector3.one * reach * 2.2f), false);
    }

    private void GetFrame(SceneView view, out Vector3 position, out Quaternion rotation)
    {
        if (character != null)
        {
            Transform root = character.transform;
            CombatEntity entity = character.GetComponentInParent<CombatEntity>();
            if (entity != null) root = entity.transform;
            position = root.position;
            rotation = ResolvedShape.FlattenRotation(root.rotation);
            return;
        }
        position = view != null ? view.pivot : Vector3.zero;
        rotation = Quaternion.identity;
    }

    // ------------------------------------------------------------------ scene view
    private void OnSceneGUI(SceneView view)
    {
        if (weapon == null || Event.current.type != EventType.Repaint)
            return;
        AttackComponent c = Current();
        if (c == null)
            return;

        float duration = Mathf.Max(0.001f, Duration(c));
        float f = Mathf.Clamp01(time / duration);
        GetFrame(view, out Vector3 basePos, out Quaternion rot);
        Vector3 lunge = rot * c.ForwardMovement * AttackPreviewUtility.LungeProgress(c, f);
        Vector3 pos = basePos + lunge;
        Color color = AttackPreviewUtility.PhaseColor(c, f);
        CompareFunction oldZ = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        // Weapon range
        if (showRange && weapon.MaxRange > 0f)
        {
            Handles.color = new Color(0.35f, 0.65f, 1f, 0.8f);
            Handles.DrawWireDisc(basePos + Vector3.up * 0.03f, Vector3.up, weapon.MaxRange);
            if (weapon.MinRange > 0f)
            {
                Handles.color = new Color(0.35f, 0.65f, 1f, 0.35f);
                Handles.DrawWireDisc(basePos + Vector3.up * 0.03f, Vector3.up, weapon.MinRange);
            }
            Handles.Label(basePos + rot * Vector3.right * weapon.MaxRange + Vector3.up * 0.2f, $"range {weapon.MaxRange:0.#} m", EditorStyles.miniBoldLabel);
        }

        // Character marker and facing
        Handles.color = new Color(1f, 1f, 1f, 0.8f);
        Handles.DrawWireDisc(pos + Vector3.up * 0.03f, Vector3.up, 0.4f);
        Handles.DrawAAPolyLine(3f, pos + Vector3.up * 0.05f, pos + Vector3.up * 0.05f + rot * Vector3.forward * 0.9f);
        if (lunge.sqrMagnitude > 0.0001f)
        {
            Handles.color = new Color(0.4f, 1f, 0.5f, 0.8f);
            Handles.DrawDottedLine(basePos + Vector3.up * 0.05f, pos + Vector3.up * 0.05f, 3f);
        }

        HitDetectionMode mode = AttackPreviewUtility.Detection(weapon, c);
        if (mode == HitDetectionMode.HitShape && c.hitShape != null)
        {
            ResolvedShape shape = ResolvedShape.Resolve(c.hitShape, pos, rot, AreaScale(c));
            shape.GetOutline(outer, inner, 48, 0.05f);
            Color fill = color;
            fill.a = AttackPreviewUtility.PhaseAt(c, f) == "Active (hits)" ? 0.22f : 0.08f;
            if (c.hitShape.type != HitShapeType.Ring)
                FillLoop(outer, fill);
            Handles.color = color;
            Handles.DrawAAPolyLine(4f, outer.ToArray());
            if (inner.Count > 1)
                Handles.DrawAAPolyLine(3f, inner.ToArray());
            // Top of the vertical band, so the height of the area can be read
            if (c.hitShape.type != HitShapeType.Sphere)
            {
                Vector3 up = Vector3.up * (c.hitShape.baseOffset + c.hitShape.height);
                Vector3 down = Vector3.up * c.hitShape.baseOffset;
                Handles.color = new Color(color.r, color.g, color.b, 0.35f);
                var top = new Vector3[outer.Count];
                for (int i = 0; i < outer.Count; i++) top[i] = outer[i] + up;
                Handles.DrawAAPolyLine(1.5f, top);
                int step = Mathf.Max(1, outer.Count / 8);
                for (int i = 0; i < outer.Count; i += step)
                    Handles.DrawLine(outer[i] + down, outer[i] + up);
            }
            else
            {
                Handles.color = color;
                Handles.DrawWireDisc(shape.origin, Vector3.right, c.hitShape.radius * AreaScale(c));
                Handles.DrawWireDisc(shape.origin, Vector3.forward, c.hitShape.radius * AreaScale(c));
            }
        }
        else if (mode == HitDetectionMode.WeaponBlade)
        {
            DrawVolumes(c, color, f);
        }
        else if (mode == HitDetectionMode.WeaponCast && weapon.attackCast != null)
        {
            Transform hand = null;
            if (character != null)
            {
                WeaponController wc = character.GetComponentInParent<WeaponController>();
                if (wc == null) wc = character.transform.root.GetComponentInChildren<WeaponController>(true);
                if (wc != null) hand = wc.HandTransform;
            }
            Vector3 origin = (hand != null ? hand.position : pos + Vector3.up * 1.2f + rot * Vector3.forward * 0.4f) + weapon.attackCast.customOrigin;
            Vector3 forward = hand != null ? hand.forward : rot * Vector3.forward;
            DrawCast(weapon.attackCast, origin, forward, color);
            Handles.Label(origin + Vector3.up * 0.3f, hand != null ? "Weapon Cast (hand)" : "Weapon Cast (hand not found: approximate)", EditorStyles.miniBoldLabel);
        }

        // Impact (ground slam) area where the weapon reached the ground
        if (impactShown && c.impact != null && c.impact.enabled && c.impact.area != null)
        {
            ResolvedShape shape = ResolvedShape.Resolve(c.impact.area, impactPoint, rot, AreaScale(c));
            shape.GetOutline(outer, inner, 48, 0.06f);
            var orange = new Color(1f, 0.6f, 0.15f, 1f);
            float since = time - impactTime;
            if (c.impact.area.type != HitShapeType.Ring)
                FillLoop(outer, new Color(orange.r, orange.g, orange.b, since < 0.3f ? 0.3f : 0.1f));
            Handles.color = orange;
            Handles.DrawAAPolyLine(4f, outer.ToArray());
            if (inner.Count > 1) Handles.DrawAAPolyLine(3f, inner.ToArray());
            Handles.DrawWireDisc(impactPoint + Vector3.up * 0.05f, Vector3.up, 0.15f);
            Handles.Label(impactPoint + Vector3.up * 0.4f, impactEstimated ? "Impact (estimated)" : "Impact", EditorStyles.whiteBoldLabel);
        }

        var style = new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleCenter };
        Handles.Label(pos + Vector3.up * 2.4f, $"{c.DisplayName}\n{AttackPreviewUtility.PhaseAt(c, f)}  {time:0.00}s / {duration:0.00}s", style);
        Handles.zTest = oldZ;
    }

    /// <summary>The weapon's hit volumes on the held model (active ones in the phase colour, the others faint) and the swing trail.</summary>
    private void DrawVolumes(AttackComponent c, Color color, float f)
    {
        Transform model = HeldModel(create: false); // never create objects while the Scene view repaints
        if (model == null)
            return;
        float scale = AreaScale(c);
        weapon.ActiveVolumes(c, activeVolumes);
        IReadOnlyList<WeaponBlade> all = weapon.HitVolumes;
        for (int i = 0; i < all.Count; i++)
        {
            WeaponBlade v = all[i];
            if (v == null) continue;
            bool used = activeVolumes.Contains(v);
            Color col = used ? color : new Color(0.7f, 0.7f, 0.7f, 0.35f);
            VolumePose pose = PoseOf(v, model);
            DrawVolume(v, pose, scale, col, used ? 3f : 1.5f);
            if (used)
                Handles.Label((pose.a + pose.b) * 0.5f + Vector3.up * 0.15f, v.name, EditorStyles.miniBoldLabel);
        }

        if (!showTrail || trail.Count < 2)
            return;
        // One ribbon per volume: the path of its two ends (capsules) or its centre (spheres, boxes).
        for (int v = 0; v < activeVolumes.Count; v++)
        {
            Vector3? lastA = null, lastB = null;
            int n = 0;
            for (int i = 0; i < trail.Count; i++)
            {
                TrailSample sample = trail[i];
                if (sample.volume != v) continue;
                Handles.color = new Color(1f, 0.45f, 0.2f, 0.55f);
                if (lastA.HasValue)
                {
                    Handles.DrawAAPolyLine(2f, lastA.Value, sample.pose.a);
                    if (activeVolumes[v].shape == WeaponBlade.VolumeShape.Capsule)
                        Handles.DrawAAPolyLine(2f, lastB.Value, sample.pose.b);
                }
                if (activeVolumes[v].shape == WeaponBlade.VolumeShape.Capsule && n++ % 3 == 0)
                {
                    Handles.color = new Color(1f, 0.45f, 0.2f, 0.2f);
                    Handles.DrawLine(sample.pose.a, sample.pose.b);
                }
                lastA = sample.pose.a;
                lastB = sample.pose.b;
            }
        }
    }

    private static void DrawVolume(WeaponBlade v, in VolumePose pose, float scale, Color color, float thickness)
    {
        Handles.color = color;
        switch (v.shape)
        {
            case WeaponBlade.VolumeShape.Sphere:
                {
                    float r = v.radius * scale;
                    Handles.DrawWireDisc(pose.a, Vector3.up, r);
                    Handles.DrawWireDisc(pose.a, Vector3.right, r);
                    Handles.DrawWireDisc(pose.a, Vector3.forward, r);
                    break;
                }
            case WeaponBlade.VolumeShape.Box:
                {
                    Matrix4x4 old = Handles.matrix;
                    Handles.matrix = Matrix4x4.TRS(pose.a, pose.rotation, Vector3.one);
                    Handles.DrawWireCube(Vector3.zero, v.HalfExtents(scale) * 2f);
                    Handles.matrix = old;
                    break;
                }
            default:
                {
                    float r = v.radius * scale;
                    Vector3 axis = pose.b - pose.a;
                    Vector3 dir = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
                    Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized * r;
                    Vector3 side2 = Vector3.Cross(dir, side).normalized * r;
                    Handles.DrawWireDisc(pose.a, dir, r);
                    Handles.DrawWireDisc(pose.b, dir, r);
                    Handles.DrawAAPolyLine(thickness, pose.a + side, pose.b + side);
                    Handles.DrawAAPolyLine(thickness, pose.a - side, pose.b - side);
                    Handles.DrawAAPolyLine(thickness, pose.a + side2, pose.b + side2);
                    Handles.DrawAAPolyLine(thickness, pose.a - side2, pose.b - side2);
                    break;
                }
        }
    }

    private static void FillLoop(List<Vector3> loop, Color color)
    {
        if (loop.Count < 3)
            return;
        Vector3 centre = Vector3.zero;
        for (int i = 0; i < loop.Count; i++) centre += loop[i];
        centre /= loop.Count;
        Handles.color = color;
        for (int i = 0; i < loop.Count - 1; i++)
            Handles.DrawAAConvexPolygon(centre, loop[i], loop[i + 1]);
    }

    private static void DrawCast(AttackCast cast, Vector3 origin, Vector3 forward, Color color)
    {
        Handles.color = color;
        switch (cast.castType)
        {
            case CastBase.CastType.Sphere:
                Handles.DrawWireDisc(origin, Vector3.up, cast.castSize);
                Handles.DrawWireDisc(origin, Vector3.right, cast.castSize);
                Handles.DrawWireDisc(origin, Vector3.forward, cast.castSize);
                break;
            case CastBase.CastType.Box:
                Handles.DrawWireCube(origin, cast.boxSize);
                break;
            case CastBase.CastType.Capsule:
                Handles.DrawWireDisc(origin, forward, cast.castSize * 0.5f);
                Handles.DrawWireDisc(origin + forward * cast.castSize, forward, cast.castSize * 0.5f);
                Handles.DrawLine(origin, origin + forward * cast.castSize);
                break;
            case CastBase.CastType.Ray:
                Handles.DrawAAPolyLine(3f, origin, origin + forward * cast.castSize);
                break;
        }
    }
}

/// <summary>Shared helpers of the weapon inspector and the attack preview.</summary>
public static class AttackPreviewUtility
{
    /// <summary>The attack of an input as stored (even an unused optional one).</summary>
    public static AttackAction RawAction(WeaponSO weapon, AttackType input)
    {
        if (weapon == null) return null;
        switch (input)
        {
            case AttackType.Light: return weapon.LightAction;
            case AttackType.Heavy: return weapon.HeavyAction;
            case AttackType.Special: return weapon.SpecialAction;
            case AttackType.Alternate: return weapon.AlternateAction;
            default: return weapon.NormalAction;
        }
    }

    /// <summary>The attack (variation 0) or one of its chain variations (1..n).</summary>
    public static AttackComponent Resolve(WeaponSO weapon, AttackType input, int variation)
    {
        AttackAction a = RawAction(weapon, input);
        if (a == null) return null;
        if (variation <= 0 || a.variations == null || variation > a.variations.Count)
            return a;
        return (AttackComponent)a.variations[variation - 1] ?? a;
    }

    /// <summary>Seconds the attack lasts in game (its timings, its speed and the weapon's attack speed).</summary>
    public static float Duration(WeaponSO weapon, AttackComponent c)
    {
        if (c == null) return 0f;
        float speed = weapon != null ? weapon.AttackSpeedMultiplier : 1f;
        return Mathf.Max(0.01f, c.GetTotalDuration() / Mathf.Max(0.01f, speed));
    }

    public static HitDetectionMode Detection(WeaponSO weapon, AttackComponent c)
    {
        if (c.hitDetection != HitDetectionMode.Auto) return c.hitDetection;
        return weapon != null && weapon.HasUsableAttackCast ? HitDetectionMode.WeaponCast : HitDetectionMode.HitShape;
    }

    public static string DescribeDetection(WeaponSO weapon, AttackComponent c, HitDetectionMode mode, float areaScale)
    {
        switch (mode)
        {
            case HitDetectionMode.HitShape:
                return c.hitShape != null ? $"Hit Shape - {c.hitShape.Describe(areaScale)}" : "Hit Shape (not set)";
            case HitDetectionMode.WeaponCast:
                return weapon != null && weapon.attackCast != null ? $"Weapon Cast - {weapon.attackCast.castType} {weapon.attackCast.castSize:0.#} m at the hand" : "Weapon Cast (not set)";
            case HitDetectionMode.WeaponBlade:
                {
                    if (weapon == null) return "Weapon Blade";
                    var used = new List<WeaponBlade>();
                    weapon.ActiveVolumes(c, used);
                    var names = new List<string>();
                    foreach (WeaponBlade v in used) names.Add(v.Describe());
                    return $"Weapon Blade - {(names.Count > 0 ? string.Join(", ", names) : "no volume")}, follows the animation";
                }
            default:
                return "nothing directly (only its behaviours: projectiles, abilities...)";
        }
    }

    /// <summary>How far from the attacker the attack can hit (metres), the lunge included.</summary>
    public static float Reach(WeaponSO weapon, AttackComponent c, float areaScale = 1f)
    {
        if (c == null) return 0f;
        Vector3 m = c.ForwardMovement;
        float lunge = new Vector2(m.x, m.z).magnitude;
        switch (Detection(weapon, c))
        {
            case HitDetectionMode.HitShape:
                return (c.hitShape != null ? c.hitShape.Reach(areaScale) : 0f) + lunge;
            case HitDetectionMode.WeaponCast:
                AttackCast cast = weapon.attackCast;
                float size = cast.castType == CastBase.CastType.Box ? cast.boxSize.magnitude * 0.5f : cast.castSize;
                return 0.5f + new Vector2(cast.customOrigin.x, cast.customOrigin.z).magnitude + size + lunge;
            case HitDetectionMode.WeaponBlade:
                {
                    var used = new List<WeaponBlade>();
                    weapon.ActiveVolumes(c, used);
                    float best = 0f;
                    foreach (WeaponBlade v in used) best = Mathf.Max(best, v.Reach(areaScale));
                    return ArmReach + best + lunge;
                }
            default:
                return 0f;
        }
    }

    /// <summary>Approximate distance from the attacker's centre to the hand at full extension.</summary>
    public const float ArmReach = 0.6f;

    /// <summary>Farthest any of the weapon's hit volumes reaches from the grip.</summary>
    public static float BladeLength(WeaponSO weapon)
    {
        if (weapon == null) return 0f;
        float best = 0f;
        foreach (WeaponBlade v in weapon.HitVolumes)
            if (v != null) best = Mathf.Max(best, v.Reach(1f));
        return best;
    }

    /// <summary>Where an impact is expected in front of the attacker (for drawings without an animation).</summary>
    public static float ImpactDistance(WeaponSO weapon, AttackComponent c)
    {
        Vector3 m = c.ForwardMovement;
        return ArmReach + Mathf.Max(0.6f, BladeLength(weapon)) + new Vector2(m.x, m.z).magnitude;
    }

    /// <summary>Farthest point the impact area can reach from the attacker.</summary>
    public static float ImpactReach(WeaponSO weapon, AttackComponent c, float areaScale)
    {
        if (c?.impact == null || !c.impact.enabled || c.impact.area == null) return 0f;
        return ImpactDistance(weapon, c) + c.impact.area.Reach(areaScale);
    }

    /// <summary>The largest reach of all the weapon's attacks and chain hits (charged attacks at full charge).</summary>
    public static float MaxReach(WeaponSO weapon)
    {
        float max = 0f;
        foreach (AttackType t in System.Enum.GetValues(typeof(AttackType)))
        {
            AttackAction a = weapon.GetAction(t);
            if (a == null) continue;
            max = Mathf.Max(max, Reach(weapon, a, FullArea(a)), ImpactReach(weapon, a, FullArea(a)));
            if (a.variations != null)
                foreach (AttackVariation v in a.variations)
                    if (v != null) max = Mathf.Max(max, Reach(weapon, v, FullArea(v)), ImpactReach(weapon, v, FullArea(v)));
        }
        return max;
    }

    private static float FullArea(AttackComponent c) => c.charge != null && c.charge.enabled ? c.charge.areaAtFullCharge : 1f;

    /// <summary>Phase name at a fraction (0-1) of the attack.</summary>
    public static string PhaseAt(AttackComponent c, float fraction)
    {
        float total = Mathf.Max(0.0001f, c.StartupFrames + c.ActiveFrames + c.RecoveryFrames);
        float t = Mathf.Clamp01(fraction) * total;
        if (t < c.StartupFrames) return "Startup (wind-up)";
        if (t <= c.StartupFrames + c.ActiveFrames) return "Active (hits)";
        return "Recovery";
    }

    public static Color PhaseColor(AttackComponent c, float fraction)
    {
        switch (PhaseAt(c, fraction))
        {
            case "Startup (wind-up)": return new Color(1f, 0.85f, 0.2f, 1f);
            case "Active (hits)": return new Color(1f, 0.25f, 0.2f, 1f);
            default: return new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }

    /// <summary>How far along its Forward Movement the attacker is (it moves during startup + active).</summary>
    public static float LungeProgress(AttackComponent c, float fraction)
    {
        float total = Mathf.Max(0.0001f, c.StartupFrames + c.ActiveFrames + c.RecoveryFrames);
        float moving = Mathf.Max(0.0001f, c.StartupFrames + c.ActiveFrames);
        return Mathf.Clamp01(Mathf.Clamp01(fraction) * total / moving);
    }

    /// <summary>Startup / active / recovery bar with the seconds of each phase, the cancel point and a cursor.</summary>
    public static void DrawTimeline(Rect rect, AttackComponent c, float duration, float cursor = -1f)
    {
        float total = c.StartupFrames + c.ActiveFrames + c.RecoveryFrames;
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
        if (total <= 0f)
        {
            GUI.Label(rect, "no timing", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        float scale = duration / total;
        float x = rect.x;
        (float len, Color col, string name)[] parts =
        {
            (c.StartupFrames, new Color(0.95f, 0.78f, 0.2f, 0.85f), "startup"),
            (c.ActiveFrames, new Color(0.9f, 0.25f, 0.2f, 0.9f), "hits"),
            (c.RecoveryFrames, new Color(0.5f, 0.5f, 0.55f, 0.85f), "recovery"),
        };
        var label = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        label.normal.textColor = Color.black;
        foreach (var p in parts)
        {
            float w = rect.width * p.len / total;
            if (w <= 0f) continue;
            var r = new Rect(x, rect.y, w, rect.height);
            EditorGUI.DrawRect(r, p.col);
            GUI.Label(r, new GUIContent(w > 70f ? $"{p.name} {p.len * scale:0.##}s" : $"{p.len * scale:0.##}", $"{p.name}: {p.len * scale:0.###} s"), label);
            x += w;
        }
        // Cancel point: from here a buffered next attack may start
        float cancel = (c.StartupFrames + c.ActiveFrames + c.RecoveryFrames * c.cancelPoint) / total;
        if (c.cancelPoint < 0.999f)
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * cancel - 1f, rect.y, 2f, rect.height), new Color(0.2f, 0.9f, 1f, 1f));
        if (cursor >= 0f)
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * Mathf.Clamp01(cursor) - 1f, rect.y - 2f, 2f, rect.height + 4f), Color.white);
    }
}

/// <summary>Top-down drawing (in the inspector) of the area an attack hits, the lunge, and the weapon's range.</summary>
public static class HitAreaDiagram
{
    private static readonly List<Vector3> outer = new List<Vector3>(64);
    private static readonly List<Vector3> inner = new List<Vector3>(64);

    public static void Draw(Rect rect, WeaponSO weapon, AttackComponent c, float areaScale, Color color, float lungeProgress, bool showRange = true)
    {
        if (Event.current.type != EventType.Repaint || c == null)
            return;
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.11f, 0.13f, 1f));

        HitDetectionMode mode = AttackPreviewUtility.Detection(weapon, c);
        Vector3 lunge = c.ForwardMovement;
        lunge.y = 0f;
        outer.Clear();
        inner.Clear();
        if (mode == HitDetectionMode.HitShape && c.hitShape != null)
            ResolvedShape.Resolve(c.hitShape, Vector3.zero, Quaternion.identity, areaScale).GetOutline(outer, inner, 48, 0f);

        // Scale: everything (shape at the end of the lunge, range) fits
        float extent = 1.5f;
        foreach (Vector3 p in outer) extent = Mathf.Max(extent, (p + lunge).magnitude, p.magnitude);
        extent = Mathf.Max(extent, lunge.magnitude + 0.5f);
        if ((mode == HitDetectionMode.WeaponCast && weapon.attackCast != null) || mode == HitDetectionMode.WeaponBlade)
            extent = Mathf.Max(extent, AttackPreviewUtility.Reach(weapon, c, areaScale));
        extent = Mathf.Max(extent, AttackPreviewUtility.ImpactReach(weapon, c, areaScale));
        if (showRange && weapon != null) extent = Mathf.Max(extent, weapon.MaxRange);
        extent *= 1.12f;
        float ppm = Mathf.Min(rect.width, rect.height) * 0.5f / extent;
        var centre = new Vector2(rect.center.x, rect.center.y);
        System.Func<Vector3, Vector3> gui = p => new Vector3(centre.x + p.x * ppm, centre.y - p.z * ppm, 0f);

        // Distance rings (1 m, or 2 / 5 m when far)
        float step = extent > 12f ? 5f : extent > 6f ? 2f : 1f;
        Handles.color = new Color(1f, 1f, 1f, 0.08f);
        for (float m = step; m < extent; m += step)
            Handles.DrawWireDisc(gui(Vector3.zero), Vector3.forward, m * ppm);
        Handles.DrawLine(gui(new Vector3(-extent, 0f, 0f)), gui(new Vector3(extent, 0f, 0f)));
        Handles.DrawLine(gui(new Vector3(0f, 0f, -extent)), gui(new Vector3(0f, 0f, extent)));

        if (showRange && weapon != null && weapon.MaxRange > 0f)
        {
            Handles.color = new Color(0.35f, 0.65f, 1f, 0.7f);
            Handles.DrawWireDisc(gui(Vector3.zero), Vector3.forward, weapon.MaxRange * ppm);
        }

        Vector3 now = lunge * lungeProgress;
        if (outer.Count > 1)
        {
            if (lunge.sqrMagnitude > 0.0001f && lungeProgress < 0.999f)
            {
                // Where the area ends up after the lunge
                Handles.color = new Color(color.r, color.g, color.b, 0.25f);
                Handles.DrawAAPolyLine(1.5f, Map(outer, lunge, gui));
            }
            Vector3[] pts = Map(outer, now, gui);
            if (c.hitShape.type != HitShapeType.Ring)
            {
                Vector3 mid = Vector3.zero;
                foreach (Vector3 p in pts) mid += p;
                mid /= pts.Length;
                Handles.color = new Color(color.r, color.g, color.b, 0.18f);
                for (int i = 0; i < pts.Length - 1; i++)
                    Handles.DrawAAConvexPolygon(mid, pts[i], pts[i + 1]);
            }
            Handles.color = color;
            Handles.DrawAAPolyLine(2.5f, pts);
            if (inner.Count > 1)
                Handles.DrawAAPolyLine(2f, Map(inner, now, gui));
        }
        else if (mode == HitDetectionMode.WeaponBlade && weapon != null)
        {
            // The blade sweeps a band in front of the attacker (the exact path comes from the animation: ▶ Preview).
            float r0 = AttackPreviewUtility.ArmReach + Mathf.Min(Mathf.Abs(weapon.Blade.start), Mathf.Abs(weapon.Blade.end));
            float r1 = AttackPreviewUtility.ArmReach + AttackPreviewUtility.BladeLength(weapon) + weapon.Blade.radius * areaScale;
            var outerArc = new List<Vector3>();
            var innerArc = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float ang = Mathf.Lerp(-75f, 75f, i / 24f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                outerArc.Add(gui(now + dir * r1));
                innerArc.Add(gui(now + dir * r0));
            }
            Handles.color = new Color(color.r, color.g, color.b, 0.12f);
            for (int i = 0; i < 24; i++)
                Handles.DrawAAConvexPolygon(innerArc[i], outerArc[i], outerArc[i + 1], innerArc[i + 1]);
            Handles.color = color;
            Handles.DrawAAPolyLine(2f, outerArc.ToArray());
            Handles.DrawAAPolyLine(1.5f, innerArc.ToArray());
        }
        else if (mode == HitDetectionMode.WeaponCast && weapon.attackCast != null)
        {
            Handles.color = color;
            Vector3 hand = now + new Vector3(0.3f, 0f, 0.4f) + weapon.attackCast.customOrigin;
            float r = weapon.attackCast.castType == CastBase.CastType.Box ? weapon.attackCast.boxSize.magnitude * 0.5f : weapon.attackCast.castSize;
            if (weapon.attackCast.castType == CastBase.CastType.Ray)
                Handles.DrawAAPolyLine(2.5f, gui(hand), gui(hand + Vector3.forward * r));
            else
                Handles.DrawWireDisc(gui(hand), Vector3.forward, r * ppm);
        }

        // Impact (ground slam) area at the expected impact point
        if (c.impact != null && c.impact.enabled && c.impact.area != null)
        {
            var impactPoint = new Vector3(0f, 0f, AttackPreviewUtility.ImpactDistance(weapon, c));
            var impactOuter = new List<Vector3>();
            var impactInner = new List<Vector3>();
            ResolvedShape.Resolve(c.impact.area, impactPoint, Quaternion.identity, areaScale).GetOutline(impactOuter, impactInner, 48, 0f);
            Handles.color = new Color(1f, 0.6f, 0.15f, 0.9f);
            if (impactOuter.Count > 1) Handles.DrawDottedLines(Segments(impactOuter, gui), 3f);
            if (impactInner.Count > 1) Handles.DrawDottedLines(Segments(impactInner, gui), 3f);
            Handles.DrawWireDisc(gui(impactPoint), Vector3.forward, 3f);
        }

        // Attacker and facing
        if (lunge.sqrMagnitude > 0.0001f)
        {
            Handles.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Handles.DrawDottedLine(gui(Vector3.zero), gui(lunge), 2f);
        }
        Handles.color = Color.white;
        Handles.DrawWireDisc(gui(now), Vector3.forward, Mathf.Max(3f, 0.4f * ppm));
        Handles.DrawAAPolyLine(2f, gui(now), gui(now + Vector3.forward * 0.8f));

        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f), $"Top view · forward ↑ · rings every {step:0} m", style);
        string bottom = mode == HitDetectionMode.None ? "No direct hit: only the behaviours (projectiles, abilities)"
            : $"{AttackPreviewUtility.DescribeDetection(weapon, c, mode, areaScale)} · reach {AttackPreviewUtility.Reach(weapon, c, areaScale):0.#} m";
        if (c.impact != null && c.impact.enabled)
            bottom += " · impact (orange, estimated)";
        if (showRange && weapon != null && weapon.MaxRange > 0f) bottom += $" · range {weapon.MaxRange:0.#} m (blue)";
        GUI.Label(new Rect(rect.x + 4f, rect.yMax - 16f, rect.width - 8f, 16f), bottom, style);
    }

    /// <summary>Pairs of points (a, b, b, c...) for dotted outlines.</summary>
    private static Vector3[] Segments(List<Vector3> pts, System.Func<Vector3, Vector3> gui)
    {
        var a = new Vector3[(pts.Count - 1) * 2];
        for (int i = 0; i < pts.Count - 1; i++)
        {
            a[i * 2] = gui(pts[i]);
            a[i * 2 + 1] = gui(pts[i + 1]);
        }
        return a;
    }

    private static Vector3[] Map(List<Vector3> pts, Vector3 offset, System.Func<Vector3, Vector3> gui)
    {
        var a = new Vector3[pts.Count];
        for (int i = 0; i < pts.Count; i++) a[i] = gui(pts[i] + offset);
        return a;
    }
}
#endif
