using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ready-made disabilities: traits with a negative cost (they GIVE trait points to spend on other traits) that take
/// something away from the character - for good (a limp, a blind eye, deafness, a missing hand, an appetite that is
/// never satisfied, an action it can never do) or now and then, at random and with a strength that changes (migraines,
/// sleep attacks, a knee that gives way, a pain with good hours and bad ones).
/// <para>They are part of <see cref="TraitPresets.All"/>: Assets ▸ Create ▸ SimpleMovements ▸ Character ▸ Trait From
/// Preset ▸ Disability, the Trait Database's preset menu, or all at once with Tools ▸ SimpleMovements ▸ Player ▸ Create
/// Disability Traits (which also makes the degrees of one thing - Limp / Bad Leg / Lame - mutually exclusive).</para>
/// </summary>
public static class DisabilityTraitPresets
{
    public const string Category = "Disability";

    // ------------------------------------------------------------------ small builders
    private static TraitModifier Pct(TraitStat stat, float value) => TraitModifier.Percent(stat, value);
    private static TraitModifier Flat(TraitStat stat, float value) => TraitModifier.Flat(stat, value);
    private static CombatStatModifier Combat(CombatStatType stat, float value) => new CombatStatModifier(stat, value);
    private static BlockActionsTrait Cannot(BodyAction actions, string reason) => new BlockActionsTrait { actions = actions, reason = reason };

    private static VisionImpairmentTrait Sight(BodyPartSide eye, float darkness, float haze = 0f, float tunnel = 0f, float sway = 0f, SightWhen when = SightWhen.Always) =>
        new VisionImpairmentTrait { eye = eye, sight = new VisionEffect(darkness, haze, tunnel, sway), when = when };

    private static InjuryResistanceTrait Weakness(string label, float injuryChance, float worsening, params string[] injuries) =>
        new InjuryResistanceTrait { resistances = new List<InjuryResistance> { new InjuryResistance(label, injuries) { injuryChance = injuryChance, worsening = worsening } } };

    /// <summary>
    /// A disability: <paramref name="points"/> it gives, what it does in a few words (the menu's tooltip and the first
    /// line of the trait), a line of flavour, and its parts (modifiers, combat stats, behaviours).
    /// </summary>
    private static TraitPresets.Preset D(string name, int points, string group, TraitType type, string summary, string flavour, params object[] parts)
    {
        string gives = $"Gives {points} point{(points == 1 ? "" : "s")}.";
        return TraitPresets.P(Category, name, $"{summary} {gives}", () => TraitPresets.New(name, flavour, -points, type, TraitPresets.Bad, parts), group);
    }

