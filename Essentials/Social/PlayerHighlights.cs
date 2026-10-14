using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subtle marks on other players that are bound to you, on this machine's screen: party members, guild and alliance
/// members get a coloured glow along their outline (and party members a faint silhouette through walls), and
/// players in a duel - yours or anyone's - a distinct orange silhouette and a glow on the ground, so a fight is easy
/// to tell apart in a crowd. Each bond has its own <see cref="Style"/> (colour, glow, silhouette through walls,
/// ground glow).
/// <para>The glow and silhouette are extra materials (shader "SimpleMovements/Player Silhouette", in a Resources
/// folder) added to the character's mesh renderers and removed again when the bond ends or the player goes out of
/// range; when the shader is missing the ground glow is used instead. Created next to the <see cref="PlayerTargeting"/>
/// (its Create Interface), never on a dedicated server; put one on the Player Targeting object to change it.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Player Highlights")]
public class PlayerHighlights : MonoBehaviour
{
    /// <summary>How players with one kind of bond are marked.</summary>
    [Serializable]
    public class Style
    {
        [Tooltip("Mark these players at all.")]
        public bool enabled = true;
        [Tooltip("Colour of the marks (alpha = strength).")]
        public Color color = Color.white;
        [Tooltip("A coloured glow along the visible edges of the character.")]
        public bool outlineGlow = true;
        [Tooltip("A flat, faint silhouette where walls and terrain hide the character.")]
        public bool throughWalls;
        [Tooltip("A soft coloured disc on the ground under the character.")]
        public bool groundGlow;

        public Style() { }

        public Style(Color color, bool outlineGlow, bool throughWalls, bool groundGlow)
        {
            this.color = color;
            this.outlineGlow = outlineGlow;
            this.throughWalls = throughWalls;
            this.groundGlow = groundGlow;
        }
    }

    [Tooltip("Your party (group) members.")]
    public Style party = new Style(new Color(0.3f, 1f, 0.45f, 0.55f), true, true, false);
    [Tooltip("Members of your guild.")]
    public Style guild = new Style(new Color(0.35f, 0.85f, 1f, 0.45f), true, false, false);
    [Tooltip("Members of your guild's alliance.")]
    public Style alliance = new Style(new Color(0.55f, 0.65f, 1f, 0.3f), true, false, false);
    [Tooltip("Players in a duel (countdown or fight), yours or anyone's.")]
    public Style duel = new Style(new Color(1f, 0.5f, 0.12f, 0.75f), true, true, true);

    [Tooltip("Players farther than this (metres) are not marked.")]
    [Min(1f)] public float maxDistance = 60f;
    [Tooltip("How tight the outline glow is (higher = thinner, only the very edges).")]
    [Range(0.5f, 8f)] public float outlineSharpness = 2.5f;
    [Tooltip("Strength of the silhouette through walls (multiplies the style's alpha).")]
    [Range(0f, 1f)] public float throughWallsStrength = 0.45f;
    [Tooltip("Radius of the ground glow, relative to the character's radius.")]
    [Min(0.5f)] public float groundGlowScale = 2.6f;

    /// <summary>The highlights of this machine (null on a dedicated server or before one exists).</summary>
    public static PlayerHighlights Instance { get; private set; }

    private const string ShaderName = "SimpleMovements/Player Silhouette";
    private const string GlowName = "Player Highlight Glow";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int RimOnlyId = Shader.PropertyToID("_RimOnly");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    private sealed class Marked
    {
        public CombatEntity entity;
        public PlayerBond bond;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public Material outline, occluded;
        public GameObject glow;
        public float nextRescan;
        public int seenFrame;
    }

    private sealed class StyleMaterials
    {
        public Material outline, occluded, ground;
    }

    private Shader shader;
    private bool shaderLooked;
    private Texture2D glowTexture;
    private readonly Dictionary<PlayerBond, StyleMaterials> materials = new Dictionary<PlayerBond, StyleMaterials>();
    private readonly Dictionary<CombatEntity, Marked> marked = new Dictionary<CombatEntity, Marked>();
    private readonly List<CombatEntity> drop = new List<CombatEntity>();
    private readonly List<Renderer> rendererBuffer = new List<Renderer>();
    private readonly List<Material> materialBuffer = new List<Material>();
    private float nextUpdate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDisable() => ClearAll();

    private void OnDestroy()
    {
        ClearAll();
        foreach (StyleMaterials m in materials.Values)
        {
            if (m.outline != null) Destroy(m.outline);
            if (m.occluded != null) Destroy(m.occluded);
            if (m.ground != null) Destroy(m.ground);
        }
        materials.Clear();
        if (glowTexture != null)
            Destroy(glowTexture);
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        // Colours changed in the Inspector: the shared materials follow.
        foreach (KeyValuePair<PlayerBond, StyleMaterials> kv in materials)
            Tint(kv.Value, StyleOf(kv.Key));
    }

