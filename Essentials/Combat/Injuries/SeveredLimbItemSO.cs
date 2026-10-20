using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A severed limb as an item: lying in the world where it fell, picked up into the inventory, and used (from the
/// Health page) to reattach it before its time limit runs out. Each copy carries the id of its
/// <see cref="SeveredLimbRecord"/> in its durability list (whose limb it is, its condition, when it came off), so one
/// asset per body part serves every character. Create them with the Injury Settings inspector (Create Limb Items):
/// network sessions and saves need assets; offline, a limb without one gets an item made at runtime.
/// </summary>
[CreateAssetMenu(fileName = "Severed Limb", menuName = "SimpleMovements/Items/Severed Limb", order = 7)]
public class SeveredLimbItemSO : ItemSO, IItemInstanceTooltip
{
    [Header("Severed Limb")]
    [Tooltip("Body part id this item stands for (LeftArm). Empty = any part.")]
    [SerializeField] private string partId = "";

    [System.NonSerialized] private SeveredLimbRecord runtimeRecord;

    public string PartId => partId;

    /// <summary>Its record, for limbs made at runtime (one item per limb). Null for assets (the copy's durability says).</summary>
    public SeveredLimbRecord RuntimeRecord => runtimeRecord;

    public override bool ShowsDurability => false;

    public SeveredLimbItemSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 2f;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
        canBeSold = false;
    }

    /// <summary>Sets the asset's fields (editor tool).</summary>
    public void Setup(string part, string displayName, Sprite itemIcon, GameObject model, float itemWeight)
    {
        partId = part;
        name = displayName;
        description = "A severed limb. Reattach it from the Health page before its time limit runs out.";
        icon = itemIcon;
        prefab = model;
        weight = itemWeight;
        stackMax = 1;
    }

    // ------------------------------------------------------------------ records of copies
    /// <summary>The record id a copy carries (0 = none).</summary>
    public static int RecordIdOf(InventoryItem item)
    {
        if (item == null)
            return 0;
        if (item.itemScriptableObject is SeveredLimbItemSO s && s.runtimeRecord != null)
            return s.runtimeRecord.Id;
        if (item.DurabilityList != null && item.DurabilityList.Count > 0)
            return item.DurabilityList[item.DurabilityList.Count - 1];
        return Mathf.RoundToInt(item.durability);
    }

    /// <summary>The record id a world copy carries (0 = none).</summary>
    public static int RecordIdOf(ItemPickable pickable)
    {
        if (pickable == null)
            return 0;
        if (pickable.itemScriptableObject is SeveredLimbItemSO s && s.runtimeRecord != null)
            return s.runtimeRecord.Id;
        List<int> d = pickable.DurabilityList;
        return d != null && d.Count > 0 ? d[d.Count - 1] : 0;
    }

    public static SeveredLimbRecord RecordOf(InventoryItem item) =>
        item != null && item.itemScriptableObject is SeveredLimbItemSO ? InjuryController.FindRecord(RecordIdOf(item)) : null;

    public static SeveredLimbRecord RecordOf(ItemPickable pickable) =>
        pickable != null && pickable.itemScriptableObject is SeveredLimbItemSO ? InjuryController.FindRecord(RecordIdOf(pickable)) : null;

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        SeveredLimbRecord r = RecordOf(item);
        if (r != null)
            r.Describe(lines);
        else
            lines.Add("<color=#9aa0a6>A severed limb nobody can use any more.</color>");
    }

    /// <summary>Using it from the hotbar does nothing: reattach it from the Health page.</summary>
    public override void UseItem(GameObject player, PlayerStatusController statusController)
    {
        Component anchor = statusController != null ? statusController : player != null ? player.transform : null;
        InventoryManager.For(anchor)?.ShowMessage("Open the inventory's Health page to reattach it.", InventoryFeedback.Kind.Info);
    }

    // ------------------------------------------------------------------ runtime items (offline, no asset)
    /// <summary>An item made at runtime for one limb (offline projects without limb item assets).</summary>
    internal static SeveredLimbItemSO CreateRuntime(InjuryBodyPart part, SeveredLimbRecord record, Sprite itemIcon, GameObject model)
    {
        var s = CreateInstance<SeveredLimbItemSO>();
        ((Object)s).name = "Severed " + part.id;
        s.hideFlags = HideFlags.DontSave;
        s.partId = part.id;
        s.name = record.Title;
        s.description = "A severed limb. Reattach it from the Health page before its time limit runs out.";
        s.icon = itemIcon != null ? itemIcon : LimbArt.Icon(part.kind);
        s.prefab = model != null ? model : LimbArt.Template(part.kind);
        s.weight = DefaultWeight(part.kind);
        s.runtimeRecord = record;
        return s;
    }

    /// <summary>Renames a runtime limb item ("Rotten Left Arm of Kaynan").</summary>
    internal void Rename(string displayName)
    {
        if (runtimeRecord != null)
            name = displayName;
    }

    public static float DefaultWeight(BodyPartKind kind)
    {
        switch (kind)
        {
            case BodyPartKind.Arm: return 3.5f;
            case BodyPartKind.Leg: return 9f;
            case BodyPartKind.Hand: case BodyPartKind.Foot: return 0.8f;
            default: return 2f;
        }
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (stackMax != 1)
            errors.Add("A severed limb must not stack (Stack Max 1): each copy is a different limb.");
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        stackMax = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }
}

