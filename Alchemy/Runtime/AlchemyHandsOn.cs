using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>What the player is working with their hands at a station.</summary>
public enum AlchemyHandsOnMode { None, Stir, Bellows, Filter, Grind }

/// <summary>
/// The station worked with the hands, in 3D, instead of buttons. The window steps aside, the view moves to the vessel
/// and the mouse becomes the hand; Put Down (right-click or Q) puts the tool down and brings the window back, Escape goes
/// back to the game - the preparation goes on exactly as it is, and the tool can be taken up again at any time. Every key
/// is an input action of the station map (<see cref="StationControls"/>): rebindable, and the hints show the key bound;
/// the keys named below are the defaults.
/// <list type="bullet">
/// <item><b>Spoon</b>: hold the left button to dip the spoon and move the mouse in circles. How fast, how wide, how
/// steadily and which way it turns becomes the stirring (<see cref="AlchemyRequestKind.SetStirring"/>): a reversal or a
/// jerky hand stirs unevenly, a thick brew drags the spoon behind the hand, strong arms (Strength) stir harder and tire
/// later (Endurance). What floats is pushed aside, the swirl follows the spoon.</item>
/// <item><b>Bellows</b>: hold the left button and move the mouse down to squeeze them, up to let them fill (or tap
/// Space). Each squeeze is one pump (<see cref="AlchemyRequestKind.Bellows"/>): a full, quick squeeze blows hardest -
/// with the strength of the arms (Strength), and they tire (Endurance).</item>
/// <item><b>Filter</b>: hold the left button and raise the mouse to tilt the jug and pour the brew through the filter
/// into the flask. Too fast and the funnel overflows: what spills over the edge goes through unfiltered (the filter's
/// care, <see cref="AlchemyStation.Filter"/>). A steady hand (Agility) trembles less.</item>
/// <item><b>Mortar</b>: the ingredient lies in the bowl as pieces (<see cref="AlchemyMortar"/>). Click (or Space) to
/// pound - it breaks the big pieces, best on hard roots and crystals; hold the left button and grind round the bowl -
/// it wears down what the pestle passes over, best on soft petals and leaves, down to dust. Pieces slide back to the
/// bottom: work the whole bowl, or the missed ones stay lumps (an uneven grind). Enter tips it into the vessel at the
/// fineness reached (<see cref="AlchemyRequestKind.AddItem"/> with its form and evenness); 1-5 set how many, before
/// starting. Strength grinds and pounds harder; Endurance keeps the arms going.</item>
/// </list>
/// Presentation and input only: what reaches the preparation goes through <see cref="AlchemyStation.Request"/>, like the
/// window's buttons (so the rules, the history and saves are those of the simulation).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Hands On")]
public class AlchemyHandsOn : MonoBehaviour
{
    [Header("Views")]
    [Tooltip("Where the view is while stirring (empty = above the front of the vessel, looking in).")]
    [SerializeField] private Transform stirView;
    [Tooltip("Where the view is while working the bellows (empty = beside the bellows and the fire).")]
    [SerializeField] private Transform bellowsView;
    [Tooltip("Where the view is while filtering (empty = in front of the filter stand).")]
    [SerializeField] private Transform filterView;
    [Tooltip("Where the view is while grinding (empty = above the front of the mortar).")]
    [SerializeField] private Transform mortarView;
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 50f;

    [Header("Feel")]
    [Tooltip("Turns of the spoon per second that stir at full speed (for a vessel's full width).")]
    [SerializeField, Min(0.2f)] private float turnsForFullStir = 1.4f;
    [Tooltip("The player's stat that gives the arms their strength (stirring a thick brew, squeezing the bellows).")]
    [SerializeField] private CombatStatType strengthStat = CombatStatType.Strength;
    [Tooltip("The stat that keeps the arms from tiring.")]
    [SerializeField] private CombatStatType staminaStat = CombatStatType.Endurance;
    [Tooltip("The stat that steadies the hand (pouring through the filter).")]
    [SerializeField] private CombatStatType steadinessStat = CombatStatType.Agility;
    [Tooltip("Stat points that make an average pair of hands (half the stat's possible bonus).")]
    [SerializeField, Min(1f)] private float statReference = 25f;
    [Tooltip("Seconds a full funnel takes to run through (×1.3 filter paper, ×1.8 charcoal; thick brews slower).")]
    [SerializeField, Min(0.5f)] private float funnelSeconds = 2.5f;

    private AlchemyStation station;
    private AlchemyStationVisuals visuals;
    private CombatEntity player;
    private AlchemyHandsOnMode mode;
    private Action<bool> onEnd;
    private readonly AlchemyLook look = new AlchemyLook();
    private float lookTimer, hintTimer;

    private Camera cam;
    private Vector3 camPos;
    private Quaternion camRot;
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLocked;

    private Canvas hud;
    private TextMeshProUGUI hudTitle, hudHint, hudFeel;
    private Image fatigueFill;

    // The arms.
    private float fatigue;

    // Stirring.
    private Vector3 tip;
    private float lastAngle, omega, sentStir, sentAt;
    private int lastSign;
    private bool dipped, hadAngle;

