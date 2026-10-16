using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How an option's prefab is put on the character.</summary>
public enum AppearanceAttachMode
{
    /// <summary>Skinned meshes are bound to the character's skeleton; anything else rides on the bone.</summary>
    Auto,
    /// <summary>On the bone (Attach To), with the position / rotation / scale.</summary>
    Rigid,
    /// <summary>Its skinned meshes use the character's bones (same names, or both Humanoid): it moves with the body.</summary>
    Skinned,
    /// <summary>Placed on the bone, then wrapped around the body (any mesh): it bends with the body.</summary>
    FitToBody,
}

/// <summary>The colour an option takes.</summary>
public enum AppearanceTint
{
    /// <summary>Hair colour for hair, beards and eyebrows; none for the rest.</summary>
    Auto,
    None,
    Skin,
    Hair,
    Eyes,
    Shirt,
    Trousers,
}

/// <summary>Whether your own options of a part come after the placeholder choices or replace them.</summary>
public enum PlaceholderStyleMode
{
    /// <summary>Kept while the placeholder body is used (your options are extra choices), else replaced.</summary>
    Auto,
    /// <summary>Your options are always extra choices after the placeholder ones.</summary>
    Always,
    /// <summary>A part with options of its own shows only those.</summary>
    Never,
}

/// <summary>Which body types may use an appearance option.</summary>
public enum AppearanceBodyFilter
{
    Any,
    MaleOnly,
    FemaleOnly,
}

/// <summary>A blend shape set to a weight.</summary>
[Serializable]
public class AppearanceBlendShape
{
    public SkinnedMeshRenderer renderer;
    [Tooltip("Blend shape name on the renderer's mesh.")]
    public string shape;
    [Range(0f, 100f)] public float weight = 100f;
}

/// <summary>A blend shape driven by a 0-1 face slider (nose size, eye size).</summary>
[Serializable]
public class AppearanceBlendSlider
{
    public SkinnedMeshRenderer renderer;
    public string shape;
    [Tooltip("Weight at slider 0.")]
    [Range(0f, 100f)] public float atZero = 0f;
    [Tooltip("Weight at slider 1.")]
    [Range(0f, 100f)] public float atOne = 100f;
}

/// <summary>Where a colour goes: a renderer's material colour (all materials or one).</summary>
[Serializable]
public class AppearanceColorTarget
{
    public Renderer renderer;
    [Tooltip("-1 = every material of the renderer.")]
    public int materialIndex = -1;
    [Tooltip("Colour property. Empty = _BaseColor (URP / HDRP) and _Color (Built-in).")]
    public string property = "";
}

/// <summary>The renderers of one part of the body (hidden while armour covers it).</summary>
[Serializable]
public class AppearanceRegion
{
    public BodyRegion region = BodyRegion.Torso;
    public Renderer[] renderers = new Renderer[0];
}

/// <summary>
/// One choice of a part (a hairstyle, a beard, an eye or nose shape): objects of the model shown while it is picked,
/// a prefab attached to a bone, and / or blend shapes.
/// </summary>
[Serializable]
public class AppearanceOption
{
    public string name = "Style";
    [Tooltip("Body types that can pick it (beards: Male Only, for example).")]
    public AppearanceBodyFilter bodyTypes = AppearanceBodyFilter.Any;
    [Tooltip("Objects of the model shown while this option is picked (hidden while another one is).")]
    public GameObject[] objects = new GameObject[0];
    [Tooltip("A prefab put on the character while this option is picked: a hair mesh, a body part, a whole body model.")]
    public GameObject prefab;
    [Tooltip("Auto: skinned meshes made for the character's skeleton (same bone names, or both Humanoid) are bound to it and " +
             "move with the body; other meshes go on the bone. Rigid: on the bone. Fit To Body: on the bone, then wrapped " +
             "around the body.")]
    public AppearanceAttachMode attachMode = AppearanceAttachMode.Auto;
    [Tooltip("The bone a rigid (or fitted) prefab goes on.")]
    public HumanBodyBones attachTo = HumanBodyBones.Head;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
    [Tooltip("Fit To Body: distance from the body's surface (metres).")]
    public float fitOffset = 0.004f;
    [Tooltip("Blend shapes set while this option is picked (set back to 0 when another one is).")]
    public AppearanceBlendShape[] blendShapes = new AppearanceBlendShape[0];
    [Tooltip("Parts of the body hidden while this option is shown: a new torso hides the Torso, a hood the Hair. " +
             "Body Models: None = the whole placeholder body is replaced.")]
    public BodyRegion hides = BodyRegion.None;
    [Tooltip("The colour it takes. Auto = the hair colour for hair, beards and eyebrows, none for the rest.")]
    public AppearanceTint tint = AppearanceTint.Auto;

    public bool Allows(CharacterBodyType body) =>
        bodyTypes == AppearanceBodyFilter.Any ||
        (bodyTypes == AppearanceBodyFilter.MaleOnly && body == CharacterBodyType.Male) ||
        (bodyTypes == AppearanceBodyFilter.FemaleOnly && body == CharacterBodyType.Female);
}

/// <summary>
/// What a character looks like, applied to its model: male / female body, skin colour, hairstyle and colour, beard,
/// eye shape and colour, nose shape and two face sliders (<see cref="CharacterAppearanceData"/>).
/// <para><b>Your own model:</b> list the options of each part — objects of the model to show (hair meshes, beards),
/// prefabs to attach to a bone, and / or blend shapes — plus the male / female body objects or blend shapes and the
/// renderers that take the skin, hair and eye colours.</para>
/// <para><b>Nothing set up yet:</b> each part without options gets simple placeholder shapes on the head (Placeholder
/// Parts), so every choice on the creation screen is visible on any character at once. Replace them part by part
/// with real options when the art is ready.</para>
/// Put it on the player's model (the object with the Animator); it is added there automatically when missing.
/// </summary>
[DisallowMultipleComponent]
public class CharacterAppearance : MonoBehaviour
{
    [Header("Body Type")]
    [Tooltip("Objects shown only for a male body (a male mesh, male clothes).")]
    public GameObject[] maleObjects = new GameObject[0];
    [Tooltip("Objects shown only for a female body (a female mesh, female clothes).")]
    public GameObject[] femaleObjects = new GameObject[0];
    [Tooltip("Blend shapes set for a female body (0 for a male one).")]
    public AppearanceBlendShape[] femaleBlendShapes = new AppearanceBlendShape[0];
    [Tooltip("Blend shapes set for a male body (0 for a female one).")]
    public AppearanceBlendShape[] maleBlendShapes = new AppearanceBlendShape[0];

    [Header("Styles (empty = placeholder shapes)")]
    public List<AppearanceOption> hairStyles = new List<AppearanceOption>();
    public List<AppearanceOption> beardStyles = new List<AppearanceOption>();
    public List<AppearanceOption> eyeStyles = new List<AppearanceOption>();
    public List<AppearanceOption> noseStyles = new List<AppearanceOption>();
    public List<AppearanceOption> eyebrowStyles = new List<AppearanceOption>();
    [Tooltip("Head / face meshes (modular characters). Empty = the head's proportions (round, long, square...).")]
    public List<AppearanceOption> headStyles = new List<AppearanceOption>();
    [Tooltip("Your options next to the placeholder shapes. Auto: while the placeholder body is used your options are extra " +
             "choices after the built-in ones; with a model of your own they replace them. Always / Never.")]
    public PlaceholderStyleMode placeholderStyles = PlaceholderStyleMode.Auto;

    [Header("Body Models")]
    [Tooltip("Other bodies the player can pick (Appearance > Body > Body model), after the default one: a whole body or body " +
             "parts, made for the character's skeleton (same bone names, or Humanoid) - they move with its animation. " +
             "Hides = None replaces the whole placeholder body; tick regions to replace only those parts.")]
    public List<AppearanceOption> bodyModels = new List<AppearanceOption>();

    [Header("Body Regions (hidden under armour)")]
    [Tooltip("Renderers of each part of the body: worn armour hides the regions it covers (a helmet the hair, boots the feet) " +
             "so the body never shows through it. Scan Model fills this from the part names.")]
    public List<AppearanceRegion> bodyRegions = new List<AppearanceRegion>();