    /// <summary>Every disability preset.</summary>
    public static TraitPresets.Preset[] Build() => new[]
    {
        // ============================================================== legs and movement
        D("Limp", 1, "legs", TraitType.Movement, "-12% move speed, -15% sprint speed.", "An old injury never quite healed.",
            Pct(TraitStat.MoveSpeed, -12f), Pct(TraitStat.SprintSpeed, -15f)),
        D("Bad Leg", 3, "legs", TraitType.Movement, "-20% move speed, -20% jump, cannot sprint.", "One leg drags behind the other.",
            Pct(TraitStat.MoveSpeed, -20f), Pct(TraitStat.JumpForce, -20f), Cannot(BodyAction.Sprint, "Your bad leg will not run")),
        D("Lame", 5, "legs", TraitType.Movement, "-35% move speed; cannot sprint, jump, dash or roll.", "Every step is an effort.",
            Pct(TraitStat.MoveSpeed, -35f), Cannot(BodyAction.Sprint | BodyAction.Jump | BodyAction.Dash | BodyAction.Roll, "Your legs will not allow it")),
        D("Missing Foot", 4, "legs", TraitType.Physical, "The left foot is missing: hobbles (injury system) or cannot sprint, dash or roll.", "Lost long ago. You learned to live without it.",
            new MissingLimbTrait { part = "LeftFoot", withoutInjurySystem = BodyAction.Sprint | BodyAction.Dash | BodyAction.Roll, reason = "You have one foot" }),
        D("One-Legged", 6, "legs", TraitType.Physical, "The left leg ends at the knee: hops (injury system) or cannot sprint, jump, dash or roll.", "One leg, and no complaints left.",
            new MissingLimbTrait { part = "LeftShin", withoutInjurySystem = BodyAction.Sprint | BodyAction.Jump | BodyAction.Dash | BodyAction.Roll, reason = "You have one leg" }),
        D("Stiff Joints", 2, null, TraitType.Movement, "Cannot dash or roll.", "The body does not bend like it used to.",
            Cannot(BodyAction.Dash | BodyAction.Roll, "Your joints are too stiff")),
        D("Bad Knees", 2, null, TraitType.Movement, "Cannot jump; +30% fall damage, -1.5 m safe fall height.", "They crack on every stair.",
            Cannot(BodyAction.Jump, "Your knees will not take a jump"), Pct(TraitStat.FallDamage, 30f), Flat(TraitStat.SafeFallHeight, -1.5f)),
        D("Trick Knee", 2, null, TraitType.Movement, "While sprinting the knee gives way now and then: a stumble, then -35% move speed for a while.", "Fine for weeks. Then, mid-stride, it is not.",
            new EpisodicDisabilityTrait
            {
                label = "Buckled Knee", broughtOnBy = EpisodeTrigger.Sprinting, timing = new EpisodeTiming(2.5f, 10f, 25f, 0.5f, 1f, 20f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -35f), Pct(TraitStat.JumpForce, -30f) } },
                stunSeconds = 1f, startMessage = "Your knee gives way!", endMessage = "Your knee holds again.",
            }),
        D("Vertigo", 2, null, TraitType.Movement, "In the air the world spins now and then: dizzy and slower for a while.", "Heights were never your friend.",
            new EpisodicDisabilityTrait
            {
                label = "Vertigo", broughtOnBy = EpisodeTrigger.InTheAir, timing = new EpisodeTiming(0.15f, 8f, 16f, 0.4f, 1f, 25f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -18f) }, combatStats = new List<CombatStatModifier> { Combat(CombatStatType.CriticalChance, -8f) } },
                sight = new VisionEffect(0f, 0.2f, 0f, 0.85f), startMessage = "The world spins.",
            }),
        D("Poor Balance", 2, null, TraitType.Movement, "Always a little dizzy; +20% fall damage; cannot roll.", "The ground is never quite where it should be.",
            Sight(BodyPartSide.Center, 0f, 0f, 0f, 0.4f), Pct(TraitStat.FallDamage, 20f), Cannot(BodyAction.Roll, "You would only fall over")),
        D("Overweight", 1, "weight", TraitType.Physical, "-6% move speed, -10% sprint speed, +20% stamina cost.", "Carries a little extra.",
            Pct(TraitStat.MoveSpeed, -6f), Pct(TraitStat.SprintSpeed, -10f), Pct(TraitStat.StaminaCost, 20f)),
        D("Obese", 3, "weight", TraitType.Physical, "-14% move speed, -20% sprint speed, +45% stamina cost, +25% fall damage; cannot roll.", "Every hill is a mountain.",
            Pct(TraitStat.MoveSpeed, -14f), Pct(TraitStat.SprintSpeed, -20f), Pct(TraitStat.StaminaCost, 45f), Pct(TraitStat.FallDamage, 25f),
            Cannot(BodyAction.Roll, "You cannot roll")),
        D("Underweight", 2, "weight", TraitType.Physical, "-12% max health, -25% carry weight, -8% weapon damage, feels the cold.", "Skin and bones.",
            Pct(TraitStat.MaxHealth, -12f), Pct(TraitStat.CarryWeight, -25f), Pct(TraitStat.WeaponDamage, -8f), Combat(CombatStatType.ColdResistance, -20f)),

        // ============================================================== arms and hands
        D("Missing Hand", 4, "arms", TraitType.Physical, "The off hand is missing: no two-handed weapons, nothing in the off hand.", "You make do with one.",
            new MissingLimbTrait { part = "LeftHand", onTheOffHandSide = true, withoutInjurySystem = BodyAction.TwoHandedWeapons | BodyAction.OffHand, reason = "You have one hand" }),
        D("One-Armed", 5, "arms", TraitType.Physical, "The off-hand arm is missing: no two-handed weapons, no shield, nothing in the off hand.", "One arm does the work of two. Slowly.",
            new MissingLimbTrait { part = "LeftUpperArm", onTheOffHandSide = true, withoutInjurySystem = BodyAction.TwoHandedWeapons | BodyAction.OffHand | BodyAction.Block, reason = "You have one arm" },
            Combat(CombatStatType.ReloadSpeed, -25f)),
        D("Weak Grip", 2, null, TraitType.Combat, "-12% attack speed, -20% charge, draw and reload speed.", "Things slip.",
            Combat(CombatStatType.AttackSpeed, -12f), Combat(CombatStatType.ChargeSpeed, -20f), Combat(CombatStatType.DrawSpeed, -20f), Combat(CombatStatType.ReloadSpeed, -20f)),
        D("Tremor", 2, null, TraitType.Combat, "The hands shake, some hours hardly, some badly: up to -15 critical chance, -12% attack speed, +15% cast time.", "Steady this morning. Not now.",
            new EpisodicDisabilityTrait
            {
                label = "Tremor", timing = new EpisodeTiming(0f, 120f, 240f, 0f, 1f, 0f),
                effect = new StatusConsequence("", true)
                {
                    characterStats = new List<TraitModifier> { Pct(TraitStat.AbilityCastTime, 15f) },
                    combatStats = new List<CombatStatModifier> { Combat(CombatStatType.CriticalChance, -15f), Combat(CombatStatType.AttackSpeed, -12f) },
                },
                startMessage = "Your hands start to shake.", endMessage = "Your hands are steady.",
            }),
        D("Clumsy", 1, null, TraitType.Physical, "Fumbles now and then while moving: a short stumble that interrupts what you were casting.", "Trips over flat ground.",
            new EpisodicDisabilityTrait
            {
                label = "Fumble", broughtOnBy = EpisodeTrigger.Moving, timing = new EpisodeTiming(4f, 2f, 3f, 1f, 1f, 30f),
                effect = new StatusConsequence("", true) { combatStats = new List<CombatStatModifier> { Combat(CombatStatType.AttackSpeed, -20f) } },
                stunSeconds = 0.8f, interruptsCasting = true, startMessage = "You stumble.",
            }),
        D("Open Guard", 1, null, TraitType.Combat, "Cannot block.", "Never learned to raise a guard.",
            Cannot(BodyAction.Block, "You do not know how to block")),
        D("Pacifist", 5, null, TraitType.Mental, "Cannot attack with weapons.", "Will not raise a weapon against anyone.",
            Cannot(BodyAction.WeaponAttacks, "You will not raise a weapon")),
        D("Magic-Dead", 6, null, TraitType.Magic, "Cannot cast abilities.", "Not a spark of power answers you.",
            Cannot(BodyAction.CastAbilities, "No power answers you")),

        // ============================================================== sight
        D("Short-Sighted", 1, "sight", TraitType.Physical, "The picture is a little hazy; -3 critical chance.", "Far things are a blur.",
            Sight(BodyPartSide.Center, 0f, 0.22f), Combat(CombatStatType.CriticalChance, -3f)),
        D("Poor Eyesight", 2, "sight", TraitType.Physical, "Hazy, narrowed sight; -8 critical chance, -10% ability range.", "You squint at everything.",
            Sight(BodyPartSide.Center, 0f, 0.42f, 0.15f), Combat(CombatStatType.CriticalChance, -8f), Pct(TraitStat.AbilityRange, -10f)),
        D("Nearly Blind", 5, "sight", TraitType.Physical, "A thick haze and tunnel vision; -20 critical chance, -25% ability range.", "Shapes and light, little more.",
            Sight(BodyPartSide.Center, 0f, 0.7f, 0.45f), Combat(CombatStatType.CriticalChance, -20f), Pct(TraitStat.AbilityRange, -25f)),
        D("One-Eyed (Left)", 3, "eyes", TraitType.Physical, "The left eye is blind: the left half of the screen is dark; -8 critical chance.", "The left eye sees nothing.",
            Sight(BodyPartSide.Left, 1f), Combat(CombatStatType.CriticalChance, -8f)),
        D("One-Eyed (Right)", 3, "eyes", TraitType.Physical, "The right eye is blind: the right half of the screen is dark; -8 critical chance.", "The right eye sees nothing.",
            Sight(BodyPartSide.Right, 1f), Combat(CombatStatType.CriticalChance, -8f)),
        D("Tunnel Vision", 2, null, TraitType.Physical, "The edges of the screen are closed in.", "You see what is ahead. Only that.",
            Sight(BodyPartSide.Center, 0f, 0f, 0.65f)),
        D("Night Blind", 2, null, TraitType.Physical, "In the dark the sight dims and narrows, the darker the more.", "After sunset you might as well close your eyes.",
            Sight(BodyPartSide.Center, 0.55f, 0f, 0.4f, 0f, SightWhen.InTheDark)),
        D("Light-Sensitive", 1, null, TraitType.Physical, "In daylight the picture is washed out, the brighter the more.", "The sun is too loud.",
            Sight(BodyPartSide.Center, 0f, 0.45f, 0f, 0f, SightWhen.InDaylight)),
        D("Migraines", 2, null, TraitType.Mental, "Now and then a migraine: hazy tunnel vision, +25% cast time, -10 critical chance, sanity loss; mild to severe.", "You know it is coming. You cannot stop it.",
            new EpisodicDisabilityTrait
            {
                label = "Migraine", timing = new EpisodeTiming(8f, 40f, 120f, 0.3f, 1f, 60f),
                effect = new StatusConsequence("", true)
                {
                    characterStats = new List<TraitModifier> { Pct(TraitStat.AbilityCastTime, 25f) },
                    combatStats = new List<CombatStatModifier> { Combat(CombatStatType.CriticalChance, -10f) },
                    sanityPerSecond = -0.05f,
                },
                sight = new VisionEffect(0f, 0.5f, 0.6f), startMessage = "A migraine sets in.", endMessage = "The migraine passes.",
            }),

        // ============================================================== hearing
        D("Hard Of Hearing", 1, "hearing", TraitType.Physical, "Hears about a third of the sound.", "Say that again?",
            new HearingImpairmentTrait { volume = 0.35f }),
        D("Deaf", 3, "hearing", TraitType.Physical, "Hears nothing: no footsteps, no warning.", "The world went quiet long ago.",
            new HearingImpairmentTrait { volume = 0f }),
        D("Tinnitus", 1, null, TraitType.Physical, "Now and then the ears ring: little is heard, and it wears on the mind.", "A whistle only you can hear.",
            new EpisodicDisabilityTrait
            {
                label = "Ringing Ears", timing = new EpisodeTiming(5f, 20f, 60f, 0.4f, 1f, 45f),
                effect = new StatusConsequence("", true) { sanityPerSecond = -0.03f },
                hearing = 0.2f, startMessage = "Your ears start ringing.",
            }),

        // ============================================================== hunger, thirst, sleep
        D("Hearty Appetite", 1, "appetite", TraitType.Survival, "Hunger drains 40% faster.", "Always room for more.",
            new NeedRatesTrait { hunger = 1.4f }),
        D("Insatiable", 3, "appetite", TraitType.Survival, "Hunger drains twice as fast.", "The last meal was an hour ago. It feels like days.",
            new NeedRatesTrait { hunger = 2f }),
        D("High Thirst", 1, "thirst", TraitType.Survival, "Thirst drains 40% faster.", "Never far from a waterskin.",
            new NeedRatesTrait { thirst = 1.4f }),
        D("Always Parched", 3, "thirst", TraitType.Survival, "Thirst drains twice as fast.", "No amount of water is enough.",
            new NeedRatesTrait { thirst = 2f }),
        D("Sleepyhead", 1, "sleep", TraitType.Survival, "Gets tired 50% faster.", "Could always sleep another hour.",
            new NeedRatesTrait { rest = 1.5f }),
        D("Insomniac", 2, "sleep", TraitType.Survival, "Sleep restores rest half as fast.", "Lies awake counting the cracks in the ceiling.",
            Combat(CombatStatType.RestRecovery, -50f)),
        D("Narcoleptic", 4, null, TraitType.Survival, "Now and then falls asleep where it stands for a few seconds: eyes shut, helpless.", "Sleep does not ask first.",
            new EpisodicDisabilityTrait
            {
                label = "Sleep Attack", timing = new EpisodeTiming(6f, 4f, 9f, 1f, 1f, 60f),
                sight = new VisionEffect(1f), outForAllOfIt = true, interruptsCasting = true,
                startMessage = "You fall asleep on your feet.", endMessage = "You wake with a start.",
            }),

        // ============================================================== body
        D("Asthmatic", 3, null, TraitType.Survival, "-15% max stamina; sprinting brings on asthma attacks: stamina drains, -20% move speed.", "Running is a negotiation with your lungs.",
            Pct(TraitStat.MaxStamina, -15f),
            new EpisodicDisabilityTrait
            {
                label = "Asthma Attack", broughtOnBy = EpisodeTrigger.Sprinting, timing = new EpisodeTiming(2f, 8f, 16f, 0.5f, 1f, 30f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -20f), Pct(TraitStat.StaminaRegenRate, -60f) }, staminaPerSecond = -6f },
                stunSeconds = 0.6f, interruptsCasting = true, startMessage = "You cannot breathe!", endMessage = "Your breath comes back.",
            }),
        D("Anemic", 2, null, TraitType.Physical, "+30% bleeding, -12% max stamina, feels the cold.", "Pale, and tired by noon.",
            Pct(TraitStat.BleedingRate, 30f), Pct(TraitStat.MaxStamina, -12f), Combat(CombatStatType.ColdResistance, -15f)),
        D("Slow Healer", 2, null, TraitType.Survival, "-40% injury healing, -25% healing received, -40% health regeneration.", "Wounds take their time with you.",
            Pct(TraitStat.InjuryHealing, -40f), Pct(TraitStat.HealingReceived, -25f), Pct(TraitStat.HealthRegenRate, -40f)),
        D("Weak Stomach", 1, null, TraitType.Survival, "Twice as likely to get food poisoning and dysentery (injury system).", "Yesterday's stew is today's regret.",
            Weakness("Weak Stomach", 100f, 0f, InjuryDefaults.TagGut)),
        D("Prone To Illness", 2, null, TraitType.Survival, "+75% chance to catch illnesses, and they get worse more often (injury system).", "If it is going around, you have it.",
            Weakness("Prone To Illness", 75f, 25f, InjuryDefaults.TagIllness)),
        D("Weak", 2, null, TraitType.Physical, "-18% weapon damage, -25% carry weight.", "Never the one asked to carry things.",
            Pct(TraitStat.WeaponDamage, -18f), Pct(TraitStat.CarryWeight, -25f)),
        D("Cold-Natured", 1, null, TraitType.Survival, "The cold affects the body 40% more.", "Wears a coat in summer.",
            Combat(CombatStatType.ColdResistance, -40f)),
        D("Heat-Intolerant", 1, null, TraitType.Survival, "The heat affects the body 40% more.", "Wilts in the sun.",
            Combat(CombatStatType.HeatResistance, -40f)),
        D("Sun Sickness", 3, null, TraitType.Survival, "In daylight the skin burns: health drains slowly and the sight hazes, until back in the dark.", "The day is not for you.",
            new EpisodicDisabilityTrait
            {
                label = "Sun Sickness", broughtOnBy = EpisodeTrigger.InDaylight, endsWhenItStops = true, timing = new EpisodeTiming(0f, 600f, 600f, 1f, 1f, 0f),
                effect = new StatusConsequence("", true) { healthPerSecond = -0.2f, characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, -20f) } },
                sight = new VisionEffect(0f, 0.3f), startMessage = "The sun burns your skin.", endMessage = "Out of the sun at last.",
            }),
        D("Chronic Pain", 3, null, TraitType.Physical, "A pain that never leaves, some hours faint, some crippling: up to -18% move speed, -25% stamina regeneration, -8% attack speed, sanity loss.", "Good hours and bad hours. You never know which.",
            new EpisodicDisabilityTrait
            {
                label = "Pain", timing = new EpisodeTiming(0f, 180f, 360f, 0f, 1f, 0f),
                effect = new StatusConsequence("", true)
                {
                    characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -18f), Pct(TraitStat.StaminaRegenRate, -25f) },
                    combatStats = new List<CombatStatModifier> { Combat(CombatStatType.AttackSpeed, -8f) },
                    sanityPerSecond = -0.03f,
                },
                startMessage = "The pain is back.", endMessage = "The pain eases.",
            }),

        // ============================================================== mind and nerves
        D("Fragile Mind", 2, null, TraitType.Mental, "Loses sanity 50% faster.", "The dark has always whispered to you.",
            Combat(CombatStatType.SanityResistance, -50f)),
        D("Panic Attacks", 3, null, TraitType.Mental, "In a fight panic strikes now and then: stamina drains, -20% weapon damage, +25% cast time, tunnel vision, sanity loss.", "The heart races before the mind can argue.",
            new EpisodicDisabilityTrait
            {
                label = "Panic", broughtOnBy = EpisodeTrigger.InCombat, timing = new EpisodeTiming(3f, 10f, 20f, 0.5f, 1f, 40f),
                effect = new StatusConsequence("", true)
                {
                    characterStats = new List<TraitModifier> { Pct(TraitStat.WeaponDamage, -20f), Pct(TraitStat.AbilityCastTime, 25f) },
                    staminaPerSecond = -4f, sanityPerSecond = -0.15f,
                },
                sight = new VisionEffect(0f, 0f, 0.55f), startMessage = "Panic grips you.", endMessage = "The panic lets go.",
            }),
        D("Faint-Hearted", 4, null, TraitType.Mental, "Badly hurt (below 35% health), may faint for a few seconds.", "The sight of your own blood is too much.",
            new EpisodicDisabilityTrait
            {
                label = "Faint", broughtOnBy = EpisodeTrigger.LowHealth, timing = new EpisodeTiming(1.5f, 2.5f, 4.5f, 1f, 1f, 45f),
                sight = new VisionEffect(1f), outForAllOfIt = true, interruptsCasting = true,
                startMessage = "Everything goes black.", endMessage = "You come to.",
            }),

        // ============================================================== small annoyances
        D("Allergies", 1, null, TraitType.Survival, "In daylight a sneezing fit now and then: a short stun that interrupts casting, -10 critical chance for a moment.", "Spring is the enemy.",
            new EpisodicDisabilityTrait
            {
                label = "Sneezing Fit", broughtOnBy = EpisodeTrigger.InDaylight, timing = new EpisodeTiming(6f, 6f, 12f, 1f, 1f, 45f),
                effect = new StatusConsequence("", true) { combatStats = new List<CombatStatModifier> { Combat(CombatStatType.CriticalChance, -10f) } },
                sight = new VisionEffect(0f, 0.15f), stunSeconds = 0.7f, interruptsCasting = true, startMessage = "You sneeze. And again.",
            }),
        D("Hiccups", 1, null, TraitType.Mental, "Now and then a bout of hiccups: interrupts what you were casting, +20% cast time while it lasts.", "Hic.",
            new EpisodicDisabilityTrait
            {
                label = "Hiccups", timing = new EpisodeTiming(7f, 30f, 90f, 1f, 1f, 60f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.AbilityCastTime, 20f) } },
                interruptsCasting = true, startMessage = "Hic!", endMessage = "The hiccups stop.",
            }),
        // ============================================================== fears
        D("Afraid Of The Dark", 2, null, TraitType.Mental, "At night: sanity drains, -8% weapon damage, the edges of the sight close in.", "Something is out there. Something is always out there.",
            new EpisodicDisabilityTrait
            {
                label = "Fear Of The Dark", broughtOnBy = EpisodeTrigger.AtNight, endsWhenItStops = true, timing = new EpisodeTiming(0f, 600f, 600f, 1f, 1f, 0f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.WeaponDamage, -8f) }, sanityPerSecond = -0.05f },
                sight = new VisionEffect(0f, 0f, 0.25f), startMessage = "Night falls. You do not like it.", endMessage = "Daylight. You breathe again.",
            }),
        D("Afraid Of Water", 2, null, TraitType.Mental, "While swimming: panic - stamina drains, sanity drains, -15% move speed, tunnel vision.", "It is deep. You cannot see the bottom.",
            new EpisodicDisabilityTrait
            {
                label = "Panic In The Water", broughtOnBy = EpisodeTrigger.Swimming, endsWhenItStops = true, timing = new EpisodeTiming(0f, 600f, 600f, 1f, 1f, 0f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -15f) }, staminaPerSecond = -5f, sanityPerSecond = -0.1f },
                sight = new VisionEffect(0f, 0f, 0.4f), startMessage = "You panic in the water!", endMessage = "Solid ground.",
            }),
        D("Squeamish", 2, null, TraitType.Mental, "While bleeding, retches now and then: a short stun, -15% weapon damage, sanity loss (injury system).", "Your own blood is the worst kind.",
            new EpisodicDisabilityTrait
            {
                label = "Queasy", broughtOnBy = EpisodeTrigger.Bleeding, timing = new EpisodeTiming(1f, 5f, 9f, 0.5f, 1f, 20f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.WeaponDamage, -15f) }, sanityPerSecond = -0.1f },
                sight = new VisionEffect(0f, 0.3f), stunSeconds = 1f, interruptsCasting = true, startMessage = "The sight of your blood turns your stomach.",
            }),
        D("Coward", 3, null, TraitType.Mental, "In a fight: -15% weapon and ability damage, sanity drains.", "Bravery is for people with less imagination.",
            new EpisodicDisabilityTrait
            {
                label = "Afraid", broughtOnBy = EpisodeTrigger.InCombat, endsWhenItStops = true, timing = new EpisodeTiming(0f, 600f, 600f, 1f, 1f, 0f),
                effect = new StatusConsequence("", true)
                {
                    characterStats = new List<TraitModifier> { Pct(TraitStat.WeaponDamage, -15f), Pct(TraitStat.AbilityDamage, -15f) },
                    sanityPerSecond = -0.05f,
                },
            }),

        // ============================================================== more of the body
        D("Aching Joints", 2, null, TraitType.Physical, "In the cold the joints seize: -15% move speed, -10% attack speed.", "You can tell the weather by your knees.",
            new EpisodicDisabilityTrait
            {
                label = "Aching Joints", broughtOnBy = EpisodeTrigger.WhileCold, endsWhenItStops = true, timing = new EpisodeTiming(0f, 600f, 600f, 1f, 1f, 0f),
                effect = new StatusConsequence("", true)
                {
                    characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -15f) },
                    combatStats = new List<CombatStatModifier> { Combat(CombatStatType.AttackSpeed, -10f) },
                },
                startMessage = "The cold gets into your joints.", endMessage = "Your joints loosen up.",
            }),
        D("Motion Sick", 1, null, TraitType.Physical, "Sprinting turns the stomach now and then: dizzy, blurred, stamina comes back slowly.", "Fine, as long as nothing moves. Including you.",
            new EpisodicDisabilityTrait
            {
                label = "Motion Sickness", broughtOnBy = EpisodeTrigger.Sprinting, timing = new EpisodeTiming(3f, 10f, 20f, 0.4f, 1f, 30f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, -30f) } },
                sight = new VisionEffect(0f, 0.25f, 0f, 0.5f), startMessage = "Your stomach turns.",
            }),
        D("Bad Back", 2, null, TraitType.Physical, "-30% carry weight, +15% fall damage; cannot roll.", "Lift with the knees, they said.",
            Pct(TraitStat.CarryWeight, -30f), Pct(TraitStat.FallDamage, 15f), Cannot(BodyAction.Roll, "Your back will not have it")),
        D("Slow Reflexes", 2, null, TraitType.Combat, "-12% attack speed, +20% stun and slow duration; cannot dash.", "Sees it coming. Eventually.",
            Combat(CombatStatType.AttackSpeed, -12f), Pct(TraitStat.ControlTaken, 20f), Cannot(BodyAction.Dash, "You are not that quick")),
        D("Two Left Feet", 2, null, TraitType.Movement, "-25% jump; cannot dash; trips now and then while sprinting.", "Dancing is out of the question.",
            Pct(TraitStat.JumpForce, -25f), Cannot(BodyAction.Dash, "You would only trip"),
            new EpisodicDisabilityTrait
            {
                label = "Tripped", broughtOnBy = EpisodeTrigger.Sprinting, timing = new EpisodeTiming(3f, 2f, 3f, 1f, 1f, 20f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.MoveSpeed, -20f) } },
                stunSeconds = 1f, interruptsCasting = true, startMessage = "You trip over your own feet.",
            }),

        // ============================================================== more of the mind, and bad luck
        D("Tongue-Tied", 2, null, TraitType.Magic, "While casting, the words tangle now and then: the cast is lost, +15% cast time for a moment.", "The spell was on the tip of your tongue.",
            new EpisodicDisabilityTrait
            {
                label = "Tongue-Tied", broughtOnBy = EpisodeTrigger.Casting, timing = new EpisodeTiming(0.4f, 6f, 10f, 1f, 1f, 15f),
                effect = new StatusConsequence("", true) { characterStats = new List<TraitModifier> { Pct(TraitStat.AbilityCastTime, 15f) } },
                interruptsCasting = true, startMessage = "The words come out wrong.",
            }),
        D("Absent-Minded", 2, null, TraitType.Mental, "+20% ability cooldowns.", "Now, what were you about to do?",
            Pct(TraitStat.AbilityCooldown, 20f)),
        D("Daydreamer", 1, null, TraitType.Mental, "Standing still, drifts off now and then: out for a couple of seconds.", "Present in body.",
            new EpisodicDisabilityTrait
            {
                label = "Daydreaming", broughtOnBy = EpisodeTrigger.StandingStill, timing = new EpisodeTiming(5f, 2f, 3.5f, 1f, 1f, 60f),
                sight = new VisionEffect(0f, 0.5f), outForAllOfIt = true, startMessage = "You drift off...", endMessage = "Where were you?",
            }),
        D("Conspicuous", 1, null, TraitType.Mental, "+50% threat: enemies pick you first.", "Somehow always the one they notice.",
            Combat(CombatStatType.ThreatGenerated, 50f)),
        D("Jinxed", 2, null, TraitType.Mental, "In a fight, every 20 seconds a coin flip you nearly always lose lowers a random stat for a while.", "If it can go wrong, it goes wrong for you.",
            new GamblerTrait { luck = 0.1f }),
    };
}
