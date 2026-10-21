using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The abilities bar of the HUD: one <see cref="HudAbilitySlot"/> per ability key of the player (icon, key, casting
/// glow, cooldown countdown, charges, hover information) and the active trait's slot on its left. Built by
/// <b>Player HUD ▸ Build Abilities UI</b> (in the editor: saved in your prefab / scene, so you can move, resize and
/// restyle it, or swap slots for your own) or when the game starts.
/// </summary>
public class HudAbilityBar : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The slots' parent (a layout group places them).")]
    public RectTransform slotsParent;
    [Tooltip("One slot per ability key, in key order (a slot's Key Index can pick another key).")]
    public List<HudAbilitySlot> slots = new List<HudAbilitySlot>();
    [Tooltip("The active trait's panel (optional).")]
    public RectTransform traitPanel;
    public HudAbilitySlot traitSlot;
    [Tooltip("Shows the active trait's name.")]
    public TextMeshProUGUI traitCaption;

    [Header("Behaviour")]
    [Tooltip("Placed above the inventory hotbar at runtime (off: stays where you put it).")]
    public bool dockAboveHotbar = true;
    [Tooltip("Adds slots for keys added at runtime and hides slots without a key.")]
    public bool matchKeyCount = true;
    [Tooltip("Resizes the panel to its slots.")]
    public bool fitToSlots = true;
    [Tooltip("Text of the trait caption when the character has no active trait.")]
    public string noTraitText = "No Active Trait";

    private PlayerHUD hud;
    private AbilitiesStateMachine keys;
    private PlayerAbilityController abilities;
    private TraitSource traitSource;
    private float nextCheck;
    private int keyCount = -1;

    // ------------------------------------------------------------------ build
    /// <summary>Builds the bar (and the trait panel when <paramref name="withTrait"/>) under <paramref name="parent"/>.</summary>
    public static HudAbilityBar Build(Transform parent, PlayerHUD owner, AbilitiesStateMachine abilityKeys, bool withTrait)
    {
        HudPalette p = owner.palette;
        float size = owner.abilitySlotSize;
        Image frame = HudStyle.Frame("HUD Abilities", parent, p, 14);
        var bar = frame.gameObject.AddComponent<HudAbilityBar>();
        HudStyle.Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(200f, size + 20f));
        bar.slotsParent = HudStyle.Rect("Slots", frame.transform);
        HudStyle.Stretch(bar.slotsParent, 10f);
        var h = bar.slotsParent.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        int count = abilityKeys != null ? abilityKeys.KeyCount : 0;
        for (int i = 0; i < count; i++)
        {
            HudAbilitySlot slot = bar.NewSlot(owner, i);
            AbilityStateMachine key = abilityKeys.GetKey(i);
            slot.Preview(key != null ? key.Icon : null, key != null ? HudStyle.ShortKey(SafeKey(key)) : "");
        }
        bar.FitToSlots(size);

        if (withTrait)
        {
            float ts = owner.traitSlotSize;
            float w = Mathf.Max(ts + 20f, 92f);
            Image tp = HudStyle.Frame("Active Trait", frame.transform, p, 14);
            bar.traitPanel = tp.rectTransform;
            // Left of the bar, bottoms aligned: it moves with the bar.
            HudStyle.Place(bar.traitPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 0f), new Vector2(w, ts + 38f));
            bar.traitCaption = HudStyle.Text("Caption", tp.transform, "Active Trait", 12f, p.accent, TextAlignmentOptions.Center, FontStyles.Bold);
            HudStyle.Place(bar.traitCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(w - 8f, 16f));
            bar.traitCaption.enableAutoSizing = true;
            bar.traitCaption.fontSizeMin = 8f;
            bar.traitCaption.fontSizeMax = 12f;
            bar.traitSlot = owner.traitSlotPrefab != null
                ? Instantiate(owner.traitSlotPrefab, tp.transform, false)
                : HudAbilitySlot.Create(tp.transform, "Trait Slot", ts, p, true);
            bar.traitSlot.name = "Trait Slot";
            HudStyle.Place((RectTransform)bar.traitSlot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(ts, ts));
            bar.traitSlot.Preview(null, "J");
        }
        return bar;
    }

    private static string SafeKey(AbilityStateMachine key)
    {
        try { return key.KeyLabel; }
        catch { return ""; } // input not set up yet (edit mode)
    }

    private HudAbilitySlot NewSlot(PlayerHUD owner, int index)
    {
        float size = owner != null ? owner.abilitySlotSize : 58f;
        HudAbilitySlot template = owner != null && owner.abilitySlotPrefab != null ? owner.abilitySlotPrefab
            : Application.isPlaying && slots.Count > 0 && slots[slots.Count - 1] != null ? slots[slots.Count - 1] : null;
        HudAbilitySlot slot = template != null
            ? Instantiate(template, slotsParent, false)
            : HudAbilitySlot.Create(slotsParent, $"Ability {index + 1}", size, owner != null ? owner.palette : new HudPalette());
        slot.name = $"Ability {index + 1}";
        slot.keyIndex = index;
        slot.gameObject.SetActive(true);
        if (template == null || owner == null || owner.abilitySlotPrefab == null)
            ((RectTransform)slot.transform).sizeDelta = new Vector2(size, size);
        slots.Add(slot);
        return slot;
    }

    private void FitToSlots(float size)
    {
        if (!fitToSlots) return;
        var rt = (RectTransform)transform;
        int shown = 0;
        float width = 0f, height = size;
        foreach (HudAbilitySlot s in slots)
        {
            if (s == null || !s.gameObject.activeSelf) continue;
            Vector2 sz = ((RectTransform)s.transform).sizeDelta;
            width += sz.x;
            height = Mathf.Max(height, sz.y);
            shown++;
        }
        float spacing = slotsParent != null && slotsParent.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup g ? g.spacing : 8f;
        width += Mathf.Max(0, shown - 1) * spacing + 20f;
        rt.sizeDelta = new Vector2(Mathf.Max(width, size + 20f), height + 20f);
    }

    // ------------------------------------------------------------------ runtime
    /// <summary>Connects the slots to the player's ability keys and active trait (the game started).</summary>
    public void Bind(PlayerHUD owner, AbilitiesStateMachine abilityKeys, PlayerAbilityController controller, TraitManager traits)
    {
        hud = owner;
        keys = abilityKeys;
        abilities = controller;
        slots.RemoveAll(s => s == null);
        BindSlots();
        if (traitSlot != null)
        {
            traitSource = traits != null ? new TraitSource(traits, controller) : null;
            traitSlot.Bind(traitSource, owner, owner != null ? owner.palette : null);
        }
        else if (traitPanel != null)
            traitPanel.gameObject.SetActive(false);
        nextCheck = 0f;
    }

    private void BindSlots()
    {
        int count = keys != null ? keys.KeyCount : 0;
        keyCount = count;
        if (matchKeyCount)
            while (slots.Count < count)
                NewSlot(hud, slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            HudAbilitySlot s = slots[i];
            int index = s.keyIndex >= 0 ? s.keyIndex : i;
            AbilityStateMachine key = keys != null && index < count ? keys.GetKey(index) : null;
            if (matchKeyCount && index >= count) { s.gameObject.SetActive(false); continue; }
            s.gameObject.SetActive(true);
            s.Bind(key != null ? new KeySource(key) : null, hud, hud != null ? hud.palette : null);
        }
        FitToSlots(hud != null ? hud.abilitySlotSize : 58f);
        gameObject.SetActive(count > 0 || !matchKeyCount);
    }

    /// <summary>Called every frame by the Player HUD.</summary>
    public void Tick()
    {
        HudFocus.Apply(gameObject, !HudFocus.Hidden);
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        if (keys != null && keys.KeyCount != keyCount) BindSlots();
        if (traitSource != null)
        {
            traitSource.Refresh();
            if (traitCaption != null) traitCaption.text = traitSource.Trait != null ? traitSource.Trait.Name : noTraitText;
        }
    }

    // ------------------------------------------------------------------ sources
    /// <summary>An ability key of the player.</summary>
    public sealed class KeySource : IHudAbilitySource
    {
        private readonly AbilityStateMachine key;
        public KeySource(AbilityStateMachine k) { key = k; }
        public AbilitySlot Slot => key != null ? key.Slot : null;
        public Sprite Icon => key != null ? key.Icon : null;
        public string Name => key != null ? key.DisplayName : "";
        /// <summary>From the key's Input Action Reference (else the AbilityN action), as Unity shows its binding.</summary>
        public string KeyLabel => key != null ? HudStyle.ShortKey(key.KeyLabel) : "";
        public CastFailReason ReadyReason => key != null ? key.ReadyReason : CastFailReason.NoAbility;
        public bool TryUse() => key != null && key.CastNow();
        public string Describe()
        {
            AbilitySlot s = Slot;
            return s != null && s.ability != null ? s.ability.Describe(s.modifiers) : "";
        }
    }

    /// <summary>The character's active trait (the trait with a skill on its own key).</summary>
    public sealed class TraitSource : IHudAbilitySource
    {
        private readonly TraitManager traits;
        private readonly PlayerAbilityController abilities;
        private ActiveAbilityTrait behaviour;
        public Trait Trait { get; private set; }

        public TraitSource(TraitManager t, PlayerAbilityController a) { traits = t; abilities = a; Refresh(); }

        public void Refresh()
        {
            Trait = null;
            behaviour = null;
            if (traits == null) return;
            foreach (Trait t in traits.Traits)
            {
                if (t == null || !t.HasActiveSkill) continue;
                Trait = t;
                foreach (TraitBehaviour b in traits.GetRuntimeBehaviours(t))
                    if (b is ActiveAbilityTrait aa) { behaviour = aa; return; }
                return;
            }
        }

        public AbilitySlot Slot => behaviour != null && abilities != null && behaviour.SlotIndex >= 0 ? abilities.GetSlot(behaviour.SlotIndex) : null;
        public Sprite Icon => Trait != null && Trait.icon != null ? Trait.icon : Slot != null && Slot.ability != null ? Slot.ability.icon : null;
        public string Name => Trait != null ? Trait.Name : "";
        /// <summary>From the trait's Input (else its ActiveTrait action, else its fallback key).</summary>
        public string KeyLabel => behaviour != null ? HudStyle.ShortKey(behaviour.KeyText) : "";
        public CastFailReason ReadyReason => abilities != null && behaviour != null && behaviour.SlotIndex >= 0 ? abilities.CheckSlotReady(behaviour.SlotIndex) : CastFailReason.NoAbility;
        public bool TryUse() => traits != null && Trait != null && traits.TryActivate(Trait);
        public string Describe()
        {
            if (Trait == null) return "";
            string d = Trait.GetFormattedDescription();
            AbilitySlot s = Slot;
            if (s != null && s.ability != null)
            {
                string a = s.ability.Describe(s.modifiers);
                int nl = a.IndexOf('\n');
                d += "\n\n" + (nl >= 0 ? a.Substring(nl + 1) : a);
            }
            return d;
        }
    }
}
