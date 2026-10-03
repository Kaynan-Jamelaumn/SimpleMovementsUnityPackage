using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What happens when the player absorbs an ability it already has.</summary>
public enum DuplicateAbsorbPolicy
{
    /// <summary>Keep the one it has; the new copy is refused.</summary>
    KeepExisting,
    /// <summary>Replace it only if the new copy is stronger.</summary>
    ReplaceIfStronger,
    /// <summary>Always replace it with the new copy.</summary>
    AlwaysReplace,
    /// <summary>Put the new copy in another slot (both are kept).</summary>
    AddAnotherSlot,
}

/// <summary>What happens when the player absorbs an ability with every slot full.</summary>
public enum FullSlotsPolicy
{
    /// <summary>The new ability is refused.</summary>
    Refuse,
    /// <summary>Replace the ability absorbed longest ago (starting abilities are never replaced).</summary>
    ReplaceOldestAbsorbed,
    /// <summary>Replace the last slot.</summary>
    ReplaceLastSlot,
}

/// <summary>How absorbed abilities reach the player.</summary>
public enum AbsorbDelivery
{
    /// <summary>A glowing pickup drops where the mob died; the player walks over it.</summary>
    Pickup,
    /// <summary>Granted immediately to the killer.</summary>
    Instant,
}

/// <summary>
/// An ability given to a character (absorbed from a mob, a reward...): which ability and with which changes.
/// Serializable and savable through <see cref="ToSaveData"/>.
/// </summary>
[Serializable]
public class AbilityGrant
{
    public AbilityDefinition ability;
    public AbilityModifierSet modifiers = new AbilityModifierSet();
    [Tooltip("Name shown to the player, e.g. 'Lesser Firebolt'.")]
    public string displayName = "";
    public AbsorbTier tier = AbsorbTier.Same;
    [Tooltip("The mob ability it came from (can differ from Ability when the rules say 'Absorbed As').")]
    public AbilityDefinition sourceAbility;
    [NonSerialized] public float receivedTime;

    public AbilityGrant() { }

    public AbilityGrant(AbilityDefinition ability, AbilityModifierSet modifiers, string displayName, AbsorbTier tier = AbsorbTier.Same, AbilityDefinition source = null)
    {
        this.ability = ability;
        this.modifiers = modifiers != null ? modifiers.Clone() : new AbilityModifierSet();
        this.displayName = displayName ?? "";
        this.tier = tier;
        sourceAbility = source != null ? source : ability;
    }

    public string Name => !string.IsNullOrEmpty(displayName) ? displayName : (ability != null ? ability.DisplayName : "(none)");

    /// <summary>Rough strength of an ability with modifiers (compares absorbed copies).</summary>
    public static float Power(AbilityDefinition def, AbilityModifierSet mods)
    {
        if (def == null)
            return 0f;
        AbilityStats s = AbilityStats.From(mods);
        float dmg = def.EstimateDamage(s) + def.EstimateControl(s) * 8f;
        float cd = Mathf.Max(0.5f, def.Cooldown(s) + def.CastTime(s));
        return dmg / cd * (1f + def.MaxReach(s) * 0.01f);
    }

    public string Describe() => ability != null ? ability.Describe(modifiers) : "(none)";

    public AbilityGrantSaveData ToSaveData() => new AbilityGrantSaveData
    {
        abilityId = ability != null ? ability.Id : "",
        sourceAbilityId = sourceAbility != null ? sourceAbility.Id : "",
        modifiersJson = modifiers != null ? JsonUtility.ToJson(modifiers) : "",
        displayName = displayName,
        tier = tier,
    };

    /// <summary>Rebuilds a grant from save data (abilities are looked up in the Ability Database).</summary>
    public static AbilityGrant FromSaveData(AbilityGrantSaveData data, AbilityDatabase database = null)
    {
        if (data == null || string.IsNullOrEmpty(data.abilityId))
            return null;
        if (database == null)
            database = AbilityDatabase.Instance;
        AbilityDefinition def = database != null ? database.Find(data.abilityId) : null;
        if (def == null)
        {
            Debug.LogWarning($"Absorbed ability '{data.abilityId}' is not in the Ability Database; it cannot be restored.");
            return null;
        }
        var mods = new AbilityModifierSet();
        if (!string.IsNullOrEmpty(data.modifiersJson))
            JsonUtility.FromJsonOverwrite(data.modifiersJson, mods);
        AbilityDefinition src = database != null && !string.IsNullOrEmpty(data.sourceAbilityId) ? database.Find(data.sourceAbilityId) : null;
        return new AbilityGrant(def, mods, data.displayName, data.tier, src);
    }
}

/// <summary>Plain data for saving an absorbed ability (JsonUtility friendly).</summary>
[Serializable]
public class AbilityGrantSaveData
{
    /// <summary>Slot the ability was in (-1 = any free slot).</summary>
    public int slot = -1;
    public string abilityId;
    public string sourceAbilityId;
    public string modifiersJson;
    public string displayName;
    public AbsorbTier tier;
}