    // Bellows.
    private float opening = 1f, lastOpening = 1f, stroke, strokeSpeed, spaceSqueeze = -1f;

    // Filtering.
    private AlchemyFilter filter;
    private float total, poured, inFunnel, overflowed, inFlask, funnelCapacity, drainRate, tilt, doneAt = -1f;

    // Grinding.
    private ItemSO grindItem;
    private int grindCount = 1, grindHave;
    private AlchemyMortar mortar;
    private List<AlchemyIngredientForm> grindLevels;
    private Vector2 head, lastHead;
    private float pressAt, pressMoved, blowAt = -1f;
    private bool pressing, grinding;
    private Color grindColor;
    private readonly List<Transform> chunks = new List<Transform>();
    private Transform dust;

    /// <summary>The hands-on modes of <paramref name="s"/> (added when missing).</summary>
    public static AlchemyHandsOn For(AlchemyStation s)
    {
        if (s == null)
            return null;
        AlchemyHandsOn h = s.GetComponent<AlchemyHandsOn>();
        return h != null ? h : s.gameObject.AddComponent<AlchemyHandsOn>();
    }

    public bool Active => mode != AlchemyHandsOnMode.None;
    public AlchemyHandsOnMode Mode => mode;
    /// <summary>0 fresh - 1 exhausted: tired arms stir and pump weaker.</summary>
    public float Fatigue => fatigue;

    // ------------------------------------------------------------------ the player's hands
    /// <summary>The player's practice at alchemy (0 novice - 1 master): a steadier pestle, spoon and jug, arms that tire less.</summary>
    private float Practice => player != null ? (float)(AlchemyKnowledge.For(player)?.Mastery ?? 0) : 0f;

    /// <summary>Stamina for the work: the stat's, and a practised alchemist's economy of movement.</summary>
    private float Endurance => Power(staminaStat) / (float)CraftMastery.FatigueFactor(Practice);

    /// <summary>
    /// What <paramref name="stat"/> makes of a pair of hands: 1 for a character without combat stats, else 0.7 (no
    /// points) rising to 1.1 at the reference and toward 1.5.
    /// </summary>
    public float Power(CombatStatType stat)
    {
        CombatStats stats = player != null ? CombatStats.For(player, false) : null;
        if (stats == null)
            return 1f;
        float p = Mathf.Max(0f, stats.Get(stat));
        return 0.7f + 0.8f * p / (p + statReference);
    }

    // ------------------------------------------------------------------ starting and stopping
    /// <summary>
    /// Takes up <paramref name="what"/> for <paramref name="who"/>: <paramref name="ended"/> is called when it is put
    /// down (true = back to the station's window, false = back to the game). False (and why) when it cannot be used now.
    /// <paramref name="filterId"/>: the filter to pour through; <paramref name="item"/>: the ingredient to grind.
    /// </summary>
    public bool Begin(AlchemyHandsOnMode what, CombatEntity who, Action<bool> ended, out string reason, string filterId = null, ItemSO item = null)
    {
        reason = "";
        station = GetComponent<AlchemyStation>();
        if (station == null || who == null || what == AlchemyHandsOnMode.None)
            return false;
        if (Active)
            End(false, false);
        AlchemyBrew brew = station.Brew;
        switch (what)
        {
            case AlchemyHandsOnMode.Stir:
                if (brew == null || brew.IsEmpty) { reason = "There is nothing to stir."; return false; }
                break;
            case AlchemyHandsOnMode.Bellows:
                if (!station.HasManualFire) { reason = "This vessel is heated by a burner: there are no bellows."; return false; }
                break;
            case AlchemyHandsOnMode.Filter:
            {
                filter = station.Rules.Filter(filterId);
                if (filter == null || !station.AvailableFilters().Contains(filter)) { reason = "There is no such filter here."; return false; }
                if (brew == null || brew.Mixture.LiquidVolume <= station.Rules.Settings.traceAmount) { reason = "There is no liquid to filter."; return false; }
                if (station.FilterUses(who, filter) == 0) { reason = $"The {filter.Name.ToLowerInvariant()} needs {filter.medium}."; return false; }
                break;
            }
            case AlchemyHandsOnMode.Grind:
            {
                grindItem = item;
                grindLevels = item != null ? station.GrindLevels(item) : new List<AlchemyIngredientForm>();
                if (item == null || grindLevels.Count == 0) { reason = item != null ? $"{item.Name} cannot be ground here." : "Nothing to grind."; return false; }
                InventoryManager inv = InventoryManager.For(who);
                grindHave = station.IsFreeSupply(item) ? 5 : inv != null ? inv.GetItemCount(item) : 0;
                if (grindHave <= 0) { reason = $"You have no {item.Name}."; return false; }
                break;
            }
        }
        visuals = station.GetComponentInChildren<AlchemyStationVisuals>();
        if (visuals == null)
            visuals = station.gameObject.AddComponent<AlchemyStationVisuals>();
        visuals.EnsureProps();
        player = who;
        mode = what;
        onEnd = ended;
        look.Read(brew);
        lookTimer = 0f;

        // The view.
        Camera main = Camera.main;
        var go = new GameObject("Alchemy Hands-On View");
        cam = go.AddComponent<Camera>();
        if (main != null)
        {
            cam.CopyFrom(main);
            cam.depth = main.depth + 5f;
            go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        }
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.02f;
        TargetView(out camPos, out camRot);
        if (main == null) go.transform.SetPositionAndRotation(camPos, camRot);
        cursorWasVisible = Cursor.visible;
        cursorWasLocked = Cursor.lockState;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = what == AlchemyHandsOnMode.Filter;
        StationControls.BeginHandsOn(this); // the gameplay keys go off while the hands are at work
        if (what == AlchemyHandsOnMode.Grind)
        {
            grindCount = 1;
            grindColor = IngredientColor(item);
            NewMortar();
        }
        BuildHud();

        // The tool.
        switch (what)
        {
            case AlchemyHandsOnMode.Stir:
                tip = visuals.SurfaceCenter;
                hadAngle = false;
                omega = 0f;
                sentStir = 0f;
                lastSign = 0;
                break;
            case AlchemyHandsOnMode.Bellows:
                opening = lastOpening = 1f;
                stroke = strokeSpeed = 0f;
                spaceSqueeze = -1f;
                break;
            case AlchemyHandsOnMode.Filter:
            {
                total = (float)brew.Mixture.LiquidVolume;
                poured = inFunnel = overflowed = inFlask = tilt = 0f;
                doneAt = -1f;
                funnelCapacity = Mathf.Max(2f, total * 0.18f);
                float slow = filter.id == "charcoal" ? 1.8f : filter.id == "paper" ? 1.3f : filter.id == "strainer" ? 0.7f : 1f;
                drainRate = funnelCapacity / (funnelSeconds * slow * (1f + 1.5f * look.Thickness));
                break;
            }
        }
        return true;
    }

