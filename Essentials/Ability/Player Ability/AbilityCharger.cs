using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hold-to-charge abilities. Put it on the player next to the <see cref="PlayerAbilityController"/> and list the abilities that can be
/// charged: pressing their key starts charging, holding it charges, and letting go casts the ability with the charge's multipliers
/// (damage, healing, range, size, duration and cost, see <see cref="ChargeProfile"/>). Abilities that are not listed, and abilities cast from
/// a button, are cast as always. Other systems (Rune Magic's spells) make their abilities chargeable through <see cref="IChargeSource"/>; this
/// component does the holding for them too, so one charge bar and one set of rules serve both.
/// <para>The key is the ability's own input (the Input System action of its key: nothing is fixed in code). The cast asks for the charge the
/// player's machine measured; the server (or the offline rules) keeps what the ability's profile and the caster's resources allow.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Abilities/Ability Charger")]
public class AbilityCharger : MonoBehaviour, IChargeSource
{
    [Serializable]
    public class Rule
    {
        [Tooltip("The ability that can be charged.")]
        public AbilityDefinition ability;
        public ChargeProfile profile = new ChargeProfile();
    }

    [SerializeField] private PlayerAbilityController controller;
    [Tooltip("The abilities that can be charged by holding their key.")]
    [SerializeField] private List<Rule> rules = new List<Rule>();
    [Tooltip("Show the charge bar while charging (the local player only).")]
    [SerializeField] private bool showBar = true;

    private bool charging;
    private int slot = -1;
    private float startTime;
    private ChargeProfile profile;
    private AbilitySlot chargingSlot;
    private bool damageHooked;

    /// <summary>Is a charge going on?</summary>
    public bool IsCharging => charging;
    /// <summary>The slot being charged (-1 when none).</summary>
    public int ChargingSlot => charging ? slot : -1;
    /// <summary>The seconds the key has been held (0 when none).</summary>
    public float HeldSeconds => charging ? Time.time - startTime : 0f;
    /// <summary>How charged the ability is now, 0 to 1.</summary>
    public float Charge => charging && profile != null ? profile.Fraction(HeldSeconds) : 0f;
    /// <summary>Is the key held long enough to count as charging (a tap shorter than this casts as a tap)?</summary>
    public bool PastMinimum => charging && profile != null && HeldSeconds >= profile.minSeconds;
    /// <summary>A charge began (slot).</summary>
    public event Action<int> ChargeStarted;
    /// <summary>A charge ended: slot, the charge it cast with (0 = a tap or a cancel), whether it was cast.</summary>
    public event Action<int, float, bool> ChargeEnded;

    public IList<Rule> Rules => rules;
    public PlayerAbilityController Controller => controller;

    private void Awake()
    {
        if (controller == null) TryGetComponent(out controller);
        if (controller == null) controller = GetComponentInParent<PlayerAbilityController>();
        if (controller == null) controller = GetComponentInChildren<PlayerAbilityController>(true);
    }

    private void OnEnable()
    {
        if (controller == null) return;
        controller.PreCast += OnPreCast;
        AbilityCharging.Register(this);
    }

    private void OnDisable()
    {
        if (controller != null) controller.PreCast -= OnPreCast;
        AbilityCharging.Unregister(this);
        UnhookDamage();
        EndCharge(false, 0f);
    }

    // ------------------------------------------------------------------ the profiles of the listed abilities
    bool IChargeSource.TryGetProfile(AbilityCaster caster, AbilitySlot s, out ChargeProfile p)
    {
        p = null;
        if (caster != controller || s == null || s.ability == null) return false;
        foreach (Rule r in rules)
            if (r != null && r.ability != null && r.profile != null && (r.ability == s.ability || r.ability.Id == s.ability.Id))
            {
                p = r.profile;
                return true;
            }
        return false;
    }

    // Abilities with a cost of their own pay it through the ability system (the charged cost is checked there).
    bool IChargeSource.CanAfford(AbilityCaster caster, AbilitySlot s, ChargeProfile p, float fraction) => true;

    // ------------------------------------------------------------------ input
    /// <summary>The ability controller asks before it casts a pressed key: a chargeable ability starts charging instead of casting.</summary>
    private bool OnPreCast(int slotIndex)
    {
        if (controller == null || !controller.LocalInputEnabled)
            return false;
        if (charging)
            return slotIndex == slot; // a second press of the key being held does nothing
        AbilitySlot s = controller.GetSlot(slotIndex);
        if (s == null || s.ability == null || s.ability.targeting.playerConfirmsTarget)
            return false; // a click-to-confirm ability has its own second step
        ChargeProfile p = AbilityCharging.ProfileOf(controller, s);
        if (p == null)
            return false;
        if (controller.CheckSlotReady(slotIndex) != CastFailReason.None)
            return false; // the cast goes on as usual and reports why it cannot start
        if (controller.InputOfSlot(slotIndex) == null)
            return false; // cast from a button: there is no key to hold
        charging = true;
        slot = slotIndex;
        chargingSlot = s;
        profile = p;
        startTime = Time.time;
        if (p.cancelOnDamage) HookDamage();
        ChargeStarted?.Invoke(slotIndex);
        if (showBar && controller.LocalInputEnabled) AbilityChargeBar.Show(this);
        return true;
    }

