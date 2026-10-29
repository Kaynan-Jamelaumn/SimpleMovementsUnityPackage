using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A seam of ore in the world. Hit it with a pick (a weapon whose tool type is Pick Axe: its Tool Damage breaks the
/// rock) or work it by hand (hold Interact: loose surface stones, slowly) and lumps of ore break off into the
/// inventory. Every lump of a deposit comes from the same seam - its richness, its rock and its impurities - and no
/// two lumps are quite alike: they keep their deposit and their number, so the station recreates exactly that lump.
/// A deposit gives so many lumps, then is worked out for a while.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Crafting/Ore Deposit")]
public class OreDeposit : MonoBehaviour, IInteractable, IWeaponHittable
{
    [Tooltip("The ore (a Crafting Database ore id: hematite, magnetite, bog_iron, malachite, chalcopyrite, cassiterite, galena, native_silver, gold_quartz, calamine, meteorite, mythril_ore, orichalcum_ore).")]
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

    private int taken, gathered;
    private float work, depletedAt = -1f;

    public string Ore => ore;
    public int Deposit => deposit >= 0 ? deposit : (int)(CraftMath.Hash(Mathf.RoundToInt(transform.position.x * 10f), Mathf.RoundToInt(transform.position.z * 10f), Mathf.RoundToInt(transform.position.y)) & 0xFFFFF);
    public int LumpsLeft => Mathf.Max(0, lumps - taken);
    public bool WorkedOut => LumpsLeft <= 0;

    private void Update()
    {
        if (WorkedOut && regrowSeconds > 0f && depletedAt >= 0f && Time.time - depletedAt >= regrowSeconds)
        {
            taken = 0;
            gathered = 0;
            depletedAt = -1f;
            ShowState();
        }
    }

    private void ShowState()
    {
        if (depletedVisual != null) depletedVisual.SetActive(WorkedOut);
        if (richVisual != null) richVisual.SetActive(!WorkedOut);
    }

    /// <summary>The item a lump of this deposit is.</summary>
    public CraftStockItemSO LumpItem => oreItem != null ? oreItem : CraftItems.Stock(CraftDatabase.Instance.Rules, CraftStockKind.Ore, ore);

    /// <summary>Breaks one lump off into <paramref name="player"/>'s inventory (where the rules run).</summary>
    public bool TakeLump(CombatEntity player)
    {
        if (WorkedOut || !GameNet.NetAuthority.RunsRules(gameObject))
            return false;
        CraftStockItemSO item = LumpItem;
        if (item == null)
            return false;
        int code = CraftStockItemSO.OreCode(Deposit, taken);
        taken++;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int left = inv != null ? inv.AddItem(item, 1, new List<int> { code }) : 1;
        if (left > 0)
            ItemHandler.SpawnWorldItem(item, left, new List<int> { code }, transform.position + Vector3.up * 0.5f, gameObject);
        GiveGem(inv, taken - 1);
        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position);
        if (WorkedOut)
            depletedAt = Time.time;
        ShowState();
        return true;
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
        work += Mathf.Max(0f, hit.weapon.ToolDamage);
        while (work >= workPerLump && !WorkedOut)
        {
            work -= workPerLump;
            TakeLump(hit.attacker);
        }
        return true;
    }

    // ------------------------------------------------------------------ by hand
    public string InteractionName => WorkedOut ? "Worked-out seam" : "Ore seam";
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
        if (player != null && TakeLump(player))
            gathered++;
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }
}