    /// <summary>Puts the tool down: back to the window (<paramref name="backToWindow"/>) or the game; <paramref name="notify"/> = tell whoever started it.</summary>
    public void End(bool backToWindow, bool notify = true)
    {
        if (!Active)
            return;
        AlchemyHandsOnMode was = mode;
        mode = AlchemyHandsOnMode.None;
        if (was == AlchemyHandsOnMode.Stir && station != null && player != null && sentStir > 0f)
            station.Request(player, new AlchemyRequest(AlchemyRequestKind.SetStirring, 0f), out _); // the spoon is lifted out
        sentStir = 0f;
        ClearChunks();
        mortar = null;
        if (visuals != null)
        {
            visuals.HoldPestle(false, Vector3.zero);
            visuals.HoldSpoon(false, Vector3.zero, Vector3.up, 0f);
            visuals.SetBellows(1f);
            visuals.SetFiltering(false, 0f, 0f, 0f, 0f, false, Color.clear, "");
        }
        StationControls.EndHandsOn(this);
        if (cam != null) Destroy(cam.gameObject);
        cam = null;
        if (hud != null) Destroy(hud.gameObject);
        hud = null;
        Cursor.lockState = backToWindow ? CursorLockMode.None : cursorWasLocked;
        Cursor.visible = backToWindow || cursorWasVisible;
        Action<bool> callback = onEnd;
        onEnd = null;
        if (notify) callback?.Invoke(backToWindow);
    }

    private void OnDisable() => End(false);

    // ------------------------------------------------------------------ every frame
    private void Update()
    {
        if (!Active)
            return;
        float dt = Time.unscaledDeltaTime;
        if (station == null || player == null || !player.isActiveAndEnabled || visuals == null)
        {
            End(false);
            return;
        }
        lookTimer += dt;
        if (lookTimer >= 0.1f)
        {
            lookTimer = 0f;
            look.Read(station.Brew);
        }
        if (StationControls.Pressed(StationControl.PutDown))
        {
            End(true);
            return;
        }
        Vector2 pointer = StationControls.Pointer;
        bool held = StationControls.Held(StationControl.Work);
        switch (mode)
        {
            case AlchemyHandsOnMode.Stir: UpdateStir(dt, pointer, held); break;
            case AlchemyHandsOnMode.Bellows: UpdateBellows(dt, pointer, held, StationControls.Pressed(StationControl.Quick)); break;
            case AlchemyHandsOnMode.Filter: UpdateFilter(dt, pointer, held); break;
            case AlchemyHandsOnMode.Grind: UpdateGrind(dt, pointer, held); break;
        }
        if (!Active)
            return;
        hintTimer -= dt;
        if (hintTimer <= 0f && hudHint != null)
        {
            hintTimer = 0.5f; // the keys follow a rebind or a switch to the gamepad
            hudHint.text = Hint();
        }
        // The view glides to its place (and follows the surface as it rises and falls).
        TargetView(out Vector3 p, out Quaternion r);
        float k = 1f - Mathf.Exp(-dt * 5f);
        camPos = Vector3.Lerp(cam.transform.position, p, k);
        camRot = Quaternion.Slerp(cam.transform.rotation, r, k);
        cam.transform.SetPositionAndRotation(camPos, camRot);
        if (fatigueFill != null)
        {
            fatigueFill.rectTransform.anchorMax = new Vector2(1f - fatigue, 1f);
            fatigueFill.color = fatigue < 0.5f ? UITheme.Good : fatigue < 0.8f ? UITheme.Gold : UITheme.Bad;
        }
    }