    /// <summary>The style of a bond (null for None).</summary>
    public Style StyleOf(PlayerBond bond)
    {
        switch (bond)
        {
            case PlayerBond.Party: return party;
            case PlayerBond.Guild: return guild;
            case PlayerBond.Alliance: return alliance;
            case PlayerBond.Duel: return duel;
            default: return null;
        }
    }

    // ------------------------------------------------------------------ every frame

    private void LateUpdate()
    {
        if (GameNet.NetAuthority.IsHeadless)
            return;
        if (Time.unscaledTime >= nextUpdate)
        {
            nextUpdate = Time.unscaledTime + 0.2f;
            UpdateMarks();
        }
        // The duel's ground glow breathes a little.
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 3f);
        foreach (Marked m in marked.Values)
        {
            if (m.glow == null || m.entity == null)
                continue;
            m.glow.transform.position = m.entity.Position + Vector3.up * 0.05f;
            if (m.bond == PlayerBond.Duel)
                SizeGlow(m.glow, m.entity, 0.95f + 0.05f * pulse);
        }
    }

    private void UpdateMarks()
    {
        CombatEntity me = PlayerLocator.Local;
        int frame = Time.frameCount;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p == me || !p.isActiveAndEnabled)
                continue;
            if (me != null && (CombatRules.AreSeparated(me, p) || Vector3.Distance(me.Position, p.Position) > maxDistance))
                continue;
            PlayerBond bond = PlayerBonds.Between(me, p);
            Style style = StyleOf(bond);
            if (style == null || !style.enabled)
                continue;
            if (!marked.TryGetValue(p, out Marked m))
            {
                m = new Marked { entity = p, bond = PlayerBond.None };
                marked.Add(p, m);
            }
            m.seenFrame = frame;
            if (m.bond != bond)
            {
                Unmark(m);
                m.bond = bond;
                Mark(m, style);
            }
            else if (Time.unscaledTime >= m.nextRescan)
                Mark(m, style); // new equipment, a changed body: their renderers get the marks too
        }
        drop.Clear();
        foreach (KeyValuePair<CombatEntity, Marked> kv in marked)
            if (kv.Value.seenFrame != frame)
                drop.Add(kv.Key);
        for (int i = 0; i < drop.Count; i++)
        {
            Unmark(marked[drop[i]]);
            marked.Remove(drop[i]);
        }
    }

    // ------------------------------------------------------------------ marks

    private void Mark(Marked m, Style style)
    {
        m.nextRescan = Time.unscaledTime + 1f;
        StyleMaterials mats = MaterialsFor(m.bond, style);
        bool shaderOk = mats.outline != null;
        m.outline = shaderOk && style.outlineGlow ? mats.outline : null;
        m.occluded = shaderOk && style.throughWalls ? mats.occluded : null;
        if (m.outline != null || m.occluded != null)
        {
            m.entity.GetComponentsInChildren(false, rendererBuffer);
            for (int i = 0; i < rendererBuffer.Count; i++)
            {
                Renderer r = rendererBuffer[i];
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || !r.enabled || r.gameObject.name == GlowName)
                    continue;
                if (AddMaterials(r, m.outline, m.occluded) && !m.renderers.Contains(r))
                    m.renderers.Add(r);
            }
        }
        // The ground glow: asked for, or in place of the outline when the shader is missing.
        bool wantGlow = style.groundGlow || (!shaderOk && (style.outlineGlow || style.throughWalls));
        if (wantGlow && m.glow == null)
            m.glow = CreateGlow(m.entity, mats.ground);
        else if (!wantGlow && m.glow != null)
        {
            Destroy(m.glow);
            m.glow = null;
        }
    }

    private void Unmark(Marked m)
    {
        for (int i = 0; i < m.renderers.Count; i++)
            if (m.renderers[i] != null)
                RemoveMaterials(m.renderers[i]);
        m.renderers.Clear();
        m.outline = m.occluded = null;
        if (m.glow != null)
            Destroy(m.glow);
        m.glow = null;
    }

    private void ClearAll()
    {
        foreach (Marked m in marked.Values)
            Unmark(m);
        marked.Clear();
    }

    /// <summary>Adds the marks a renderer lacks. False when it has none of ours afterwards.</summary>
    private bool AddMaterials(Renderer r, Material outline, Material occluded)
    {
        materialBuffer.Clear();
        r.GetSharedMaterials(materialBuffer);
        bool hasOutline = outline == null, hasOccluded = occluded == null;
        bool foreign = false;
        for (int i = materialBuffer.Count - 1; i >= 0; i--)
        {
            Material mat = materialBuffer[i];
            if (!IsOurs(mat))
                continue;
            if (mat == outline) hasOutline = true;
            else if (mat == occluded) hasOccluded = true;
            else
            {
                materialBuffer.RemoveAt(i); // a mark of another bond (or a copy someone made)
                foreign = true;
            }
        }
        if (hasOutline && hasOccluded && !foreign)
            return outline != null || occluded != null;
        if (!hasOccluded)
            materialBuffer.Add(occluded);
        if (!hasOutline)
            materialBuffer.Add(outline);
        r.sharedMaterials = materialBuffer.ToArray();
        return true;
    }

    private void RemoveMaterials(Renderer r)
    {
        materialBuffer.Clear();
        r.GetSharedMaterials(materialBuffer);
        int before = materialBuffer.Count;
        materialBuffer.RemoveAll(IsOurs);
        if (materialBuffer.Count != before)
            r.sharedMaterials = materialBuffer.ToArray();
    }

    /// <summary>One of the highlight materials (also a copy made by <c>renderer.materials</c>).</summary>
    private bool IsOurs(Material mat) => mat != null && shader != null && mat.shader == shader;

    // ------------------------------------------------------------------ materials

    private StyleMaterials MaterialsFor(PlayerBond bond, Style style)
    {
        if (materials.TryGetValue(bond, out StyleMaterials m))
            return m;
        m = new StyleMaterials();
        if (!shaderLooked)
        {
            shaderLooked = true;
            shader = Shader.Find(ShaderName);
            if (shader == null)
                Debug.LogWarning($"[Player Highlights] Shader '{ShaderName}' not found (it is in Essentials/Social/Resources): using ground glows only.", this);
        }
        if (shader != null)
        {
            m.outline = new Material(shader) { name = $"Player Highlight ({bond})", hideFlags = HideFlags.DontSave };
            m.outline.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            m.outline.SetFloat(RimOnlyId, 1f);
            m.occluded = new Material(shader) { name = $"Player Silhouette ({bond})", hideFlags = HideFlags.DontSave };
            m.occluded.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.Greater);
            m.occluded.SetFloat(RimOnlyId, 0f);
            m.occluded.renderQueue = m.outline.renderQueue = 3020;
        }
        Material telegraph = CombatSettings.Instance != null ? CombatSettings.Instance.TelegraphMaterial : null;
        if (telegraph != null)
        {
            m.ground = new Material(telegraph) { name = $"Player Ground Glow ({bond})", hideFlags = HideFlags.DontSave };
            m.ground.mainTexture = GlowTexture;
        }
        Tint(m, style);
        materials.Add(bond, m);
        return m;
    }

    private void Tint(StyleMaterials m, Style style)
    {
        if (m == null || style == null)
            return;
        if (m.outline != null)
        {
            m.outline.SetColor(ColorId, style.color);
            m.outline.SetFloat(RimPowerId, outlineSharpness);
        }
        if (m.occluded != null)
        {
            Color c = style.color;
            c.a *= throughWallsStrength;
            m.occluded.SetColor(ColorId, c);
        }
        if (m.ground != null)
            m.ground.color = style.color;
    }

    /// <summary>Sizes the ground glow in world metres (whatever the character's scale), times <paramref name="factor"/>.</summary>
    private void SizeGlow(GameObject glow, CombatEntity e, float factor)
    {
        float size = Mathf.Max(0.5f, e.Radius) * groundGlowScale * 2f * factor;
        Vector3 parentScale = glow.transform.parent != null ? glow.transform.parent.lossyScale : Vector3.one;
        glow.transform.localScale = new Vector3(size / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)), size / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)), 1f);
    }

    private GameObject CreateGlow(CombatEntity e, Material material)
    {
        if (material == null)
            return null;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = GlowName;
        Collider c = go.GetComponent<Collider>();
        if (c != null)
            Destroy(c);
        go.transform.SetParent(e.transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.position = e.Position + Vector3.up * 0.05f;
        SizeGlow(go, e, 1f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    /// <summary>A soft white disc, transparent at the edge (tinted by the material colour).</summary>
    private Texture2D GlowTexture
    {
        get
        {
            if (glowTexture != null)
                return glowTexture;
            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Player Glow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // A brighter ring near the edge over a faint fill, fading to nothing at the rim.
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.12f, 2f));
                    float fill = Mathf.Clamp01(1f - d) * 0.35f;
                    float a = Mathf.Clamp01(Mathf.Max(ring, fill)) * (d < 1f ? 1f : 0f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            glowTexture.SetPixels32(pixels);
            glowTexture.Apply(false, true);
            return glowTexture;
        }
    }
}
