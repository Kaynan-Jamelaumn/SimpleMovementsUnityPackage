using UnityEngine;

/// <summary>
/// Trickster, curse and fate abilities: Acid Blood Burst, Lucky Coin, Equivalent Exchange, Phantom Exchange, Gravital
/// Pull, Gravity Well, Soul Link, Incoming Debt, Heist, Mutual Exchange, Twin Gates, Leech Life, Chronal Recall,
/// Mirror Ward, Transposition, Mana Burn and Blood Pact. Listed with the other presets and created by Generate Power Abilities.
/// </summary>
public static class TricksterAbilityPresets
{
    public static readonly AbilityPresets.Preset[] All =
    {
        P("Acid Blood Burst", "Spend 25 health: your acid blood bursts out, poisoning enemies and corroding their equipment (and a bit of yours).", AcidBloodBurst),
        P("Lucky Coin", "Flip a coin: a random stat goes up or down for 20 s.", LuckyCoin),
        P("Equivalent Exchange", "A random stat goes up and another goes down for 20 s.", EquivalentExchange),
        P("Phantom Exchange", "Leave a still phantom; cast again to swap places with it while it lasts.", PhantomExchange),
        P("Gravital Pull", "Pulls every enemy within 10 m to you.", GravitalPull),
        P("Gravity Well", "A well at the aimed point that keeps pulling enemies to its centre.", GravityWell),
        P("Soul Link", "Link your soul to a target: the damage either of you takes, the other takes too.", SoulLink),
        P("Incoming Debt", "The target's next ability costs double, or its cooldown is double.", IncomingDebt),
        P("Heist", "Steal a random item from a player's bag.", Heist),
        P("Mutual Exchange", "Swap a random stat with the target for 15 s.", MutualExchange),
        P("Twin Gates", "Open two linked gates: in front of you and at the aimed point. Anyone can go through.", TwinGates),
        P("Leech Life", "A tether that keeps draining the target's health into you.", LeechLife),
        P("Chronal Recall", "In 4 s you return to where you are now, with the health you have now.", ChronalRecall),
        P("Mirror Ward", "For 5 s, half of the damage you take goes back to the attacker.", MirrorWard),
        P("Transposition", "Swap places with the target.", Transposition),
        P("Mana Burn", "Burns 30 of the target's mana and deals damage.", ManaBurn),
        P("Blood Pact", "Spend 30 health to heal allies around you for 40.", BloodPact),
    };

    private static AbilityPresets.Preset P(string name, string description, System.Func<AbilityDefinition> create) =>
        new AbilityPresets.Preset { name = name, description = description, create = create };

    private static AbilityDefinition New(string name, string description, AbilityTag tags, float castTime, float cooldown, float mana)
    {
        var d = ScriptableObject.CreateInstance<AbilityDefinition>();
        d.name = name;
        d.SetDisplayName(name);
        d.description = description;
        d.tags = tags;
        d.castTime = castTime;
        d.cooldown = cooldown;
        d.manaCost = mana;
        d.targeting.mode = AbilityTargetingMode.Self;
        d.absorption.canBeAbsorbed = false;
        return d;
    }

    private static void OnSelf(AbilityDefinition d, params AbilityEffect[] effects) =>
        d.actions.Add(new DirectEffectAction { applyTo = DirectEffectAction.Recipient.Caster, hit = new HitSettings(TargetFilter.Self, effects) { blockedByObstacles = false } });

    private static void OnTarget(AbilityDefinition d, float range, TargetFilter filter, params AbilityEffect[] effects)
    {
        d.targeting.mode = AbilityTargetingMode.Unit;
        d.targeting.range = range;
        d.actions.Add(new DirectEffectAction { applyTo = DirectEffectAction.Recipient.Target, hit = new HitSettings(filter, effects) { blockedByObstacles = true } });
    }

    private const TargetFilter Others = TargetFilter.Enemies | TargetFilter.Neutral | TargetFilter.Allies;