    // ------------------------------------------------------------------ the spoon
    private void UpdateStir(float dt, Vector2 pointer, bool held)
    {
        AlchemyBrew brew = station.Brew;
        if (brew == null || brew.IsEmpty)
        {
            station.Tell(player, "There is nothing left to stir.");
            End(true);
            return;
        }
        Vector3 center = visuals.SurfaceCenter;
        float radius = Mathf.Max(0.01f, visuals.WorldRadiusAtSurface * 0.82f);
        Vector3 aim = tip;
        var plane = new Plane(Vector3.up, center);
        Ray ray = cam.ScreenPointToRay(pointer);
        if (plane.Raycast(ray, out float d))
        {
            Vector3 local = ray.GetPoint(d) - center;
            local.y = 0f;
            if (local.magnitude > radius) local = local.normalized * radius;
            aim = center + local;
        }
        // A thick brew drags the spoon behind the hand; strong, fresh arms push it through.
        float power = Power(strengthStat) * (1f - 0.45f * fatigue);
        float resist = 1f / (1f + 2.4f * look.Thickness);
        dipped = held;
        float follow = dipped ? 14f * resist * power : 25f;
        tip = Vector3.Lerp(tip, aim, 1f - Mathf.Exp(-dt * follow));
        Vector3 off = tip - center;
        off.y = 0f;
        float reach = Mathf.Clamp01(off.magnitude / radius);

        // How fast and which way it goes round (only where it is far enough from the middle to have a direction).
        bool reversed = false;
        if (dipped && off.magnitude > radius * 0.12f)
        {
            float angle = Mathf.Atan2(off.z, off.x);
            float w = hadAngle ? Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad / Mathf.Max(1e-4f, dt) : 0f;
            lastAngle = angle;
            hadAngle = true;
            omega = Mathf.Lerp(omega, w, 1f - Mathf.Exp(-dt * 6f));
            int sign = Mathf.Abs(omega) > 1.2f ? (omega > 0f ? 1 : -1) : 0;
            if (sign != 0 && lastSign != 0 && sign != lastSign) reversed = true;
            if (sign != 0) lastSign = sign;
        }
        else
        {
            hadAngle = false;
            omega = Mathf.Lerp(omega, 0f, 1f - Mathf.Exp(-dt * 4f));
        }
        float turns = Mathf.Abs(omega) / (Mathf.PI * 2f);
        float stir = dipped ? Mathf.Clamp01(turns / turnsForFullStir * (0.35f + 0.65f * reach) * Mathf.Clamp(power, 0.6f, 1.5f)) : 0f;

        // The arms tire with hard stirring (sooner in a thick brew), and rest when it is gentle.
        float endurance = Endurance;
        fatigue = Mathf.Clamp01(fatigue + dt * (stir * 0.06f * (1f + look.Thickness) / endurance - 0.1f * (1f - stir)));

        // What reaches the preparation: a few changes a second (each is a step of the history). A reversal is sent at
        // once, as the dip it is - the simulation counts how unevenly it was stirred.
        float q = Mathf.Round(stir * 20f) / 20f;
        float now = Time.unscaledTime;
        if (reversed)
        {
            Send(new AlchemyRequest(AlchemyRequestKind.SetStirring, Mathf.Round(q * 6f) / 20f));
            sentStir = q * 0.3f;
            sentAt = now;
        }
        else if ((Mathf.Abs(q - sentStir) >= 0.05f || q <= 0f && sentStir > 0f) && now - sentAt >= 0.25f)
        {
            Send(new AlchemyRequest(AlchemyRequestKind.SetStirring, q));
            sentStir = q;
            sentAt = now;
        }

        // The spoon in the hand: its bowl under the surface while dipped, the handle leaning out toward the view.
        float depth = visuals.WorldRadius * (dipped ? 0.12f : -0.12f);
        Vector3 bowl = tip - Vector3.up * depth;
        Vector3 toView = cam.transform.position - bowl;
        toView.y = 0f;
        Vector3 handle = Vector3.up + (off.sqrMagnitude > 1e-6f ? off.normalized * 0.35f : Vector3.zero) + (toView.sqrMagnitude > 1e-6f ? toView.normalized * 0.3f : Vector3.zero);
        visuals.HoldSpoon(true, bowl, handle, dipped ? omega : 0f);

        hudFeel.text = StirFeel(stir, reversed);
    }

    private string StirFeel(float stir, bool reversed)
    {
        if (!dipped) return "<color=#9aa0a6>The spoon is lifted out.</color>";
        if (reversed) return "<color=#e8b86b>It sloshes as the spoon turns the other way.</color>";
        if (fatigue > 0.85f) return "<color=#f0a090>Your arms are burning.</color>";
        if (look.Slopping) return "<color=#f0a090>It slops over the rim!</color>";
        if (look.Thickness > 0.55f && stir > 0.05f) return "The spoon drags through it.";
        if (look.Layered) return look.Emulsion > 0.5f ? "The two layers swirl into one." : "The layers part round the spoon.";
        if (look.Foam > 0.3f) return "Froth rises as you stir.";
        if (stir < 0.05f) return "<color=#9aa0a6>Move it round in circles.</color>";
        return look.Turbulence > 0.5f ? "It churns unevenly." : stir > 0.7f ? "It whirls round the vessel." : "It swirls round evenly.";
    }