/// <summary>
/// The absorption mechanic: when a mob with absorbable abilities is killed by a player, each ability rolls its
/// chance; a winning roll creates an <see cref="AbilityGrant"/> (the same, weaker, stronger or altered copy - e.g. a
/// mob's triple shot becomes a single shot) delivered as a pickup or instantly. Listens to
/// <see cref="CombatEvents.Killed"/> automatically.
/// </summary>
/// <summary>An ability a kill can grant and its share (0-1) of a successful absorption roll.</summary>
public struct AbsorptionShare
{
    public AbilityDefinition ability;
    public float share;
}

/// <summary>
/// A character that decides itself what killing it can grant: ONE chance per kill, then a share per ability
/// (shares add up to 1 at most; the rest means nothing). Mobs implement it in <see cref="MobAbilityController"/>.
/// Casters without it use each ability's own Absorption ▸ Absorb Chance instead.
/// </summary>
public interface IAbsorptionTable
{
    /// <summary>Chance (0-1) that a kill grants an ability, before the global multiplier.</summary>
    float ChancePerKill { get; }

    /// <summary>Fills <paramref name="into"/> with the abilities a successful roll can grant and their shares.</summary>
    void GetAbsorptionShares(List<AbsorptionShare> into);
}

public static class AbilityAbsorption
{
    /// <summary>A kill produced a grant (before delivery). Victim, killer (player side), grant.</summary>
    public static event Action<CombatEntity, CombatEntity, AbilityGrant> GrantRolled;
    /// <summary>A grant was received by a character (receiver entity, grant, slot index).</summary>
    public static event Action<CombatEntity, AbilityGrant, int> GrantReceived;
    /// <summary>A grant was refused (full slots / duplicate policy) (receiver entity, grant).</summary>
    public static event Action<CombatEntity, AbilityGrant> GrantRefused;