    [Header("Colours")]
    [Tooltip("Renderers / materials tinted with the skin colour.")]
    public AppearanceColorTarget[] skinTargets = new AppearanceColorTarget[0];
    [Tooltip("Renderers / materials tinted with the hair colour (the chosen hair and beard objects are tinted too).")]
    public AppearanceColorTarget[] hairTargets = new AppearanceColorTarget[0];
    [Tooltip("Renderers / materials tinted with the eye colour.")]
    public AppearanceColorTarget[] eyeTargets = new AppearanceColorTarget[0];
    [Tooltip("The hair colour also tints every renderer of the chosen hairstyle and beard.")]
    public bool tintChosenHair = true;

    [Header("Face Sliders (blend shapes)")]
    public AppearanceBlendSlider[] noseSize = new AppearanceBlendSlider[0];
    public AppearanceBlendSlider[] eyeSize = new AppearanceBlendSlider[0];

    [Header("Placeholder Parts")]
    [Tooltip("Parts without options get simple shapes on the head so the choice can be seen.")]
    public bool placeholderParts = true;
    [Tooltip("The head. Empty = the Animator's Head bone, then a child named 'Head', then the top of the model.")]
    public Transform head;
    [Tooltip("Head diameter in metres for the placeholder shapes. 0 = estimated from the model's height.")]
    [Min(0f)] public float headSize = 0f;
    [Tooltip("Placeholder shapes, in head sizes: moves them all (x right, y up, z forward).")]
    public Vector3 placeholderOffset = Vector3.zero;

    [Header("Placeholder Body")]
    [Tooltip("A whole body for a model that has none: torso, pelvis, neck, arms, hands, legs, booted feet and the skin of the " +
             "head, on a humanoid skeleton (the Animator's own bones, else one built with a Humanoid Avatar) so the character's " +
             "humanoid animations play on it. Auto = only when the model has no skinned mesh (an empty model or a capsule).")]
    public PlaceholderBodyMode placeholderBody = PlaceholderBodyMode.Auto;

    [SerializeField, HideInInspector] private CharacterAppearanceData current = new CharacterAppearanceData();

    /// <summary>Raised after an appearance was applied.</summary>
    public event Action<CharacterAppearanceData> Changed;

    public CharacterAppearanceData Current => current;

    private readonly Dictionary<AppearancePart, GameObject> attached = new Dictionary<AppearancePart, GameObject>();
    private readonly Dictionary<AppearancePart, Transform> placeholderGroups = new Dictionary<AppearancePart, Transform>();
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private AppearanceRules rules = AppearanceRules.None;
    private readonly List<GameObject> raceAttached = new List<GameObject>();
    private readonly List<string> raceShapes = new List<string>();
    private Transform earGroup;

    /// <summary>The race / class appearance rules in effect (build, ears, beards, skin tones...).</summary>
    public AppearanceRules Rules => rules;

    /// <summary>
    /// Sets the race / class rules (<see cref="AppearanceRules.From"/>): which beards are allowed, ears, race blend
    /// shapes and attached parts. Shown at the next <see cref="Apply"/>.
    /// </summary>
    public void SetRules(AppearanceRules newRules) => rules = newRules ?? AppearanceRules.None;
    private Transform placeholderRoot;
    private Transform headAnchor;
    private Transform headSkin;
    private bool headFound;
    private ProceduralBody procedural;

    /// <summary>The placeholder body built for a model without one (null when the model has its own).</summary>
    public ProceduralBody Body => procedural;
    private MaterialPropertyBlock block;
    private bool applied;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly string[] HairNames = { "Bald", "Short", "Long", "Ponytail", "Mohawk", "Bun", "Curly" };
    private static readonly string[] BeardNames = { "None", "Stubble", "Goatee", "Full Beard", "Moustache", "Long Beard" };
    private static readonly string[] EyeNames = { "Round", "Almond", "Narrow", "Wide Set", "Large" };
    private static readonly string[] NoseNames = { "Straight", "Button", "Wide", "Long", "Hooked", "Small" };
    private static readonly string[] BrowNames = { "Natural", "Thick", "Thin", "Arched", "Straight", "None" };
    private static readonly string[] HeadNames = { "Oval", "Round", "Long", "Square", "Narrow" };
    private static readonly string[] BodyNames = { "Default" };
    private static readonly Vector3[] HeadShapes =
    {
        Vector3.one, new Vector3(1.05f, 0.97f, 1.03f), new Vector3(0.96f, 1.07f, 0.99f), new Vector3(1.06f, 1.0f, 1.04f), new Vector3(0.93f, 1.03f, 0.97f),
    };

    // ------------------------------------------------------------------ lookup
    /// <summary>The appearance of a character (added to its model when missing and <paramref name="addIfMissing"/>).</summary>
    public static CharacterAppearance For(GameObject root, bool addIfMissing = true)
    {
        if (root == null)
            return null;
        CharacterAppearance a = root.GetComponentInChildren<CharacterAppearance>(true);
        if (a != null || !addIfMissing)
            return a;
        Animator anim = root.GetComponentInChildren<Animator>(true);
        GameObject host = anim != null ? anim.gameObject : root;
        return host.AddComponent<CharacterAppearance>();
    }

    /// <summary>Names of the placeholder options of a part.</summary>
    public static string[] PlaceholderNames(AppearancePart part)
    {
        switch (part)
        {
            case AppearancePart.Hair: return HairNames;
            case AppearancePart.Beard: return BeardNames;
            case AppearancePart.Eyes: return EyeNames;
            case AppearancePart.Eyebrows: return BrowNames;
            case AppearancePart.Head: return HeadNames;
            case AppearancePart.Body: return BodyNames;
            default: return NoseNames;
        }
    }

    public List<AppearanceOption> Options(AppearancePart part)
    {
        switch (part)
        {
            case AppearancePart.Hair: return hairStyles;
            case AppearancePart.Beard: return beardStyles;
            case AppearancePart.Eyes: return eyeStyles;
            case AppearancePart.Eyebrows: return eyebrowStyles ?? (eyebrowStyles = new List<AppearanceOption>());
            case AppearancePart.Head: return headStyles ?? (headStyles = new List<AppearanceOption>());
            case AppearancePart.Body: return bodyModels ?? (bodyModels = new List<AppearanceOption>());
            default: return noseStyles;
        }
    }

    /// <summary>The part has no options of its own and shows only placeholder shapes.</summary>
    public bool UsesPlaceholder(AppearancePart part) =>
        part != AppearancePart.Body && placeholderParts && (Options(part) == null || Options(part).Count == 0);

    /// <summary>
    /// How many placeholder choices come first: all of them when the part has no options, or when your options are extra
    /// choices (Placeholder Styles); the body always starts with "Default".
    /// </summary>
    public int PlaceholderCount(AppearancePart part)
    {
        if (part == AppearancePart.Body) return 1;
        if (!placeholderParts) return 0;
        List<AppearanceOption> o = Options(part);
        if (o == null || o.Count == 0) return PlaceholderNames(part).Length;
        bool keep = placeholderStyles == PlaceholderStyleMode.Always || (placeholderStyles == PlaceholderStyleMode.Auto && procedural != null);
        return keep ? PlaceholderNames(part).Length : 0;
    }

    /// <summary>Choice <paramref name="index"/> is a placeholder one (shapes, or the default body).</summary>
    public bool IsPlaceholderChoice(AppearancePart part, int index) => index >= 0 && index < PlaceholderCount(part);

    /// <summary>Your option behind choice <paramref name="index"/> (null for placeholder choices).</summary>
    public AppearanceOption OptionAt(AppearancePart part, int index)
    {
        int i = index - PlaceholderCount(part);
        List<AppearanceOption> o = Options(part);
        return o != null && i >= 0 && i < o.Count ? o[i] : null;
    }

    public int OptionCount(AppearancePart part)
    {
        List<AppearanceOption> o = Options(part);
        return PlaceholderCount(part) + (o != null ? o.Count : 0);
    }