    // ------------------------------------------------------------------ the bellows
    private void UpdateBellows(float dt, Vector2 pointer, bool held, bool space)
    {
        // Held: the mouse's height is how far open they are; let go, they fill again by themselves.
        if (space && spaceSqueeze < 0f) spaceSqueeze = 0f;
        if (spaceSqueeze >= 0f)
        {
            spaceSqueeze += dt;
            opening = spaceSqueeze < 0.22f ? Mathf.Lerp(lastOpening, 0.1f, spaceSqueeze / 0.22f) : Mathf.MoveTowards(opening, 1f, dt * 2.5f);
            if (spaceSqueeze > 0.22f && opening >= 0.99f) spaceSqueeze = -1f;
        }
        else if (held)
            opening = Mathf.Clamp01((pointer.y / Mathf.Max(1f, Screen.height) - 0.2f) / 0.6f);
        else
            opening = Mathf.MoveTowards(opening, 1f, dt * 1.6f);

        float change = opening - lastOpening;
        lastOpening = opening;
        if (change < -1e-4f)
        {
            stroke += -change; // air pushed out
            strokeSpeed = Mathf.Max(strokeSpeed, -change / Mathf.Max(1e-4f, dt));
        }
        else if (stroke > 0f && (change > 0.005f || opening <= 0.02f || !held && spaceSqueeze < 0f))
        {
            // The end of a squeeze: one pump, as full and as quick as it was, with the strength of the arms.
            if (stroke > 0.06f)
            {
                float power = Power(strengthStat) * (1f - 0.5f * fatigue);
                float strength = Mathf.Clamp01(stroke * (0.55f + 0.45f * Mathf.Clamp01(strokeSpeed / 3f)) * power);
                Send(new AlchemyRequest(AlchemyRequestKind.Bellows, Mathf.Round(strength * 100f) / 100f));
                visuals.PuffBellows(strength);
                fatigue = Mathf.Clamp01(fatigue + 0.05f * strength / Endurance);
            }
            stroke = strokeSpeed = 0f;
        }
        fatigue = Mathf.Clamp01(fatigue - dt * 0.12f);
        visuals.SetBellows(opening);

        AlchemyMixture m = station.Brew?.Mixture;
        hudFeel.text = m == null || !m.FireManual && m.Fuel <= 0 ? "<color=#9aa0a6>There is no fire to blow on.</color>"
            : fatigue > 0.85f ? "<color=#f0a090>Your arms are tiring.</color>"
            : m.Fuel <= 0 && m.FireLevel < 0.02 ? "<color=#9aa0a6>Only cold ashes: it needs fuel.</color>"
            : m.Damper < 0.15 ? "The air vent is closed: the fire chokes."
            : m.Air > 0.6 ? "The flames roar white-hot." : m.Air > 0.25 ? "The flames leap up." : m.FireLevel < 0.15 ? "The embers glow faintly." : "The fire burns steadily.";
    }

    // ------------------------------------------------------------------ the filter
    private void UpdateFilter(float dt, Vector2 pointer, bool held)
    {
        if (doneAt >= 0f)
        {
            if (Time.unscaledTime - doneAt > 1.2f) End(true);
            return;
        }
        // Held: the mouse's height tilts the jug; a less steady hand trembles.
        float steady = Power(steadinessStat) * (1f + 0.35f * Practice);
        float want = held ? Mathf.Clamp01((pointer.y / Mathf.Max(1f, Screen.height) - 0.15f) / 0.7f) : 0f;
        if (held) want += (Mathf.PerlinNoise(Time.unscaledTime * 5f, 0.37f) - 0.5f) * 0.14f / steady;
        tilt = Mathf.Lerp(tilt, Mathf.Clamp01(want), 1f - Mathf.Exp(-dt * 8f));
        float pour = tilt > 0.25f ? (tilt - 0.25f) / 0.75f : 0f;
        float rate = pour * pour * total / 1.5f;
        float p = Mathf.Min(rate * dt, total - poured);
        poured += p;
        inFunnel += p;
        float through = Mathf.Min(inFunnel, drainRate * dt);
        inFunnel -= through;
        inFlask += through;
        bool overflowing = inFunnel > funnelCapacity;
        if (overflowing)
        {
            overflowed += inFunnel - funnelCapacity;
            inFunnel = funnelCapacity;
        }
        visuals.SetFiltering(true, tilt, pour, inFunnel / funnelCapacity, Mathf.Clamp01((inFlask + overflowed) / Mathf.Max(0.01f, total)), overflowing, look.Color, filter.id);

        if (poured >= total * 0.999f && inFunnel <= funnelCapacity * 0.02f)
        {
            // All through: the filter catches what it catches - less for what spilled over its edge.
            float care = Mathf.Clamp(1f - 1.8f * overflowed / Mathf.Max(0.01f, total), 0.05f, 1f);
            station.RequestAndTell(player, new AlchemyRequest(AlchemyRequestKind.Filter, care) { id = filter.id });
            hudFeel.text = care > 0.95f ? "Poured through carefully, every drop." : care > 0.7f ? "Through - a little spilled over the edge." : "<color=#e8b86b>Through - but much of it ran over the edge.</color>";
            doneAt = Time.unscaledTime;
            return;
        }
        float full = inFunnel / funnelCapacity;
        hudFeel.text = overflowing ? "<color=#f0a090>It runs over the edge of the filter!</color>"
            : full > 0.8f ? "<color=#e8b86b>The funnel is nearly full.</color>"
            : pour > 0.02f ? (full > 0.4f ? "It runs through slowly." : "A steady stream into the funnel.")
            : poured > 0f ? "<color=#9aa0a6>Tilt the jug to pour more.</color>" : $"<color=#9aa0a6>Hold {StationControls.K(StationControl.Work)} and raise the hand to tilt the jug.</color>";
    }

