using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

/// <summary>
/// Gives a sound (and, through the cue, an effect and an Animator trigger) to things that happen in the game and had
/// none: a carcass harvested, loot dropping, a trade, a knock-out, a chest opening... One component for the whole game:
/// put it in the scene (on the SoundManager's object, for instance) and fill the slots you want - an empty slot does
/// nothing. It only listens to the events the systems already raise, so no system depends on it.
/// <para><b>Where it is heard.</b> A cue plays on the machine where the game raises the event. Most of these events
/// are raised where the rules run: in a single-player game that is this machine, in a network game it is the host or
/// the server - other clients hear the sounds that their own systems play (attacks, abilities, hits, deaths, doors,
/// injuries) but not these. The slots marked "this player" are raised for the local player on every machine.</para>
/// <para>A cue set to 3D is heard from where the thing happened; one with an Animator trigger sets it on the character
/// who did it (the harvester's "Harvest" trigger, for example).</para>
/// </summary>
[DisallowMultipleComponent]
public class GameCues : MonoBehaviour
{
    [Header("Harvesting and loot")]
    [Tooltip("A carcass was harvested. Played on the harvester (its Animator trigger is set on them).")]
    [SerializeField] private AudioCue harvested;
    [Tooltip("Loot dropped, where it fell.")]
    [SerializeField] private AudioCue lootDropped;
    [Tooltip("An ingredient went into an alchemy vessel. Played on who added it.")]
    [SerializeField] private AudioCue ingredientAdded;

    [Header("Characters")]
    [Tooltip("A character was knocked out. Played on them.")]
    [SerializeField] private AudioCue knockedOut;
    [Tooltip("A character was brought back from a knock-out. Played on them.")]
    [SerializeField] private AudioCue broughtBack;
    [Tooltip("A character gained an ability from a kill. Played on them.")]
    [SerializeField] private AudioCue abilityGained;

    [Header("Money and trade")]
    [Tooltip("A purchase or a sale went through, at the merchant.")]
    [SerializeField] private AudioCue trade;
    [Tooltip("A trade was refused (no room, not enough money), at the merchant.")]
    [SerializeField] private AudioCue tradeRefused;
    [Tooltip("This player's money went up.")]
    [SerializeField] private AudioCue moneyGained;
    [Tooltip("This player's money went down.")]
    [SerializeField] private AudioCue moneySpent;

    [Header("Dungeon")]
    [SerializeField] private AudioCue chestOpened;
    [SerializeField] private AudioCue leverPulled;
    [SerializeField] private AudioCue puzzleSolved;
    [SerializeField] private AudioCue shrineBlessing;
    [SerializeField] private AudioCue nestDestroyed;
    [SerializeField] private AudioCue restPointUsed;

    private static GameCues active;
    private bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;

    private void OnEnable()
    {
        // One listener, however many scenes carry the component (a second one would play every sound twice).
        if (active != null && active != this)
            return;
        active = this;
        Hook(true);
    }

    private void OnDisable()
    {
        Hook(false);
        if (active == this)
            active = null;
    }