    public string OptionName(AppearancePart part, int index)
    {
        if (IsPlaceholderChoice(part, index))
        {
            string[] n = PlaceholderNames(part);
            return index < n.Length ? n[index] : "-";
        }
        AppearanceOption o = OptionAt(part, index);
        return o != null ? o.name : "-";
    }

    /// <summary>Can this body type pick option <paramref name="index"/> of the part?</summary>
    public bool IsAllowed(AppearancePart part, int index, CharacterBodyType body)
    {
        if (index < 0 || index >= OptionCount(part))
            return false;
        if (IsPlaceholderChoice(part, index))
        {
            if (part != AppearancePart.Beard)
                return true;
            if (index == 0)
                return !rules.alwaysBeard; // "None"
            return body == CharacterBodyType.Male || rules.beardOnAnyBody;
        }
        AppearanceOption o = OptionAt(part, index);
        if (o == null)
            return true;
        if (part == AppearancePart.Beard)
        {
            if (rules.alwaysBeard && IsEmpty(o))
                return false; // the "no beard" option
            if (rules.beardOnAnyBody)
                return true;
        }
        return o.Allows(body);
    }

    private static bool IsEmpty(AppearanceOption o) =>
        o.prefab == null && (o.objects == null || Array.TrueForAll(o.objects, x => x == null)) &&
        (o.blendShapes == null || Array.TrueForAll(o.blendShapes, x => x == null || x.renderer == null));

    /// <summary>The next allowed option after <paramref name="index"/> going <paramref name="step"/> (wraps around).</summary>
    public int Step(AppearancePart part, int index, int step, CharacterBodyType body)
    {
        int count = OptionCount(part);
        if (count <= 0)
            return 0;
        int i = index;
        for (int n = 0; n < count; n++)
        {
            i = ((i + step) % count + count) % count;
            if (IsAllowed(part, i, body))
                return i;
        }
        return index;
    }

    /// <summary>A copy of <paramref name="data"/> with every index valid for this model and body type.</summary>
    public CharacterAppearanceData Validated(CharacterAppearanceData data)
    {
        CharacterAppearanceData d = data != null ? data.Clone() : new CharacterAppearanceData();
        foreach (AppearancePart part in (AppearancePart[])Enum.GetValues(typeof(AppearancePart)))
        {
            int count = OptionCount(part);
            int i = d.Style(part);
            if (count == 0) { d.SetStyle(part, 0); continue; }
            if (i < 0 || i >= count || !IsAllowed(part, i, d.bodyType))
            {
                int preferred = part == AppearancePart.Beard ? rules.defaultBeard : part == AppearancePart.Hair ? rules.defaultHair : -1;
                if (preferred >= 0 && preferred < count && IsAllowed(part, preferred, d.bodyType)) i = preferred;
                else i = IsAllowed(part, 0, d.bodyType) ? 0 : Step(part, 0, 1, d.bodyType);
            }
            d.SetStyle(part, i);
        }
        d.noseSize = Mathf.Clamp01(d.noseSize);
        d.eyeSize = Mathf.Clamp01(d.eyeSize);
        return d;
    }

    // ------------------------------------------------------------------ apply
    private void Awake()
    {
        if (Application.isPlaying)
            EnsureBody();
    }

    private void Start()
    {
        if (!applied)
            Apply(current);
    }

    private void OnDestroy()
    {
        foreach (Material m in materials.Values)
            if (m != null) DestroyObj(m);
        materials.Clear();
    }

    /// <summary>Shows <paramref name="data"/> on the model.</summary>
    public void Apply(CharacterAppearanceData data)
    {
        applied = true;
        EnsureBody(); // first: with the placeholder body your options are extra choices (the indices depend on it)
        current = Validated(data);
        if (procedural != null)
            procedural.SetBodyType(current.bodyType);
        ApplyBody();
        foreach (AppearancePart part in (AppearancePart[])Enum.GetValues(typeof(AppearancePart)))
        {
            if (IsPlaceholderChoice(part, current.Style(part)))
            {
                ClearOptions(part);
                BuildPlaceholder(part);
            }
            else
            {
                ClearPlaceholder(part);
                ApplyOptions(part);
            }
        }
        BuildHeadSkin();
        ApplySliders(noseSize, current.noseSize);
        ApplySliders(eyeSize, current.eyeSize);
        ApplyRace();
        ApplyColors();
        RefreshRegions();
        Changed?.Invoke(current);
    }

    private void ApplyBody()
    {
        bool female = current.bodyType == CharacterBodyType.Female;
        foreach (GameObject go in maleObjects)
            if (go != null) go.SetActive(!female);
        foreach (GameObject go in femaleObjects)
            if (go != null) go.SetActive(female);
        SetShapes(femaleBlendShapes, female);
        SetShapes(maleBlendShapes, !female);
    }

    private void ApplyOptions(AppearancePart part)
    {
        List<AppearanceOption> options = Options(part);
        AppearanceOption pick = OptionAt(part, current.Style(part));
        // Hide every other option first, so objects shared with the chosen one end up shown.
        foreach (AppearanceOption o in options)
        {
            if (o == null || o == pick) continue;
            foreach (GameObject go in o.objects)
                if (go != null) go.SetActive(false);
            SetShapes(o.blendShapes, false);
        }
        // The same choice keeps its instance (a body model is not rebuilt at every slider move).
        bool keep = pick != null && attachedOption.TryGetValue(part, out AppearanceOption was) && was == pick &&
                    attached.TryGetValue(part, out GameObject still) && still != null;
        if (!keep)
        {
            if (attached.TryGetValue(part, out GameObject old) && old != null)
                DestroyObj(old);
            attached.Remove(part);
            attachedOption.Remove(part);
        }
        if (pick == null)
        {
            ShowRegions(PartKey(part));
            return;
        }
        foreach (GameObject go in pick.objects)
            if (go != null) go.SetActive(true);
        SetShapes(pick.blendShapes, true);
        if (pick.prefab != null && !keep)
        {
            GameObject inst = PutOn(pick, part);
            if (inst != null)
            {
                attached[part] = inst;
                attachedOption[part] = pick;
            }
        }
        BodyRegion hides = pick.hides;
        if (part == AppearancePart.Body && hides == BodyRegion.None)
            hides = WholeBody;
        if (hides != BodyRegion.None) HideRegions(PartKey(part), hides);
        else ShowRegions(PartKey(part));
        if (part == AppearancePart.Body && !keep)
            RemeasureCharacter();
    }

    /// <summary>The Character Body sizes the colliders to the body again (another body model).</summary>
    private void RemeasureCharacter()
    {
        if (!Application.isPlaying) return;
        CharacterBody cb = GetComponentInParent<CharacterBody>();
        if (cb == null)
        {
            CharacterController cc = GetComponentInParent<CharacterController>();
            if (cc != null) cb = cc.GetComponentInChildren<CharacterBody>(true);
        }
        if (cb != null) cb.Remeasure();
    }

    /// <summary>The regions of the placeholder body a whole body model replaces.</summary>
    private const BodyRegion WholeBody = BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hands | BodyRegion.Legs | BodyRegion.Feet | BodyRegion.Neck;

    private readonly Dictionary<AppearancePart, AppearanceOption> attachedOption = new Dictionary<AppearancePart, AppearanceOption>();
    private readonly Dictionary<AppearancePart, string> partKeys = new Dictionary<AppearancePart, string>();

    /// <summary>The owner under which a part's option hides body regions.</summary>
    private string PartKey(AppearancePart part)
    {
        if (!partKeys.TryGetValue(part, out string k)) partKeys[part] = k = "appearance:" + part;
        return k;
    }

