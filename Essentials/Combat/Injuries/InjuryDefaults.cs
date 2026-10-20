using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The default injury configuration, inspired by Project Zomboid: seventeen body parts (head, neck, upper and lower torso,
/// groin, and on each side the upper arm, forearm, hand, thigh, shin and foot), four severity levels, scratches, lacerations,
/// deep wounds, punctures, bite wounds, bruises, fractures, concussions, burns, frostbite, infections, amputation stumps
/// and reattached limbs. Each injury has odds by <see cref="DamageForm"/>: slashes cut and bleed, piercing makes deep
/// punctures, blunt blows bruise, break bones and concuss, bites tear and get infected. Used by new Injury Settings
/// assets and by the inspector's Reset To Defaults.
/// </summary>
public static class InjuryDefaults
{
    // Ids of the built-in injury types (treatments and limb rules refer to them).
    public const string Scratch = "Scratch";
    public const string Laceration = "Laceration";
    public const string DeepWound = "DeepWound";
    public const string Fracture = "Fracture";
    public const string Burn = "Burn";
    public const string Infection = "Infection";
    public const string Amputation = "Amputation";
    public const string Reattached = "Reattached";
    public const string Bruise = "Bruise";
    public const string Puncture = "Puncture";
    public const string BiteWound = "BiteWound";
    public const string Concussion = "Concussion";
    public const string Frostbite = "Frostbite";

    // Tags.
    public const string TagWound = "wound";
    public const string TagBleeding = "bleeding";
    public const string TagFracture = "fracture";
    public const string TagBurn = "burn";
    public const string TagInfection = "infection";
    public const string TagStump = "stump";
    public const string TagBruise = "bruise";
    public const string TagPuncture = "puncture";
    public const string TagBite = "bite";
    public const string TagConcussion = "concussion";
    public const string TagCold = "cold";

    /// <summary>Fills <paramref name="s"/> with the default body, injuries and rules (the system on).</summary>
    public static void Fill(InjurySettings s)
    {
        s.enabled = true;
        s.players = true;
        s.mobs = false;
        s.mainHandSide = BodyPartSide.Right;
        s.defaultPart = "UpperTorso";
        s.severityLevels = new List<InjurySeverityLevel>
        {
            new InjurySeverityLevel("Minor", new Color(0.95f, 0.85f, 0.4f)),
            new InjurySeverityLevel("Moderate", new Color(1f, 0.62f, 0.25f)),
            new InjurySeverityLevel("Severe", new Color(0.95f, 0.33f, 0.27f)),
            new InjurySeverityLevel("Critical", new Color(0.75f, 0.12f, 0.2f)),
        };
        s.bodyParts = BodyParts();
        s.injuryTypes = InjuryTypes();
        s.bleeding = new BleedingRules
        {
            status = new StatusConsequence("Bleeding")
            {
                staminaPerSecond = -0.4f,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -10f) },
            },
        };
        s.reattachment = new ReattachmentRules();
        s.cleanup = new LimbCleanupRules();
        s.locomotion = new LocomotionRules();
        s.cityTreatment = new CityTreatmentSettings();
    }

    // ------------------------------------------------------------------ body
    /// <summary>
    /// The 17 default parts: head, neck, upper and lower torso, groin, and on each side the upper arm, forearm, hand,
    /// thigh, shin and foot. Limbs hang on each other (hand on forearm on upper arm; foot on shin on thigh), so a limb is
    /// cut off from the segment that was hit down: a shin takes the foot with it, a thigh the whole leg.
    /// </summary>
    public static List<InjuryBodyPart> BodyParts()
    {
        var parts = new List<InjuryBodyPart>
        {
            new InjuryBodyPart
            {
                id = "Head", displayName = "Head", kind = BodyPartKind.Head, side = BodyPartSide.Center,
                aliases = new List<string> { "Skull", "Face" },
                hitParts = new List<string> { "Head" }, heightRange = new Vector2(0.87f, 1f), conditionDamageMultiplier = 1f,
                attachedTo = "Neck",
                atZeroCondition = ZeroConditionRule.Stun, zeroConditionStunSeconds = 2f,
                impaired = new StatusConsequence("Head Injury")
                {
                    combatStats = new List<CombatStatModifier>
                    {
                        new CombatStatModifier(CombatStatType.CastingSpeed, -25f),
                        new CombatStatModifier(CombatStatType.CriticalChance, -10f),
                    },
                    sanityPerSecond = -0.05f,
                    stunChancePerMinute = 0.2f,
                    stunSeconds = 1f,
                },
                actionRules = new List<BodyActionRule> { new BodyActionRule(BodyAction.CastAbilities, 0.9f) },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Helmet },
                diagramRect = new Rect(0.40f, 0.86f, 0.20f, 0.13f),
            },
            new InjuryBodyPart
            {
                id = "Neck", displayName = "Neck", kind = BodyPartKind.Neck, side = BodyPartSide.Center,
                aliases = new List<string> { "Throat" },
                hitParts = new List<string> { "Neck" }, heightRange = new Vector2(0.82f, 0.87f), hitWeight = 0.5f,
                maxCondition = 60f, conditionDamageMultiplier = 1.3f, attachedTo = "UpperTorso",
                atZeroCondition = ZeroConditionRule.Stun, zeroConditionStunSeconds = 1.5f,
                impaired = new StatusConsequence("Neck Injury")
                {
                    combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.CastingSpeed, -15f) },
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -10f) },
                },
                actionRules = new List<BodyActionRule> { new BodyActionRule(BodyAction.CastAbilities, 0.95f) },
                diagramRect = new Rect(0.45f, 0.81f, 0.10f, 0.05f),
            },
            new InjuryBodyPart
            {
                id = "UpperTorso", displayName = "Upper Torso", kind = BodyPartKind.Torso, side = BodyPartSide.Center,
                aliases = new List<string> { "Torso", "Chest", "Back" },
                hitParts = new List<string> { "UpperTorso", "Torso", "Chest" }, heightRange = new Vector2(0.62f, 0.82f),
                impaired = new StatusConsequence("Chest Injury")
                {
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -30f),
                        TraitModifier.Percent(TraitStat.StaminaCost, 25f),
                    },
                },
                actionRules = new List<BodyActionRule> { new BodyActionRule(BodyAction.Sprint | BodyAction.Roll, 0.85f) },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Chestplate },
                diagramRect = new Rect(0.34f, 0.63f, 0.32f, 0.18f),
            },
            new InjuryBodyPart
            {
                id = "LowerTorso", displayName = "Lower Torso", kind = BodyPartKind.Torso, side = BodyPartSide.Center,
                aliases = new List<string> { "Abdomen", "Belly", "Stomach" },
                hitParts = new List<string> { "LowerTorso", "Abdomen", "Torso" }, heightRange = new Vector2(0.50f, 0.62f), hitWeight = 0.8f,
                attachedTo = "UpperTorso",
                impaired = new StatusConsequence("Abdominal Injury")
                {
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -15f),
                        TraitModifier.Percent(TraitStat.StaminaCost, 20f),
                    },
                    hungerDrain = 1.2f,
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.Sprint, 0.8f),
                    new BodyActionRule(BodyAction.Jump | BodyAction.Roll, 0.7f),
                },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Belt },
                diagramRect = new Rect(0.36f, 0.52f, 0.28f, 0.11f),
            },
            new InjuryBodyPart
            {
                id = "Groin", displayName = "Groin", kind = BodyPartKind.Groin, side = BodyPartSide.Center,
                aliases = new List<string> { "Pelvis", "Hips" },
                hitParts = new List<string> { "Groin", "Hips" }, heightRange = new Vector2(0.44f, 0.50f), hitWeight = 0.6f,
                maxCondition = 70f, attachedTo = "LowerTorso",
                atZeroCondition = ZeroConditionRule.Stun, zeroConditionStunSeconds = 1f,
                impaired = new StatusConsequence("Groin Injury")
                {
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MoveSpeed, -15f),
                        TraitModifier.Percent(TraitStat.SprintSpeed, -20f),
                    },
                },
                actionRules = new List<BodyActionRule> { new BodyActionRule(BodyAction.Sprint | BodyAction.Jump | BodyAction.Roll, 0.6f) },
                diagramRect = new Rect(0.40f, 0.46f, 0.20f, 0.06f),
            },
        };
        foreach (BodyPartSide side in new[] { BodyPartSide.Right, BodyPartSide.Left })
        {
            string sn = side.ToString();
            bool right = side == BodyPartSide.Right;
            // Front view: the character's right side is on the viewer's left (the left side mirrors it).
            Rect Mirror(Rect r) => right ? r : new Rect(1f - r.x - r.width, r.y, r.width, r.height);

            // ---- arm: upper arm > forearm > hand
            parts.Add(new InjuryBodyPart
            {
                id = sn + "UpperArm", displayName = sn + " Upper Arm", kind = BodyPartKind.Arm, side = side,
                aliases = new List<string> { sn + "Arm", sn + " Arm" },
                hitParts = new List<string> { sn + "UpperArm", "UpperArms", "Arms" }, heightRange = new Vector2(0.62f, 0.82f), hitWeight = 0.6f,
                attachedTo = "UpperTorso",
                impaired = new StatusConsequence($"Injured {sn} Arm")
                {
                    combatStats = new List<CombatStatModifier>
                    {
                        new CombatStatModifier(CombatStatType.WeaponDamage, -20f),
                        new CombatStatModifier(CombatStatType.AttackSpeed, -10f),
                    },
                },
                lost = new StatusConsequence($"Missing {sn} Arm")
                {
                    combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.AttackSpeed, -10f) },
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -5f) },
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.TwoHandedWeapons, 0.6f),
                    new BodyActionRule(BodyAction.HandOnThisSide, 0.95f),
                },
                limbLoss = new LimbLossRules { canBeLost = true, singleHitThreshold = 0.6f, chance = 0.3f },
                useHumanoidBone = true, humanoidBone = right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
                diagramRect = Mirror(new Rect(0.20f, 0.63f, 0.13f, 0.18f)),
            });
            parts.Add(new InjuryBodyPart
            {
                id = sn + "Forearm", displayName = sn + " Forearm", kind = BodyPartKind.Arm, side = side,
                aliases = new List<string> { sn + "LowerArm" },
                hitParts = new List<string> { sn + "Forearm", "Forearms", "LowerArms", "Arms" }, heightRange = new Vector2(0.48f, 0.62f), hitWeight = 0.5f,
                attachedTo = sn + "UpperArm", maxCondition = 80f,
                impaired = new StatusConsequence($"Injured {sn} Forearm")
                {
                    combatStats = new List<CombatStatModifier>
                    {
                        new CombatStatModifier(CombatStatType.AttackSpeed, -15f),
                        new CombatStatModifier(CombatStatType.WeaponDamage, -10f),
                    },
                },
                lost = new StatusConsequence($"Missing {sn} Forearm")
                {
                    combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.AttackSpeed, -8f) },
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.TwoHandedWeapons, 0.6f),
                    new BodyActionRule(BodyAction.HandOnThisSide, 0.95f),
                },
                limbLoss = new LimbLossRules { canBeLost = true, singleHitThreshold = 0.55f, chance = 0.35f },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Bracers },
                useHumanoidBone = true, humanoidBone = right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
                diagramRect = Mirror(new Rect(0.15f, 0.48f, 0.12f, 0.15f)),
            });
            parts.Add(new InjuryBodyPart
            {
                id = sn + "Hand", displayName = sn + " Hand", kind = BodyPartKind.Hand, side = side,
                hitParts = new List<string> { sn + "Hand", "Hands", "Arms" }, heightRange = new Vector2(0.38f, 0.48f), hitWeight = 0.3f,
                attachedTo = sn + "Forearm", maxCondition = 60f,
                impaired = new StatusConsequence($"Injured {sn} Hand")
                {
                    combatStats = new List<CombatStatModifier>
                    {
                        new CombatStatModifier(CombatStatType.AttackSpeed, -15f),
                        new CombatStatModifier(CombatStatType.CriticalChance, -8f),
                        new CombatStatModifier(CombatStatType.DrawSpeed, -25f),
                        new CombatStatModifier(CombatStatType.ReloadSpeed, -25f),
                    },
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.TwoHandedWeapons, 0.6f),
                    new BodyActionRule(BodyAction.HandOnThisSide, 0.9f),
                },
                limbLoss = new LimbLossRules { canBeLost = true, singleHitThreshold = 0.5f, chance = 0.3f },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Gloves },
                useHumanoidBone = true, humanoidBone = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand,
                diagramRect = Mirror(new Rect(0.10f, 0.39f, 0.12f, 0.09f)),
            });

            // ---- leg: thigh > shin > foot. Missing legs change how the character moves (Injury Settings ▸ Movement):
            // their Lost consequence has no speed penalty of its own - hopping and crawling set the speed.
            parts.Add(new InjuryBodyPart
            {
                id = sn + "Thigh", displayName = sn + " Thigh", kind = BodyPartKind.Leg, side = side,
                aliases = new List<string> { sn + "Leg", sn + " Leg", sn + "UpperLeg" },
                hitParts = new List<string> { sn + "Thigh", "Thighs", "Legs" }, heightRange = new Vector2(0.26f, 0.44f), hitWeight = 0.6f,
                attachedTo = "Groin",
                impaired = new StatusConsequence("Limping")
                {
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MoveSpeed, -25f),
                        TraitModifier.Percent(TraitStat.SprintSpeed, -25f),
                        TraitModifier.Percent(TraitStat.JumpForce, -35f),
                    },
                },
                lost = new StatusConsequence($"Missing {sn} Leg")
                {
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -10f) },
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.Sprint, 0.5f),
                    new BodyActionRule(BodyAction.Jump | BodyAction.Dash | BodyAction.Roll, 0.7f),
                },
                limbLoss = new LimbLossRules { canBeLost = true, singleHitThreshold = 0.65f, chance = 0.25f },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Leggings },
                useHumanoidBone = true, humanoidBone = right ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg,
                diagramRect = Mirror(new Rect(0.34f, 0.27f, 0.15f, 0.18f)),
            });
            parts.Add(new InjuryBodyPart
            {
                id = sn + "Shin", displayName = sn + " Shin", kind = BodyPartKind.Leg, side = side,
                aliases = new List<string> { sn + "LowerLeg", sn + "Calf" },
                hitParts = new List<string> { sn + "Shin", "Shins", "LowerLegs", "Legs" }, heightRange = new Vector2(0.07f, 0.26f), hitWeight = 0.5f,
                attachedTo = sn + "Thigh", maxCondition = 80f,
                impaired = new StatusConsequence($"Hurt {sn} Shin")
                {
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MoveSpeed, -20f),
                        TraitModifier.Percent(TraitStat.SprintSpeed, -25f),
                        TraitModifier.Percent(TraitStat.JumpForce, -25f),
                    },
                },
                lost = new StatusConsequence($"Missing {sn} Lower Leg")
                {
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -5f) },
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.Sprint, 0.55f),
                    new BodyActionRule(BodyAction.Jump | BodyAction.Dash | BodyAction.Roll, 0.75f),
                },
                limbLoss = new LimbLossRules { canBeLost = true, singleHitThreshold = 0.55f, chance = 0.3f },
                useHumanoidBone = true, humanoidBone = right ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg,
                diagramRect = Mirror(new Rect(0.335f, 0.08f, 0.14f, 0.19f)),
            });
            parts.Add(new InjuryBodyPart
            {
                id = sn + "Foot", displayName = sn + " Foot", kind = BodyPartKind.Foot, side = side,
                hitParts = new List<string> { sn + "Foot", "Feet", "Legs" }, heightRange = new Vector2(0f, 0.07f), hitWeight = 0.25f,
                attachedTo = sn + "Shin", maxCondition = 60f,
                impaired = new StatusConsequence($"Sore {sn} Foot")
                {
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MoveSpeed, -15f),
                        TraitModifier.Percent(TraitStat.SprintSpeed, -20f),
                    },
                },
                actionRules = new List<BodyActionRule>
                {
                    new BodyActionRule(BodyAction.Sprint, 0.75f),
                    new BodyActionRule(BodyAction.Dash | BodyAction.Roll, 0.9f),
                },
                limbLoss = new LimbLossRules { canBeLost = true, singleHitThreshold = 0.5f, chance = 0.3f },
                armorSlots = new List<ArmorSlotType> { ArmorSlotType.Boots },
                useHumanoidBone = true, humanoidBone = right ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot,
                diagramRect = Mirror(new Rect(0.30f, 0.01f, 0.18f, 0.07f)),
            });
        }
        return parts;
    }

    // ------------------------------------------------------------------ injuries
    public static List<InjuryTypeDefinition> InjuryTypes()
    {
        return new List<InjuryTypeDefinition>
        {
            new InjuryTypeDefinition
            {
                id = Burn, displayName = "Burn", color = new Color(1f, 0.55f, 0.2f),
                description = "Fire damage burnt the skin. Bandages help it heal; severe burns need treatment and can get infected.",
                tags = new List<string> { TagBurn },
                trigger = new InjuryTrigger { chance = 0.6f, minHitFraction = 0.08f, damageKinds = InjuryDamageKinds.All, elements = new List<ElementType> { ElementType.Fire } },
                healStageByStage = true,
                stages = new List<InjuryStage>
                {
                    new InjuryStage { name = "Minor Burn", severity = 0, minHitFraction = 0.08f, impairment = 0.1f, healSeconds = 240f, untreatedHealRate = 0.6f },
                    new InjuryStage { name = "Burn", severity = 1, minHitFraction = 0.2f, impairment = 0.3f, healSeconds = 480f, untreatedHealRate = 0.3f },
                    new InjuryStage
                    {
                        name = "Severe Burn", severity = 2, minHitFraction = 0.4f, impairment = 0.6f, healSeconds = 900f, needsTreatmentToHeal = true,
                        worsenAfterSeconds = 600f, worsensInto = Infection, conditionLossPerSecond = 0.02f,
                        consequence = new StatusConsequence("") { sanityPerSecond = -0.03f },
                    },
                },
            },
            FrostbiteType(),
            ConcussionType(),
            new InjuryTypeDefinition
            {
                id = Fracture, displayName = "Fracture", color = new Color(0.85f, 0.85f, 0.95f),
                description = "A broken bone. The part works badly until it heals; a broken or compound fracture needs a splint (or surgery) to heal.",
                tags = new List<string> { TagFracture },
                trigger = new InjuryTrigger
                {
                    chance = 0.3f, minHitFraction = 0.22f, damageKinds = InjuryDamageKinds.Physical,
                    formChances = FractureOdds(), formStrength = FractureStrength(),
                },
                healStageByStage = true,
                mergeIntoExisting = true,
                maxPerPart = 1,
                stages = new List<InjuryStage>
                {
                    new InjuryStage { name = "Cracked Bone", severity = 1, minHitFraction = 0.22f, impairment = 0.35f, healSeconds = 420f, untreatedHealRate = 0.35f },
                    new InjuryStage { name = "Broken Bone", severity = 2, minHitFraction = 0.35f, impairment = 0.75f, healSeconds = 720f, needsTreatmentToHeal = true },
                    new InjuryStage
                    {
                        name = "Compound Fracture", severity = 3, minHitFraction = 0.55f, impairment = 0.95f, healSeconds = 1080f, needsTreatmentToHeal = true,
                        bleedingPerSecond = 0.35f, conditionLossPerSecond = 0.04f, worsenAfterSeconds = 420f, worsensInto = Infection,
                    },
                },
            },
            new InjuryTypeDefinition
            {
                id = DeepWound, displayName = "Deep Wound", color = new Color(0.8f, 0.15f, 0.2f),
                description = "A deep cut that bleeds heavily and does not stop by itself. It needs stitches to heal and gets infected if left open.",
                tags = new List<string> { TagWound, TagBleeding },
                trigger = new InjuryTrigger { chance = 0.4f, minHitFraction = 0.3f, damageKinds = InjuryDamageKinds.Physical, formChances = DeepWoundOdds() },
                maxPerPart = 1,
                stages = new List<InjuryStage>
                {
                    new InjuryStage
                    {
                        name = "Deep Wound", severity = 2, minHitFraction = 0.3f, bleedingPerSecond = 0.9f, impairment = 0.4f, healSeconds = 600f,
                        needsTreatmentToHeal = true, worsenAfterSeconds = 360f, worsensInto = Infection,
                    },
                },
            },
            BiteWoundType(),
            PunctureType(),
            new InjuryTypeDefinition
            {
                id = Laceration, displayName = "Laceration", color = new Color(0.95f, 0.35f, 0.3f),
                description = "A bleeding cut. A bandage stops most of the bleeding; left open it can get infected.",
                tags = new List<string> { TagWound, TagBleeding },
                trigger = new InjuryTrigger { chance = 0.5f, minHitFraction = 0.12f, damageKinds = InjuryDamageKinds.Physical, formChances = LacerationOdds() },
                stages = new List<InjuryStage>
                {
                    new InjuryStage
                    {
                        name = "Laceration", severity = 1, minHitFraction = 0.12f, bleedingPerSecond = 0.4f, clotsAfterSeconds = 150f, impairment = 0.15f,
                        healSeconds = 360f, untreatedHealRate = 0.5f, worsenAfterSeconds = 600f, worsensInto = Infection,
                    },
                },
            },
            new InjuryTypeDefinition
            {
                id = Scratch, displayName = "Scratch", color = new Color(1f, 0.6f, 0.55f),
                description = "A light cut. It bleeds a little and stops by itself; a bandage makes it heal faster.",
                tags = new List<string> { TagWound, TagBleeding },
                trigger = new InjuryTrigger { chance = 0.55f, minHitFraction = 0.04f, damageKinds = InjuryDamageKinds.Physical, formChances = ScratchOdds() },
                maxPerPart = 3,
                mergeIntoExisting = false,
                stages = new List<InjuryStage>
                {
                    new InjuryStage { name = "Scratch", severity = 0, minHitFraction = 0.04f, bleedingPerSecond = 0.12f, clotsAfterSeconds = 40f, impairment = 0.03f, healSeconds = 150f, untreatedHealRate = 0.6f },
                },
            },
            BruiseType(),
            new InjuryTypeDefinition
            {
                id = Infection, displayName = "Infection", color = new Color(0.55f, 0.8f, 0.3f),
                description = "An untreated wound got infected. It drains health and wears the part down until disinfected or treated with antibiotics.",
                tags = new List<string> { TagInfection },
                causedByHits = false,
                healStageByStage = true,
                maxPerPart = 1,
                stages = new List<InjuryStage>
                {
                    new InjuryStage
                    {
                        name = "Infection", severity = 1, impairment = 0.2f, conditionLossPerSecond = 0.05f, healSeconds = 480f, needsTreatmentToHeal = true,
                        worsenAfterSeconds = 900f,
                        consequence = new StatusConsequence("Infection") { healthPerSecond = -0.03f, hungerDrain = 1.2f, thirstDrain = 1.3f },
                    },
                    new InjuryStage
                    {
                        name = "Severe Infection", severity = 2, impairment = 0.35f, conditionLossPerSecond = 0.1f, healSeconds = 720f, needsTreatmentToHeal = true,
                        consequence = new StatusConsequence("Severe Infection")
                        {
                            healthPerSecond = -0.08f, hungerDrain = 1.4f, thirstDrain = 1.6f, stunChancePerMinute = 0.08f, stunSeconds = 1.5f,
                        },
                    },
                },
            },
            new InjuryTypeDefinition
            {
                id = Amputation, displayName = "Amputation Stump", color = new Color(0.6f, 0.05f, 0.1f),
                description = "Where a limb was cut off. It bleeds heavily until bandaged and stitched (or reattached).",
                tags = new List<string> { TagStump, TagWound, TagBleeding },
                causedByHits = false,
                maxPerPart = 1,
                stages = new List<InjuryStage>
                {
                    new InjuryStage
                    {
                        name = "Bleeding Stump", severity = 3, bleedingPerSecond = 1.4f, healSeconds = 900f, needsTreatmentToHeal = true,
                        worsenAfterSeconds = 300f, worsensInto = Infection,
                    },
                },
            },
            new InjuryTypeDefinition
            {
                id = Reattached, displayName = "Reattached Limb", color = new Color(0.55f, 0.75f, 1f),
                description = "A limb that was put back on. It works badly at first and recovers with time.",
                tags = new List<string> { "reattached" },
                causedByHits = false,
                healStageByStage = true,
                maxPerPart = 1,
                stages = new List<InjuryStage>
                {
                    new InjuryStage { name = "Knitting Limb", severity = 0, impairment = 0.15f, healSeconds = 240f, untreatedHealRate = 1f },
                    new InjuryStage { name = "Healing Limb", severity = 1, impairment = 0.45f, healSeconds = 240f, untreatedHealRate = 1f },
                    new InjuryStage { name = "Fresh Reattachment", severity = 2, impairment = 0.8f, healSeconds = 240f, untreatedHealRate = 1f },
                },
            },
        };
    }

    // ------------------------------------------------------------------ damage forms
    // Odds by form: (formless, slash, pierce, blunt, bite, claw, crush). A slash cuts far more often than a mace; a mace
    // breaks bones; bites tear; claws scratch. Formless physical hits (weapons without a category, abilities and scripts
    // that set no form) keep the type's usual chance, as before damage forms.
    public static DamageFormChances ScratchOdds() => new DamageFormChances(1f, 1.2f, 0.6f, 0.15f, 0.8f, 1.8f, 0.2f);
    public static DamageFormChances LacerationOdds() => new DamageFormChances(1f, 1.5f, 0.5f, 0.15f, 1.2f, 1.4f, 0.3f);
    public static DamageFormChances DeepWoundOdds() => new DamageFormChances(1f, 1.3f, 0.7f, 0.05f, 1f, 0.8f, 0.2f);
    public static DamageFormChances FractureOdds() => new DamageFormChances(1f, 0.3f, 0.25f, 1.6f, 0.4f, 0.15f, 2f);
    /// <summary>Blunt and crushing blows break bones worse than cuts of the same damage.</summary>
    public static DamageFormChances FractureStrength() => new DamageFormChances(1f, 0.8f, 0.7f, 1.2f, 0.8f, 0.6f, 1.5f);

    /// <summary>Blunt blows (maces, fists, pushes, falls): a bruise or a deep bruise that heals by itself.</summary>
    public static InjuryTypeDefinition BruiseType() => new InjuryTypeDefinition
    {
        id = Bruise, displayName = "Bruise", color = new Color(0.55f, 0.45f, 0.75f),
        description = "A blow left a bruise. It does not bleed and heals by itself; a deep bruise hurts to use for a while.",
        tags = new List<string> { TagBruise },
        trigger = new InjuryTrigger
        {
            chance = 0.55f, minHitFraction = 0.06f, damageKinds = InjuryDamageKinds.Physical,
            formChances = new DamageFormChances(0.5f, 0.1f, 0.1f, 1.6f, 0.4f, 0.1f, 1.5f),
        },
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Bruise", severity = 0, minHitFraction = 0.06f, impairment = 0.05f, healSeconds = 180f, untreatedHealRate = 1f },
            new InjuryStage { name = "Deep Bruise", severity = 1, minHitFraction = 0.2f, impairment = 0.2f, healSeconds = 300f, untreatedHealRate = 0.8f },
        },
    };

    /// <summary>Points (spears, stabs, arrows, horns): a narrow, deep wound that bleeds inside and gets infected easily.</summary>
    public static InjuryTypeDefinition PunctureType() => new InjuryTypeDefinition
    {
        id = Puncture, displayName = "Puncture Wound", color = new Color(0.85f, 0.25f, 0.35f),
        description = "A narrow, deep wound from a point. It bleeds less than a cut but closes over dirt: clean and stitch it, or it gets infected quickly.",
        tags = new List<string> { TagWound, TagBleeding, TagPuncture },
        trigger = new InjuryTrigger
        {
            chance = 0.5f, minHitFraction = 0.1f, damageKinds = InjuryDamageKinds.Physical,
            formChances = new DamageFormChances(0.3f, 0.15f, 1.6f, 0f, 0.5f, 0.2f, 0f),
        },
        maxPerPart = 2,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Puncture Wound", severity = 1, minHitFraction = 0.1f, bleedingPerSecond = 0.3f, clotsAfterSeconds = 120f, impairment = 0.15f,
                healSeconds = 420f, untreatedHealRate = 0.4f, worsenAfterSeconds = 420f, worsensInto = Infection,
            },
            new InjuryStage
            {
                name = "Deep Puncture", severity = 2, minHitFraction = 0.3f, bleedingPerSecond = 0.7f, impairment = 0.35f, healSeconds = 600f,
                needsTreatmentToHeal = true, worsenAfterSeconds = 300f, worsensInto = Infection,
            },
        },
    };

    /// <summary>Teeth: a torn, ragged wound that bleeds and very often gets infected.</summary>
    public static InjuryTypeDefinition BiteWoundType() => new InjuryTypeDefinition
    {
        id = BiteWound, displayName = "Bite Wound", color = new Color(0.75f, 0.3f, 0.2f),
        description = "Torn by teeth. It bleeds, and a mouth is dirty: it gets infected quickly unless it is disinfected.",
        tags = new List<string> { TagWound, TagBleeding, TagBite },
        trigger = new InjuryTrigger
        {
            chance = 0.7f, minHitFraction = 0.06f, damageKinds = InjuryDamageKinds.Physical,
            formChances = new DamageFormChances(0f, 0f, 0f, 0f, 1f, 0f, 0f),
        },
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Bite Wound", severity = 1, minHitFraction = 0.06f, bleedingPerSecond = 0.35f, clotsAfterSeconds = 120f, impairment = 0.15f,
                healSeconds = 400f, untreatedHealRate = 0.4f, worsenAfterSeconds = 240f, worsensInto = Infection,
            },
            new InjuryStage
            {
                name = "Mauled", severity = 2, minHitFraction = 0.3f, bleedingPerSecond = 0.8f, impairment = 0.45f, healSeconds = 700f,
                needsTreatmentToHeal = true, worsenAfterSeconds = 200f, worsensInto = Infection,
            },
        },
    };

    /// <summary>A hard blunt blow to the head: dizzy spells (short stuns) until it passes.</summary>
    public static InjuryTypeDefinition ConcussionType() => new InjuryTypeDefinition
    {
        id = Concussion, displayName = "Concussion", color = new Color(0.95f, 0.85f, 0.5f),
        description = "A blow to the head. Dizzy spells come and go (short stuns) until it passes; rest is the only cure.",
        tags = new List<string> { TagConcussion },
        trigger = new InjuryTrigger
        {
            chance = 0.35f, minHitFraction = 0.25f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Head },
            formChances = new DamageFormChances(0.5f, 0.1f, 0.1f, 1.5f, 0.1f, 0.1f, 1.5f),
        },
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Concussion", severity = 2, minHitFraction = 0.25f, impairment = 0.2f, healSeconds = 300f, untreatedHealRate = 1f,
                consequence = new StatusConsequence("Concussion") { stunChancePerMinute = 0.2f, stunSeconds = 1f, sanityPerSecond = -0.02f },
            },
        },
    };

    /// <summary>Ice damage: frostnip, or frostbite that needs treatment and can rot (an infection).</summary>
    public static InjuryTypeDefinition FrostbiteType() => new InjuryTypeDefinition
    {
        id = Frostbite, displayName = "Frostbite", color = new Color(0.55f, 0.8f, 1f),
        description = "Ice damage froze the skin. Frostnip heals by itself; frostbite needs treatment (burn salve, bandages) and rots if left alone.",
        tags = new List<string> { TagBurn, TagCold },
        trigger = new InjuryTrigger { chance = 0.5f, minHitFraction = 0.1f, damageKinds = InjuryDamageKinds.All, elements = new List<ElementType> { ElementType.Ice } },
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Frostnip", severity = 0, minHitFraction = 0.1f, impairment = 0.1f, healSeconds = 200f, untreatedHealRate = 0.7f },
            new InjuryStage
            {
                name = "Frostbite", severity = 2, minHitFraction = 0.35f, impairment = 0.5f, healSeconds = 800f, needsTreatmentToHeal = true,
                conditionLossPerSecond = 0.03f, worsenAfterSeconds = 600f, worsensInto = Infection,
            },
        },
    };

    /// <summary>
    /// Brings an existing settings asset up to the damage forms without touching anything else: the built-in injury types
    /// get their odds by form (Scratch, Laceration, Deep Wound, Fracture), the new built-in types that are missing are
    /// added in their place (Frostbite, Concussion, Bite Wound, Puncture Wound, Bruise), the limbs get their severing odds
    /// by form. Returns what it did.
    /// </summary>
    public static List<string> ApplyDamageForms(InjurySettings s)
    {
        var done = new List<string>();
        if (s == null)
            return done;
        if (s.injuryTypes == null)
            s.injuryTypes = new List<InjuryTypeDefinition>();
        List<InjuryTypeDefinition> defaults = InjuryTypes();
        foreach (InjuryTypeDefinition t in s.injuryTypes)
        {
            if (t == null || t.trigger == null)
                continue;
            InjuryTypeDefinition d = defaults.Find(x => x.id == t.id);
            if (d == null || d.trigger == null)
                continue;
            if (d.trigger.formChances.IsNeutral && d.trigger.formStrength.IsNeutral)
                continue;
            t.trigger.formChances = d.trigger.formChances.Clone();
            t.trigger.formStrength = d.trigger.formStrength.Clone();
            done.Add($"{t.Name}: {t.trigger.formChances.Describe()}");
        }
        for (int i = 0; i < defaults.Count; i++)
        {
            InjuryTypeDefinition d = defaults[i];
            if (s.FindType(d.id) != null)
                continue;
            // Put it before the first built-in type that comes after it by default (the order of the list matters for hits).
            int at = s.injuryTypes.Count;
            for (int k = i + 1; k < defaults.Count && at == s.injuryTypes.Count; k++)
            {
                int idx = s.injuryTypes.FindIndex(x => x != null && x.id == defaults[k].id);
                if (idx >= 0)
                    at = idx;
            }
            s.injuryTypes.Insert(at, d);
            done.Add($"Added {d.Name}");
        }
        if (s.bodyParts != null)
        {
            int limbs = 0;
            foreach (InjuryBodyPart p in s.bodyParts)
            {
                if (p == null || p.limbLoss == null || !p.limbLoss.canBeLost)
                    continue;
                p.limbLoss.formChances = LimbLossRules.DefaultSevering();
                limbs++;
            }
            if (limbs > 0)
                done.Add($"Severing odds by form on {limbs} limbs ({LimbLossRules.DefaultSevering().Describe()})");
        }
        return done;
    }

    // ------------------------------------------------------------------ resistance presets
    /// <summary>A ready-made set of injury resistances for a kind of creature (Injury Resistances component ▸ Presets).</summary>
    public struct ResistancePreset
    {
        public string name;
        public string description;
        public List<InjuryResistance> resistances;
    }

    /// <summary>Skeleton, stone golem, troll, slime, armoured beast, undead.</summary>
    public static List<ResistancePreset> ResistancePresets()
    {
        return new List<ResistancePreset>
        {
            new ResistancePreset
            {
                name = "Skeleton", description = "No flesh: never bleeds or gets infected; blades and arrows slip through the bones, maces shatter them.",
                resistances = new List<InjuryResistance>
                {
                    new InjuryResistance("No flesh", TagWound, TagBruise, TagInfection) { injuryChance = -100f, bleeding = -100f, worsening = -100f },
                    new InjuryResistance("Hollow bones") { forms = DamageFormMask.Slash | DamageFormMask.Pierce | DamageFormMask.Bite | DamageFormMask.Claw, damageTaken = -40f },
                    new InjuryResistance("Brittle bones", TagFracture) { forms = DamageFormMask.Blunt | DamageFormMask.Crush, injuryChance = 60f, damageTaken = 50f },
                },
            },
            new ResistancePreset
            {
                name = "Stone Golem", description = "A body of stone: no wounds, no bleeding; only heavy blunt blows crack it.",
                resistances = new List<InjuryResistance>
                {
                    new InjuryResistance("Stone body", TagWound, TagBruise, TagInfection, TagBurn, TagConcussion) { injuryChance = -100f, bleeding = -100f },
                    new InjuryResistance("Stone skin") { forms = DamageFormMask.Slash | DamageFormMask.Pierce | DamageFormMask.Bite | DamageFormMask.Claw, damageTaken = -60f, limbLoss = -80f },
                    new InjuryResistance("Cracks", TagFracture) { forms = DamageFormMask.Blunt | DamageFormMask.Crush, damageTaken = 25f },
                },
            },
            new ResistancePreset
            {
                name = "Troll", description = "Regenerates: injuries heal very fast and bleed little; fire still hurts.",
                resistances = new List<InjuryResistance>
                {
                    new InjuryResistance("Regeneration") { healing = 300f, bleeding = -50f, worsening = -100f },
                    new InjuryResistance("Fear of fire", TagBurn) { injuryChance = 50f, healing = -60f },
                },
            },
            new ResistancePreset
            {
                name = "Slime", description = "No bones, no limbs to lose: blunt blows splash through it; cuts close at once.",
                resistances = new List<InjuryResistance>
                {
                    new InjuryResistance("No bones", TagFracture, TagConcussion) { injuryChance = -100f, limbLoss = -100f },
                    new InjuryResistance("Jelly") { forms = DamageFormMask.Blunt | DamageFormMask.Crush, damageTaken = -50f },
                    new InjuryResistance("Closes up", TagWound) { bleeding = -80f, healing = 200f },
                },
            },
            new ResistancePreset
            {
                name = "Armoured Beast", description = "Thick hide or scales: few cuts from blades and claws; blunt blows and spears get through.",
                resistances = new List<InjuryResistance>
                {
                    new InjuryResistance("Thick hide", TagWound) { forms = DamageFormMask.Slash | DamageFormMask.Claw | DamageFormMask.Bite, injuryChance = -60f },
                    new InjuryResistance("Scales") { forms = DamageFormMask.Slash | DamageFormMask.Claw, damageTaken = -30f },
                },
            },
            new ResistancePreset
            {
                name = "Undead", description = "Dead flesh: bleeds slowly, never gets infected, feels no concussion; it does not heal by itself either.",
                resistances = new List<InjuryResistance>
                {
                    new InjuryResistance("Dead flesh") { bleeding = -70f, healing = -90f },
                    new InjuryResistance("No life to lose", TagInfection, TagConcussion) { injuryChance = -100f, worsening = -100f },
                },
            },
        };
    }

    // ------------------------------------------------------------------ medical item presets
    /// <summary>A medical item preset: name, description, treatments.</summary>
    public struct MedicalPreset
    {
        public string name;
        public string description;
        public int stackMax;
        public float weight;
        public float price;
        public List<InjuryTreatment> treatments;
    }

    /// <summary>Bandage, sterile bandage, suture kit, splint, disinfectant, antibiotics, burn salve, surgical kit.</summary>
    public static List<MedicalPreset> MedicalPresets()
    {
        return new List<MedicalPreset>
        {
            new MedicalPreset
            {
                name = "Bandage", description = "Stops most of a wound's bleeding and helps it heal.", stackMax = 10, weight = 0.05f, price = 8f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Bandage", applySeconds = 2f,
                        target = new TreatmentTarget { tags = new List<string> { TagWound, TagBleeding, TagBurn, TagStump } },
                        effects = new List<TreatmentEffect> { new StopBleedingTreatment { effectiveness = 0.85f, healRateMultiplier = 1.5f } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Sterile Bandage", description = "A clean bandage: stops nearly all bleeding and keeps the wound from getting infected.", stackMax = 10, weight = 0.05f, price = 18f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Sterile Bandage", applySeconds = 2f,
                        target = new TreatmentTarget { tags = new List<string> { TagWound, TagBleeding, TagBurn, TagStump } },
                        effects = new List<TreatmentEffect> { new StopBleedingTreatment { effectiveness = 0.95f, healRateMultiplier = 1.8f, sterile = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Suture Kit", description = "Needle and thread: closes deep wounds and stumps so they stop bleeding and can heal.", stackMax = 5, weight = 0.1f, price = 30f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Stitch", applySeconds = 5f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { DeepWound, Laceration, Amputation, Puncture, BiteWound } },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Stitched", allowsHealing = true, healRateMultiplier = 1.6f, impairmentReduction = 0.3f, stopsWorsening = true, stopsBleeding = 1f },
                        },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Splint", description = "Holds a cracked or broken bone in place so it can heal. Compound fractures need surgery.", stackMax = 3, weight = 0.4f, price = 20f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Splint", applySeconds = 4f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Fracture }, maxSeverity = 2, partKinds = new List<BodyPartKind> { BodyPartKind.Arm, BodyPartKind.Hand, BodyPartKind.Leg, BodyPartKind.Foot } },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Splinted", allowsHealing = true, healRateMultiplier = 1.5f, impairmentReduction = 0.45f, stopsWorsening = true },
                        },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Disinfectant", description = "Cleans wounds (no infection) and cures a beginning infection.", stackMax = 5, weight = 0.2f, price = 15f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Disinfect", applySeconds = 1.5f,
                        target = new TreatmentTarget { tags = new List<string> { TagWound, TagBurn, TagStump } },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Disinfected", allowsHealing = false, healRateMultiplier = 1f, impairmentReduction = 0f, stopsWorsening = true } },
                    },
                    new InjuryTreatment
                    {
                        name = "Treat Infection", applySeconds = 1.5f,
                        target = new TreatmentTarget { tags = new List<string> { TagInfection }, maxSeverity = 1 },
                        effects = new List<TreatmentEffect> { new CureInjuryTreatment { removeCompletely = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Antibiotics", description = "Fights any infection: it heals over time instead of spreading.", stackMax = 10, weight = 0.05f, price = 40f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Antibiotics", applySeconds = 1f,
                        target = new TreatmentTarget { tags = new List<string> { TagInfection } },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Antibiotics", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0.3f, stopsWorsening = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Burn Salve", description = "Soothes burns: they heal faster and impair less.", stackMax = 5, weight = 0.1f, price = 15f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Apply Salve", applySeconds = 2f,
                        target = new TreatmentTarget { tags = new List<string> { TagBurn } },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Salved", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0.4f, stopsWorsening = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Surgical Kit", description = "Surgery: sets compound fractures and reattaches a lost limb within its time limit (the limb must be carried).", stackMax = 2, weight = 1f, price = 120f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Reattach Limb", applySeconds = 8f,
                        target = new TreatmentTarget(),
                        effects = new List<TreatmentEffect> { new ReattachLimbTreatment() },
                    },
                    new InjuryTreatment
                    {
                        name = "Surgery", applySeconds = 6f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Fracture, DeepWound, Amputation, Puncture, BiteWound, Frostbite }, minSeverity = 2 },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Operated", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0.5f, stopsWorsening = true, stopsBleeding = 1f },
                        },
                    },
                },
            },
        };
    }
}