/// <summary>Generated look of limbs without their own model or icon (a capsule, a simple icon).</summary>
public static class LimbArt
{
    private static readonly Dictionary<BodyPartKind, GameObject> templates = new Dictionary<BodyPartKind, GameObject>();
    private static readonly Dictionary<BodyPartKind, Sprite> icons = new Dictionary<BodyPartKind, Sprite>();
    private static GameObject root;
    private static Material material;

    public static readonly Color Skin = new Color(0.86f, 0.66f, 0.52f);
    public static readonly Color Wound = new Color(0.55f, 0.06f, 0.08f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        templates.Clear();
        icons.Clear();
        root = null;
        material = null;
    }

    /// <summary>The size of a generated limb (capsule diameter, length) in metres.</summary>
    public static Vector2 Size(BodyPartKind kind)
    {
        switch (kind)
        {
            case BodyPartKind.Arm: return new Vector2(0.11f, 0.62f);
            case BodyPartKind.Leg: return new Vector2(0.15f, 0.85f);
            case BodyPartKind.Hand: return new Vector2(0.09f, 0.2f);
            case BodyPartKind.Foot: return new Vector2(0.1f, 0.26f);
            default: return new Vector2(0.15f, 0.4f);
        }
    }

    /// <summary>An inactive capsule used as the prefab of runtime limb items (instances are made active when dropped).</summary>
    public static GameObject Template(BodyPartKind kind)
    {
        if (templates.TryGetValue(kind, out GameObject t) && t != null)
            return t;
        if (root == null)
        {
            root = new GameObject("[Severed Limb Templates]");
            root.SetActive(false);
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(root);
            else
                root.hideFlags = HideFlags.HideAndDontSave;
        }
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Severed " + kind;
        go.transform.SetParent(root.transform, false);
        Vector2 size = Size(kind);
        go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.x);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // lying on its side
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = Material;
            if (m != null)
                r.sharedMaterial = m;
        }
        templates[kind] = go;
        return go;
    }

    private static Material Material
    {
        get
        {
            if (material != null)
                return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            material = new Material(shader) { name = "Severed Limb", hideFlags = HideFlags.DontSave, color = Skin };
            return material;
        }
    }

    /// <summary>A generated icon: a skin-coloured limb with a red cut end.</summary>
    public static Sprite Icon(BodyPartKind kind)
    {
        if (icons.TryGetValue(kind, out Sprite s) && s != null)
            return s;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Limb Icon " + kind,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        Vector2 dim = Size(kind);
        float thick = Mathf.Clamp(dim.x / dim.y * 64f, 9f, 22f) * 0.5f; // half thickness in pixels
        var a = new Vector2(12f, 12f);
        var b = new Vector2(52f, 52f);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(p, a + ab * t);
                float alpha = Mathf.Clamp01(thick - d + 0.5f);
                Color c = t > 0.88f ? Wound : Color.Lerp(Skin, Skin * 0.8f, d / Mathf.Max(1f, thick));
                c.a = alpha;
                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        s.name = tex.name;
        s.hideFlags = HideFlags.DontSave;
        icons[kind] = s;
        return s;
    }
}