    /// <summary>Instantiates an option's prefab on the character (skinned to its skeleton, on a bone, or fitted).</summary>
    private GameObject PutOn(AppearanceOption pick, AppearancePart part)
    {
        AppearanceAttachMode mode = pick.attachMode;
        if (mode == AppearanceAttachMode.Auto)
            mode = pick.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ? AppearanceAttachMode.Skinned : AppearanceAttachMode.Rigid;
        Animator a = Anim();
        GameObject inst;
        if (mode == AppearanceAttachMode.Skinned && a != null)
        {
            // Beside the skeleton (under the model), so it scales with the character.
            Transform hips = a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform parent = hips != null && hips.parent != null ? hips.parent : a.transform;
            inst = Instantiate(pick.prefab, parent, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            Prepare(inst);
            int missing = SkeletonBinder.Bind(inst, a);
            if (missing > 0)
                Debug.LogWarning($"[Appearance] '{pick.name}': {missing} bone(s) of its skinned meshes are not on this character's skeleton " +
                                 "(another rig?). Use a model rigged like the character (or Humanoid), or Attach Mode Fit To Body.", this);
        }
        else
        {
            Transform bone = Bone(pick.attachTo);
            if (bone == null) return null;
            inst = Instantiate(pick.prefab, bone, false);
            inst.transform.localPosition = pick.position;
            inst.transform.localRotation = Quaternion.Euler(pick.rotation);
            inst.transform.localScale = pick.scale;
            Prepare(inst);
            if (mode == AppearanceAttachMode.FitToBody)
            {
                int n = BodyFitter.Fit(inst, BodySkins(inst), pick.fitOffset, out string problem);
                if (problem != null)
                    Debug.LogWarning($"[Appearance] '{pick.name}': fitted {n} mesh(es) to the body; {problem}.", this);
            }
        }
        inst.name = pick.prefab.name + " (" + part + ")";
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = gameObject.layer;
        return inst;
    }

    /// <summary>Look only: no colliders, physics or animator of its own.</summary>
    private static void Prepare(GameObject inst)
    {
        foreach (Collider c in inst.GetComponentsInChildren<Collider>(true))
            DestroyObj(c);
        foreach (Rigidbody rb in inst.GetComponentsInChildren<Rigidbody>(true))
            DestroyObj(rb);
        foreach (Animator an in inst.GetComponentsInChildren<Animator>(true))
            an.enabled = false;
        if (!inst.activeSelf) inst.SetActive(true);
    }

    /// <summary>The body's visible skinned meshes (to fit pieces on), without <paramref name="exclude"/>.</summary>
    private List<SkinnedMeshRenderer> BodySkins(GameObject exclude)
    {
        var list = new List<SkinnedMeshRenderer>();
        Animator a = Anim();
        Transform root = a != null ? a.transform : transform;
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr == null || !smr.enabled || smr.sharedMesh == null) continue;
            if (exclude != null && smr.transform.IsChildOf(exclude.transform)) continue;
            list.Add(smr);
        }
        return list;
    }

    private void ClearOptions(AppearancePart part)
    {
        if (attached.TryGetValue(part, out GameObject old) && old != null)
            DestroyObj(old);
        attached.Remove(part);
        attachedOption.Remove(part);
        // Your options' objects go hidden too (a placeholder choice is shown instead).
        List<AppearanceOption> options = Options(part);
        if (options != null)
            foreach (AppearanceOption o in options)
            {
                if (o == null) continue;
                foreach (GameObject go in o.objects)
                    if (go != null) go.SetActive(false);
                SetShapes(o.blendShapes, false);
            }
        bool wasModel = part == AppearancePart.Body && hiddenBy.ContainsKey(PartKey(part));
        ShowRegions(PartKey(part));
        if (wasModel)
            RemeasureCharacter();
    }

    private static void SetShapes(AppearanceBlendShape[] shapes, bool on)
    {
        if (shapes == null) return;
        foreach (AppearanceBlendShape s in shapes)
        {
            if (s == null || s.renderer == null || s.renderer.sharedMesh == null || string.IsNullOrEmpty(s.shape)) continue;
            int i = s.renderer.sharedMesh.GetBlendShapeIndex(s.shape);
            if (i >= 0) s.renderer.SetBlendShapeWeight(i, on ? s.weight : 0f);
        }
    }

    private static void ApplySliders(AppearanceBlendSlider[] sliders, float t)
    {
        if (sliders == null) return;
        foreach (AppearanceBlendSlider s in sliders)
        {
            if (s == null || s.renderer == null || s.renderer.sharedMesh == null || string.IsNullOrEmpty(s.shape)) continue;
            int i = s.renderer.sharedMesh.GetBlendShapeIndex(s.shape);
            if (i >= 0) s.renderer.SetBlendShapeWeight(i, Mathf.Lerp(s.atZero, s.atOne, t));
        }
    }

    // ------------------------------------------------------------------ colours
    private void ApplyColors()
    {
        Tint(skinTargets, current.skinColor);
        Tint(eyeTargets, current.eyeColor);
        if (procedural != null)
            procedural.SetColors(current.skinColor, current.topColor, current.bottomColor);
        Tint(hairTargets, current.hairColor);
        // Your chosen options take their colour.
        foreach (AppearancePart part in (AppearancePart[])Enum.GetValues(typeof(AppearancePart)))
        {
            AppearanceOption pick = OptionAt(part, current.Style(part));
            if (pick == null) continue;
            AppearanceTint tint = pick.tint;
            if (tint == AppearanceTint.Auto)
                tint = tintChosenHair && (part == AppearancePart.Hair || part == AppearancePart.Beard || part == AppearancePart.Eyebrows)
                    ? AppearanceTint.Hair : AppearanceTint.None;
            if (tint == AppearanceTint.None) continue;
            Color c = tint == AppearanceTint.Skin ? current.skinColor : tint == AppearanceTint.Hair ? current.hairColor
                : tint == AppearanceTint.Eyes ? current.eyeColor : tint == AppearanceTint.Shirt ? current.topColor : current.bottomColor;
            foreach (GameObject go in pick.objects)
                if (go != null)
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) Tint(r, -1, null, c);
            if (attached.TryGetValue(part, out GameObject inst) && inst != null)
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) Tint(r, -1, null, c);
        }
    }

    private void Tint(AppearanceColorTarget[] targets, Color color)
    {
        if (targets == null) return;
        foreach (AppearanceColorTarget t in targets)
            if (t != null && t.renderer != null) Tint(t.renderer, t.materialIndex, t.property, color);
    }

    private void Tint(Renderer r, int materialIndex, string property, Color color)
    {
        if (r == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        int count = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
        for (int i = 0; i < count; i++)
        {
            if (materialIndex >= 0 && i != materialIndex) continue;
            r.GetPropertyBlock(block, i);
            if (!string.IsNullOrEmpty(property))
                block.SetColor(property, color);
            else
            {
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
            }
            r.SetPropertyBlock(block, i);
        }
    }

    // ------------------------------------------------------------------ bones
    private Animator Anim()
    {
        Animator a = GetComponent<Animator>();
        if (a == null) a = GetComponentInChildren<Animator>(true);
        if (a == null) a = GetComponentInParent<Animator>();
        return a;
    }

    private Transform Bone(HumanBodyBones bone)
    {
        Animator a = Anim();
        if (a != null && a.isHuman && a.avatar != null)
        {
            Transform t = a.GetBoneTransform(bone);
            if (t != null) return t;
        }
        return bone == HumanBodyBones.Head ? Head() : transform;
    }

    /// <summary>The head the placeholder shapes (and head prefabs) attach to.</summary>
    public Transform Head()
    {
        if (head != null) return head;
        Animator a = Anim();
        if (a != null && a.isHuman && a.avatar != null)
        {
            Transform h = a.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) return head = h;
        }
        Transform best = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (placeholderRoot != null && t.IsChildOf(placeholderRoot)) continue;
            string n = t.name.ToLowerInvariant();
            if (n == "head" || n.EndsWith(":head") || n.EndsWith("_head") || n.EndsWith(" head") || n == "bip001 head")
            {
                best = t;
                break;
            }
        }
        if (best != null)
        {
            headFound = true;
            return head = best;
        }
        // No bone: an anchor at the top of the model.
        if (headAnchor == null)
        {
            Bounds b = ModelBounds();
            float s = EstimatedHeadSize(b);
            headAnchor = new GameObject("Head Anchor").transform;
            headAnchor.SetParent(transform, false);
            headAnchor.position = new Vector3(b.center.x, b.max.y - s * 0.95f, b.center.z);
        }
        return headAnchor;
    }

    private Bounds ModelBounds()
    {
        bool any = false;
        var b = new Bounds(transform.position + Vector3.up, Vector3.one * 0.1f);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (placeholderRoot != null && r.transform.IsChildOf(placeholderRoot)) continue;
            if (procedural != null && r.transform.IsChildOf(procedural.transform)) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (procedural != null)
        {
            // Skinned bounds are only right once rendered: the body's own size instead.
            Bounds pb = procedural.WorldBounds();
            if (!any) { b = pb; any = true; }
            else b.Encapsulate(pb);
        }
        if (!any)
        {
            CharacterController cc = GetComponentInParent<CharacterController>();
            if (cc != null) b = new Bounds(cc.transform.TransformPoint(cc.center), new Vector3(cc.radius * 2f, cc.height, cc.radius * 2f));
            else b = new Bounds(transform.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
        }
        return b;
    }

    private float EstimatedHeadSize(Bounds b) =>
        headSize > 0f ? headSize : procedural != null ? procedural.HeadSize : Mathf.Clamp(b.size.y * 0.13f, 0.1f, 0.6f);

    // ------------------------------------------------------------------ placeholder body
    /// <summary>
    /// Builds the placeholder body when the model needs one (Placeholder Body; Play Mode only). Called when the
    /// component starts and before each look is applied; call it before measuring the model. True when there is one.
    /// </summary>
    public bool EnsureBody()
    {
        if (procedural != null)
            return true;
        if (!Application.isPlaying || placeholderBody == PlaceholderBodyMode.Off)
            return false;
        procedural = GetComponentInChildren<ProceduralBody>(true);
        if (procedural != null)
            return true;
        Animator a = Anim();
        Transform host = a != null ? a.transform : transform;
        if (placeholderBody == PlaceholderBodyMode.Auto && HasRealBody(host))
            return false;

        // Size: the Character Body's model height (else 1.8 m) at the model's own scale; feet on the controller's bottom.
        CharacterController cc = GetComponentInParent<CharacterController>();
        CharacterBody cb = GetComponentInParent<CharacterBody>();
        if (cb == null && cc != null) cb = cc.GetComponentInChildren<CharacterBody>(true);
        float modelHeight = cb != null ? cb.modelHeight : ProceduralBody.ReferenceHeight;
        float scale = Mathf.Abs(host.lossyScale.y);
        if (cb != null && cb.Model == host && cb.Scale > 0.01f) scale /= cb.Scale;
        float localHeight = modelHeight / Mathf.Max(1e-4f, scale);
        float feetY = 0f;
        if (cc != null)
        {
            Vector3 bottom = cc.transform.TransformPoint(cc.center + Vector3.down * (cc.height * 0.5f));
            feetY = host.InverseTransformPoint(bottom).y;
            if (Mathf.Abs(feetY) > 0.1f * localHeight && a != null && a.runtimeAnimatorController != null)
            {
                Debug.LogWarning($"[Appearance] '{host.name}' (the Animator) is not at the character's feet: humanoid animation stands the " +
                                 "body on the Animator's position, so the placeholder body stands there. Move the model so its origin is at the feet.", host);
                feetY = 0f;
            }
        }
        procedural = ProceduralBody.Create(host, a, localHeight, feetY, current.bodyType);
        // Colliders, camera targets and model height follow the new body.
        if (cb != null)
            cb.Remeasure();

        // The face parts move to the body's head.
        if (headFound) { head = null; headFound = false; }
        if (headAnchor != null) { DestroyObj(headAnchor.gameObject); headAnchor = null; }
        ResetHeadShape();
        if (placeholderRoot != null)
        {
            DestroyObj(placeholderRoot.gameObject);
            placeholderRoot = null;
            placeholderGroups.Clear();
            earGroup = null;
            headSkin = null;
        }
        return true;
    }

    /// <summary>
    /// The model shows a body of its own: a skinned mesh, or a tall mesh that is not a primitive stand-in. Switched-off
    /// meshes (an inactive object, a disabled renderer) do not count: the model's mesh turned off = no body.
    /// </summary>
    private static bool HasRealBody(Transform root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled || r.GetComponentInParent<CharacterAppearancePartsMarker>() != null || r.GetComponentInParent<ProceduralBody>() != null)
                continue;
            if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh != null) return true;
                continue;
            }
            if (!(r is MeshRenderer)) continue;
            MeshFilter f = r.GetComponent<MeshFilter>();
            Mesh m = f != null ? f.sharedMesh : null;
            if (m == null) continue;
            string n = m.name;
            if (n == "Capsule" || n == "Cylinder" || n == "Cube" || n == "Sphere" || n.StartsWith("Capsule ") || n.StartsWith("Cylinder ")) continue;
            if (r.bounds.size.y >= 0.8f) return true;
        }
        return false;
    }

    /// <summary>The skin of the head (with ears and lips) for the placeholder body, under the face parts.</summary>
    private void BuildHeadSkin()
    {
        if (headSkin != null) DestroyObj(headSkin.gameObject);
        headSkin = null;
        if (procedural == null || !IsPlaceholderChoice(AppearancePart.Head, current.headStyle))
            return; // no placeholder body, or a head mesh of yours is chosen
        Transform root = PlaceholderRoot();
        if (root == null) return;
        headSkin = new GameObject("Head Skin").transform;
        headSkin.SetParent(root, false);
        headSkin.gameObject.layer = root.gameObject.layer;
        Color skin = current.skinColor;
        AddMesh(headSkin, "Skin", AppearanceMeshes.HeadSkin(), skin, "skin");
        if (!rules.HasRealEars)
            for (int side = -1; side <= 1; side += 2)
                Shape(headSkin, "Ear", PrimitiveType.Sphere, new Vector3(side * 0.455f, 0f, -0.03f), new Vector3(0.07f, 0.2f, 0.12f),
                    new Vector3(0f, side * -12f, 0f), skin, "skin");
        Color lips = Color.Lerp(skin, new Color(0.72f, 0.32f, 0.32f), 0.35f);
        Shape(headSkin, "Lips", PrimitiveType.Capsule, new Vector3(0f, -0.235f, 0.425f), new Vector3(0.045f, 0.1f, 0.04f),
            new Vector3(0f, 0f, 90f), lips, "skin");
    }

    // ------------------------------------------------------------------ placeholder shapes
    private Transform PlaceholderRoot()
    {
        Transform h = Head();
        if (h == null) return null;
        if (placeholderRoot != null && placeholderRoot.parent == h)
            return placeholderRoot;
        if (placeholderRoot != null) DestroyObj(placeholderRoot.gameObject);
        placeholderGroups.Clear();

        float s = EstimatedHeadSize(ModelBounds());
        Animator a = Anim();
        Transform basis = a != null ? a.transform : transform;
        bool bone = h != headAnchor;
        // A head bone sits at the base of the skull: the centre of the head is a bit above it.
        Vector3 centre = h.position + basis.up * (bone ? s * 0.45f : 0f);
        centre += basis.rotation * (placeholderOffset * s);

        placeholderRoot = new GameObject("Appearance Parts", typeof(CharacterAppearancePartsMarker)).transform;
        placeholderRoot.SetParent(h, true);
        placeholderRoot.position = centre;
        placeholderRoot.rotation = basis.rotation;
        Vector3 ls = h.lossyScale;
        float parentScale = Mathf.Max(1e-4f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
        placeholderRoot.localScale = Vector3.one * (s / parentScale);
        placeholderRoot.gameObject.layer = h.gameObject.layer;
        return placeholderRoot;
    }

    private Transform Group(AppearancePart part)
    {
        Transform root = PlaceholderRoot();
        if (root == null) return null;
        if (placeholderGroups.TryGetValue(part, out Transform g) && g != null)
            return g;
        g = new GameObject(part.ToString()).transform;
        g.SetParent(root, false);
        g.gameObject.layer = root.gameObject.layer;
        placeholderGroups[part] = g;
        return g;
    }

    // ------------------------------------------------------------------ body regions (armour)
    private readonly Dictionary<object, BodyRegion> hiddenBy = new Dictionary<object, BodyRegion>();
    private readonly List<Renderer> regionHidden = new List<Renderer>();

    /// <summary>The regions hidden by worn armour.</summary>
    public BodyRegion HiddenRegions
    {
        get
        {
            BodyRegion h = BodyRegion.None;
            foreach (BodyRegion r in hiddenBy.Values) h |= r;
            return h;
        }
    }

    /// <summary>Hides <paramref name="regions"/> while <paramref name="owner"/> (a worn piece) covers them; None = shows them again.</summary>
    public void HideRegions(object owner, BodyRegion regions)
    {
        if (owner == null) return;
        if (regions == BodyRegion.None) hiddenBy.Remove(owner);
        else hiddenBy[owner] = regions;
        RefreshRegions();
    }

    /// <summary>What <paramref name="owner"/> hid shows again.</summary>
    public void ShowRegions(object owner)
    {
        if (owner != null && hiddenBy.Remove(owner))
            RefreshRegions();
    }

    /// <summary>Shows or hides each region's renderers: the Body Regions list, the chosen hair / beard and the placeholder parts.</summary>
    private void RefreshRegions()
    {
        foreach (Renderer r in regionHidden)
            if (r != null) r.enabled = true;
        regionHidden.Clear();
        BodyRegion h = HiddenRegions;
        if (h == BodyRegion.None)
            return;
        void HideAll(IEnumerable<Renderer> rs)
        {
            foreach (Renderer r in rs)
                if (r != null && r.enabled) { r.enabled = false; regionHidden.Add(r); }
        }
        foreach (AppearanceRegion ar in bodyRegions)
            if (ar != null && ar.renderers != null && (ar.region & h) != 0)
                HideAll(ar.renderers);
        if (procedural != null)
            foreach (var (renderer, region) in procedural.Regions())
                if ((region & h) != 0) HideAll(new[] { renderer });
        void HidePart(AppearancePart part)
        {
            if (placeholderGroups.TryGetValue(part, out Transform g) && g != null)
                HideAll(g.GetComponentsInChildren<Renderer>(true));
            if (attached.TryGetValue(part, out GameObject inst) && inst != null)
                HideAll(inst.GetComponentsInChildren<Renderer>(true));
            AppearanceOption chosen = OptionAt(part, current.Style(part));
            if (chosen != null)
                foreach (GameObject go in chosen.objects)
                    if (go != null) HideAll(go.GetComponentsInChildren<Renderer>(true));
        }
        if ((h & BodyRegion.Hair) != 0) HidePart(AppearancePart.Hair);
        if ((h & BodyRegion.Beard) != 0) HidePart(AppearancePart.Beard);
        if ((h & BodyRegion.Face) != 0)
        {
            HidePart(AppearancePart.Eyes);
            HidePart(AppearancePart.Nose);
            HidePart(AppearancePart.Eyebrows);
            if (headSkin != null) HideAll(headSkin.GetComponentsInChildren<Renderer>(true));
        }
        if ((h & BodyRegion.Ears) != 0 && earGroup != null)
            HideAll(earGroup.GetComponentsInChildren<Renderer>(true));
    }

    // ------------------------------------------------------------------ modular models
    private static readonly string[] BodyWords = { "torso", "chest", "arm", "hand", "leg", "hip", "foot", "feet", "body", "spine", "shoulder", "elbow", "knee" };

    /// <summary>
    /// Fills the parts from the model's own objects, by name — how modular character packs are organised (one mesh per
    /// hairstyle, beard, eyebrows, head, and male / female body parts): "hair" → Hair, "beard" / "facialhair" →
    /// Beard, "eyebrow" → Eyebrows, "head" / "face" → Head, male / female body parts → Male / Female Objects, and
    /// every part into its Body Region. Options get "None" first for hair and beards. Parts already listed are kept.
    /// Returns what was found.
    /// </summary>
    public string ScanModel()
    {
        var found = new Dictionary<AppearancePart, List<(GameObject go, AppearanceBodyFilter filter)>>();
        var regionMap = new Dictionary<BodyRegion, List<Renderer>>();
        var male = new List<GameObject>();
        var female = new List<GameObject>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (placeholderRoot != null && r.transform.IsChildOf(placeholderRoot)) continue;
            if (r.GetComponentInParent<CharacterAppearancePartsMarker>() != null) continue;
            if (r.GetComponentInParent<ProceduralBody>() != null) continue;
            string n = r.gameObject.name.ToLowerInvariant();
            AppearanceBodyFilter filter = n.Contains("female") || n.Contains("_f_") || n.EndsWith("_f") ? AppearanceBodyFilter.FemaleOnly
                : n.Contains("male") || n.Contains("_m_") || n.EndsWith("_m") ? AppearanceBodyFilter.MaleOnly : AppearanceBodyFilter.Any;
            AppearancePart? part = null;
            if (n.Contains("eyebrow") || n.Contains("brow")) part = AppearancePart.Eyebrows;
            else if (n.Contains("facialhair") || n.Contains("facial_hair") || n.Contains("beard") || n.Contains("moustache") || n.Contains("mustache") || n.Contains("goatee")) part = AppearancePart.Beard;
            else if (n.Contains("hair")) part = AppearancePart.Hair;
            else if (n.Contains("head") || n.Contains("face")) part = AppearancePart.Head;
            if (part.HasValue)
            {
                if (!found.TryGetValue(part.Value, out var list)) found[part.Value] = list = new List<(GameObject, AppearanceBodyFilter)>();
                if (!list.Exists(x => x.go == r.gameObject)) list.Add((r.gameObject, filter));
            }
            else if (filter != AppearanceBodyFilter.Any && Array.Exists(BodyWords, w => n.Contains(w)))
            {
                (filter == AppearanceBodyFilter.FemaleOnly ? female : male).Add(r.gameObject);
            }
            BodyRegion region = BodyRegions.FromName(r.gameObject.name);
            if (region != BodyRegion.None && !(part.HasValue && part.Value == AppearancePart.Head && region == BodyRegion.Face))
            {
                if (!regionMap.TryGetValue(region, out var rl)) regionMap[region] = rl = new List<Renderer>();
                rl.Add(r);
            }
        }

        var report = new List<string>();
        foreach (KeyValuePair<AppearancePart, List<(GameObject go, AppearanceBodyFilter filter)>> kv in found)
        {
            List<AppearanceOption> options = Options(kv.Key);
            if (options.Count == 0 && (kv.Key == AppearancePart.Hair || kv.Key == AppearancePart.Beard || kv.Key == AppearancePart.Eyebrows))
                options.Add(new AppearanceOption { name = kv.Key == AppearancePart.Hair ? "Bald" : "None",
                    bodyTypes = AppearanceBodyFilter.Any });
            int added = 0;
            foreach ((GameObject go, AppearanceBodyFilter filter) in kv.Value)
            {
                if (options.Exists(o => o != null && o.objects != null && Array.IndexOf(o.objects, go) >= 0)) continue;
                options.Add(new AppearanceOption
                {
                    name = NiceName(go.name),
                    bodyTypes = kv.Key == AppearancePart.Beard && filter == AppearanceBodyFilter.Any ? AppearanceBodyFilter.MaleOnly : filter,
                    objects = new[] { go },
                });
                added++;
            }
            report.Add($"{kv.Key}: {added} new option(s)");
        }
        if (male.Count > 0 && female.Count > 0)
        {
            foreach (GameObject go in male) if (Array.IndexOf(maleObjects, go) < 0) maleObjects = Append(maleObjects, go);
            foreach (GameObject go in female) if (Array.IndexOf(femaleObjects, go) < 0) femaleObjects = Append(femaleObjects, go);
            report.Add($"Body: {male.Count} male and {female.Count} female part(s)");
        }
        foreach (KeyValuePair<BodyRegion, List<Renderer>> kv in regionMap)
        {
            AppearanceRegion ar = bodyRegions.Find(x => x != null && x.region == kv.Key);
            if (ar == null) bodyRegions.Add(ar = new AppearanceRegion { region = kv.Key });
            var all = new List<Renderer>(ar.renderers ?? new Renderer[0]);
            foreach (Renderer r in kv.Value) if (!all.Contains(r)) all.Add(r);
            ar.renderers = all.ToArray();
        }
        if (regionMap.Count > 0) report.Add($"Body regions: {string.Join(", ", regionMap.Keys)}");
        return report.Count > 0 ? string.Join("\n", report) : "No parts recognised by name (hair, beard, eyebrow, head, torso, arms, legs...).";
    }

    private static T[] Append<T>(T[] array, T item)
    {
        var list = new List<T>(array ?? new T[0]) { item };
        return list.ToArray();
    }

    /// <summary>"Chr_Hair_Male_03" → "Hair Male 03".</summary>
    private static string NiceName(string objectName)
    {
        string n = objectName;
        foreach (string prefix in new[] { "Chr_", "chr_", "SM_", "SK_", "Mesh_" })
            if (n.StartsWith(prefix)) n = n.Substring(prefix.Length);
        n = n.Replace('_', ' ').Replace('-', ' ');
        return n.Trim();
    }

    // ------------------------------------------------------------------ race
    /// <summary>The race's blend shapes, attached parts and ears.</summary>
    private void ApplyRace()
    {
        // Blend shapes the previous race set go back to 0.
        var wanted = new Dictionary<string, float>();
        foreach (NamedBlendShape b in rules.blendShapes)
            if (b != null && !string.IsNullOrEmpty(b.shape)) wanted[b.shape] = b.weight;
        var all = new List<string>(raceShapes);
        foreach (string n in wanted.Keys) if (!all.Contains(n)) all.Add(n);
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null || smr.sharedMesh == null) continue;
            foreach (string n in all)
            {
                int i = smr.sharedMesh.GetBlendShapeIndex(n);
                if (i >= 0) smr.SetBlendShapeWeight(i, wanted.TryGetValue(n, out float w) ? w : 0f);
            }
        }
        raceShapes.Clear();
        raceShapes.AddRange(wanted.Keys);

        foreach (GameObject go in raceAttached)
            if (go != null) DestroyObj(go);
        raceAttached.Clear();
        foreach (AppearanceAttachment a in rules.attachments)
        {
            if (a == null || a.prefab == null) continue;
            Transform bone = Bone(a.bone);
            if (bone == null) continue;
            GameObject inst = Instantiate(a.prefab, bone, false);
            inst.name = a.prefab.name + " (race)";
            inst.transform.localPosition = a.position;
            inst.transform.localRotation = Quaternion.Euler(a.rotation);
            inst.transform.localScale = a.scale;
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true))
                DestroyObj(c);
            raceAttached.Add(inst);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                Tint(r, -1, null, current.skinColor);
        }
        BuildEars();
    }

    private void BuildEars()
    {
        if (earGroup != null)
            for (int i = earGroup.childCount - 1; i >= 0; i--)
                DestroyObj(earGroup.GetChild(i).gameObject);
        if (rules.ears == EarShape.Human || !placeholderParts || rules.HasRealEars)
            return;
        Transform root = PlaceholderRoot();
        if (root == null) return;
        if (earGroup == null || earGroup.parent != root)
        {
            earGroup = new GameObject("Ears").transform;
            earGroup.SetParent(root, false);
            earGroup.gameObject.layer = root.gameObject.layer;
        }
        Color skin = current.skinColor;
        bool longEars = rules.ears == EarShape.LongPointed;
        for (int side = -1; side <= 1; side += 2)
        {
            var pos = new Vector3(side * (longEars ? 0.55f : 0.5f), longEars ? 0.12f : 0.08f, -0.04f);
            var size = longEars ? new Vector3(0.05f, 0.3f, 0.09f) : new Vector3(0.05f, 0.19f, 0.09f);
            var rot = new Vector3(longEars ? -18f : -8f, 0f, side * (longEars ? -58f : -30f));
            Shape(earGroup, "Ear Tip", PrimitiveType.Capsule, pos, size, rot, skin, "skin");
        }
    }

    private void ClearPlaceholder(AppearancePart part)
    {
        if (part == AppearancePart.Body)
            return;
        if (part == AppearancePart.Head)
        {
            ResetHeadShape();
            return;
        }
        if (!placeholderGroups.TryGetValue(part, out Transform g) || g == null)
            return;
        for (int i = g.childCount - 1; i >= 0; i--)
            DestroyObj(g.GetChild(i).gameObject);
    }

    private void BuildPlaceholder(AppearancePart part)
    {
        if (part == AppearancePart.Body)
            return; // the default body: the model's own, or the placeholder body
        if (part == AppearancePart.Head)
        {
            ApplyHeadShape(current.Style(part));
            return;
        }
        Transform g = Group(part);
        if (g == null) return;
        ClearPlaceholder(part);
        int i = current.Style(part);
        Color hair = current.hairColor;
        switch (part)
        {
            case AppearancePart.Hair:
                switch (i)
                {
                    case 0: break; // bald
                    case 1: AddMesh(g, "Hair", AppearanceMeshes.HairCap("short", 0.05f), hair, "hair"); break;
                    case 2:
                        AddMesh(g, "Hair", AppearanceMeshes.HairCap("long", 0.055f), hair, "hair");
                        AddMesh(g, "Long", AppearanceMeshes.HairBack("long", -0.95f, 0.06f), hair, "hair");
                        break;
                    case 3:
                        AddMesh(g, "Hair", AppearanceMeshes.HairCap("tied", 0.045f), hair, "hair");
                        Shape(g, "Tie", PrimitiveType.Sphere, new Vector3(0f, 0.12f, -0.55f), Vector3.one * 0.17f, Vector3.zero, hair * 0.8f, "hair");
                        Shape(g, "Tail", PrimitiveType.Capsule, new Vector3(0f, -0.22f, -0.6f), new Vector3(0.15f, 0.34f, 0.15f), new Vector3(-14f, 0f, 0f), hair, "hair");
                        break;
                    case 4:
                        AddMesh(g, "Sides", AppearanceMeshes.HairCap("buzz", 0.012f), Color.Lerp(hair, current.skinColor, 0.4f), "hair");
                        Shape(g, "Crest", PrimitiveType.Capsule, new Vector3(0f, 0.44f, -0.08f), new Vector3(0.12f, 0.5f, 0.28f), new Vector3(90f, 0f, 0f), hair, "hair");
                        break;
                    case 5:
                        AddMesh(g, "Hair", AppearanceMeshes.HairCap("bun", 0.045f), hair, "hair");
                        Shape(g, "Bun", PrimitiveType.Sphere, new Vector3(0f, 0.42f, -0.36f), Vector3.one * 0.32f, Vector3.zero, hair, "hair");
                        break;
                    case 6:
                        AddMesh(g, "Hair", AppearanceMeshes.HairCap("curly", 0.08f, 4f,
                            (t, p) => 0.035f * (0.5f + 0.5f * Mathf.Sin(t * 0.21f) * Mathf.Sin(p * 0.35f))), hair, "hair");
                        break;
                    default:
                        AddMesh(g, "Hair", AppearanceMeshes.HairCap("buzz", 0.012f), hair, "hair");
                        break;
                }
                break;

            case AppearancePart.Beard:
                if (i == 0 || (current.bodyType == CharacterBodyType.Female && !rules.beardOnAnyBody)) break;
                switch (i)
                {
                    case 1: AddMesh(g, "Stubble", AppearanceMeshes.Beard("stubble", 0.012f, 0f), Color.Lerp(hair, current.skinColor, 0.45f), "hair"); break;
                    case 2:
                        AddMesh(g, "Goatee", AppearanceMeshes.Goatee(), hair, "hair");
                        AddMesh(g, "Moustache", AppearanceMeshes.Moustache(false), hair, "hair");
                        break;
                    case 3:
                        AddMesh(g, "Beard", AppearanceMeshes.Beard("full", 0.06f, 0.04f), hair, "hair");
                        AddMesh(g, "Moustache", AppearanceMeshes.Moustache(true), hair, "hair");
                        break;
                    case 4: AddMesh(g, "Moustache", AppearanceMeshes.Moustache(true), hair, "hair"); break;
                    default:
                        AddMesh(g, "Beard", AppearanceMeshes.Beard("full", 0.06f, 0.04f), hair, "hair");
                        AddMesh(g, "Moustache", AppearanceMeshes.Moustache(true), hair, "hair");
                        AddMesh(g, "Long", AppearanceMeshes.BeardHang("long", -1.05f), hair, "hair");
                        break;
                }
                break;

            case AppearancePart.Eyebrows:
                if (i >= BrowNames.Length - 1) break; // none
                AddMesh(g, "Brow L", AppearanceMeshes.Eyebrow(i, true), hair * 0.85f, "hair");
                AddMesh(g, "Brow R", AppearanceMeshes.Eyebrow(i, false), hair * 0.85f, "hair");
                break;

            case AppearancePart.Eyes:
            {
                Vector3 eye;
                float spread = 0.17f;
                switch (i)
                {
                    case 1: eye = new Vector3(0.15f, 0.09f, 0.08f); break;
                    case 2: eye = new Vector3(0.15f, 0.05f, 0.07f); break;
                    case 3: eye = new Vector3(0.13f, 0.11f, 0.08f); spread = 0.21f; break;
                    case 4: eye = new Vector3(0.17f, 0.16f, 0.1f); break;
                    default: eye = new Vector3(0.12f, 0.12f, 0.08f); break;
                }
                eye *= Mathf.Lerp(0.75f, 1.3f, current.eyeSize);
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = new Vector3(side * spread, 0.04f, 0.44f);
                    Shape(g, "Eye", PrimitiveType.Sphere, p, eye, Vector3.zero, new Color(0.95f, 0.95f, 0.93f), "eyewhite");
                    Shape(g, "Iris", PrimitiveType.Sphere, p + new Vector3(0f, 0f, eye.z * 0.42f), new Vector3(eye.x * 0.55f, eye.y * 0.8f, eye.z * 0.4f), Vector3.zero, current.eyeColor, "eye");
                    Shape(g, "Pupil", PrimitiveType.Sphere, p + new Vector3(0f, 0f, eye.z * 0.6f), new Vector3(eye.x * 0.25f, eye.y * 0.36f, eye.z * 0.2f), Vector3.zero, new Color(0.03f, 0.03f, 0.03f), "eye");
                    // Upper lid in skin colour: the eye looks set in the face instead of stuck on it.
                    Shape(g, "Lid", PrimitiveType.Sphere, p + new Vector3(0f, eye.y * 0.22f, -eye.z * 0.05f), new Vector3(eye.x * 1.12f, eye.y * 0.62f, eye.z * 1.05f), Vector3.zero, current.skinColor * 0.97f, "skin");
                    if (current.bodyType == CharacterBodyType.Female)
                        Shape(g, "Lashes", PrimitiveType.Cube, p + new Vector3(side * eye.x * 0.35f, eye.y * 0.4f, eye.z * 0.3f), new Vector3(eye.x * 0.55f, 0.018f, 0.03f), new Vector3(0f, 0f, side * 18f), new Color(0.05f, 0.04f, 0.04f), "lash");
                }
                break;
            }

            case AppearancePart.Nose:
            {
                float k = Mathf.Lerp(0.75f, 1.35f, current.noseSize);
                Color skin = current.skinColor * 0.96f;
                skin.a = 1f;
                var p = new Vector3(0f, -0.06f, 0.47f);
                switch (i)
                {
                    case 1: Shape(g, "Nose", PrimitiveType.Sphere, p, new Vector3(0.12f, 0.12f, 0.12f) * k, Vector3.zero, skin, "skin"); break;
                    case 2: Shape(g, "Nose", PrimitiveType.Capsule, p, new Vector3(0.17f, 0.11f, 0.13f) * k, new Vector3(0f, 0f, 90f), skin, "skin"); break;
                    case 3: Shape(g, "Nose", PrimitiveType.Capsule, p + new Vector3(0f, -0.02f, 0f), new Vector3(0.09f, 0.16f, 0.12f) * k, new Vector3(-12f, 0f, 0f), skin, "skin"); break;
                    case 4: Shape(g, "Nose", PrimitiveType.Capsule, p + new Vector3(0f, 0f, 0.01f), new Vector3(0.09f, 0.14f, 0.13f) * k, new Vector3(-28f, 0f, 0f), skin, "skin"); break;
                    case 5: Shape(g, "Nose", PrimitiveType.Sphere, p, Vector3.one * 0.085f * k, Vector3.zero, skin, "skin"); break;
                    default: Shape(g, "Nose", PrimitiveType.Capsule, p, new Vector3(0.09f, 0.13f, 0.11f) * k, new Vector3(-14f, 0f, 0f), skin, "skin"); break;
                }
                break;
            }
        }
    }

    private Vector3 headScaleOriginal;
    private Transform scaledHead;

    /// <summary>Head proportions (placeholder Head shapes): the head bone is scaled (humanoid animation never animates scale).</summary>
    private void ApplyHeadShape(int index)
    {
        Transform h = Head();
        if (h == null || h == headAnchor)
            return;
        if (scaledHead != h)
        {
            scaledHead = h;
            headScaleOriginal = h.localScale;
        }
        Vector3 k = HeadShapes[Mathf.Clamp(index, 0, HeadShapes.Length - 1)];
        h.localScale = Vector3.Scale(headScaleOriginal, k);
    }

    private void ResetHeadShape()
    {
        if (scaledHead != null)
            scaledHead.localScale = headScaleOriginal;
        scaledHead = null;
    }

    /// <summary>A shell / curtain mesh of the placeholder parts (head units: the parts' root is one head wide).</summary>
    private void AddMesh(Transform parent, string name, Mesh mesh, Color color, string materialKey)
    {
        if (mesh == null) return;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.sharedMaterial = MaterialFor(materialKey, TemplateMaterial());
        Tint(r, -1, null, color);
    }

    private static Material template;

    /// <summary>The render pipeline's default material (what Unity gives primitives).</summary>
    internal static Material DefaultMaterial() => TemplateMaterial();

    /// <summary>The render pipeline's default material (the one Unity gives primitives).</summary>
    private static Material TemplateMaterial()
    {
        if (template != null) return template;
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        template = probe.GetComponent<MeshRenderer>().sharedMaterial;
        DestroyObj(probe);
        return template;
    }

    private void Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, Color color, string materialKey)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider c = go.GetComponent<Collider>();
        if (c != null) DestroyObj(c);
        go.layer = parent.gameObject.layer;
        Transform t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.sharedMaterial = MaterialFor(materialKey, r.sharedMaterial);
        Tint(r, -1, null, color); // one material per kind of part; the colour goes in a property block
    }

    private Material MaterialFor(string key, Material template)
    {
        if (materials.TryGetValue(key, out Material m) && m != null)
            return m;
        Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
        if (fallback == null) fallback = Shader.Find("Standard");
        m = template != null ? new Material(template) : new Material(fallback);
        m.name = "Appearance " + key;
        m.hideFlags = HideFlags.DontSave;
        float smooth = key == "eye" || key == "eyewhite" ? 0.85f : key == "hair" ? 0.25f : 0.35f;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        materials[key] = m;
        return m;
    }

    private static void DestroyObj(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    [ContextMenu("Preview Random Appearance")]
    private void PreviewRandom() => Apply(CharacterAppearanceData.Random(this));

    [ContextMenu("Remove Placeholder Parts")]
    private void RemovePlaceholders()
    {
        if (placeholderRoot != null) DestroyObj(placeholderRoot.gameObject);
        placeholderRoot = null;
        placeholderGroups.Clear();
    }
}

/// <summary>Marks the placeholder shapes of a <see cref="CharacterAppearance"/> (left out when the model is measured).</summary>
[AddComponentMenu("")]
public class CharacterAppearancePartsMarker : MonoBehaviour { }