    private void Update()
    {
        if (!charging)
            return;
        AbilitySlot s = controller.GetSlot(slot);
        // Something stopped the ability being cast (stun, silence, death, the slot changed, the resources are gone): the charge is dropped.
        if (s == null || s != chargingSlot || s.ability == null || controller.CheckSlotReady(slot) != CastFailReason.None)
        {
            EndCharge(false, 0f);
            return;
        }
        InputAction input = controller.InputOfSlot(slot);
        bool held = input != null && input.IsPressed();
        bool atMax = profile.autoReleaseAtMax && HeldSeconds >= profile.maxSeconds;
        if (!held || atMax)
            Release();
    }

    private void Release()
    {
        int castSlot = slot;
        float held = HeldSeconds;
        ChargeProfile p = profile;
        AbilitySlot s = chargingSlot;
        float f = p != null ? p.Fraction(held) : 0f;
        if (f <= 0f && p != null && p.belowMinimum == ChargeBelowMin.Cancel)
        {
            EndCharge(false, 0f);
            return;
        }
        EndCharge(false, 0f); // the state is clear before the cast: it cannot be taken for a new charge
        if (s == null || s.ability == null)
            return;
        CastRequest request = controller.BuildAimRequest(s.ability, s.Stats);
        request.charge = f;
        bool cast = controller.SubmitCast(castSlot, request);
        ChargeEnded?.Invoke(castSlot, cast ? f : 0f, cast);
    }

    /// <summary>Stops charging without casting anything.</summary>
    public void Cancel() => EndCharge(true, 0f);

    private void EndCharge(bool raise, float cast)
    {
        bool was = charging;
        int s = slot;
        charging = false;
        slot = -1;
        profile = null;
        chargingSlot = null;
        UnhookDamage();
        if (was && raise) ChargeEnded?.Invoke(s, cast, false);
    }

    private void HookDamage()
    {
        CombatEntity e = controller != null ? controller.Entity : null;
        if (e == null || damageHooked) return;
        e.Damaged += OnDamaged;
        damageHooked = true;
    }

    private void UnhookDamage()
    {
        if (!damageHooked) return;
        CombatEntity e = controller != null ? controller.Entity : null;
        if (e != null) e.Damaged -= OnDamaged;
        damageHooked = false;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (charging && profile != null && profile.cancelOnDamage && !info.isPeriodic)
            EndCharge(true, 0f);
    }
}

/// <summary>The charge bar: a thin bar under the crosshair that fills while a key is held (the local player's charge).</summary>
public class AbilityChargeBar : MonoBehaviour
{
    private static AbilityChargeBar instance;

    private AbilityCharger source;
    private Canvas canvas;
    private Image fill, back;

    /// <summary>Shows the bar for <paramref name="charger"/> until its charge ends.</summary>
    public static void Show(AbilityCharger charger)
    {
        if (charger == null || Application.isBatchMode) return;
        if (instance == null)
        {
            var go = new GameObject("[Ability Charge Bar]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AbilityChargeBar>();
            instance.Build();
        }
        instance.source = charger;
        instance.canvas.gameObject.SetActive(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void Build()
    {
        canvas = UITheme.Canvas("Ability Charge Bar", 60, null);
        DontDestroyOnLoad(canvas.gameObject);
        back = InventoryUIFactory.Panel("Back", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
        SocialUI.Place(back.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(260f, 14f));
        fill = InventoryUIFactory.Panel("Fill", back.transform, new Color(1f, 0.83f, 0.3f, 0.95f));
        fill.sprite = SocialUI.WhiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        InventoryUIFactory.Stretch(fill.rectTransform);
        back.raycastTarget = fill.raycastTarget = false;
        canvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (source == null || !source.IsCharging)
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
            return;
        }
        fill.fillAmount = source.Charge;
        // Dim until the key has been held long enough to count (a tap casts the ability as usual).
        fill.color = source.PastMinimum ? (source.Charge >= 0.999f ? new Color(1f, 0.55f, 0.2f, 1f) : new Color(1f, 0.83f, 0.3f, 0.95f)) : new Color(0.7f, 0.7f, 0.7f, 0.6f);
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (instance == this) instance = null;
    }
}