    private void Hook(bool on)
    {
        if (hooked == on)
            return;
        hooked = on;
        if (on)
        {
            HarvestableCreature.AnyHarvested += OnHarvested;
            ItemDropManager.Dropped += OnLootDropped;
            AlchemyStation.IngredientAdded += OnIngredientAdded;
            Unconsciousness.AnyKnockedOut += OnKnockedOut;
            Unconsciousness.AnyRevived += OnBroughtBack;
            AbilityAbsorption.GrantReceived += OnAbilityGained;
            Merchant.AnyTransaction += OnTransaction;
            CurrencyWallet.AnyBalanceChanged += OnBalanceChanged;
            DungeonChest.AnyOpened += OnChestOpened;
            DungeonLever.AnyPulled += OnLeverPulled;
            DungeonPuzzle.AnySolved += OnPuzzleSolved;
            DungeonShrine.AnyBlessing += OnShrineBlessing;
            DungeonNest.AnyDestroyed += OnNestDestroyed;
            DungeonRestPoint.AnyUsed += OnRestPointUsed;
        }
        else
        {
            HarvestableCreature.AnyHarvested -= OnHarvested;
            ItemDropManager.Dropped -= OnLootDropped;
            AlchemyStation.IngredientAdded -= OnIngredientAdded;
            Unconsciousness.AnyKnockedOut -= OnKnockedOut;
            Unconsciousness.AnyRevived -= OnBroughtBack;
            AbilityAbsorption.GrantReceived -= OnAbilityGained;
            Merchant.AnyTransaction -= OnTransaction;
            CurrencyWallet.AnyBalanceChanged -= OnBalanceChanged;
            DungeonChest.AnyOpened -= OnChestOpened;
            DungeonLever.AnyPulled -= OnLeverPulled;
            DungeonPuzzle.AnySolved -= OnPuzzleSolved;
            DungeonShrine.AnyBlessing -= OnShrineBlessing;
            DungeonNest.AnyDestroyed -= OnNestDestroyed;
            DungeonRestPoint.AnyUsed -= OnRestPointUsed;
        }
    }

    // ------------------------------------------------------------------ what happened
    private void OnHarvested(HarvestableCreature creature, CombatEntity harvester, HarvestReport report)
    {
        if (harvester != null) On(harvested, harvester);
        else At(harvested, creature);
    }

    private void OnLootDropped(ItemDropTable table, IReadOnlyList<ItemStack> items, Vector3 position, GameObject source)
    {
        if (lootDropped != null && items != null && items.Count > 0)
            lootDropped.Play(position);
    }

    private void OnIngredientAdded(CombatEntity who, ItemSO item, int count) => On(ingredientAdded, who);

    private void OnKnockedOut(CombatEntity victim, CombatEntity attacker) => On(knockedOut, victim);

    private void OnBroughtBack(CombatEntity revived, CombatEntity reviver) => On(broughtBack, revived);

    private void OnAbilityGained(CombatEntity receiver, AbilityGrant grant, int slot) => On(abilityGained, receiver);

    private void OnTransaction(Merchant merchant, MerchantTransactionResult result) =>
        At(result.Succeeded ? trade : tradeRefused, merchant);

    private void OnBalanceChanged(CurrencyWallet wallet, CurrencyDefinition currency, int before, int after)
    {
        if (wallet == null || before == after || !IsLocalPlayer(wallet.gameObject))
            return;
        AudioCue cue = after > before ? moneyGained : moneySpent;
        if (cue != null)
            cue.Play(wallet.transform.position);
    }

    private void OnChestOpened(DungeonChest chest, int items) => At(chestOpened, chest);
    private void OnLeverPulled(DungeonLever lever) => At(leverPulled, lever);
    private void OnPuzzleSolved(DungeonPuzzle puzzle) => At(puzzleSolved, puzzle);
    private void OnShrineBlessing(DungeonShrine shrine, DungeonShrine.Blessing blessing) => At(shrineBlessing, shrine);
    private void OnNestDestroyed(DungeonNest nest) => At(nestDestroyed, nest);
    private void OnRestPointUsed(DungeonRestPoint point) => At(restPointUsed, point);

    // ------------------------------------------------------------------ helpers
    /// <summary>On a character: at its middle, with the cue's effect attached and its Animator trigger set.</summary>
    private static void On(AudioCue cue, CombatEntity who)
    {
        if (cue != null && who != null)
            cue.Play(who.gameObject);
    }

    /// <summary>Where a thing of the world is.</summary>
    private static void At(AudioCue cue, Component thing)
    {
        if (cue != null && thing != null)
            cue.Play(thing.transform.position);
    }

    private static bool IsLocalPlayer(GameObject go)
    {
        CombatEntity e = CombatEntity.Resolve(go);
        GameObject root = e != null ? e.gameObject : go;
        return GameNet.NetAuthority.IsNetworkedObject(root)
            ? GameNet.NetAuthority.IsLocallyControlled(root)
            : GameNet.NetAuthority.HasLocalPlayer;
    }
}