    // ------------------------------------------------------------------ the mortar
    private void NewMortar()
    {
        float hardness = station.IngredientOf(grindItem)?.AlchemyProfile?.hardness ?? 0.4f;
        mortar = new AlchemyMortar(grindCount, hardness, grindLevels.Count, grindItem.Name.GetHashCode());
        head = lastHead = Vector2.zero;
        pressing = grinding = false;
        blowAt = -1f;
        ClearChunks();
        if (hudTitle != null) hudTitle.text = $"Mortar · {grindItem.Name} × {grindCount}";
    }

    private void UpdateGrind(float dt, Vector2 pointer, bool held)
    {
        if (visuals.Mortar == null)
        {
            End(true);
            return;
        }
        // How many, before the work starts.
        if (!mortar.Worked)
        {
            int want = StationControls.ChoicePressed(5) + 1;
            if (want > 0 && Mathf.Min(want, grindHave) != grindCount)
            {
                grindCount = Mathf.Min(want, grindHave);
                NewMortar();
            }
        }
        if (StationControls.Pressed(StationControl.Confirm))
        {
            FinishGrinding();
            return;
        }

        // Where the hand is in the bowl.
        Vector3 bottom = visuals.MortarBottom;
        Transform m = visuals.Mortar;
        float unit = Mathf.Max(1e-3f, visuals.MortarRadius);
        Vector2 aim = head;
        var plane = new Plane(m.up, bottom);
        Ray ray = cam.ScreenPointToRay(pointer);
        if (plane.Raycast(ray, out float d))
        {
            Vector3 local = ray.GetPoint(d) - bottom;
            aim = new Vector2(Vector3.Dot(local, m.right), Vector3.Dot(local, m.forward)) / unit;
            if (aim.magnitude > 0.9f) aim = aim.normalized * 0.9f;
        }
        float power = Power(strengthStat) * (1f - 0.45f * fatigue);
        float endurance = Endurance;
        lastHead = head;
        head = Vector2.Lerp(head, aim, 1f - Mathf.Exp(-dt * (pressing ? 12f : 22f)));
        float moved = (head - lastHead).magnitude;
        float now = Time.unscaledTime;

        // A quick click pounds; holding and moving grinds.
        bool pressedNow = StationControls.Pressed(StationControl.Work);
        if (pressedNow)
        {
            pressing = true;
            grinding = false;
            pressAt = now;
            pressMoved = 0f;
        }
        if (pressing && held)
        {
            pressMoved += moved;
            if (!grinding && (pressMoved > 0.12f || now - pressAt > 0.22f)) grinding = true;
            if (grinding)
            {
                mortar.Grind(lastHead.x, lastHead.y, head.x, head.y, power);
                fatigue = Mathf.Clamp01(fatigue + moved * 0.012f / endurance);
            }
        }
        bool blow = StationControls.Pressed(StationControl.Quick);
        if (pressing && !held)
        {
            if (!grinding) blow = true;
            pressing = grinding = false;
        }
        if (blow && (blowAt < 0f || now - blowAt > 0.25f))
        {
            blowAt = now;
            mortar.Pound(head.x, head.y, power);
            fatigue = Mathf.Clamp01(fatigue + 0.025f / endurance);
            visuals.KnockMortar(grindColor);
        }
        if (!grinding)
            fatigue = Mathf.Clamp01(fatigue - dt * 0.1f);
        mortar.Settle(dt);

        // The pestle: down in the bowl while grinding, lifted otherwise; a blow drops it.
        float lift = grinding ? 0f : unit * 0.6f;
        if (blowAt >= 0f && now - blowAt < 0.25f)
        {
            float t = (now - blowAt) / 0.25f;
            lift = unit * (t < 0.3f ? 0.9f * (1f - t / 0.3f) : 0.6f * (t - 0.3f) / 0.7f);
        }
        visuals.HoldPestle(true, visuals.MortarPoint(head.x, head.y, lift + unit * 0.12f));
        DrawChunks(unit);
        hudFeel.text = GrindFeel();
    }

