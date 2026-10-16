using System.Collections.Generic;
using UnityEngine;

/// <summary>When <see cref="CharacterAppearance"/> builds a <see cref="ProceduralBody"/>.</summary>
public enum PlaceholderBodyMode
{
    /// <summary>Only when the model has no body of its own (no skinned mesh: an empty model, a capsule).</summary>
    Auto,
    /// <summary>Always (the model's own meshes stay as they are).</summary>
    Always,
    /// <summary>Never.</summary>
    Off,
}

/// <summary>
/// A whole body for a model that has none, so a new character is a person instead of a floating head: torso, pelvis,
/// neck, arms, hands, legs and booted feet, skinned to a humanoid skeleton, with a male and a female shape, skin,
/// shirt and trouser colours and a "Muscular" blend shape (races with Blend Shapes ▸ Muscular use it).
/// <para>The skeleton: the Animator's own humanoid bones when it has them, else a skeleton built here (Hips, Spine,
/// Chest, Upper Chest, Neck, Head, shoulders, arms, hands, legs, feet, toes) with a Humanoid Avatar made at run time
/// and given to the Animator — so the character's humanoid animations (idle, walk, run, attacks) play on it, worn
/// armour can be skinned or fitted to it, weapons go in its hands.</para>
/// Built by <see cref="CharacterAppearance"/> (Placeholder Body); nothing to set up. Replace it with a real model
/// whenever you have one: a model with skinned meshes never gets it (Auto).
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class ProceduralBody : MonoBehaviour
{
    // ------------------------------------------------------------------ the skeleton
    private const int Hips = 0, Spine = 1, Chest = 2, UpperChest = 3, Neck = 4, Head = 5,
        LShoulder = 6, LUpperArm = 7, LLowerArm = 8, LHand = 9, RShoulder = 10, RUpperArm = 11, RLowerArm = 12, RHand = 13,
        LUpperLeg = 14, LLowerLeg = 15, LFoot = 16, LToes = 17, RUpperLeg = 18, RLowerLeg = 19, RFoot = 20, RToes = 21;

    private static readonly HumanBodyBones[] Human =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
    };

    private static readonly int[] Parent = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 8, 3, 10, 11, 12, 0, 14, 15, 16, 0, 18, 19, 20 };

    /// <summary>The T-pose of a 1.8 m body, feet at 0, facing +Z (its left at -X).</summary>
    private static readonly Vector3[] Pose =
    {
        new Vector3(0f, 0.98f, 0f), new Vector3(0f, 1.08f, 0f), new Vector3(0f, 1.22f, 0f), new Vector3(0f, 1.36f, 0f),
        new Vector3(0f, 1.50f, -0.01f), new Vector3(0f, 1.585f, 0f),
        new Vector3(-0.03f, 1.44f, 0f), new Vector3(-0.18f, 1.44f, 0f), new Vector3(-0.46f, 1.44f, 0f), new Vector3(-0.72f, 1.44f, 0f),
        new Vector3(0.03f, 1.44f, 0f), new Vector3(0.18f, 1.44f, 0f), new Vector3(0.46f, 1.44f, 0f), new Vector3(0.72f, 1.44f, 0f),
        new Vector3(-0.095f, 0.93f, 0f), new Vector3(-0.095f, 0.50f, 0.005f), new Vector3(-0.095f, 0.085f, -0.01f), new Vector3(-0.095f, 0.02f, 0.13f),
        new Vector3(0.095f, 0.93f, 0f), new Vector3(0.095f, 0.50f, 0.005f), new Vector3(0.095f, 0.085f, -0.01f), new Vector3(0.095f, 0.02f, 0.13f),
    };

    /// <summary>Height of the spine joints in <see cref="Pose"/> (Hips to Head), to place the torso on any spine.</summary>
    private static readonly float[] SpineHeights = { 0.98f, 1.08f, 1.22f, 1.36f, 1.50f, 1.585f };
    private static readonly int[] SpineBones = { Hips, Spine, Chest, UpperChest, Neck, Head };

    public const float ReferenceHeight = 1.8f;

    // Colour slots of the meshes.
    private const int SkinSlot = 0, TopSlot = 1, BottomSlot = 2, BootSlot = 3;

    private Animator animator;
    private Avatar avatar;
    private readonly Transform[] boneOf = new Transform[Human.Length]; // the transform each part follows
    private readonly Vector3[] joints = new Vector3[Human.Length];     // bind-pose joint positions (this object's space)
    private Transform[] skinBones;
    private int[] boneMap;
    private Matrix4x4[] bindposes;
    private float k = 1f;
    private float floorY;
    private Material material;
    private MaterialPropertyBlock block;
    private readonly List<Mesh> meshes = new List<Mesh>();
    private readonly List<Renderer> standIns = new List<Renderer>();
    private readonly List<(SkinnedMeshRenderer renderer, BodyRegion region, int[] slots)> parts = new List<(SkinnedMeshRenderer, BodyRegion, int[])>();

    /// <summary>The Animator that moves this body.</summary>
    public Animator Animator => animator;

    /// <summary>The skeleton was built here (and its avatar given to the Animator).</summary>
    public bool OwnsSkeleton { get; private set; }

    /// <summary>The body shape the meshes were built for.</summary>
    public CharacterBodyType BodyType { get; private set; }

    /// <summary>Height in this object's space (scale 1).</summary>
    public float LocalHeight => ReferenceHeight * k;

    /// <summary>Height in metres, now.</summary>
    public float WorldHeight => LocalHeight * Mathf.Abs(transform.lossyScale.y);

    /// <summary>Some of its parts are shown (not all replaced by a body model or hidden).</summary>
    public bool IsShown
    {
        get
        {
            foreach (var p in parts)
                if (p.renderer != null && p.renderer.enabled && p.region != BodyRegion.Neck) return true;
            return false;
        }
    }

    /// <summary>Distance between the shoulder joints in metres, now.</summary>
    public float WorldShoulderWidth => Vector3.Distance(joints[LUpperArm], joints[RUpperArm]) * Mathf.Abs(transform.lossyScale.x);

    /// <summary>The diameter of the head in metres, now (the size of the face parts).</summary>
    public float HeadSize => 0.225f * k * Mathf.Abs(transform.lossyScale.y);

    /// <summary>The head bone (the face parts go on it).</summary>
    public Transform HeadBone => boneOf[Head];

    /// <summary>Every renderer of the body with the region it belongs to (worn armour hides regions).</summary>
    public IEnumerable<(Renderer renderer, BodyRegion region)> Regions()
    {
        foreach (var p in parts)
            if (p.renderer != null) yield return (p.renderer, p.region);
    }

    /// <summary>The space the body fills (feet to the top of the head), in world space.</summary>
    public Bounds WorldBounds()
    {
        float h = LocalHeight;
        var b = new Bounds(transform.TransformPoint(new Vector3(0f, floorY + h * 0.5f, 0f)), Vector3.zero);
        Vector3 half = new Vector3(0.25f * k, h * 0.5f, 0.15f * k);
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) == 0 ? -half.x : half.x, floorY + ((i & 2) == 0 ? 0f : h), (i & 4) == 0 ? -half.z : half.z);
            b.Encapsulate(transform.TransformPoint(c));
        }
        return b;
    }

    // ------------------------------------------------------------------ build
    /// <summary>
    /// Builds a body under <paramref name="parent"/> (the Animator's object; null Animator = a new one on the body).
    /// <paramref name="localHeight"/> and <paramref name="feetY"/> are in <paramref name="parent"/>'s space.
    /// </summary>
    public static ProceduralBody Create(Transform parent, Animator animator, float localHeight, float feetY, CharacterBodyType bodyType)
    {
        // On an existing skeleton the body lives beside it (under the model), so it scales with the model.
        Transform hips = animator != null && animator.isHuman && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (hips != null && hips.parent != null && animator.GetBoneTransform(HumanBodyBones.Head) != null)
            parent = hips.parent;
        var go = new GameObject("Placeholder Body");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var body = go.AddComponent<ProceduralBody>();
        body.k = Mathf.Clamp(localHeight, 0.3f, 6f) / ReferenceHeight;
        body.floorY = feetY;
        if (animator == null)
        {
            animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
        }
        body.animator = animator;
        body.BuildSkeleton();
        body.HideStandIns();
        body.BodyType = bodyType;
        body.BuildMeshes();
        body.WakeAnimator();
        return body;
    }

    /// <summary>
    /// The Animator listed its renderers when it started, before this body existed: with a culling mode and the model's
    /// own meshes switched off it saw nothing visible and never moved the bones (the body stood in its bind pose).
    /// It is rebound (it now knows the body) and set to always animate.
    /// </summary>
    private void WakeAnimator()
    {
        if (animator == null)
            return;
        if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); // now that the body's renderers exist
        if (animator.runtimeAnimatorController == null)
            Debug.LogWarning($"[Appearance] '{animator.name}' has no Animator Controller: the placeholder body stands still.", animator);
    }

    private void BuildSkeleton()
    {
        // The Animator's own humanoid skeleton, when it has one.
        if (animator.isHuman && animator.avatar != null && animator.GetBoneTransform(HumanBodyBones.Hips) != null &&
            animator.GetBoneTransform(HumanBodyBones.Head) != null)
        {
            UseExistingSkeleton();
            return;
        }

        OwnsSkeleton = true;
        var created = new Transform[Human.Length];
        for (int i = 0; i < Human.Length; i++)
        {
            joints[i] = Pose[i] * k + new Vector3(0f, floorY, 0f);
            var t = new GameObject("Body " + Human[i]).transform;
            t.gameObject.layer = gameObject.layer;
            int p = Parent[i];
            t.SetParent(p < 0 ? transform : created[p], false);
            t.localPosition = p < 0 ? joints[i] : joints[i] - joints[p];
            t.localRotation = Quaternion.identity;
            created[i] = t;
            boneOf[i] = t;
        }

        // A Humanoid Avatar for these bones: the Animator's humanoid clips play on it.
        Transform root = animator.transform;
        var human = new List<HumanBone>();
        var skeleton = new List<SkeletonBone>
        {
            new SkeletonBone { name = root.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one },
        };
        if (root != transform)
            skeleton.Add(Skeleton(transform));
        for (int i = 0; i < Human.Length; i++)
        {
            skeleton.Add(Skeleton(created[i]));
            int h = (int)Human[i];
            if (h < 0 || h >= HumanTrait.BoneName.Length) continue;
            var hb = new HumanBone { humanName = HumanTrait.BoneName[h], boneName = created[i].name };
            hb.limit.useDefaultValues = true;
            human.Add(hb);
        }
        var description = new HumanDescription
        {
            human = human.ToArray(),
            skeleton = skeleton.ToArray(),
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false,
        };
        avatar = AvatarBuilder.BuildHumanAvatar(root.gameObject, description);
        if (avatar != null && avatar.isValid && avatar.isHuman)
        {
            avatar.name = "Placeholder Body Avatar";
            animator.avatar = avatar;
            animator.Rebind();
        }
        else
        {
            Debug.LogWarning("[Appearance] The placeholder body's humanoid avatar could not be built: the body is shown without animation.", this);
            if (avatar != null) Destroy(avatar);
            avatar = null;
        }
    }

    private static SkeletonBone Skeleton(Transform t) =>
        new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };

    /// <summary>The model's humanoid bones as they are now; missing optional bones are placed between their neighbours.</summary>
    private void UseExistingSkeleton()
    {
        OwnsSkeleton = false;
        for (int i = 0; i < Human.Length; i++)
            boneOf[i] = animator.GetBoneTransform(Human[i]);
        for (int i = 0; i < Human.Length; i++)
            if (boneOf[i] != null) joints[i] = transform.InverseTransformPoint(boneOf[i].position);
        Vector3 J(int i) => joints[i];
        bool Has(int i) => boneOf[i] != null;
        if (!Has(Spine)) joints[Spine] = Vector3.Lerp(J(Hips), Has(Chest) ? J(Chest) : J(Head), 0.3f);
        if (!Has(Chest)) joints[Chest] = Vector3.Lerp(J(Spine), J(Head), 0.3f);
        if (!Has(Neck)) joints[Neck] = Vector3.Lerp(J(Chest), J(Head), 0.7f);
        if (!Has(UpperChest)) joints[UpperChest] = Vector3.Lerp(J(Chest), J(Neck), 0.5f);
        foreach (int side in new[] { 0, 1 })
        {
            int sh = side == 0 ? LShoulder : RShoulder, ua = sh + 1, la = sh + 2, hand = sh + 3;
            int ul = side == 0 ? LUpperLeg : RUpperLeg, ll = ul + 1, foot = ul + 2, toes = ul + 3;
            if (!Has(ua)) joints[ua] = J(UpperChest) + Vector3.right * (side == 0 ? -0.18f : 0.18f);
            if (!Has(sh)) joints[sh] = Vector3.Lerp(J(UpperChest), J(ua), 0.2f);
            if (!Has(la)) joints[la] = J(ua) + (J(ua) - J(sh)).normalized * 0.28f;
            if (!Has(hand)) joints[hand] = J(la) + (J(la) - J(ua)).normalized * 0.26f;
            if (!Has(ul)) joints[ul] = J(Hips) + Vector3.right * (side == 0 ? -0.095f : 0.095f);
            if (!Has(ll)) joints[ll] = J(ul) + Vector3.down * 0.43f;
            if (!Has(foot)) joints[foot] = J(ll) + Vector3.down * 0.41f;
            if (!Has(toes)) joints[toes] = J(foot) + new Vector3(0f, -0.065f, 0.14f);
        }
        // A missing bone's part follows its nearest existing parent.
        for (int i = 0; i < Human.Length; i++)
            if (boneOf[i] == null)
            {
                int p = Parent[i];
                while (p >= 0 && animator.GetBoneTransform(Human[p]) == null) p = Parent[p];
                boneOf[i] = p >= 0 ? animator.GetBoneTransform(Human[p]) : animator.transform;
            }
        float footY = Mathf.Min(J(LFoot).y, J(RFoot).y);
        k = Mathf.Clamp((J(Head).y - footY) / (Pose[Head].y - Pose[LFoot].y), 0.2f, 4f);
        floorY = footY - Pose[LFoot].y * k;
    }

    /// <summary>The look's body shape: rebuilds the meshes (same skeleton) when it changed.</summary>
    public void SetBodyType(CharacterBodyType type)
    {
        if (type == BodyType && parts.Count > 0)
            return;
        BodyType = type;
        BuildMeshes();
    }

    /// <summary>Skin, shirt and trouser colours (boots are leather).</summary>
    public void SetColors(Color skin, Color top, Color bottom)
    {
        if (block == null) block = new MaterialPropertyBlock();
        Color boots = new Color(0.24f, 0.17f, 0.12f);
        foreach (var p in parts)
        {
            if (p.renderer == null) continue;
            for (int i = 0; i < p.slots.Length; i++)
            {
                Color c = p.slots[i] == SkinSlot ? skin : p.slots[i] == TopSlot ? top : p.slots[i] == BottomSlot ? bottom : boots;
                p.renderer.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                p.renderer.SetPropertyBlock(block, i);
            }
        }
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Primitive stand-ins (the capsule of a test character) go hidden while the body is shown.</summary>
    private void HideStandIns()
    {
        CharacterController cc = GetComponentInParent<CharacterController>();
        Transform root = cc != null ? cc.transform : animator.transform;
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || !r.enabled || r.transform.IsChildOf(transform)) continue;
            MeshFilter f = r.GetComponent<MeshFilter>();
            string n = f != null && f.sharedMesh != null ? f.sharedMesh.name : "";
            if (n == "Capsule" || n == "Cylinder" || n.StartsWith("Capsule ") || n.StartsWith("Cylinder "))
            {
                r.enabled = false;
                standIns.Add(r);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Renderer r in standIns)
            if (r != null) r.enabled = true;
        foreach (Mesh m in meshes)
            if (m != null) Destroy(m);
        if (material != null) Destroy(material);
        if (avatar != null)
        {
            if (animator != null && animator.avatar == avatar) animator.avatar = null;
            Destroy(avatar);
        }
    }

    // ------------------------------------------------------------------ meshes
    private void BuildMeshes()
    {
        if (skinBones == null)
        {
            // Every part shares one bone list and its bind poses (taken once, while the skeleton is in its bind pose).
            var list = new List<Transform>();
            boneMap = new int[Human.Length];
            for (int i = 0; i < Human.Length; i++)
            {
                int found = list.IndexOf(boneOf[i]);
                if (found < 0) { found = list.Count; list.Add(boneOf[i]); }
                boneMap[i] = found;
            }
            skinBones = list.ToArray();
            bindposes = new Matrix4x4[skinBones.Length];
            for (int i = 0; i < skinBones.Length; i++)
                bindposes[i] = skinBones[i].worldToLocalMatrix * transform.localToWorldMatrix;
        }
        if (material == null)
        {
            material = new Material(CharacterAppearance.DefaultMaterial()) { name = "Placeholder Body", hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.3f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.3f);
        }
        bool female = BodyType == CharacterBodyType.Female;

        var pelvis = new Part(); BuildPelvis(pelvis, female);
        var torso = new Part(); BuildTorso(torso, female);
        var neck = new Part(); BuildNeck(neck, female);
        var arms = new Part(); BuildArm(arms, true, female); BuildArm(arms, false, female);
        var hands = new Part(); BuildHand(hands, true, female); BuildHand(hands, false, female);
        var legs = new Part(); BuildLeg(legs, true, female); BuildLeg(legs, false, female);
        var feet = new Part(); BuildFoot(feet, true, female); BuildFoot(feet, false, female);

        foreach (Mesh m in meshes)
            if (m != null) Destroy(m);
        meshes.Clear();
        int at = 0;
        Set(at++, "Pelvis", pelvis, BodyRegion.Legs);
        Set(at++, "Torso", torso, BodyRegion.Torso);
        Set(at++, "Neck", neck, BodyRegion.Neck);
        Set(at++, "Arms", arms, BodyRegion.Arms);
        Set(at++, "Hands", hands, BodyRegion.Hands);
        Set(at++, "Legs", legs, BodyRegion.Legs);
        Set(at++, "Feet", feet, BodyRegion.Feet);
    }

    private void Set(int index, string name, Part part, BodyRegion region)
    {
        Mesh mesh = part.ToMesh("Placeholder " + name, boneMap, bindposes, out int[] slots);
        meshes.Add(mesh);
        SkinnedMeshRenderer smr;
        if (index < parts.Count && parts[index].renderer != null)
            smr = parts[index].renderer;
        else
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.bones = skinBones;
            smr.rootBone = skinBones[boneMap[Hips]];
            smr.updateWhenOffscreen = true;
        }
        smr.sharedMesh = mesh;
        var mats = new Material[slots.Length];
        for (int i = 0; i < mats.Length; i++) mats[i] = material;
        smr.sharedMaterials = mats;
        if (index < parts.Count) parts[index] = (smr, region, slots);
        else parts.Add((smr, region, slots));
    }

    // Spine positions by the height they have in the reference pose (any spine: straight or not, any proportions).
    private Vector3 SpinePoint(float y, out Vector3 axis)
    {
        int last = SpineHeights.Length - 1;
        int i = 0;
        while (i < last - 1 && y > SpineHeights[i + 1]) i++;
        Vector3 a = joints[SpineBones[i]], b = joints[SpineBones[i + 1]];
        axis = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Vector3.up;
        float t = (y - SpineHeights[i]) / (SpineHeights[i + 1] - SpineHeights[i]);
        return a + (b - a) * t; // t beyond 0..1 continues the end segments (below the hips, above the head joint)
    }

    /// <summary>Spine weights by reference height: each bone strongest at the middle of its segment.</summary>
    private static W SpineWeight(float y)
    {
        int n = SpineHeights.Length;
        float Mid(int i) => i < n - 1 ? (SpineHeights[i] + SpineHeights[i + 1]) * 0.5f : SpineHeights[i] + 0.1f;
        if (y <= Mid(0)) return W.One(SpineBones[0]);
        for (int i = 0; i < n - 1; i++)
            if (y <= Mid(i + 1))
                return W.Blend(SpineBones[i], SpineBones[i + 1], Mathf.InverseLerp(Mid(i), Mid(i + 1), y));
        return W.One(SpineBones[n - 1]);
    }

    private static readonly Vector3 Fwd = Vector3.forward, Up = Vector3.up, Right = Vector3.right;

    // (reference height, half width, front, back, forward shift, muscle)
    private static readonly float[,] PelvisMale =
    {
        { 0.85f, 0.07f, 0.06f, 0.06f, 0f, 0f }, { 0.885f, 0.14f, 0.09f, 0.10f, -0.01f, 0.03f }, { 0.94f, 0.163f, 0.10f, 0.115f, -0.01f, 0.05f },
        { 1.0f, 0.158f, 0.095f, 0.105f, -0.005f, 0.05f }, { 1.06f, 0.15f, 0.095f, 0.10f, 0f, 0.05f },
    };
    private static readonly float[,] PelvisFemale =
    {
        { 0.85f, 0.075f, 0.065f, 0.065f, 0f, 0f }, { 0.885f, 0.152f, 0.095f, 0.11f, -0.012f, 0.03f }, { 0.94f, 0.177f, 0.105f, 0.125f, -0.015f, 0.04f },
        { 1.0f, 0.166f, 0.095f, 0.11f, -0.008f, 0.04f }, { 1.06f, 0.145f, 0.09f, 0.10f, 0f, 0.04f },
    };
    private static readonly float[,] TorsoMale =
    {
        { 1.035f, 0.162f, 0.10f, 0.108f, 0f, 0.05f }, { 1.12f, 0.15f, 0.101f, 0.097f, 0.005f, 0.1f }, { 1.2f, 0.156f, 0.108f, 0.096f, 0.01f, 0.12f },
        { 1.28f, 0.168f, 0.118f, 0.098f, 0.012f, 0.16f }, { 1.35f, 0.178f, 0.118f, 0.10f, 0.01f, 0.16f }, { 1.41f, 0.186f, 0.105f, 0.095f, 0.005f, 0.14f },
        { 1.45f, 0.184f, 0.086f, 0.086f, 0f, 0.1f }, { 1.485f, 0.13f, 0.07f, 0.068f, -0.005f, 0.05f },
    };
    private static readonly float[,] TorsoFemale =
    {
        { 1.035f, 0.16f, 0.096f, 0.106f, 0f, 0.03f }, { 1.12f, 0.13f, 0.092f, 0.09f, 0.005f, 0.06f }, { 1.2f, 0.136f, 0.10f, 0.09f, 0.01f, 0.08f },
        { 1.26f, 0.148f, 0.135f, 0.09f, 0.02f, 0.08f }, { 1.32f, 0.155f, 0.13f, 0.092f, 0.02f, 0.08f }, { 1.38f, 0.16f, 0.10f, 0.09f, 0.01f, 0.08f },
        { 1.43f, 0.165f, 0.08f, 0.08f, 0f, 0.06f }, { 1.47f, 0.12f, 0.065f, 0.064f, -0.005f, 0.03f },
    };
    private static readonly float[,] NeckShape =
    {
        { 1.455f, 0.062f, 0.062f, 0.058f, -0.005f, 0.1f }, { 1.50f, 0.058f, 0.058f, 0.055f, 0f, 0.1f },
        { 1.56f, 0.055f, 0.057f, 0.054f, 0.005f, 0.05f }, { 1.625f, 0.052f, 0.054f, 0.05f, 0.012f, 0f },
    };

    private void SpineLoft(Part part, float[,] table, int slot, float scale)
    {
        var rings = new List<Ring>();
        for (int r = 0; r < table.GetLength(0); r++)
        {
            float y = table[r, 0];
            Vector3 c = SpinePoint(y, out Vector3 axis) + Fwd * (table[r, 4] * k);
            rings.Add(new Ring
            {
                c = c, axis = axis, side = Right,
                sPos = table[r, 1] * k * scale, sNeg = table[r, 1] * k * scale,
                uPos = table[r, 2] * k * scale, uNeg = table[r, 3] * k * scale,
                muscle = table[r, 5], w = SpineWeight(y), slot = slot,
            });
        }
        part.Loft(rings, 22, true, true);
    }

    private void BuildPelvis(Part part, bool female) => SpineLoft(part, female ? PelvisFemale : PelvisMale, BottomSlot, 1f);
    private void BuildTorso(Part part, bool female) => SpineLoft(part, female ? TorsoFemale : TorsoMale, TopSlot, 1f);
    private void BuildNeck(Part part, bool female) => SpineLoft(part, NeckShape, SkinSlot, female ? 0.88f : 1f);

    /// <summary>A ring every listed fraction of a chain of joints: (segment, fraction, radius, slot, muscle).</summary>
    private void ChainLoft(Part part, Vector3[] points, int[] bones, float blend, (int seg, float f, float r, int slot, float muscle)[] table,
        float scale, float flatten, bool frontBack)
    {
        var arc = new float[points.Length];
        for (int i = 1; i < points.Length; i++) arc[i] = arc[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        var rings = new List<Ring>();
        foreach (var e in table)
        {
            Vector3 a = points[e.seg], b = points[e.seg + 1];
            Vector3 axis = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Vector3.down;
            float s = Mathf.Lerp(arc[e.seg], arc[e.seg + 1], e.f);
            float r = e.r * k * scale;
            rings.Add(new Ring
            {
                c = Vector3.Lerp(a, b, e.f), axis = axis, side = Fwd,
                sPos = r * (frontBack ? 0.97f : 1f), sNeg = r * (frontBack ? 1.03f : 1f),
                uPos = r * flatten, uNeg = r * flatten,
                muscle = e.muscle, w = W.Chain(s, arc, bones, blend * k), slot = e.slot,
            });
        }
        part.Loft(rings, 16, true, true);
    }

    private void BuildArm(Part part, bool left, bool female)
    {
        int sh = left ? LShoulder : RShoulder, ua = sh + 1, la = sh + 2, hand = sh + 3;
        Vector3 start = joints[ua] + (joints[sh] - joints[ua]).normalized * (0.05f * k);
        var points = new[] { start, joints[ua], joints[la], joints[hand] };
        var bones = new[] { sh, ua, la, hand };
        var table = new (int, float, float, int, float)[]
        {
            (0, 0f, 0.052f, TopSlot, 0.15f), (0, 1f, 0.058f, TopSlot, 0.22f),
            (1, 0.15f, 0.056f, TopSlot, 0.25f), (1, 0.35f, 0.051f, TopSlot, 0.25f), (1, 0.5f, 0.049f, TopSlot, 0.22f),
            (1, 0.505f, 0.045f, SkinSlot, 0.22f), (1, 0.7f, 0.044f, SkinSlot, 0.22f), (1, 0.88f, 0.039f, SkinSlot, 0.15f), (1, 1f, 0.036f, SkinSlot, 0.1f),
            (2, 0.12f, 0.039f, SkinSlot, 0.15f), (2, 0.3f, 0.042f, SkinSlot, 0.18f), (2, 0.55f, 0.037f, SkinSlot, 0.15f),
            (2, 0.8f, 0.031f, SkinSlot, 0.1f), (2, 1f, 0.027f, SkinSlot, 0.05f),
        };
        ChainLoft(part, points, bones, 0.045f, table, female ? 0.86f : 1f, 0.94f, false);
    }

    private void BuildHand(Part part, bool left, bool female)
    {
        int la = left ? LLowerArm : RLowerArm, hand = la + 1;
        Vector3 wrist = joints[hand];
        Vector3 dir = (joints[hand] - joints[la]).sqrMagnitude > 1e-8f ? (joints[hand] - joints[la]).normalized : (left ? Vector3.left : Vector3.right);
        float s = female ? 0.9f : 1f;
        // (distance from the wrist, width, thickness): palm, knuckles, fingers (a mitten).
        var table = new[] { (-0.005f, 0.026f, 0.02f), (0.03f, 0.04f, 0.021f), (0.075f, 0.046f, 0.019f), (0.11f, 0.045f, 0.016f), (0.145f, 0.04f, 0.013f), (0.172f, 0.032f, 0.011f) };
        var rings = new List<Ring>();
        foreach (var e in table)
            rings.Add(new Ring
            {
                c = wrist + dir * (e.Item1 * k * s), axis = dir, side = Fwd,
                sPos = e.Item2 * k * s, sNeg = e.Item2 * k * s, uPos = e.Item3 * k * s, uNeg = e.Item3 * k * s,
                muscle = 0.05f, w = e.Item1 < 0f ? W.Blend(la, hand, 0.6f) : W.One(hand), slot = SkinSlot,
            });
        part.Loft(rings, 14, true, true);
        // Thumb, on the front of the hand.
        Vector3 down = Vector3.ProjectOnPlane(Vector3.down, dir).normalized;
        Vector3 p0 = wrist + dir * (0.02f * k * s) + Fwd * (0.028f * k * s) + down * (0.006f * k * s);
        Vector3 p1 = wrist + dir * (0.075f * k * s) + Fwd * (0.062f * k * s) + down * (0.01f * k * s);
        Vector3 ta = (p1 - p0).normalized;
        var thumb = new List<Ring>();
        foreach (var e in new[] { (0f, 0.013f), (0.5f, 0.0125f), (1f, 0.01f) })
            thumb.Add(new Ring
            {
                c = Vector3.Lerp(p0, p1, e.Item1), axis = ta, side = Up,
                sPos = e.Item2 * k * s, sNeg = e.Item2 * k * s, uPos = e.Item2 * k * s, uNeg = e.Item2 * k * s,
                w = W.One(hand), slot = SkinSlot,
            });
        part.Loft(thumb, 10, true, true);
    }

    private void BuildLeg(Part part, bool left, bool female)
    {
        int ul = left ? LUpperLeg : RUpperLeg, ll = ul + 1, foot = ul + 2;
        Vector3 top = joints[ul] + Up * (0.06f * k);
        var points = new[] { top, joints[ul], joints[ll], joints[foot] };
        var bones = new[] { Hips, ul, ll, foot };
        float thigh = female ? 1.05f : 1f, calf = female ? 0.94f : 1f;
        var table = new (int, float, float, int, float)[]
        {
            (0, 0f, 0.08f * thigh, BottomSlot, 0.1f), (0, 1f, 0.088f * thigh, BottomSlot, 0.15f),
            (1, 0.12f, 0.087f * thigh, BottomSlot, 0.2f), (1, 0.3f, 0.08f * thigh, BottomSlot, 0.2f), (1, 0.55f, 0.07f, BottomSlot, 0.2f),
            (1, 0.8f, 0.058f, BottomSlot, 0.15f), (1, 1f, 0.052f, BottomSlot, 0.1f),
            (2, 0.12f, 0.052f * calf, BottomSlot, 0.12f), (2, 0.3f, 0.057f * calf, BottomSlot, 0.18f), (2, 0.5f, 0.053f * calf, BottomSlot, 0.15f),
            (2, 0.75f, 0.043f * calf, BottomSlot, 0.1f), (2, 0.92f, 0.037f * calf, BottomSlot, 0.05f),
        };
        ChainLoft(part, points, bones, 0.05f, table, 1f, 0.98f, true);
    }

    private void BuildFoot(Part part, bool left, bool female)
    {
        int ll = left ? LLowerLeg : RLowerLeg, foot = ll + 1, toes = ll + 2;
        Vector3 ankle = joints[foot];
        float floor = ankle.y - Pose[LFoot].y * k;
        float s = female ? 0.9f : 1f;
        // Boot shaft around the ankle.
        var shaft = new List<Ring>();
        foreach (var e in new[] { (0.16f, 0.052f, 0.3f), (0.09f, 0.054f, 0.6f), (0.03f, 0.054f, 1f) })
            shaft.Add(new Ring
            {
                c = ankle + Up * (e.Item1 * k) - Fwd * (0.008f * k), axis = Vector3.down, side = Fwd,
                sPos = e.Item2 * k * s, sNeg = e.Item2 * k * s, uPos = e.Item2 * k * s, uNeg = e.Item2 * k * s,
                w = W.Blend(ll, foot, e.Item3), slot = BootSlot,
            });
        part.Loft(shaft, 16, true, true);
        // The foot, heel to toe, flat underneath.
        float toesAt = joints[toes].z - ankle.z;
        var arc = new[] { -10f, toesAt };
        var bones = new[] { foot, toes };
        var table = new[]
        {
            (-0.065f, 0.045f, 0.038f, 0.04f, 0.045f), (-0.035f, 0.05f, 0.046f, 0.05f, 0.05f), (0.03f, 0.045f, 0.049f, 0.045f, 0.045f),
            (0.09f, 0.036f, 0.051f, 0.034f, 0.036f), (0.14f, 0.03f, 0.049f, 0.028f, 0.03f), (0.19f, 0.026f, 0.04f, 0.022f, 0.026f),
        };
        var rings = new List<Ring>();
        foreach (var e in table)
        {
            float z = e.Item1 * k * (female ? 0.95f : 1f);
            rings.Add(new Ring
            {
                c = new Vector3(ankle.x, floor + e.Item2 * k, ankle.z + z), axis = Fwd, side = Up,
                sPos = e.Item4 * k, sNeg = e.Item5 * k, uPos = e.Item3 * k * s, uNeg = e.Item3 * k * s,
                w = W.Chain(z, arc, bones, 0.04f * k), slot = BootSlot,
            });
        }
        part.Loft(rings, 16, true, true);
    }

    // ------------------------------------------------------------------ geometry
    /// <summary>Up to two bones (rig indices) and the weight of the second.</summary>
    private struct W
    {
        public int a, b;
        public float wb;

        public static W One(int a) => new W { a = a, b = a, wb = 0f };
        public static W Blend(int a, int b, float t) => new W { a = a, b = b, wb = Mathf.Clamp01(t) };

        /// <summary>Weights along a chain: bone i from arc[i] to arc[i + 1], blended over <paramref name="blend"/> around each joint.</summary>
        public static W Chain(float s, float[] arc, int[] bones, float blend)
        {
            int n = bones.Length;
            int i = n - 1;
            for (int j = 0; j < n - 1; j++)
                if (s < arc[j + 1]) { i = j; break; }
            if (blend > 0f)
            {
                if (i > 0 && s - arc[i] < blend) return Blend(bones[i - 1], bones[i], 0.5f + 0.5f * (s - arc[i]) / blend);
                if (i < n - 1 && arc[i + 1] - s < blend) return Blend(bones[i], bones[i + 1], 0.5f - 0.5f * (arc[i + 1] - s) / blend);
            }
            return One(bones[i]);
        }
    }

    /// <summary>A cross-section: centre, direction of the limb, the side axis; radii on each side of both axes.</summary>
    private struct Ring
    {
        public Vector3 c, axis, side;
        public float sPos, sNeg, uPos, uNeg, muscle;
        public W w;
        public int slot;
    }

    /// <summary>One mesh being built: vertices, the "Muscular" offsets, weights, triangles per colour slot.</summary>
    private sealed class Part
    {
        private readonly List<Vector3> v = new List<Vector3>();
        private readonly List<Vector3> muscle = new List<Vector3>();
        private readonly List<W> w = new List<W>();
        private readonly Dictionary<int, List<int>> tris = new Dictionary<int, List<int>>();

        private int Add(Vector3 p, Vector3 m, W weight)
        {
            v.Add(p);
            muscle.Add(m);
            w.Add(weight);
            return v.Count - 1;
        }

        private void Tri(int slot, int a, int b, int c, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            Vector3 centre = (v[a] + v[b] + v[c]) / 3f;
            if (Vector3.Dot(n, centre - inside) < 0f) { int t = b; b = c; c = t; }
            if (!tris.TryGetValue(slot, out List<int> list)) tris[slot] = list = new List<int>();
            list.Add(a); list.Add(b); list.Add(c);
        }

        private static Ring Scaled(Ring r, float scale, Vector3 shift)
        {
            r.c += shift;
            r.sPos *= scale; r.sNeg *= scale; r.uPos *= scale; r.uNeg *= scale;
            return r;
        }

        /// <summary>A tube through the rings, with rounded ends.</summary>
        public void Loft(List<Ring> rings, int n, bool capStart, bool capEnd)
        {
            if (rings.Count == 0) return;
            var all = new List<Ring>();
            Ring first = rings[0], last = rings[rings.Count - 1];
            float Depth(Ring r) => (r.sPos + r.sNeg + r.uPos + r.uNeg) * 0.25f * 0.85f;
            const int dome = 3;
            if (capStart)
                for (int j = dome; j >= 1; j--)
                {
                    float a = j / (dome + 1f) * Mathf.PI * 0.5f;
                    all.Add(Scaled(first, Mathf.Cos(a), -first.axis * (Mathf.Sin(a) * Depth(first))));
                }
            all.AddRange(rings);
            if (capEnd)
                for (int j = 1; j <= dome; j++)
                {
                    float a = j / (dome + 1f) * Mathf.PI * 0.5f;
                    all.Add(Scaled(last, Mathf.Cos(a), last.axis * (Mathf.Sin(a) * Depth(last))));
                }

            var starts = new int[all.Count];
            for (int r = 0; r < all.Count; r++)
            {
                Ring ring = all[r];
                Vector3 side = Vector3.ProjectOnPlane(ring.side, ring.axis);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.up, ring.axis);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.right, ring.axis);
                side.Normalize();
                Vector3 up = Vector3.Cross(side, ring.axis).normalized;
                starts[r] = v.Count;
                for (int a = 0; a < n; a++)
                {
                    float ang = a * Mathf.PI * 2f / n;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    Vector3 off = side * (cs * (cs >= 0f ? ring.sPos : ring.sNeg)) + up * (sn * (sn >= 0f ? ring.uPos : ring.uNeg));
                    Add(ring.c + off, off * ring.muscle, ring.w);
                }
            }
            for (int r = 0; r + 1 < all.Count; r++)
            {
                Vector3 inside = (all[r].c + all[r + 1].c) * 0.5f;
                int slot = all[r + 1].slot;
                for (int a = 0; a < n; a++)
                {
                    int i0 = starts[r] + a, i1 = starts[r] + (a + 1) % n;
                    int j0 = starts[r + 1] + a, j1 = starts[r + 1] + (a + 1) % n;
                    Tri(slot, i0, j0, i1, inside);
                    Tri(slot, i1, j0, j1, inside);
                }
            }
            if (capStart) Pole(all[0], -first.axis * Depth(first), starts[0], n, first);
            if (capEnd) Pole(all[all.Count - 1], last.axis * Depth(last), starts[all.Count - 1], n, last);
        }

        private void Pole(Ring ring, Vector3 offsetFromBase, int start, int n, Ring baseRing)
        {
            int pole = Add(baseRing.c + offsetFromBase, Vector3.zero, ring.w);
            for (int a = 0; a < n; a++)
                Tri(ring.slot, start + a, start + (a + 1) % n, pole, baseRing.c);
        }

        public Mesh ToMesh(string name, int[] boneMap, Matrix4x4[] bindposes, out int[] slots)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(v);
            var weights = new BoneWeight[w.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                int a = boneMap[w[i].a], b = boneMap[w[i].b];
                float wb = a == b ? 0f : w[i].wb;
                weights[i] = wb > 0.5f
                    ? new BoneWeight { boneIndex0 = b, weight0 = wb, boneIndex1 = a, weight1 = 1f - wb }
                    : new BoneWeight { boneIndex0 = a, weight0 = 1f - wb, boneIndex1 = b, weight1 = wb };
            }
            var keys = new List<int>(tris.Keys);
            keys.Sort();
            slots = keys.ToArray();
            mesh.subMeshCount = Mathf.Max(1, keys.Count);
            for (int i = 0; i < keys.Count; i++)
                mesh.SetTriangles(tris[keys[i]], i);
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            bool any = false;
            foreach (Vector3 m in muscle)
                if (m.sqrMagnitude > 0f) { any = true; break; }
            if (any)
                mesh.AddBlendShapeFrame("Muscular", 100f, muscle.ToArray(), null, null);
            if (slots.Length == 0) slots = new[] { SkinSlot };
            return mesh;
        }
    }
}