    // ------------------------------------------------------------------ essence
    public static AbilityDefinition AcidBloodBurst()
    {
        AbilityDefinition d = New("Acid Blood Burst", "Your acid blood bursts out of you: enemies around you are burned and poisoned and their equipment corrodes. The acid eats your own gear too.",
            AbilityTag.Area | AbilityTag.Debuff, 0.4f, 16f, 0f);
        d.healthCost = 25f;
        d.actions.Add(new AreaHitAction
        {
            shape = HitShape.CircleShape(4.5f),
            hit = new HitSettings(TargetFilter.Enemies,
                new DamageEffect { amount = 22f, element = ElementType.Poison },
                new DamageOverTimeEffect { element = ElementType.Poison, damagePerTick = 3f, tickInterval = 0.5f, duration = 5f },
                new CorrodeEquipmentEffect { amount = 10, items = 2 }) { edgeMultiplier = 0.7f },
        });
        OnSelf(d, new CorrodeEquipmentEffect { amount = 4, items = 1, onlyAffects = TargetFilter.Self });
        d.ai.minTargets = 1;
        return d;
    }

    // ------------------------------------------------------------------ fortune
    public static AbilityDefinition LuckyCoin()
    {
        AbilityDefinition d = New("Lucky Coin", "Flip a coin. Heads: a random stat goes up. Tails: it goes down.", AbilityTag.Buff, 0.3f, 30f, 10f);
        OnSelf(d, new StatGambleEffect { mode = StatGambleMode.CoinFlip, luck = 0.5f, duration = 20f });
        return d;
    }

    public static AbilityDefinition EquivalentExchange()
    {
        AbilityDefinition d = New("Equivalent Exchange", "To gain, something of equal value must be lost: a random stat goes up and another goes down.", AbilityTag.Buff, 0.5f, 30f, 15f);
        OnSelf(d, new StatGambleEffect { mode = StatGambleMode.Exchange, duration = 20f, exchangeCost = 0.75f });
        return d;
    }

    public static AbilityDefinition MutualExchange()
    {
        AbilityDefinition d = New("Mutual Exchange", "Swap one of your stats with the target's (the one they are better at, when there is one).", AbilityTag.Debuff | AbilityTag.Buff, 0.5f, 35f, 25f);
        OnTarget(d, 12f, Others, new StatSwapEffect { duration = 15f });
        return d;
    }

    // ------------------------------------------------------------------ phantoms and space
    public static AbilityDefinition PhantomExchange()
    {
        AbilityDefinition d = New("Phantom Exchange", "Leave a still phantom of yourself. Cast again while it stands to swap places with it. It vanishes when hit or after 10 s.",
            AbilityTag.Mobility | AbilityTag.Escape, 0f, 1f, 10f);
        d.movementWhileCasting = CasterMovementRule.Free;
        OnSelf(d, new PowerEffect(10f, new PhantomAnchorPower()) { label = "Phantom Exchange", restartCooldown = 10f });
        return d;
    }

    public static AbilityDefinition TwinGates()
    {
        AbilityDefinition d = New("Twin Gates", "Opens a gate in front of you and another at the aimed point for 12 s. Anything that walks into one comes out of the other: you, your allies, your enemies.",
            AbilityTag.Mobility | AbilityTag.Support, 1f, 30f, 35f);
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 25f;
        d.targeting.requireLineOfSight = false;
        d.actions.Add(new TwinGatesAction { duration = 12f });
        return d;
    }

    public static AbilityDefinition Transposition()
    {
        AbilityDefinition d = New("Transposition", "Swap places with the target, friend or foe.", AbilityTag.Mobility, 0.2f, 15f, 15f);
        OnTarget(d, 18f, Others, new SwapPlacesEffect());
        return d;
    }

    public static AbilityDefinition GravitalPull()
    {
        AbilityDefinition d = New("Gravital Pull", "Gravity bends around you: every enemy within 10 m is dragged to you.", AbilityTag.Area | AbilityTag.Control, 0.5f, 14f, 20f);
        d.targeting.range = 10f;
        d.actions.Add(new AreaHitAction
        {
            shape = HitShape.CircleShape(10f),
            hit = new HitSettings(TargetFilter.Enemies,
                new PullEffect { maxDistance = 10f, stopDistance = 1.5f, duration = 0.4f, anchorTo = PullAnchor.Caster },
                new DamageEffect { amount = 6f },
                new SlowEffect { slowAmount = 0.3f, duration = 2f }) { blockedByObstacles = false },
        });
        d.ai.minTargets = 2;
        return d;
    }