    /// <summary>The pieces in the bowl: big ones as chunks, what is ground fine as a layer of dust.</summary>
    private void DrawChunks(float unit)
    {
        IReadOnlyList<AlchemyMortar.Piece> pieces = mortar.Pieces;
        Transform parent = visuals.Mortar;
        double fine = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            AlchemyMortar.Piece p = pieces[i];
            if (p.Size < 0.12) fine += p.Mass;
            while (chunks.Count <= i)
            {
                GameObject go = AlchemyVesselParts.Primitive(chunks.Count % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Piece", parent, grindColor, false, 0.2f);
                go.transform.localRotation = Quaternion.Euler(chunks.Count * 37f, chunks.Count * 71f, 0f);
                chunks.Add(go.transform);
            }
            Transform c = chunks[i];
            bool on = p.Size >= 0.12;
            if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
            if (!on) continue;
            float size = (float)p.Size * unit * 0.42f * Mathf.Pow((float)(p.Mass * pieces.Count), 0.33f);
            c.position = visuals.MortarPoint((float)p.X, (float)p.Y, size * 0.4f);
            c.localScale = new Vector3(size, size * 0.6f, size * 0.85f);
            AlchemyVesselParts.SetColor(c.GetComponent<Renderer>().sharedMaterial, Color.Lerp(grindColor, grindColor * 0.75f + Color.white * 0.25f, 1f - (float)p.Size));
        }
        for (int i = pieces.Count; i < chunks.Count; i++)
            if (chunks[i].gameObject.activeSelf) chunks[i].gameObject.SetActive(false);
        if (dust == null)
        {
            dust = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Ground", parent, grindColor * 0.8f + Color.white * 0.2f, false, 0.05f).transform;
            dust.localRotation = Quaternion.identity;
        }
        bool any = fine > 0.02;
        if (dust.gameObject.activeSelf != any) dust.gameObject.SetActive(any);
        if (any)
        {
            float w = unit * 2f * Mathf.Lerp(0.4f, 1.05f, Mathf.Sqrt((float)fine));
            dust.position = visuals.MortarPoint(0f, 0f, unit * 0.02f * (float)fine);
            dust.localScale = new Vector3(w, unit * 0.03f * (0.3f + (float)fine), w);
        }
    }

    private void ClearChunks()
    {
        foreach (Transform c in chunks)
            if (c != null) Destroy(c.gameObject);
        chunks.Clear();
        if (dust != null) Destroy(dust.gameObject);
        dust = null;
    }

    /// <summary>"Finely ground, with a few lumps - it gives up its substances faster, the locked-in ones too".</summary>
    private string GrindFeel()
    {
        mortar.Result(out int level, out double even);
        if (!mortar.Worked)
            return "<color=#9aa0a6>Whole. Click to pound it, or hold the button and grind it round the bowl.</color>";
        if (level == 0)
            return "Bruised, but still whole.";
        AlchemyIngredientForm form = grindLevels[level - 1];
        string evenness = even > 0.8 ? "even" : even > 0.5 ? "with a few lumps" : "uneven: lumps among the dust";
        string how = form.selectivity >= 1f ? "it still holds back what is locked in" : form.selectivity >= 0.3f ? "the locked-in parts come out too" : "everything comes out almost at once";
        string numbers = station.RevealsResults ? $" <color=#9aa0a6>(evenness {even * 100:0}%)</color>" : "";
        return $"{form.Name}, {evenness}{numbers} - {how}{(form.volatileLoss > 0f ? ", and its scent escapes" : "")}.";
    }

    private void FinishGrinding()
    {
        mortar.Result(out int level, out double even);
        even = 1 - (1 - even) * (1 - 0.35 * Practice); // a practised pestle grinds more evenly
        AlchemyIngredientForm form = level > 0 ? grindLevels[level - 1] : null;
        station.RequestAndTell(player, new AlchemyRequest(AlchemyRequestKind.AddItem, 0f, grindCount, grindItem, form?.id, form != null ? (float)even : 1f));
        End(true);
    }

    /// <summary>The colour of what an ingredient gives up (its pieces in the mortar).</summary>
    private Color IngredientColor(ItemSO item)
    {
        AlchemyIngredientProfile p = station.IngredientOf(item)?.AlchemyProfile;
        float r = 0f, g = 0f, b = 0f, w = 0f;
        if (p?.releases != null)
            foreach (IngredientRelease rel in p.releases)
            {
                AlchemySubstance sub = rel != null ? station.Rules.Substance(rel.substance) : null;
                if (sub == null || sub.role == SubstanceRole.Solvent) continue;
                r += sub.color.r * rel.amount;
                g += sub.color.g * rel.amount;
                b += sub.color.b * rel.amount;
                w += rel.amount;
            }
        return w > 0f ? new Color(r / w, g / w, b / w) : new Color(0.45f, 0.55f, 0.3f);
    }

    private void Send(AlchemyRequest r)
    {
        if (station != null && player != null)
            station.Request(player, r, out _);
    }

