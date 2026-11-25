using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A seam of ore in the world. Hit it with a pick (a weapon whose tool type is Pick Axe: its Tool Damage breaks the
/// rock) or work it by hand (hold Interact: loose surface stones, slowly) and lumps of ore break off into the
/// inventory. Every lump of a deposit comes from the same seam - its richness, its rock and its impurities - and no
/// two lumps are quite alike: they keep their deposit and their number, so the station recreates exactly that lump.
/// A deposit gives so many lumps, then is worked out for a while; the lumps it gives after it grows back are new ones
/// (they are numbered on from the last, never the same lump twice).
/// <para>A deposit can also be of another material the crafting database knows (<see cref="YieldKind"/>): a coal seam
/// (Fuel), a bed of limestone, clay, sand or borax (Supply), rock salt (Quenchant: brine) or a crystal vein (Gem) - each
/// "lump" is then one item of it. A hard seam can ask for a better pick (<see cref="MinToolDamage"/>).</para>
/// <para>In a network session give the seam a NetworkObject and a NetInteractable (Tools > SimpleMovements > Network >
/// Make Selected Prefab Networked > Interactable, or both components on it in its scene): the server breaks the lumps off,
/// and every player sees the seam as it is - worked out, its loose stones gathered, grown back. A seam a dungeon or a
/// cave builds is networked by the dungeon instead (see <see cref="ShowOnly"/>).</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Crafting/Ore Deposit")]
public class OreDeposit : MonoBehaviour, IInteractable, IWeaponHittable, IReplicatedInteractable
{
    [Tooltip("The ore (a Crafting Database ore id: hematite, magnetite, bog_iron, malachite, chalcopyrite, cassiterite, galena, native_silver, gold_quartz, calamine, garnierite, meteorite, mythril_ore, orichalcum_ore). With another Yield Kind: the id of that fuel, supply, quenchant or gem.")]
    [SerializeField] private string ore = "hematite";
    [Tooltip("The item a lump becomes (empty = one made at runtime from the database).")]
    [SerializeField] private CraftStockItemSO oreItem;
    [Tooltip("Which seam this is: deposits with the same number give the same ore (-1 = from the object's place in the world).")]
    [SerializeField] private int deposit = -1;
    [Tooltip("A gemstone the seam carries (a Crafting Database gem id: ruby, sapphire, emerald, aquamarine, topaz, garnet, peridot, amethyst, citrine, rock_crystal, tourmaline, diamond, opal, moonstone, turquoise, lapis_lazuli; empty = none). Now and then a lump brings a rough stone out with it, crusted in the rock - clean it and cut it at a lapidary's bench.")]
    [SerializeField] private string gem = "";
    [Tooltip("The chance a lump brings a rough stone out with it (decided by the seam and the lump, so the same every time).")]
    [SerializeField, Range(0f, 1f)] private float gemChance = 0.15f;
    [Tooltip("Lumps it gives before it is worked out.")]
    [SerializeField, Min(1)] private int lumps = 12;
    [Tooltip("Seconds until a worked-out deposit can be mined again (0 = never).")]
    [SerializeField, Min(0f)] private float regrowSeconds = 900f;
    [Tooltip("Tool damage to break one lump off with a pick.")]
    [SerializeField, Min(0.1f)] private float workPerLump = 30f;
    [Tooltip("Can lumps be gathered by hand (slowly, a few loose stones)?")]
    [SerializeField] private bool gatherByHand = true;
    [Tooltip("Lumps that can be gathered by hand before a pick is needed.")]
    [SerializeField, Min(0)] private int looseStones = 3;
    [Tooltip("Seconds Interact must be held to gather a loose stone.")]
    [SerializeField, Min(0f)] private float gatherSeconds = 2.5f;
    [Tooltip("Shown while worked out (empty = nothing changes).")]
    [SerializeField] private GameObject depletedVisual;
    [Tooltip("Hidden while worked out (the ore's rock).")]
    [SerializeField] private GameObject richVisual;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private float range = 2.5f;

    [Header("What It Gives (optional)")]
    [Tooltip("What a lump is. Ore (default): a lump of ore that keeps its seam. Fuel: coal (id coal). Supply: limestone, refractory_clay, sand, borax, fullers_earth, tripoli... Quenchant: brine (rock salt). Gem: a rough stone (a gem id). The id above names it.")]
    [SerializeField] private CraftStockKind yieldKind = CraftStockKind.Ore;
    [Tooltip("Name shown when a player aims at it (empty = 'Ore seam').")]
    [SerializeField] private string displayName = "";
    [Tooltip("Tool Damage a pick needs to break anything off (0 = any pick): hard seams wait for a better pick.")]
    [SerializeField, Min(0f)] private float minToolDamage;

    // Lumps taken since it last grew back, loose stones gathered by hand; and every lump it ever gave (the next one's number).
    private int taken, gathered, issued;
    // Worked out at depletedAt (Time.time; earlier than this session's start for a seam restored part-way through its
    // regrowth, so it may be negative) and counting towards growing back.
    private float work, depletedAt;
    private bool regrowing;
    private float lastHardMessage = -100f;

    /// <summary>A lump left the seam (deposit, the player it went to - null when nobody took it -, the item, the lump's number).</summary>
    public event Action<OreDeposit, CombatEntity, CraftStockItemSO, int> LumpTaken;

    /// <summary>A lump left any seam (deposit, player, item): quests, statistics.</summary>
    public static event Action<OreDeposit, CombatEntity, CraftStockItemSO> AnyLumpTaken;

    /// <summary>The seam's state changed (a lump taken, a stone gathered, grown back, a state received or restored).</summary>
    public event Action<OreDeposit> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyLumpTaken = null;

    public string Ore => ore;
    public int Deposit => deposit >= 0 ? deposit : (int)(CraftMath.Hash(Mathf.RoundToInt(transform.position.x * 10f), Mathf.RoundToInt(transform.position.z * 10f), Mathf.RoundToInt(transform.position.y)) & 0xFFFFF);
    public int LumpsLeft => Mathf.Max(0, lumps - taken);
    public bool WorkedOut => LumpsLeft <= 0;

    /// <summary>What a lump is (ore by default; fuel, a supply, a quenchant or a gem for other seams).</summary>
    public CraftStockKind YieldKind => yieldKind;
    /// <summary>Lumps a full seam gives.</summary>
    public int LumpsTotal => lumps;
    /// <summary>Every lump the seam ever gave.</summary>
    public int LumpsIssued => issued;
    /// <summary>Seconds a worked-out seam takes to grow back (0 = never).</summary>
    public float RegrowSeconds => regrowSeconds;
    /// <summary>Tool Damage a pick needs to break anything off (0 = any pick).</summary>
    public float MinToolDamage => minToolDamage;
    /// <summary>Seconds until a worked-out seam grows back (0 when it is not worked out; infinity when it never does).</summary>
    public float SecondsUntilRegrown => !WorkedOut ? 0f : regrowSeconds <= 0f || !regrowing ? float.PositiveInfinity : Mathf.Max(0f, regrowSeconds - (Time.time - depletedAt));

    /// <summary>
    /// A copy that only shows the seam (a network client's copy of a dungeon or cave): it never breaks lumps off or gives
    /// items itself - the server's copy does, and its state arrives through <see cref="ApplyReplicatedState"/>. Hits still
    /// look and sound like hits; gathering by hand goes to <see cref="RemoteGather"/>.
    /// </summary>
    public bool ShowOnly { get; set; }

    /// <summary>Where a <see cref="ShowOnly"/> copy sends its player's gathering by hand (to the server). Null = nothing happens.</summary>
    public Func<OreDeposit, CombatEntity, bool> RemoteGather { get; set; }

    /// <summary>
    /// Numbers each new lump when the same seam exists more than once at a time (a cave visited by two groups: each has
    /// its copy, the cave keeps one count for both), so no two lumps are ever the same. Null = this seam's own count.
    /// </summary>
    public Func<OreDeposit, int> LumpNumber { get; set; }

    private void Update()
    {
        if (WorkedOut && regrowSeconds > 0f && regrowing && Time.time - depletedAt >= regrowSeconds && !ShowOnly)
        {
            taken = 0;
            gathered = 0;
            regrowing = false;
            ShowState();
            RaiseChanged();
        }
    }

    private void ShowState()
    {
        if (depletedVisual != null) depletedVisual.SetActive(WorkedOut);
        if (richVisual != null) richVisual.SetActive(!WorkedOut);
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    // ------------------------------------------------------------------ set up from code
    /// <summary>
    /// Sets the seam up from code (a cave placing its deposits; pooled seams set up again): what it gives
    /// (<paramref name="id"/> of <paramref name="kind"/>), which seam it is, how much it holds, how hard and slow it is,
    /// the gem it carries. Every count starts again (a full, untouched seam); the lumps are numbered from 0.
    /// </summary>
    public void Configure(string id, CraftStockKind kind = CraftStockKind.Ore, int seam = -1, int lumpCount = 12, float workPerLumpValue = 30f,
        float regrow = 900f, string gemId = "", float gemByProductChance = 0.15f, bool byHand = true, int looseStoneCount = 3,
        float toolDamageNeeded = 0f, string name = "", CraftStockItemSO item = null)
    {
        ore = id ?? "";
        yieldKind = kind;
        deposit = seam >= 0 ? seam & 0xFFFFF : -1;
        lumps = Mathf.Max(1, lumpCount);
        workPerLump = Mathf.Max(0.1f, workPerLumpValue);
        regrowSeconds = Mathf.Max(0f, regrow);
        gem = gemId ?? "";
        gemChance = Mathf.Clamp01(gemByProductChance);
        gatherByHand = byHand;
        looseStones = Mathf.Max(0, looseStoneCount);
        minToolDamage = Mathf.Max(0f, toolDamageNeeded);
        displayName = name ?? "";
        oreItem = item;
        ResetState();
    }

    /// <summary>Visuals swapped while worked out (rich rock hidden, depleted rock shown); null = none.</summary>
    public void SetVisuals(GameObject rich, GameObject depleted, AudioClip breakClip = null)
    {
        richVisual = rich;
        depletedVisual = depleted;
        if (breakClip != null)
            breakSound = breakClip;
        ShowState();
    }

    /// <summary>A full, untouched seam again (every count back to zero, its lumps numbered from 0).</summary>
    public void ResetState()
    {
        taken = gathered = issued = 0;
        work = 0f;
        depletedAt = 0f;
        regrowing = false;
        ShowState();
    }

    // ------------------------------------------------------------------ saving
    /// <summary>What a seam keeps between visits (see <see cref="CaptureState"/>).</summary>
    [Serializable]
    public struct SavedState
    {
        public int taken;
        public int gathered;
        public int issued;
        public float work;
        /// <summary>Seconds it had been worked out when saved (-1 = not worked out).</summary>
        public float depletedFor;
    }

    /// <summary>The seam as it is now: lumps taken, stones gathered, lumps ever given, work on the next lump, how long it has been worked out.</summary>
    public SavedState CaptureState() => new SavedState
    {
        taken = taken,
        gathered = gathered,
        issued = issued,
        work = work,
        depletedFor = WorkedOut && regrowing ? Mathf.Max(0f, Time.time - depletedAt) : -1f,
    };

    /// <summary>
    /// Puts a kept state back, <paramref name="secondsPassed"/> after it was kept: a worked-out seam goes on growing back
    /// (and is whole again when it has had its time). A seam worked out before anything counted its time (no
    /// <see cref="SavedState.depletedFor"/>) counts from when it was kept.
    /// </summary>
    public void RestoreState(SavedState state, float secondsPassed)
    {
        taken = Mathf.Clamp(state.taken, 0, lumps);
        gathered = Mathf.Max(0, state.gathered);
        issued = Mathf.Max(issued, Mathf.Max(state.issued, taken));
        work = Mathf.Max(0f, state.work);
        regrowing = false;
        if (WorkedOut)
        {
            float elapsed = Mathf.Max(0f, state.depletedFor) + Mathf.Max(0f, secondsPassed);
            if (regrowSeconds > 0f && elapsed >= regrowSeconds)
            {
                taken = 0;
                gathered = 0;
            }
            else
            {
                // Its time so far counts on (this may lie before the session started: a negative Time.time is fine).
                depletedAt = Time.time - elapsed;
                regrowing = true;
            }
        }
        ShowState();
        RaiseChanged();
    }

    /// <summary>The item a lump of this deposit is.</summary>
    public CraftStockItemSO LumpItem => oreItem != null ? oreItem : CraftItems.Stock(CraftDatabase.Instance.Rules, yieldKind, ore);

    /// <summary>Breaks one lump off into <paramref name="player"/>'s inventory (where the rules run).</summary>
    public bool TakeLump(CombatEntity player) => Take(player, false);

    /// <summary>Gathers one loose stone by hand for <paramref name="player"/> (where the rules run). True when one was taken.</summary>
    public bool GatherFor(CombatEntity player)
    {
        if (player == null || !gatherByHand || WorkedOut || gathered >= looseStones)
            return false;
        return Take(player, true);
    }

    private bool Take(CombatEntity player, bool byHand)
    {
        if (ShowOnly || WorkedOut || !GameNet.NetAuthority.RunsRules(gameObject))
            return false;
        CraftStockItemSO item = LumpItem;
        if (item == null)
            return false;
        // Numbered on: a seam grown back gives new lumps, not the same ones again (nor does another copy of the seam).
        int lump = LumpNumber != null ? Mathf.Max(issued, LumpNumber(this)) : issued;
        issued = lump + 1;
        List<int> codes = CodesFor(item, lump);
        taken++;
        if (byHand)
            gathered++;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int left = inv != null ? inv.AddItem(item, 1, codes) : 1;
        if (left > 0)
            ItemHandler.SpawnWorldItem(item, left, codes, transform.position + Vector3.up * 0.5f, gameObject);
        GiveGem(inv, lump);
        if (breakSound != null)
            GameAudio.Play(breakSound, transform.position);
        if (WorkedOut)
        {
            depletedAt = Time.time;
            regrowing = true;
        }
        ShowState();
        LumpTaken?.Invoke(this, player, item, lump);
        AnyLumpTaken?.Invoke(this, player, item);
        RaiseChanged();
        return true;
    }

    /// <summary>
    /// The value each copy keeps: an ore lump its seam and number, a rough stone which stone it is; fuel, supplies and
    /// quenchants are all alike (none).
    /// </summary>
    private List<int> CodesFor(CraftStockItemSO item, int lump)
    {
        switch (item.StockKind)
        {
            case CraftStockKind.Ore:
                return new List<int> { CraftStockItemSO.OreCode(Deposit, lump) };
            case CraftStockKind.Gem:
                return new List<int> { CraftStockItemSO.HideCode(Deposit * 1031 + lump * 7 + 1) };
            default:
                return null;
        }
    }

    /// <summary>Now and then a lump brings a rough stone of the seam's gem out with it (every stone a stone of its own).</summary>
    private void GiveGem(InventoryManager inv, int lump)
    {
        if (string.IsNullOrEmpty(gem) || CraftMath.Noise(Deposit, lump, 61) >= gemChance)
            return;
        CraftRules rules = CraftDatabase.Instance != null ? CraftDatabase.Instance.Rules : null;
        if (rules?.Gem(gem) == null)
            return;
        CraftStockItemSO stone = CraftItems.Stock(rules, CraftStockKind.Gem, gem);
        if (stone == null)
            return;
        int code = CraftStockItemSO.HideCode(Deposit * 1031 + lump * 7 + 3);
        int left = inv != null ? inv.AddItem(stone, 1, new List<int> { code }) : 1;
        if (left > 0)
            ItemHandler.SpawnWorldItem(stone, left, new List<int> { code }, transform.position + Vector3.up * 0.6f, gameObject);
    }

    // ------------------------------------------------------------------ a pick
    public bool OnWeaponHit(in WeaponHitInfo hit)
    {
        if (hit.weapon == null || hit.weapon.ToolType != ToolType.PickAxe || WorkedOut)
            return false;
        if (ShowOnly)
            return true; // a client's copy: the server's copy breaks the lumps off; the hit still shows
        float power = Mathf.Max(0f, hit.weapon.ToolDamage);
        if (minToolDamage > 0f && power < minToolDamage)
        {
            TooHard(hit.attacker);
            return true; // the pick rings off the rock
        }
        work += power;
        while (work >= workPerLump && !WorkedOut)
        {
            work -= workPerLump;
            TakeLump(hit.attacker);
        }
        return true;
    }

    /// <summary>Tells the player their pick is too weak for this seam (at most every few seconds).</summary>
    private void TooHard(CombatEntity player)
    {
        if (player == null || Time.time - lastHardMessage < 3f)
            return;
        lastHardMessage = Time.time;
        InventoryManager inv = InventoryManager.For(player);
        if (inv != null)
            inv.ShowMessage($"{InteractionName}: your pick only scratches it - it needs a harder, better pick.", InventoryFeedback.Kind.Info);
    }

    // ------------------------------------------------------------------ by hand
    public string InteractionName => !string.IsNullOrEmpty(displayName) ? (WorkedOut ? $"{displayName} (worked out)" : displayName) : WorkedOut ? "Worked-out seam" : "Ore seam";
    public string InteractionVerb => "Gather";
    public Transform InteractionPoint => transform;
    public float InteractionRange => range;
    public int InteractionPriority => 2;
    public float HoldDuration => gatherSeconds;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (!gatherByHand)
        {
            reason = "You need a pick";
            return false;
        }
        if (WorkedOut)
        {
            reason = "Worked out";
            return false;
        }
        if (gathered >= looseStones)
        {
            reason = "Only solid rock is left: you need a pick";
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        CombatEntity player = interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;
        if (player == null)
            return;
        if (ShowOnly)
        {
            RemoteGather?.Invoke(this, player);
            return;
        }
        GatherFor(player);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    // ------------------------------------------------------------------ the network
    /// <summary>What every player must see the same (read on the server): the lumps taken and the loose stones gathered.</summary>
    public int ReplicatedState => Mathf.Clamp(taken, 0, 0x7FFF) | Mathf.Clamp(gathered, 0, 0x7FFF) << 15;

    /// <summary>A client is told the server's seam: worked out or not, its loose stones gathered or not (no rules run here).</summary>
    public void ApplyReplicatedState(int state)
    {
        taken = state & 0x7FFF;
        gathered = (state >> 15) & 0x7FFF;
        ShowState();
        RaiseChanged();
    }
}