    public static AbilityDefinition GravityWell()
    {
        AbilityDefinition d = New("Gravity Well", "A well at the aimed point keeps pulling enemies into its centre for 4 s.", AbilityTag.Area | AbilityTag.Control, 0.6f, 18f, 30f);
        d.targeting.mode = AbilityTargetingMode.Point;
        d.targeting.range = 18f;
        d.actions.Add(new ZoneAction
        {
            anchor = ActionAnchor.AimPoint,
            shape = HitShape.CircleShape(6f),
            duration = 4f,
            tickInterval = 0.5f,
            hit = new HitSettings(TargetFilter.Enemies,
                new PullEffect { maxDistance = 6f, stopDistance = 0.5f, duration = 0.3f, anchorTo = PullAnchor.HitOrigin },
                new DamageEffect { amount = 3f, variance = 0f }),
        });
        return d;
    }

    // ------------------------------------------------------------------ links and curses
    public static AbilityDefinition SoulLink()
    {
        AbilityDefinition d = New("Soul Link", "Bind your soul to a target for 10 s: whatever damage either of you takes, the other takes too. Link a tough ally, or an enemy you are winning against.",
            AbilityTag.Debuff | AbilityTag.Support, 0.5f, 30f, 25f);
        OnTarget(d, 15f, Others, new SoulLinkEffect { duration = 10f });
        return d;
    }

    public static AbilityDefinition LeechLife()
    {
        AbilityDefinition d = New("Leech Life", "A blood tether drains the target for 5 s and heals you as much. It snaps beyond 14 m.", AbilityTag.Ranged | AbilityTag.Debuff, 0.3f, 12f, 20f);
        OnTarget(d, 12f, TargetFilter.Enemies | TargetFilter.Neutral, new LeechEffect { duration = 5f, damagePerSecond = 8f });
        return d;
    }

    public static AbilityDefinition IncomingDebt()
    {
        AbilityDefinition d = New("Incoming Debt", "The target's next ability costs double, or its cooldown is double (a coin decides). Expires after 20 s.", AbilityTag.Debuff, 0.3f, 18f, 15f);
        OnTarget(d, 18f, TargetFilter.Enemies | TargetFilter.Neutral, new DebtEffect());
        return d;
    }

    public static AbilityDefinition ManaBurn()
    {
        AbilityDefinition d = New("Mana Burn", "Burns 30 of the target's mana and deals 10 damage.", AbilityTag.Ranged | AbilityTag.Debuff, 0.4f, 10f, 10f);
        OnTarget(d, 15f, TargetFilter.Enemies | TargetFilter.Neutral,
            new StatEffect { effectType = AttackEffectType.Mana, amount = -30f, effectName = "Mana Burn" },
            new DamageEffect { amount = 10f, damageType = DamageType.Magical });
        return d;
    }

    public static AbilityDefinition Heist()
    {
        AbilityDefinition d = New("Heist", "Your hand is quicker than their eye: steal a random item from a player's bag.", AbilityTag.Debuff, 0.2f, 60f, 10f);
        OnTarget(d, 3f, TargetFilter.Enemies | TargetFilter.Neutral | TargetFilter.Allies, new StealItemEffect());
        return d;
    }

    // ------------------------------------------------------------------ time and defence
    public static AbilityDefinition ChronalRecall()
    {
        AbilityDefinition d = New("Chronal Recall", "Mark this moment. In 4 s you return here, with the health you have now.", AbilityTag.Defensive | AbilityTag.Escape, 0f, 25f, 25f);
        d.movementWhileCasting = CasterMovementRule.Free;
        OnSelf(d, new PowerEffect(4f, new ChronalRecallPower()) { label = "Chronal Recall" });
        return d;
    }

    public static AbilityDefinition MirrorWard()
    {
        AbilityDefinition d = New("Mirror Ward", "For 5 s, half of every hit you take is dealt back to the attacker.", AbilityTag.Defensive, 0.2f, 20f, 20f);
        OnSelf(d, new PowerEffect(5f, new ReflectDamagePower { reflectPercent = 50f }) { label = "Mirror Ward" });
        d.ai.defensive = true;
        return d;
    }

    public static AbilityDefinition BloodPact()
    {
        AbilityDefinition d = New("Blood Pact", "Spend 30 of your health to heal every ally within 8 m for 40.", AbilityTag.Support, 0.6f, 25f, 0f);
        d.healthCost = 30f;
        d.actions.Add(new AreaHitAction
        {
            shape = HitShape.CircleShape(8f),
            hit = new HitSettings(TargetFilter.Allies, new HealEffect { amount = 40f }) { blockedByObstacles = false },
        });
        return d;
    }
}