    // ------------------------------------------------------------------ the view and the HUD
    private void TargetView(out Vector3 position, out Quaternion rotation)
    {
        float r = Mathf.Max(0.1f, visuals.WorldRadius);
        Transform fixedView = mode == AlchemyHandsOnMode.Stir ? stirView : mode == AlchemyHandsOnMode.Bellows ? bellowsView
                            : mode == AlchemyHandsOnMode.Grind ? mortarView : filterView;
        if (fixedView != null)
        {
            position = fixedView.position;
            rotation = fixedView.rotation;
            return;
        }
        Vector3 front = visuals.Front;
        Vector3 at;
        switch (mode)
        {
            case AlchemyHandsOnMode.Bellows:
            {
                Transform b = visuals.BellowsTransform;
                Vector3 bed = visuals.FireBedPoint;
                Vector3 bp = b != null ? b.position : bed;
                at = Vector3.Lerp(bp, bed, 0.4f);
                Vector3 side = Vector3.Cross(Vector3.up, (bed - bp).sqrMagnitude > 1e-6f ? (bed - bp).normalized : Vector3.forward);
                if (Vector3.Dot(side, front) < 0f) side = -side;
                position = at + side * r * 3.2f + Vector3.up * r * 1.8f;
                break;
            }
            case AlchemyHandsOnMode.Grind:
                at = visuals.MortarBottom;
                position = at + front * r * 0.75f + Vector3.up * r * 1.25f;
                break;
            case AlchemyHandsOnMode.Filter:
            {
                Transform f = visuals.FilterStand;
                at = (f != null ? f.position : visuals.BrimCenter) + Vector3.up * r * 0.8f;
                position = at + front * r * 3.2f + Vector3.up * r * 1.0f;
                break;
            }
            default:
                at = visuals.SurfaceCenter;
                position = visuals.BrimCenter + front * r * 1.5f + Vector3.up * r * 2.4f;
                break;
        }
        rotation = Quaternion.LookRotation(at - position, Vector3.up);
    }

    private void BuildHud()
    {
        hud = UITheme.Canvas("Alchemy Hands-On", 57, null);
        DontDestroyOnLoad(hud.gameObject);
        Image panel = UITheme.Panel("Panel", hud.transform, new Color(0.06f, 0.07f, 0.09f, 0.82f), UITheme.Radius);
        UITheme.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-400f, 24f), new Vector2(400f, 176f));
        RectTransform col = UITheme.Column("Column", panel.transform, 12, 4f);
        InventoryUIFactory.Stretch(col);
        hudTitle = UITheme.Label(col, "", 20f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        InventoryUIFactory.Size(hudTitle, -1f, 26f);
        hudFeel = UITheme.Label(col, "", 16f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Normal, "Feel");
        InventoryUIFactory.Size(hudFeel, -1f, 44f);
        RectTransform armRow = UITheme.Row("Arms", col, 12f, 8f);
        TextMeshProUGUI arms = UITheme.Label(armRow, "Arms", 12f, UITheme.Muted);
        InventoryUIFactory.Size(arms, 48f, 12f, 0f);
        Image back = UITheme.Panel("Bar", armRow, new Color(0f, 0f, 0f, 0.4f), 4);
        InventoryUIFactory.Size(back, -1f, 8f, 1f);
        fatigueFill = UITheme.Panel("Fill", back.transform, UITheme.Good, 4);
        fatigueFill.rectTransform.anchorMin = Vector2.zero;
        fatigueFill.rectTransform.anchorMax = Vector2.one;
        fatigueFill.rectTransform.offsetMin = fatigueFill.rectTransform.offsetMax = Vector2.zero;
        hudHint = UITheme.Label(col, "", 13f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Hint");
        InventoryUIFactory.Size(hudHint, -1f, 44f);
        switch (mode)
        {
            case AlchemyHandsOnMode.Stir: hudTitle.text = "The spoon"; break;
            case AlchemyHandsOnMode.Bellows: hudTitle.text = "The bellows"; break;
            case AlchemyHandsOnMode.Grind: hudTitle.text = $"Mortar · {grindItem.Name} × {grindCount}"; break;
            default: hudTitle.text = $"Filtering · {filter.Name}"; break;
        }
        hudHint.text = Hint();
        hintTimer = 0.5f;
    }

    /// <summary>How the tool is worked, with the keys actually bound (they follow a rebind and the device in use).</summary>
    private string Hint()
    {
        string work = StationControls.K(StationControl.Work), down = StationControls.K(StationControl.PutDown), quick = StationControls.K(StationControl.Quick);
        switch (mode)
        {
            case AlchemyHandsOnMode.Stir:
                return $"Hold {work} to dip the spoon and move the hand in circles · wide, steady circles stir best · {down}: put it down · Esc: leave";
            case AlchemyHandsOnMode.Bellows:
                return $"Hold {work}: move down to squeeze, up to let them fill (or tap {quick}) · a full, quick squeeze blows hardest · {down}: let go";
            case AlchemyHandsOnMode.Grind:
                return $"Tap {work} (or {quick}) to pound: it breaks hard pieces · hold {work} and grind round the whole bowl · " +
                       $"{StationControls.Choices(5)}: how many (before you start) · {StationControls.K(StationControl.Confirm)}: tip it into the vessel · {down}: put it back";
            default:
                return $"Hold {work} and raise the hand to tilt the jug · pour steadily: what runs over the edge is not filtered · {down}: stop (it goes back in the vessel)";
        }
    }
}
