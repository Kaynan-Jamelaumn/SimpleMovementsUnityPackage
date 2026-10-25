using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A turntable for the creation and character select screens: a look-only copy of the player model (every script,
/// collider and network part removed; Animator, renderers and the <see cref="CharacterAppearance"/> kept), lit and
/// filmed far below the scene into a texture shown by a RawImage. Drag the picture to turn the model; Body / Face
/// frame it. Height and appearance changes show at once.
/// </summary>
public class CharacterPreviewStage : MonoBehaviour
{
    private static readonly Vector3 StagePosition = new Vector3(0f, -5000f, 0f);

    private Camera cam;
    private RenderTexture texture;
    private Transform pivot;
    private GameObject model;
    private CharacterAppearance appearance;
    private float modelHeight = 1.8f;
    private float baseScale = 1f;
    private float feetOffset;
    private float yaw;
    private float targetYaw;
    private float zoom; // 0 = whole body, 1 = face
    private float targetZoom;
    private Transform head;

    public CharacterAppearance Appearance => appearance;
    public RenderTexture Texture => texture;
    public bool HasModel => model != null;

    /// <summary>Builds a stage showing a copy of <paramref name="source"/> (a player prefab or a scene player).</summary>
    public static CharacterPreviewStage Create(GameObject source, int width = 900, int height = 1100)
    {
        var go = new GameObject("Character Preview Stage");
        go.transform.position = StagePosition;
        var stage = go.AddComponent<CharacterPreviewStage>();
        stage.Build(source, width, height);
        return stage;
    }

    private void Build(GameObject source, int width, int height)
    {
        texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Character Preview", antiAliasing = 4 };
        texture.Create();

        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(transform, false);

        var camGo = new GameObject("Preview Camera");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.targetTexture = texture;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.095f, 1f);
        cam.fieldOfView = 26f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 60f;
        cam.depth = -50f;
        cam.allowHDR = false;

        Light key = NewLight("Key Light", LightType.Directional, new Color(1f, 0.96f, 0.9f), 1.15f);
        key.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
        Light rim = NewLight("Rim Light", LightType.Point, new Color(0.55f, 0.7f, 1f), 2.2f);
        rim.range = 6f;
        rim.transform.localPosition = new Vector3(-1.4f, 2.2f, -1.6f);
        Light fill = NewLight("Fill Light", LightType.Point, new Color(1f, 0.85f, 0.75f), 1.2f);
        fill.range = 6f;
        fill.transform.localPosition = new Vector3(1.6f, 1.2f, 2.2f);

        // A soft floor disc so the model stands on something.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Floor";
        Destroy(floor.GetComponent<Collider>());
        floor.transform.SetParent(transform, false);
        floor.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
        var fr = floor.GetComponent<MeshRenderer>();
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.19f));
        block.SetColor("_Color", new Color(0.12f, 0.14f, 0.19f));
        fr.SetPropertyBlock(block);

        SetModel(source);
    }

    private Light NewLight(string name, LightType type, Color color, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    /// <summary>Replaces the model with a look-only copy of <paramref name="source"/>.</summary>
    public void SetModel(GameObject source)
    {
        if (model != null)
            Destroy(model);
        model = null;
        appearance = null;
        head = null;
        if (source == null)
            return;

        // Copy under an inactive holder: no Awake runs, so no gameplay script ever starts on the copy.
        var holder = new GameObject("Holder");
        holder.SetActive(false);
        holder.transform.SetParent(pivot, false);
        GameObject copy = Instantiate(source, holder.transform, false);
        copy.name = "Preview Model";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        // A copy of a live character: its generated body goes, the copy builds its own (on its own bones).
        foreach (ProceduralBody pb in copy.GetComponentsInChildren<ProceduralBody>(true))
            if (pb != null) DestroyImmediate(pb.gameObject);
        Strip(copy);
        copy.transform.SetParent(pivot, false);
        Destroy(holder);
        model = copy;
        SetLayer(model, gameObject.layer);
        model.SetActive(true);

        // The look first: a model without a body gets its placeholder body (and its humanoid skeleton) here.
        appearance = CharacterAppearance.For(model);
        if (appearance != null)
            appearance.EnsureBody();
        SetLayer(model, gameObject.layer);
        Animator anim = model.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (anim.isHuman && anim.avatar != null)
                head = anim.GetBoneTransform(HumanBodyBones.Head);
            // Stand still on the ground: no script drives the copy's animator.
            if (anim.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter p in anim.parameters)
                {
                    string n = p.name.ToLowerInvariant();
                    if (p.type == AnimatorControllerParameterType.Bool && n.Contains("ground"))
                        anim.SetBool(p.nameHash, true);
                    else if (p.type == AnimatorControllerParameterType.Bool && (n.Contains("fall") || n.Contains("jump") || n.Contains("swim") || n.Contains("dead") || n.Contains("crouch")))
                        anim.SetBool(p.nameHash, false);
                }
            }
        }
        Measure();
    }

    /// <summary>Everything but the look goes: scripts, colliders, physics, cameras, UI, audio, network parts.</summary>
    private static void Strip(GameObject root)
    {
        // Effects first (a particle system and its renderer must go together), and name tags / 3D texts (their
        // mesh renderers would stay behind as frozen text).
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;
            if (ps.gameObject != root) DestroyImmediate(ps.gameObject);
        }
        foreach (TMPro.TMP_Text t in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (t == null) continue;
            if (t.gameObject != root) DestroyImmediate(t.gameObject);
        }
        var all = new List<Component>(root.GetComponentsInChildren<Component>(true));
        // Several passes: a component another one requires can only go once its dependants are gone.
        for (int pass = 0; pass < 6 && all.Count > 0; pass++)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Component c = all[i];
                if (c == null) { all.RemoveAt(i); continue; }
                if (Keep(c)) { all.RemoveAt(i); continue; }
                if (IsRequiredByOther(c)) continue;
                DestroyImmediate(c);
                all.RemoveAt(i);
            }
        }
        // UI objects and cameras rigs are gone now; drop empty canvases / particle objects that are left.
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t != root.transform && t is RectTransform)
                DestroyImmediate(t.gameObject);
    }

    private static bool Keep(Component c)
    {
        if (c is Transform || c is Animator || c is MeshFilter || c is LODGroup)
            return true;
        if (c is ParticleSystem || c is ParticleSystemRenderer)
            return true; // only on the root (children with effects were removed); harmless there
        if (c is SkinnedMeshRenderer || c is MeshRenderer)
            return true;
        if (c is CharacterAppearance)
            return true;
        return false;
    }

    private static bool IsRequiredByOther(Component c)
    {
        Type type = c.GetType();
        foreach (Component other in c.GetComponents<Component>())
        {
            if (other == null || other == c || Keep(other))
                continue;
            foreach (object attr in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                var req = (RequireComponent)attr;
                if ((req.m_Type0 != null && req.m_Type0.IsAssignableFrom(type)) ||
                    (req.m_Type1 != null && req.m_Type1.IsAssignableFrom(type)) ||
                    (req.m_Type2 != null && req.m_Type2.IsAssignableFrom(type)))
                    return true;
            }
        }
        return false;
    }

    private static void SetLayer(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private void Measure()
    {
        if (model == null)
            return;
        model.transform.localScale = Vector3.one;
        Bounds b = Bounds();
        modelHeight = Mathf.Max(0.3f, b.size.y);
        baseScale = 1f;
        // Stand on the floor. A humanoid that animates stands on its Animator's position, whatever its meshes' pose.
        Animator anim = model.GetComponentInChildren<Animator>(true);
        bool animated = anim != null && anim.isActiveAndEnabled && anim.isHuman && anim.runtimeAnimatorController != null;
        feetOffset = animated ? anim.transform.position.y - pivot.position.y : b.min.y - pivot.position.y;
        model.transform.localPosition = new Vector3(0f, -feetOffset, 0f);
    }

    private Bounds Bounds()
    {
        bool any = false;
        var b = new Bounds(pivot.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
        if (model == null) return b;
        ProceduralBody body = appearance != null ? appearance.Body : null;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (body != null && r.transform.IsChildOf(body.transform)) continue; // skinned bounds are only right once drawn
            if (r.GetComponentInParent<CharacterAppearancePartsMarker>() != null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (body != null)
        {
            if (!any) { b = body.WorldBounds(); any = true; }
            else b.Encapsulate(body.WorldBounds());
        }
        return b;
    }

    /// <summary>Shows <paramref name="data"/> on the model.</summary>
    public void ShowAppearance(CharacterAppearanceData data)
    {
        if (appearance != null && data != null)
            appearance.Apply(data);
    }

    private float shownHeightScale = 1f;
    private float shownWidth = 1f;

    /// <summary>Scales the model to <paramref name="metres"/> (0 = as modelled).</summary>
    public void ShowHeight(float metres)
    {
        shownHeightScale = metres > 0f ? Mathf.Clamp(metres / modelHeight, 0.3f, 3f) : 1f;
        ApplySize();
    }

    /// <summary>The race's build: width and depth on top of the height (1 = as modelled).</summary>
    public void ShowBuild(float width)
    {
        shownWidth = Mathf.Clamp(width, 0.6f, 1.6f);
        ApplySize();
    }

    /// <summary>The race's appearance rules (ears, beards, blend shapes, parts) and build.</summary>
    public void ShowRules(AppearanceRules rules)
    {
        if (appearance != null)
            appearance.SetRules(rules);
        ShowBuild(rules != null ? rules.bodyWidth : 1f);
    }

    private void ApplySize()
    {
        if (model == null)
            return;
        float s = baseScale * shownHeightScale;
        model.transform.localScale = new Vector3(s * shownWidth, s, s * shownWidth);
        model.transform.localPosition = new Vector3(0f, -feetOffset * shownHeightScale, 0f); // the feet stay on the floor
    }

    /// <summary>0 = whole body, 1 = the face.</summary>
    public void Frame(float faceAmount) => targetZoom = Mathf.Clamp01(faceAmount);

    public void Rotate(float degrees) => targetYaw += degrees;

    public void ResetView()
    {
        targetYaw = 0f;
        targetZoom = 0f;
    }

    private void LateUpdate()
    {
        if (cam == null)
            return;
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        float scale = model != null ? model.transform.localScale.y : 1f;
        float h = modelHeight * scale;
        Vector3 bodyTarget = pivot.position + Vector3.up * (h * 0.52f);
        Vector3 faceTarget = head != null ? head.position + Vector3.up * (h * 0.06f) : pivot.position + Vector3.up * (h * 0.9f);
        Vector3 target = Vector3.Lerp(bodyTarget, faceTarget, zoom);
        float bodyDist = h * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.18f;
        float faceDist = Mathf.Max(0.55f, h * 0.36f);
        float dist = Mathf.Lerp(bodyDist, faceDist, zoom);
        cam.transform.position = target + new Vector3(0f, h * 0.04f * (1f - zoom), dist);
        cam.transform.LookAt(target);
    }

    private void OnDestroy()
    {
        if (cam != null)
            cam.targetTexture = null;
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    // ------------------------------------------------------------------ the model shown by default
    /// <summary>
    /// The model the screens show: the catalog's Preview Model, else the Net Player Spawner's prefab, else a player in
    /// the open scenes. Null when there is none (the picture then says so).
    /// </summary>
    public static GameObject DefaultSource()
    {
        CharacterCatalog catalog = CharacterCatalog.Instance;
        if (catalog != null && catalog.previewModel != null)
            return catalog.previewModel;
        GameObject fromSpawner = SpawnerPrefab();
        if (fromSpawner != null)
            return fromSpawner;
        PlayerStatusController scenePlayer = FindAnyObjectByType<PlayerStatusController>(FindObjectsInactive.Include);
        return scenePlayer != null ? CharacterProfileApplier.RootOf(scenePlayer) : null;
    }

    private static GameObject SpawnerPrefab()
    {
#if FISHNET
        GameNet.NetPlayerSpawner spawner = FindAnyObjectByType<GameNet.NetPlayerSpawner>(FindObjectsInactive.Include);
        if (spawner != null && spawner.playerPrefab != null)
            return spawner.playerPrefab.gameObject;
#endif
        return null;
    }
}