    private static readonly List<AbilitySlot> candidates = new List<AbilitySlot>(8);
    private static readonly List<AbilityVariant> variants = new List<AbilityVariant>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        GrantRolled = null;
        GrantReceived = null;
        GrantRefused = null;
        CombatEvents.Killed -= OnKilled;
        CombatEvents.Killed += OnKilled;
    }

    private static void OnKilled(CombatEntity victim, CombatEntity killer)
    {
        AbsorptionSettings settings = AbsorptionSettings.Instance;
        if (!settings.enabled || victim == null)
            return;
        AbilityCaster caster = victim.Caster;
        if (caster == null || !caster.CanBeAbsorbedFrom)
            return;

        CombatEntity player = PlayerSide(killer);
        if (player == null && settings.onlyPlayerKills)
            return;

        if (caster is IAbsorptionTable table)
        {
            RollTable(table, victim, player, settings);
            return;
        }

        candidates.Clear();
        IReadOnlyList<AbilitySlot> slots = caster.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s != null && s.ability != null && s.ability.absorption.canBeAbsorbed)
                candidates.Add(s);
        }
        // Shuffle so the slot order does not favour the first abilities.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            AbilitySlot tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        int granted = 0;
        for (int i = 0; i < candidates.Count && granted < settings.maxAbilitiesPerKill; i++)
        {
            AbilityDefinition def = candidates[i].ability;
            float chance = def.absorption.absorbChance * settings.globalChanceMultiplier * caster.AbsorbChanceMultiplier;
            float roll = UnityEngine.Random.value;
            if (settings.logRolls)
                Debug.Log($"[Absorption] {victim.name}: {def.DisplayName} chance {chance:P0}, roll {roll:0.00} → {(roll < chance ? "WIN" : "no")}");
            if (roll >= chance)
                continue;
            AbilityGrant grant = CreateGrant(def);
            if (grant == null)
                continue;
            granted++;
            GrantRolled?.Invoke(victim, player, grant);
            Deliver(grant, victim.Center, player);
        }
        candidates.Clear();
    }

    private static readonly List<AbsorptionShare> shares = new List<AbsorptionShare>(8);

    /// <summary>
    /// One roll with the table's chance per kill; on success one ability is picked by share (the part of 100% not
    /// given to any ability means nothing). Repeats up to Max Abilities Per Kill, without picking an ability twice.
    /// </summary>
    private static void RollTable(IAbsorptionTable table, CombatEntity victim, CombatEntity player, AbsorptionSettings settings)
    {
        shares.Clear();
        table.GetAbsorptionShares(shares);
        float chance = Mathf.Clamp01(table.ChancePerKill * settings.globalChanceMultiplier);
        for (int n = 0; n < settings.maxAbilitiesPerKill && shares.Count > 0; n++)
        {
            float roll = UnityEngine.Random.value;
            bool win = roll < chance;
            if (settings.logRolls)
                Debug.Log($"[Absorption] {victim.name}: chance per kill {chance:P0}, roll {roll:0.00} → {(win ? "WIN" : "no")}");
            if (!win)
                break;

            float pick = UnityEngine.Random.value;
            int chosen = -1;
            for (int i = 0; i < shares.Count; i++)
            {
                pick -= shares[i].share;
                if (pick < 0f)
                {
                    chosen = i;
                    break;
                }
            }
            if (chosen < 0)
            {
                if (settings.logRolls)
                    Debug.Log($"[Absorption] {victim.name}: the share roll landed on 'no ability'.");
                break;
            }

            AbilityDefinition def = shares[chosen].ability;
            shares.RemoveAt(chosen);
            if (settings.logRolls)
                Debug.Log($"[Absorption] {victim.name}: picked {def.DisplayName}.");
            AbilityGrant grant = CreateGrant(def);
            if (grant == null)
                continue;
            GrantRolled?.Invoke(victim, player, grant);
            Deliver(grant, victim.Center, player);
        }
        shares.Clear();
    }

    /// <summary>The player responsible for a kill (the killer, or the summoner of a summon), or null.</summary>
    public static CombatEntity PlayerSide(CombatEntity killer)
    {
        CombatEntity k = killer;
        for (int guard = 0; k != null && guard < 4; guard++)
        {
            if (k.Kind == CombatEntity.EntityKind.Player)
                return k;
            k = k.Summoner;
        }
        return null;
    }

    /// <summary>Rolls the absorbed form of <paramref name="source"/> (variant, modifiers, name).</summary>
    public static AbilityGrant CreateGrant(AbilityDefinition source)
    {
        if (source == null)
            return null;
        AbsorptionRules rules = source.absorption;
        AbilityDefinition granted = rules.absorbedAs != null ? rules.absorbedAs : source;

        variants.Clear();
        if (rules.useDefaultVariants)
        {
            List<AbilityVariant> defaults = AbsorptionSettings.Instance.defaultVariants;
            for (int i = 0; i < defaults.Count; i++)
                if (defaults[i] != null && defaults[i].weight > 0f)
                    variants.Add(defaults[i]);
        }
        for (int i = 0; i < rules.customVariants.Count; i++)
            if (rules.customVariants[i] != null && rules.customVariants[i].weight > 0f)
                variants.Add(rules.customVariants[i]);

        AbilityVariant v = PickWeighted(variants);
        variants.Clear();

        AbilityModifierSet mods = rules.absorbedBaseModifiers != null ? rules.absorbedBaseModifiers.Clone() : new AbilityModifierSet();
        AbsorbTier tier = AbsorbTier.Same;
        string name = granted.DisplayName;
        if (v != null)
        {
            mods = mods.CombinedWith(v.modifiers);
            tier = v.tier;
            if (!string.IsNullOrEmpty(v.name))
                name = v.name;
            else if (v.modifiers != null && !string.IsNullOrEmpty(v.modifiers.label))
                name = $"{granted.DisplayName} ({v.modifiers.label})";
            else if (tier != AbsorbTier.Same)
                name = $"{granted.DisplayName} ({tier})";
        }
        return new AbilityGrant(granted, mods, name, tier, source);
    }

    private static AbilityVariant PickWeighted(List<AbilityVariant> list)
    {
        if (list.Count == 0)
            return null;
        float total = 0f;
        for (int i = 0; i < list.Count; i++)
            total += list[i].weight;
        float r = UnityEngine.Random.value * total;
        for (int i = 0; i < list.Count; i++)
        {
            r -= list[i].weight;
            if (r <= 0f)
                return list[i];
        }
        return list[list.Count - 1];
    }

    /// <summary>Delivers a grant as configured: a pickup at <paramref name="position"/> or instantly to <paramref name="player"/>.</summary>
    public static void Deliver(AbilityGrant grant, Vector3 position, CombatEntity player)
    {
        AbsorptionSettings settings = AbsorptionSettings.Instance;
        if (settings.delivery == AbsorbDelivery.Instant && player != null)
        {
            TryGrant(grant, player);
            return;
        }
        AbilityPickup.Spawn(grant, position, settings);
    }

    /// <summary>Gives a grant to a character. Returns true if it was accepted.</summary>
    public static bool TryGrant(AbilityGrant grant, CombatEntity receiver)
    {
        if (grant == null || receiver == null)
            return false;
        IAbilityReceiver r = FindReceiver(receiver);
        if (r == null)
        {
            Debug.LogWarning($"[Absorption] {receiver.name} has no ability receiver (PlayerAbilityController); '{grant.Name}' was lost.", receiver);
            return false;
        }
        if (r.CanReceive(grant) && r.Receive(grant, out int slot))
        {
            GrantReceived?.Invoke(receiver, grant, slot);
            return true;
        }
        GrantRefused?.Invoke(receiver, grant);
        return false;
    }

    public static IAbilityReceiver FindReceiver(CombatEntity e)
    {
        if (e == null)
            return null;
        if (e.Caster is IAbilityReceiver direct)
            return direct;
        IAbilityReceiver r = e.GetComponentInChildren<IAbilityReceiver>();
        if (r == null)
            r = e.GetComponentInParent<IAbilityReceiver>();
        return r;
    }
}
