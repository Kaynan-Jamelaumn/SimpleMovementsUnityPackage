using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The default injury configuration, inspired by Project Zomboid: seventeen body parts (head, neck, upper and lower torso,
/// groin, and on each side the upper arm, forearm, hand, thigh, shin and foot) and the Whole Body part illnesses sit on,
/// four severity levels, and the injuries: scratches, lacerations, deep wounds, punctures, bite wounds, bruises,
/// fractures, dislocations, sprained ankles, concussions, eye and ear injuries, severed arteries and tendons, internal
/// bleeding, punctured lungs, burns, frostbite, infections, gangrene, amputation stumps and reattached limbs. Each
/// injury has odds by <see cref="DamageForm"/>: slashes cut and bleed, piercing makes deep punctures, blunt blows
/// bruise, break bones and concuss, bites tear and get infected, falls sprain ankles. Then the illnesses: sepsis,
/// tetanus, rabies, the cold that turns into a flu and pneumonia, dysentery, food poisoning, parasites, heatstroke and
/// venom - with what brings them (complications of wounds, the body's state, food, other characters) and the medicines
/// that treat them. Used by new Injury Settings assets and by the inspector's Reset To Defaults.
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
    public const string Sprain = "Sprain";
    public const string Dislocation = "Dislocation";
    public const string EyeInjury = "EyeInjury";
    public const string EarInjury = "EarInjury";
    public const string ArterialBleed = "ArterialBleed";
    public const string InternalBleeding = "InternalBleeding";
    public const string PuncturedLung = "PuncturedLung";
    public const string TendonDamage = "TendonDamage";
    public const string Gangrene = "Gangrene";
    // Illnesses (injury types of the whole body).
    public const string Sepsis = "Sepsis";
    public const string Tetanus = "Tetanus";
    public const string Rabies = "Rabies";
    public const string CommonCold = "CommonCold";
    public const string Dysentery = "Dysentery";
    public const string FoodPoisoning = "FoodPoisoning";
    public const string Parasites = "Parasites";
    public const string Heatstroke = "Heatstroke";
    public const string Venom = "Venom";

    /// <summary>Id of the part illnesses sit on.</summary>
    public const string WholeBodyId = "Body";

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
    public const string TagSprain = "sprain";
    public const string TagDislocation = "dislocation";
    public const string TagEye = "eye";
    public const string TagEar = "ear";
    public const string TagArtery = "artery";
    public const string TagInternal = "internal";
    public const string TagTendon = "tendon";
    public const string TagGangrene = "gangrene";
    /// <summary>Every illness of the whole body.</summary>
    public const string TagIllness = "illness";
    /// <summary>Colds, the flu, pneumonia.</summary>
    public const string TagRespiratory = "respiratory";
    /// <summary>Dysentery, food poisoning.</summary>
    public const string TagGut = "gut";
    public const string TagParasite = "parasite";
    public const string TagPoison = "poison";

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
        s.illness = new IllnessRules { exposures = Exposures() };
        s.falls = new FallRules();
        s.cityTreatment = new CityTreatmentSettings();
        s.InvalidateLookups();
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
        parts.Add(WholeBodyPart());
        return parts;
    }

    /// <summary>
    /// The part illnesses sit on (a cold, sepsis, food poisoning): hits never land on it, it has no condition to wear
    /// down and it cannot be lost. What an illness does is in its stages.
    /// </summary>
    public static InjuryBodyPart WholeBodyPart() => new InjuryBodyPart
    {
        id = WholeBodyId, displayName = "Whole Body", kind = BodyPartKind.WholeBody, side = BodyPartSide.Center,
        aliases = new List<string> { "WholeBody", "Systemic", "Illness" },
        hitParts = new List<string>(), heightRange = new Vector2(-1f, -1f), hitWeight = 0f,
        conditionDamageMultiplier = 0f, conditionRegenPerSecond = 0f,
        impaired = new StatusConsequence(""), lost = new StatusConsequence(""),
        limbLoss = new LimbLossRules { canBeLost = false, stumpInjury = "" },
        diagramRect = new Rect(0.76f, 0.84f, 0.22f, 0.14f),
    };

    private static InjuryBodyPart sharedWholeBody;

    /// <summary>The built-in Whole Body part used by characters whose settings have none (settings made before illnesses).</summary>
    public static InjuryBodyPart SharedWholeBody => sharedWholeBody ?? (sharedWholeBody = WholeBodyPart());

    // ------------------------------------------------------------------ injuries
    public static List<InjuryTypeDefinition> InjuryTypes()
    {
        // Hits try the types in this order (at most Max Injuries Per Hit): rare, specific injuries first, the common
        // wounds after them. Types that hits never cause (infections, illnesses) come last.
        return new List<InjuryTypeDefinition>
        {
            new InjuryTypeDefinition
            {
                id = Burn, displayName = "Burn", color = new Color(1f, 0.55f, 0.2f), iconShape = InjuryIconShape.Flame, hudAlert = InjuryAlertLevel.Notice,
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
            EarInjuryType(),
            EyeInjuryType(),
            ConcussionType(),
            ArterialBleedType(),
            PuncturedLungType(),
            InternalBleedingType(),
            TendonDamageType(),
            new InjuryTypeDefinition
            {
                id = Fracture, displayName = "Fracture", color = new Color(0.85f, 0.85f, 0.95f), iconShape = InjuryIconShape.Bone, hudAlert = InjuryAlertLevel.Warning,
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
            DislocationType(),
            SprainType(),
            new InjuryTypeDefinition
            {
                id = DeepWound, displayName = "Deep Wound", color = new Color(0.8f, 0.15f, 0.2f), iconShape = InjuryIconShape.Cut, hudAlert = InjuryAlertLevel.Warning,
                description = "A deep cut that bleeds heavily and does not stop by itself. It needs stitches to heal and gets infected if left open.",
                tags = new List<string> { TagWound, TagBleeding },
                trigger = new InjuryTrigger { chance = 0.4f, minHitFraction = 0.3f, damageKinds = InjuryDamageKinds.Physical, formChances = DeepWoundOdds() },
                maxPerPart = 1,
                complications = new List<InjuryComplication> { new InjuryComplication(Tetanus, ComplicationWhen.WhileUntreated, 0.01f) },
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
                id = Laceration, displayName = "Laceration", color = new Color(0.95f, 0.35f, 0.3f), iconShape = InjuryIconShape.Cut,
                description = "A bleeding cut. A bandage stops most of the bleeding and lets it close; left open it can get infected.",
                tags = new List<string> { TagWound, TagBleeding },
                trigger = new InjuryTrigger { chance = 0.5f, minHitFraction = 0.12f, damageKinds = InjuryDamageKinds.Physical, formChances = LacerationOdds() },
                complications = new List<InjuryComplication> { new InjuryComplication(Tetanus, ComplicationWhen.WhileUntreated, 0.004f) },
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
                id = Scratch, displayName = "Scratch", color = new Color(1f, 0.6f, 0.55f), iconShape = InjuryIconShape.Cut,
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
            InfectionType(),
            GangreneType(),
            new InjuryTypeDefinition
            {
                id = Amputation, displayName = "Amputation Stump", color = new Color(0.6f, 0.05f, 0.1f), iconShape = InjuryIconShape.Stump, hudAlert = InjuryAlertLevel.Warning,
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
                id = Reattached, displayName = "Reattached Limb", color = new Color(0.55f, 0.75f, 1f), iconShape = InjuryIconShape.Joint,
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
            SepsisType(),
            TetanusType(),
            RabiesType(),
            CommonColdType(),
            DysenteryType(),
            FoodPoisoningType(),
            ParasitesType(),
            HeatstrokeType(),
            VenomType(),
        };
    }

    /// <summary>
    /// An untreated wound got infected. Left alone a severe infection spreads to the blood (sepsis) and may rot the part
    /// (gangrene).
    /// </summary>
    public static InjuryTypeDefinition InfectionType() => new InjuryTypeDefinition
    {
        id = Infection, displayName = "Infection", color = new Color(0.55f, 0.8f, 0.3f), iconShape = InjuryIconShape.Biohazard, hudAlert = InjuryAlertLevel.Warning,
        description = "An untreated wound got infected. It drains health and wears the part down until disinfected or treated with antibiotics; " +
                      "left alone it reaches the blood (sepsis) and may rot the part (gangrene).",
        tags = new List<string> { TagInfection },
        causedByHits = false,
        healStageByStage = true,
        maxPerPart = 1,
        complications = new List<InjuryComplication> { new InjuryComplication(Gangrene, ComplicationWhen.WhileUntreated, 0.03f, 1) },
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
                worsenAfterSeconds = 600f, worsensInto = Sepsis,
                consequence = new StatusConsequence("Severe Infection")
                {
                    healthPerSecond = -0.08f, hungerDrain = 1.4f, thirstDrain = 1.6f, stunChancePerMinute = 0.08f, stunSeconds = 1.5f,
                },
            },
        },
    };

    // ------------------------------------------------------------------ new injuries
    /// <summary>A hard landing or a crushed foot: a twisted or sprained ankle, or a torn ligament that needs a splint.</summary>
    public static InjuryTypeDefinition SprainType() => new InjuryTypeDefinition
    {
        id = Sprain, displayName = "Sprained Ankle", color = new Color(0.75f, 0.6f, 0.9f), iconShape = InjuryIconShape.Joint, hudAlert = InjuryAlertLevel.Notice,
        description = "The ankle gave way on a hard landing. Rest heals a twisted ankle; wrap a sprain with a bandage, and splint a torn ligament or it will not heal.",
        tags = new List<string> { TagSprain },
        trigger = new InjuryTrigger
        {
            chance = 0.65f, minHitFraction = 0.05f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Foot },
            // Falls and crushing weights (Crush); a hard blunt blow now and then.
            formChances = new DamageFormChances(0f, 0f, 0f, 0.2f, 0f, 0f, 1f),
        },
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Twisted Ankle", severity = 0, minHitFraction = 0.05f, impairment = 0.25f, healSeconds = 180f, untreatedHealRate = 1f },
            new InjuryStage
            {
                name = "Sprained Ankle", severity = 1, minHitFraction = 0.14f, impairment = 0.5f, healSeconds = 420f, untreatedHealRate = 0.6f,
                blocks = BodyAction.Sprint,
            },
            new InjuryStage
            {
                name = "Torn Ligament", severity = 2, minHitFraction = 0.28f, impairment = 0.75f, healSeconds = 900f, needsTreatmentToHeal = true,
                blocks = BodyAction.Sprint | BodyAction.Jump | BodyAction.Dash | BodyAction.Roll,
            },
        },
    };

    /// <summary>A joint knocked out of place by a heavy blow: useless until it is put back, sore for a while after.</summary>
    public static InjuryTypeDefinition DislocationType() => new InjuryTypeDefinition
    {
        id = Dislocation, displayName = "Dislocation", color = new Color(0.8f, 0.75f, 0.95f), iconShape = InjuryIconShape.Joint, hudAlert = InjuryAlertLevel.Warning,
        description = "A joint knocked out of place. The limb is nearly useless until the joint is put back (a splint kit); then it is only sore.",
        tags = new List<string> { TagDislocation },
        trigger = new InjuryTrigger
        {
            chance = 0.1f, minHitFraction = 0.3f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Arm, BodyPartKind.Leg },
            formChances = new DamageFormChances(0.5f, 0.1f, 0.1f, 1.4f, 0.3f, 0.1f, 1.6f),
        },
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Sore Joint", severity = 0, impairment = 0.25f, healSeconds = 300f, untreatedHealRate = 1f },
            new InjuryStage { name = "Dislocated Joint", severity = 2, minHitFraction = 0.3f, impairment = 0.85f, healSeconds = 600f, needsTreatmentToHeal = true },
        },
    };

    /// <summary>
    /// A hit to an eye (points and claws above all): the left or the right one, and that half of the screen goes dim, then
    /// dark. One per eye: with both blinded the character sees next to nothing.
    /// </summary>
    public static InjuryTypeDefinition EyeInjuryType() => new InjuryTypeDefinition
    {
        id = EyeInjury, displayName = "Eye Injury", color = new Color(0.95f, 0.75f, 0.85f), iconShape = InjuryIconShape.Eye, hudAlert = InjuryAlertLevel.Warning,
        description = "A hit to the eye. That side of your sight dims; a blinded eye sees nothing until it is operated on. Cover it with a bandage so it can heal.",
        tags = new List<string> { TagEye, TagWound },
        sided = true,
        trigger = new InjuryTrigger
        {
            chance = 0.07f, minHitFraction = 0.08f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Head },
            formChances = new DamageFormChances(0.4f, 0.8f, 1.6f, 0.6f, 0.3f, 1.6f, 0.3f),
        },
        healStageByStage = true,
        maxPerPart = 2,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Black Eye", severity = 0, minHitFraction = 0.08f, healSeconds = 240f, untreatedHealRate = 1f,
                vision = new VisionEffect(0.3f, 0.12f),
                consequence = new StatusConsequence("") { combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.CriticalChance, -4f) } },
            },
            new InjuryStage
            {
                name = "Injured Eye", severity = 1, minHitFraction = 0.2f, bleedingPerSecond = 0.1f, clotsAfterSeconds = 60f, healSeconds = 600f,
                untreatedHealRate = 0.4f, worsenAfterSeconds = 480f, worsensInto = Infection,
                vision = new VisionEffect(0.65f, 0.25f),
                consequence = new StatusConsequence("Injured Eye") { combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.CriticalChance, -12f) } },
            },
            new InjuryStage
            {
                name = "Blinded Eye", severity = 3, minHitFraction = 0.4f, bleedingPerSecond = 0.25f, clotsAfterSeconds = 120f, healSeconds = 1200f,
                needsTreatmentToHeal = true, worsenAfterSeconds = 360f, worsensInto = Infection,
                vision = new VisionEffect(1f, 0.15f),
                consequence = new StatusConsequence("Blinded Eye")
                {
                    combatStats = new List<CombatStatModifier>
                    {
                        new CombatStatModifier(CombatStatType.CriticalChance, -25f),
                        new CombatStatModifier(CombatStatType.AttackSpeed, -8f),
                    },
                },
            },
        },
    };

    /// <summary>A blast next to the head: ringing ears, or a ruptured eardrum that leaves the character dizzy.</summary>
    public static InjuryTypeDefinition EarInjuryType() => new InjuryTypeDefinition
    {
        id = EarInjury, displayName = "Ear Injury", color = new Color(0.9f, 0.8f, 0.6f), iconShape = InjuryIconShape.Ear, hudAlert = InjuryAlertLevel.Notice,
        description = "A blast hurt the ear. Ringing passes by itself; a ruptured eardrum leaves you dizzy until it heals.",
        tags = new List<string> { TagEar },
        sided = true,
        trigger = new InjuryTrigger
        {
            chance = 0.4f, minHitFraction = 0.08f, damageKinds = InjuryDamageKinds.All, deliveries = InjuryDeliveries.Area,
            partKinds = new List<BodyPartKind> { BodyPartKind.Head },
        },
        healStageByStage = true,
        maxPerPart = 2,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Ringing Ear", severity = 0, minHitFraction = 0.08f, healSeconds = 150f, untreatedHealRate = 1f,
                vision = new VisionEffect(0f, 0f, 0f, 0.2f),
                consequence = new StatusConsequence("") { sanityPerSecond = -0.01f },
            },
            new InjuryStage
            {
                name = "Ruptured Eardrum", severity = 2, minHitFraction = 0.25f, healSeconds = 900f, untreatedHealRate = 0.6f,
                vision = new VisionEffect(0f, 0f, 0f, 0.5f),
                consequence = new StatusConsequence("Ruptured Eardrum") { stunChancePerMinute = 0.12f, stunSeconds = 1f, sanityPerSecond = -0.02f },
            },
        },
    };

    /// <summary>A deep cut that opened an artery: it bleeds faster than anything else and never stops by itself.</summary>
    public static InjuryTypeDefinition ArterialBleedType() => new InjuryTypeDefinition
    {
        id = ArterialBleed, displayName = "Severed Artery", color = new Color(0.9f, 0.05f, 0.1f), iconShape = InjuryIconShape.Drop, hudAlert = InjuryAlertLevel.Critical,
        description = "An artery is open: you are bleeding out. A tourniquet stops it on a limb, a bandage slows it; it needs stitches or surgery to close.",
        tags = new List<string> { TagWound, TagBleeding, TagArtery },
        trigger = new InjuryTrigger
        {
            chance = 0.08f, minHitFraction = 0.35f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Neck, BodyPartKind.Arm, BodyPartKind.Leg },
            formChances = new DamageFormChances(0.5f, 1.5f, 1.2f, 0f, 0.8f, 0.8f, 0f),
        },
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Severed Artery", severity = 3, minHitFraction = 0.35f, bleedingPerSecond = 2.2f, impairment = 0.3f, healSeconds = 720f,
                needsTreatmentToHeal = true, worsenAfterSeconds = 300f, worsensInto = Infection,
            },
        },
    };

    /// <summary>A heavy blow or a deep stab to the body: blood is lost inside, where no bandage reaches.</summary>
    public static InjuryTypeDefinition InternalBleedingType() => new InjuryTypeDefinition
    {
        id = InternalBleeding, displayName = "Internal Bleeding", color = new Color(0.6f, 0.1f, 0.3f), iconShape = InjuryIconShape.Drop, hudAlert = InjuryAlertLevel.Warning,
        description = "Bleeding inside the body. No bandage reaches it: it drains you slowly until a surgeon closes it.",
        tags = new List<string> { TagInternal },
        trigger = new InjuryTrigger
        {
            chance = 0.08f, minHitFraction = 0.35f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Torso },
            formChances = new DamageFormChances(0.6f, 0.4f, 1.2f, 1.2f, 0.5f, 0.3f, 1.6f),
        },
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Internal Bleeding", severity = 3, minHitFraction = 0.35f, impairment = 0.3f, healSeconds = 900f, needsTreatmentToHeal = true,
                vision = new VisionEffect(0f, 0f, 0.25f),
                consequence = new StatusConsequence("Internal Bleeding") { healthPerSecond = -0.25f, staminaPerSecond = -0.3f },
            },
        },
    };

    /// <summary>A point through the chest: the lung collapses, and with it the character's breath.</summary>
    public static InjuryTypeDefinition PuncturedLungType() => new InjuryTypeDefinition
    {
        id = PuncturedLung, displayName = "Punctured Lung", color = new Color(0.55f, 0.7f, 0.95f), iconShape = InjuryIconShape.Lungs, hudAlert = InjuryAlertLevel.Critical,
        description = "A lung is pierced. You cannot catch your breath - no sprinting, little stamina - until a surgeon closes it.",
        tags = new List<string> { TagInternal, TagPuncture },
        trigger = new InjuryTrigger
        {
            chance = 0.07f, minHitFraction = 0.3f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Torso },
            formChances = new DamageFormChances(0.3f, 0.3f, 1.6f, 0f, 0.4f, 0.3f, 0.2f),
        },
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Punctured Lung", severity = 3, minHitFraction = 0.3f, impairment = 0.4f, healSeconds = 1200f, needsTreatmentToHeal = true,
                blocks = BodyAction.Sprint,
                consequence = new StatusConsequence("Punctured Lung")
                {
                    healthPerSecond = -0.1f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -50f),
                        TraitModifier.Percent(TraitStat.StaminaRegenRate, -50f),
                    },
                },
            },
        },
    };

    /// <summary>A blade through the tendons of a hand or an arm: the hand will not close until it is operated on.</summary>
    public static InjuryTypeDefinition TendonDamageType() => new InjuryTypeDefinition
    {
        id = TendonDamage, displayName = "Cut Tendon", color = new Color(0.95f, 0.9f, 0.7f), iconShape = InjuryIconShape.Cut, hudAlert = InjuryAlertLevel.Warning,
        description = "The tendons are cut: the hand will not grip. Only surgery mends it.",
        tags = new List<string> { TagTendon },
        trigger = new InjuryTrigger
        {
            chance = 0.07f, minHitFraction = 0.3f, damageKinds = InjuryDamageKinds.Physical,
            partKinds = new List<BodyPartKind> { BodyPartKind.Hand, BodyPartKind.Arm },
            formChances = new DamageFormChances(0.4f, 1.5f, 0.6f, 0f, 0.6f, 1.2f, 0f),
        },
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Cut Tendon", severity = 2, minHitFraction = 0.3f, impairment = 0.92f, healSeconds = 1200f, needsTreatmentToHeal = true },
        },
    };

    /// <summary>
    /// Dead flesh, from frostbite or a severe infection left alone. A surgeon can still cut the dead tissue out of a
    /// beginning necrosis; gangrene only leaves amputation - or sepsis.
    /// </summary>
    public static InjuryTypeDefinition GangreneType() => new InjuryTypeDefinition
    {
        id = Gangrene, displayName = "Gangrene", color = new Color(0.35f, 0.45f, 0.2f), iconShape = InjuryIconShape.Skull, hudAlert = InjuryAlertLevel.Critical,
        description = "The flesh is dying. A surgeon can still clean out a beginning necrosis; once it is gangrene, the limb must come off before the rot reaches the blood.",
        tags = new List<string> { TagGangrene },
        causedByHits = false,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Necrosis", severity = 2, impairment = 0.4f, conditionLossPerSecond = 0.1f, healSeconds = 720f, needsTreatmentToHeal = true,
                worsenAfterSeconds = 420f,
            },
            new InjuryStage
            {
                name = "Gangrene", severity = 3, impairment = 0.85f, conditionLossPerSecond = 0.3f, healSeconds = 1200f, needsTreatmentToHeal = true,
                worsenAfterSeconds = 420f, worsensInto = Sepsis,
                consequence = new StatusConsequence("Gangrene") { healthPerSecond = -0.1f, sanityPerSecond = -0.03f },
            },
        },
    };

    // ------------------------------------------------------------------ illnesses (the whole body)
    /// <summary>An infection in the blood: the end of a severe infection or of gangrene left alone. Antibiotics, or death.</summary>
    public static InjuryTypeDefinition SepsisType() => new InjuryTypeDefinition
    {
        id = Sepsis, displayName = "Sepsis", color = new Color(0.7f, 0.85f, 0.25f), iconShape = InjuryIconShape.Biohazard, hudAlert = InjuryAlertLevel.Critical,
        description = "The infection reached your blood. Fever, weakness, then shock: without antibiotics it kills.",
        tags = new List<string> { TagIllness, TagInfection },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Sepsis", severity = 2, healSeconds = 600f, needsTreatmentToHeal = true, worsenAfterSeconds = 480f,
                vision = new VisionEffect(0f, 0.12f),
                consequence = new StatusConsequence("Sepsis")
                {
                    healthPerSecond = -0.12f, staminaPerSecond = -0.5f, thirstDrain = 1.5f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -20f),
                        TraitModifier.Percent(TraitStat.MoveSpeed, -10f),
                    },
                },
            },
            new InjuryStage
            {
                name = "Septic Shock", severity = 3, healSeconds = 900f, needsTreatmentToHeal = true,
                vision = new VisionEffect(0f, 0.25f, 0.5f, 0.3f),
                blocks = BodyAction.Sprint,
                consequence = new StatusConsequence("Septic Shock")
                {
                    healthPerSecond = -0.35f, staminaPerSecond = -0.8f, thirstDrain = 1.8f, stunChancePerMinute = 0.15f, stunSeconds = 2f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -40f),
                        TraitModifier.Percent(TraitStat.MoveSpeed, -25f),
                    },
                },
            },
        },
    };

    /// <summary>
    /// From dirt in a wound that was never cleaned (punctures and bites above all). It shows nothing for a while, then the
    /// muscles lock in spasms. An antitoxin in time stops it; cleaning wounds prevents it.
    /// </summary>
    public static InjuryTypeDefinition TetanusType() => new InjuryTypeDefinition
    {
        id = Tetanus, displayName = "Tetanus", color = new Color(0.8f, 0.7f, 0.35f), iconShape = InjuryIconShape.Germ, hudAlert = InjuryAlertLevel.Warning,
        description = "Dirt in an uncleaned wound. First nothing shows; then your muscles lock in spasms. Antitoxin cures it - disinfecting wounds prevents it.",
        tags = new List<string> { TagIllness, "tetanus" },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        immunitySeconds = 3600f,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Tetanus (incubating)", severity = 0, hidden = true, healSeconds = 600f, needsTreatmentToHeal = true, worsenAfterSeconds = 300f },
            new InjuryStage
            {
                name = "Tetanus", severity = 2, healSeconds = 900f, needsTreatmentToHeal = true, worsenAfterSeconds = 600f, worsenChance = 0.6f,
                consequence = new StatusConsequence("Muscle Spasms")
                {
                    stunChancePerMinute = 0.25f, stunSeconds = 1.5f,
                    combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.AttackSpeed, -15f) },
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -10f) },
                },
            },
            new InjuryStage
            {
                name = "Severe Tetanus", severity = 3, healSeconds = 1200f, needsTreatmentToHeal = true,
                blocks = BodyAction.Sprint | BodyAction.Roll | BodyAction.Dash,
                consequence = new StatusConsequence("Lockjaw")
                {
                    healthPerSecond = -0.08f, stunChancePerMinute = 0.5f, stunSeconds = 2.5f,
                    combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.AttackSpeed, -30f) },
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -20f) },
                },
            },
        },
    };

    /// <summary>
    /// From the bite of a sick animal. Nothing shows while it incubates - a vaccine then prevents it; once it shows, only
    /// powerful healing magic can cure it.
    /// </summary>
    public static InjuryTypeDefinition RabiesType() => new InjuryTypeDefinition
    {
        id = Rabies, displayName = "Rabies", color = new Color(0.85f, 0.4f, 0.55f), iconShape = InjuryIconShape.Fang, hudAlert = InjuryAlertLevel.Critical,
        description = "From the bite of a sick animal. A vaccine taken soon after a bite prevents it; once the fever and the fear of water start, no medicine helps.",
        tags = new List<string> { TagIllness, "rabies" },
        causedByHits = false,
        systemic = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Rabies (incubating)", severity = 0, hidden = true, healSeconds = 600f, needsTreatmentToHeal = true, worsenAfterSeconds = 600f },
            new InjuryStage
            {
                name = "Rabies", severity = 3, healSeconds = 1800f, needsTreatmentToHeal = true,
                vision = new VisionEffect(0f, 0.15f, 0f, 0.35f),
                consequence = new StatusConsequence("Rabies")
                {
                    healthPerSecond = -0.06f, sanityPerSecond = -0.12f, thirstDrain = 1.6f, stunChancePerMinute = 0.3f, stunSeconds = 1.5f,
                },
            },
        },
    };

    /// <summary>
    /// Caught from the cold, from exhaustion, or from someone coughing nearby. It usually passes; sometimes it turns into
    /// a flu, and a flu into pneumonia, which does not heal without medicine.
    /// </summary>
    public static InjuryTypeDefinition CommonColdType() => new InjuryTypeDefinition
    {
        id = CommonCold, displayName = "Cold", color = new Color(0.6f, 0.85f, 0.95f), iconShape = InjuryIconShape.Germ, hudAlert = InjuryAlertLevel.Notice,
        description = "Caught from the cold, or from someone coughing nearby. Keep warm, eat and sleep: it passes. Neglected it becomes a flu, then pneumonia.",
        tags = new List<string> { TagIllness, TagRespiratory },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        immunitySeconds = 1800f,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Cold", severity = 0, healSeconds = 600f, untreatedHealRate = 1f, worsenAfterSeconds = 300f, worsenChance = 0.3f,
                contagionPerMinute = 0.02f,
                consequence = new StatusConsequence("Cold")
                {
                    stunChancePerMinute = 0.12f, stunSeconds = 0.5f,
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegenRate, -15f) },
                },
            },
            new InjuryStage
            {
                name = "Flu", severity = 1, healSeconds = 900f, untreatedHealRate = 0.8f, worsenAfterSeconds = 480f, worsenChance = 0.25f,
                contagionPerMinute = 0.04f,
                vision = new VisionEffect(0f, 0.1f),
                consequence = new StatusConsequence("Flu")
                {
                    thirstDrain = 1.3f, stunChancePerMinute = 0.2f, stunSeconds = 0.8f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -20f),
                        TraitModifier.Percent(TraitStat.StaminaRegenRate, -30f),
                        TraitModifier.Percent(TraitStat.MoveSpeed, -8f),
                    },
                },
            },
            new InjuryStage
            {
                name = "Pneumonia", severity = 2, healSeconds = 1200f, needsTreatmentToHeal = true, contagionPerMinute = 0.02f,
                vision = new VisionEffect(0f, 0.2f),
                blocks = BodyAction.Sprint,
                consequence = new StatusConsequence("Pneumonia")
                {
                    healthPerSecond = -0.06f, thirstDrain = 1.4f, stunChancePerMinute = 0.25f, stunSeconds = 1f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -40f),
                        TraitModifier.Percent(TraitStat.StaminaRegenRate, -50f),
                        TraitModifier.Percent(TraitStat.MoveSpeed, -15f),
                    },
                },
            },
        },
    };

    /// <summary>From foul water: after a quiet start, cramps and a thirst that will not go; the severe form needs medicine.</summary>
    public static InjuryTypeDefinition DysenteryType() => new InjuryTypeDefinition
    {
        id = Dysentery, displayName = "Dysentery", color = new Color(0.65f, 0.55f, 0.3f), iconShape = InjuryIconShape.Stomach, hudAlert = InjuryAlertLevel.Warning,
        description = "From foul water or filthy food. Cramps, and you lose water far faster than you drink it. Rest and a stomach remedy; the severe form needs one.",
        tags = new List<string> { TagIllness, TagGut },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        immunitySeconds = 900f,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Dysentery (incubating)", severity = 0, hidden = true, healSeconds = 300f, needsTreatmentToHeal = true, worsenAfterSeconds = 120f },
            new InjuryStage
            {
                name = "Dysentery", severity = 1, healSeconds = 720f, untreatedHealRate = 0.7f, worsenAfterSeconds = 420f, worsenChance = 0.35f,
                contagionPerMinute = 0.005f,
                consequence = new StatusConsequence("Dysentery")
                {
                    thirstDrain = 2.2f, hungerDrain = 1.5f, staminaPerSecond = -0.4f, stunChancePerMinute = 0.15f, stunSeconds = 1f,
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -15f) },
                },
            },
            new InjuryStage
            {
                name = "Severe Dysentery", severity = 2, healSeconds = 900f, needsTreatmentToHeal = true,
                blocks = BodyAction.Sprint,
                consequence = new StatusConsequence("Severe Dysentery")
                {
                    healthPerSecond = -0.08f, thirstDrain = 3f, hungerDrain = 1.8f, staminaPerSecond = -0.6f, stunChancePerMinute = 0.25f, stunSeconds = 1.2f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -30f),
                        TraitModifier.Percent(TraitStat.MoveSpeed, -15f),
                    },
                },
            },
        },
    };

    /// <summary>From food that had turned: nausea, then - often - a bad hour of retching. It passes by itself.</summary>
    public static InjuryTypeDefinition FoodPoisoningType() => new InjuryTypeDefinition
    {
        id = FoodPoisoning, displayName = "Food Poisoning", color = new Color(0.6f, 0.75f, 0.35f), iconShape = InjuryIconShape.Stomach, hudAlert = InjuryAlertLevel.Notice,
        description = "Something you ate had turned. Nausea, then retching: you keep little food or water down until it passes. A stomach remedy shortens it.",
        tags = new List<string> { TagIllness, TagGut },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Nausea", severity = 0, healSeconds = 300f, untreatedHealRate = 1f, worsenAfterSeconds = 120f, worsenChance = 0.5f,
                consequence = new StatusConsequence("Nausea") { hungerDrain = 1.3f, staminaPerSecond = -0.2f },
            },
            new InjuryStage
            {
                name = "Food Poisoning", severity = 1, healSeconds = 480f, untreatedHealRate = 1f,
                vision = new VisionEffect(0f, 0.08f, 0f, 0.2f),
                consequence = new StatusConsequence("Food Poisoning")
                {
                    healthPerSecond = -0.03f, hungerDrain = 2f, thirstDrain = 1.8f, staminaPerSecond = -0.5f, stunChancePerMinute = 0.3f, stunSeconds = 1.2f,
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -20f) },
                },
            },
        },
    };

    /// <summary>From raw or fouled meat and foul water: worms that eat with you. They never leave by themselves.</summary>
    public static InjuryTypeDefinition ParasitesType() => new InjuryTypeDefinition
    {
        id = Parasites, displayName = "Parasites", color = new Color(0.8f, 0.65f, 0.5f), iconShape = InjuryIconShape.Germ, hudAlert = InjuryAlertLevel.Notice,
        description = "Worms, from raw or fouled meat or foul water. You are always hungry and grow weaker; they never leave by themselves - take an antiparasitic.",
        tags = new List<string> { TagIllness, TagParasite },
        causedByHits = false,
        systemic = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage { name = "Parasites (incubating)", severity = 0, hidden = true, healSeconds = 600f, needsTreatmentToHeal = true, worsenAfterSeconds = 300f },
            new InjuryStage
            {
                name = "Intestinal Parasites", severity = 1, healSeconds = 600f, needsTreatmentToHeal = true, worsenAfterSeconds = 900f, worsenChance = 0.5f,
                consequence = new StatusConsequence("Parasites")
                {
                    hungerDrain = 1.8f,
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -10f) },
                },
            },
            new InjuryStage
            {
                name = "Heavy Infestation", severity = 2, healSeconds = 900f, needsTreatmentToHeal = true,
                consequence = new StatusConsequence("Heavy Infestation")
                {
                    healthPerSecond = -0.03f, hungerDrain = 2.5f,
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -25f) },
                },
            },
        },
    };

    /// <summary>From overheating: heat exhaustion first, then heatstroke. Cooling down and drinking is the cure.</summary>
    public static InjuryTypeDefinition HeatstrokeType() => new InjuryTypeDefinition
    {
        id = Heatstroke, displayName = "Heatstroke", color = new Color(1f, 0.65f, 0.3f), iconShape = InjuryIconShape.Flame, hudAlert = InjuryAlertLevel.Warning,
        description = "Your body overheated. Get out of the heat and drink: it passes. Staying in the heat turns exhaustion into heatstroke.",
        tags = new List<string> { TagIllness, "heat" },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Heat Exhaustion", severity = 1, healSeconds = 300f, untreatedHealRate = 1f, worsenAfterSeconds = 300f, worsenChance = 0.4f,
                vision = new VisionEffect(0f, 0.12f),
                consequence = new StatusConsequence("Heat Exhaustion")
                {
                    thirstDrain = 2f,
                    characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MaxStamina, -20f) },
                },
            },
            new InjuryStage
            {
                name = "Heatstroke", severity = 2, healSeconds = 480f, untreatedHealRate = 0.6f,
                vision = new VisionEffect(0f, 0.25f, 0f, 0.4f),
                consequence = new StatusConsequence("Heatstroke")
                {
                    healthPerSecond = -0.08f, thirstDrain = 2.5f, stunChancePerMinute = 0.2f, stunSeconds = 1.5f,
                    characterStats = new List<TraitModifier>
                    {
                        TraitModifier.Percent(TraitStat.MaxStamina, -35f),
                        TraitModifier.Percent(TraitStat.MoveSpeed, -10f),
                    },
                },
            },
        },
    };

    /// <summary>
    /// Poison in the blood, from a venomous creature: give it to a mob's attack with the ability effect Injury ▸ Inflict
    /// Injury (Venom). It burns out by itself, slowly; an antivenom ends it at once.
    /// </summary>
    public static InjuryTypeDefinition VenomType() => new InjuryTypeDefinition
    {
        id = Venom, displayName = "Venom", color = new Color(0.55f, 0.85f, 0.4f), iconShape = InjuryIconShape.Fang, hudAlert = InjuryAlertLevel.Warning,
        description = "Venom in your blood. It burns out by itself if you live that long; an antivenom ends it at once.",
        tags = new List<string> { TagIllness, TagPoison },
        causedByHits = false,
        systemic = true,
        healStageByStage = true,
        maxPerPart = 1,
        stages = new List<InjuryStage>
        {
            new InjuryStage
            {
                name = "Envenomed", severity = 1, healSeconds = 240f, untreatedHealRate = 1f,
                vision = new VisionEffect(0f, 0.15f),
                consequence = new StatusConsequence("Venom") { healthPerSecond = -0.12f, staminaPerSecond = -0.4f, element = ElementType.Poison },
            },
            new InjuryStage
            {
                name = "Deadly Venom", severity = 3, healSeconds = 360f, untreatedHealRate = 0.7f,
                vision = new VisionEffect(0f, 0.3f, 0.3f, 0.3f),
                consequence = new StatusConsequence("Deadly Venom")
                {
                    healthPerSecond = -0.3f, staminaPerSecond = -0.8f, stunChancePerMinute = 0.2f, stunSeconds = 1.5f, element = ElementType.Poison,
                },
            },
        },
    };

    /// <summary>What the body's state exposes a character to: the cold gives colds and frostbitten hands and feet, the heat heatstroke.</summary>
    public static List<IllnessExposure> Exposures() => new List<IllnessExposure>
    {
        new IllnessExposure(CommonCold, ExposureSource.Cold, 0.04f),
        new IllnessExposure(CommonCold, ExposureSource.Freezing, 0.08f),
        new IllnessExposure(CommonCold, ExposureSource.Exhausted, 0.01f),
        new IllnessExposure(Frostbite, ExposureSource.Freezing, 0.06f) { partKinds = new List<BodyPartKind> { BodyPartKind.Hand, BodyPartKind.Foot } },
        new IllnessExposure(Heatstroke, ExposureSource.Overheated, 0.1f),
    };

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
        id = Bruise, displayName = "Bruise", color = new Color(0.55f, 0.45f, 0.75f), iconShape = InjuryIconShape.Bruise,
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
        id = Puncture, displayName = "Puncture Wound", color = new Color(0.85f, 0.25f, 0.35f), iconShape = InjuryIconShape.Cut,
        description = "A narrow, deep wound from a point. It bleeds less than a cut but closes over dirt: clean and stitch it, or it gets infected quickly - and may give you tetanus.",
        tags = new List<string> { TagWound, TagBleeding, TagPuncture },
        complications = new List<InjuryComplication> { new InjuryComplication(Tetanus, ComplicationWhen.WhileUntreated, 0.02f) },
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
        id = BiteWound, displayName = "Bite Wound", color = new Color(0.75f, 0.3f, 0.2f), iconShape = InjuryIconShape.Fang,
        description = "Torn by teeth. It bleeds, and a mouth is dirty: it gets infected quickly unless it is disinfected. A sick animal's bite can carry rabies: a vaccine soon after prevents it.",
        tags = new List<string> { TagWound, TagBleeding, TagBite },
        complications = new List<InjuryComplication>
        {
            new InjuryComplication(Rabies, ComplicationWhen.Inflicted, 0.015f),
            new InjuryComplication(Tetanus, ComplicationWhen.WhileUntreated, 0.015f),
        },
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
        id = Concussion, displayName = "Concussion", color = new Color(0.95f, 0.85f, 0.5f), iconShape = InjuryIconShape.Dizzy, hudAlert = InjuryAlertLevel.Notice,
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
                vision = new VisionEffect(0f, 0.15f, 0.2f, 0.6f),
                consequence = new StatusConsequence("Concussion") { stunChancePerMinute = 0.2f, stunSeconds = 1f, sanityPerSecond = -0.02f },
            },
        },
    };

    /// <summary>Ice damage: frostnip, or frostbite that needs treatment and can rot (an infection).</summary>
    public static InjuryTypeDefinition FrostbiteType() => new InjuryTypeDefinition
    {
        id = Frostbite, displayName = "Frostbite", color = new Color(0.55f, 0.8f, 1f), iconShape = InjuryIconShape.Frost, hudAlert = InjuryAlertLevel.Notice,
        description = "Ice or bitter cold froze the skin. Frostnip heals by itself; frostbite needs treatment (burn salve, bandages) and the flesh dies if it is left alone.",
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
                conditionLossPerSecond = 0.03f, worsenAfterSeconds = 600f, worsensInto = Gangrene,
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
            s.InvalidateLookups();
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
        /// <summary>Taken to be safe: used up from the hotbar even when it finds nothing to treat (a vaccine, an antitoxin).</summary>
        public bool preventive;
    }

    /// <summary>
    /// Bandage, sterile bandage, tourniquet, suture kit, splint, disinfectant, antibiotics, burn salve, surgical kit, and
    /// the medicines of the illnesses: cold remedy, stomach remedy, antiparasitic, antitoxin, rabies vaccine, antivenom.
    /// </summary>
    public static List<MedicalPreset> MedicalPresets()
    {
        return new List<MedicalPreset>
        {
            new MedicalPreset
            {
                name = "Bandage", description = "Stops most of a wound's bleeding and helps it heal. Also wraps a sprained ankle.", stackMax = 10, weight = 0.05f, price = 8f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Bandage", applySeconds = 2f,
                        target = new TreatmentTarget { tags = new List<string> { TagWound, TagBleeding, TagBurn, TagStump, TagSprain } },
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
                        target = new TreatmentTarget { tags = new List<string> { TagWound, TagBleeding, TagBurn, TagStump, TagSprain } },
                        effects = new List<TreatmentEffect> { new StopBleedingTreatment { effectiveness = 0.95f, healRateMultiplier = 1.8f, sterile = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Tourniquet", description = "Tied above a wound on an arm or a leg, it stops the bleeding at once - even a severed artery. The wound still has to be closed.", stackMax = 5, weight = 0.1f, price = 22f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Apply Tourniquet", applySeconds = 1.5f,
                        target = new TreatmentTarget
                        {
                            tags = new List<string> { TagBleeding, TagStump },
                            partKinds = new List<BodyPartKind> { BodyPartKind.Arm, BodyPartKind.Hand, BodyPartKind.Leg, BodyPartKind.Foot },
                        },
                        effects = new List<TreatmentEffect> { new StopBleedingTreatment { effectiveness = 1f, healRateMultiplier = 1f, label = "Tourniquet" } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Suture Kit", description = "Needle and thread: closes deep wounds, severed arteries and stumps so they stop bleeding and can heal.", stackMax = 5, weight = 0.1f, price = 30f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Stitch", applySeconds = 5f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { DeepWound, Laceration, Amputation, Puncture, BiteWound, ArterialBleed } },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Stitched", allowsHealing = true, healRateMultiplier = 1.6f, impairmentReduction = 0.3f, stopsWorsening = true, stopsBleeding = 1f },
                        },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Splint", description = "Holds a cracked or broken bone, or a torn ankle, in place so it can heal, and puts a dislocated joint back. Compound fractures need surgery.", stackMax = 3, weight = 0.4f, price = 20f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Splint", applySeconds = 4f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Fracture, Sprain }, maxSeverity = 2, partKinds = new List<BodyPartKind> { BodyPartKind.Arm, BodyPartKind.Hand, BodyPartKind.Leg, BodyPartKind.Foot } },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Splinted", allowsHealing = true, healRateMultiplier = 1.5f, impairmentReduction = 0.45f, stopsWorsening = true },
                        },
                    },
                    new InjuryTreatment
                    {
                        name = "Reset Joint", applySeconds = 3f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Dislocation }, minSeverity = 2 },
                        effects = new List<TreatmentEffect> { new CureInjuryTreatment { removeCompletely = false, stagesDown = 1 } },
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
                name = "Antibiotics", description = "Fights any infection, sepsis included: it heals over time instead of spreading. Also the cure for pneumonia and severe dysentery.", stackMax = 10, weight = 0.05f, price = 40f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Antibiotics", applySeconds = 1f,
                        target = new TreatmentTarget { tags = new List<string> { TagInfection } },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Antibiotics", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0.3f, stopsWorsening = true } },
                    },
                    new InjuryTreatment
                    {
                        name = "Take Antibiotics", applySeconds = 1f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { CommonCold, Dysentery }, minSeverity = 2 },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Antibiotics", allowsHealing = true, healRateMultiplier = 1.5f, impairmentReduction = 0f, stopsWorsening = true } },
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
                name = "Surgical Kit", description = "Surgery: sets compound fractures, closes arteries, lungs and internal bleeding, mends tendons and eyes, cuts out dead flesh - or amputates - and reattaches a lost limb within its time limit (the limb must be carried).", stackMax = 2, weight = 1f, price = 120f,
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
                        target = new TreatmentTarget
                        {
                            injuryTypes = new List<string>
                            {
                                Fracture, DeepWound, Amputation, Puncture, BiteWound, Frostbite, ArterialBleed, InternalBleeding, PuncturedLung, TendonDamage,
                                EyeInjury, Sprain,
                            },
                            minSeverity = 2,
                        },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Operated", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0.5f, stopsWorsening = true, stopsBleeding = 1f },
                        },
                    },
                    new InjuryTreatment
                    {
                        name = "Cut Out Dead Flesh", applySeconds = 6f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Gangrene }, maxSeverity = 2 },
                        effects = new List<TreatmentEffect>
                        {
                            new TreatInjuryTreatment { label = "Debrided", allowsHealing = true, healRateMultiplier = 1.5f, impairmentReduction = 0.3f, stopsWorsening = true },
                        },
                    },
                    new InjuryTreatment
                    {
                        name = "Amputate", applySeconds = 10f,
                        target = new TreatmentTarget
                        {
                            injuryTypes = new List<string> { Gangrene },
                            partKinds = new List<BodyPartKind> { BodyPartKind.Arm, BodyPartKind.Hand, BodyPartKind.Leg, BodyPartKind.Foot },
                        },
                        effects = new List<TreatmentEffect> { new AmputateLimbTreatment() },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Cold Remedy", description = "Herbs and honey: a cold or a flu passes faster and does not get worse. Pneumonia needs antibiotics.", stackMax = 10, weight = 0.1f, price = 12f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Remedy", applySeconds = 1.5f,
                        target = new TreatmentTarget { tags = new List<string> { TagRespiratory }, maxSeverity = 1 },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Medicated", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0f, stopsWorsening = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Stomach Remedy", description = "Charcoal and salts: settles food poisoning and dysentery so they pass faster and do not get worse.", stackMax = 10, weight = 0.1f, price = 14f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Remedy", applySeconds = 1.5f,
                        target = new TreatmentTarget { tags = new List<string> { TagGut } },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Medicated", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0f, stopsWorsening = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Antiparasitic", description = "Kills intestinal parasites. They never leave by themselves.", stackMax = 5, weight = 0.05f, price = 30f, preventive = true,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Antiparasitic", applySeconds = 1f,
                        target = new TreatmentTarget { tags = new List<string> { TagParasite } },
                        effects = new List<TreatmentEffect> { new CureInjuryTreatment { removeCompletely = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Antitoxin", description = "Stops tetanus: taken after a dirty wound it prevents it; once the spasms start it lets the body recover.", stackMax = 5, weight = 0.05f, price = 45f, preventive = true,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Antitoxin", applySeconds = 1f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Tetanus }, maxSeverity = 0 },
                        effects = new List<TreatmentEffect> { new CureInjuryTreatment { removeCompletely = true } },
                    },
                    new InjuryTreatment
                    {
                        name = "Take Antitoxin", applySeconds = 1f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Tetanus }, minSeverity = 1 },
                        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Antitoxin", allowsHealing = true, healRateMultiplier = 2f, impairmentReduction = 0f, stopsWorsening = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Rabies Vaccine", description = "Taken soon after an animal's bite it prevents rabies. It does nothing once the illness shows.", stackMax = 5, weight = 0.05f, price = 60f, preventive = true,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Vaccine", applySeconds = 1f,
                        target = new TreatmentTarget { injuryTypes = new List<string> { Rabies }, maxSeverity = 0 },
                        effects = new List<TreatmentEffect> { new CureInjuryTreatment { removeCompletely = true } },
                    },
                },
            },
            new MedicalPreset
            {
                name = "Antivenom", description = "Ends a venom in the blood at once.", stackMax = 5, weight = 0.05f, price = 50f,
                treatments = new List<InjuryTreatment>
                {
                    new InjuryTreatment
                    {
                        name = "Take Antivenom", applySeconds = 1f,
                        target = new TreatmentTarget { tags = new List<string> { TagPoison } },
                        effects = new List<TreatmentEffect> { new CureInjuryTreatment { removeCompletely = true } },
                    },
                },
            },
        };
    }

    // ------------------------------------------------------------------ bringing an older asset up to date
    /// <summary>Is <paramref name="s"/> an asset made before illnesses (no Whole Body part, or a built-in new injury type missing)?</summary>
    public static bool NeedsIllnesses(InjurySettings s)
    {
        if (s == null)
            return false;
        if (s.FindWholeBody() == null)
            return true;
        foreach (string id in NewTypeIds)
            if (s.FindType(id) == null)
                return true;
        return false;
    }

    /// <summary>The injury types added with illnesses (what <see cref="ApplyIllnesses"/> adds when missing).</summary>
    public static readonly string[] NewTypeIds =
    {
        Sprain, Dislocation, EyeInjury, EarInjury, ArterialBleed, InternalBleeding, PuncturedLung, TendonDamage, Gangrene,
        Sepsis, Tetanus, Rabies, CommonCold, Dysentery, FoodPoisoning, Parasites, Heatstroke, Venom,
    };

    /// <summary>
    /// Brings an existing settings asset up to the illnesses without touching what was changed by hand: adds the Whole
    /// Body part, the new injuries and illnesses that are missing (each in its default place in the list: the order
    /// matters for hits), and gives the built-in injuries what links them to the new ones where they still have their
    /// old defaults - a severe infection leads to sepsis and may rot (gangrene), frostbite to gangrene, dirty wounds to
    /// tetanus, bites to rabies, a concussion blurs the sight - plus their HUD warnings and icons, and the default
    /// exposures (the cold gives colds). Returns what it did.
    /// </summary>
    public static List<string> ApplyIllnesses(InjurySettings s)
    {
        var done = new List<string>();
        if (s == null)
            return done;
        if (s.bodyParts == null)
            s.bodyParts = new List<InjuryBodyPart>();
        if (s.injuryTypes == null)
            s.injuryTypes = new List<InjuryTypeDefinition>();
        if (s.FindWholeBody() == null)
        {
            s.bodyParts.Add(WholeBodyPart());
            done.Add("Added the Whole Body part (illnesses sit on it)");
        }
        s.InvalidateLookups();

        List<InjuryTypeDefinition> defaults = InjuryTypes();
        // What links the old built-in types to the new ones, only where they still have their old defaults.
        foreach (InjuryTypeDefinition t in s.injuryTypes)
        {
            if (t == null)
                continue;
            InjuryTypeDefinition d = defaults.Find(x => x.id == t.id);
            if (d == null)
                continue;
            if (t.hudAlert == InjuryAlertLevel.None && d.hudAlert != InjuryAlertLevel.None)
            {
                t.hudAlert = d.hudAlert;
                done.Add($"{t.Name}: HUD warning ({d.hudAlert})");
            }
            if (t.iconShape == InjuryIconShape.Auto)
                t.iconShape = d.iconShape;
            if ((t.complications == null || t.complications.Count == 0) && d.complications != null && d.complications.Count > 0)
            {
                t.complications = new List<InjuryComplication>();
                foreach (InjuryComplication c in d.complications)
                    t.complications.Add(new InjuryComplication(c.injuryType, c.when, c.chance, c.fromStage, c.stage));
                done.Add($"{t.Name}: may lead to {string.Join(", ", d.complications.ConvertAll(c => c.injuryType))}");
            }
            if (t.stages == null || d.stages == null)
                continue;
            for (int i = 0; i < t.stages.Count && i < d.stages.Count; i++)
            {
                InjuryStage ts = t.stages[i], ds = d.stages[i];
                if (ts == null || ds == null)
                    continue;
                if (ds.vision != null && ds.vision.HasAny && (ts.vision == null || !ts.vision.HasAny))
                {
                    ts.vision = new VisionEffect(ds.vision.darkness, ds.vision.haze, ds.vision.tunnel, ds.vision.sway);
                    done.Add($"{t.StageName(i)}: changes the sight");
                }
                // A stage that should get worse but has no chance to (a value lost on the way): the default's.
                if (ts.worsenAfterSeconds > 0f && ts.worsenChance <= 0f && ds.worsenChance > 0f)
                {
                    ts.worsenChance = ds.worsenChance;
                    done.Add($"{t.StageName(i)}: worsen chance {ds.worsenChance * 100f:0}%");
                }
                // A severe infection had nothing worse to become: now sepsis.
                if (t.id == Infection && ts.worsenAfterSeconds <= 0f && string.IsNullOrEmpty(ts.worsensInto) && ds.worsensInto == Sepsis)
                {
                    ts.worsenAfterSeconds = ds.worsenAfterSeconds;
                    ts.worsensInto = Sepsis;
                    done.Add($"{t.StageName(i)}: leads to sepsis after {ds.worsenAfterSeconds:0} s");
                }
                // Frostbite rotted as an infection: now the flesh dies (gangrene).
                if (t.id == Frostbite && ts.worsensInto == Infection && ds.worsensInto == Gangrene)
                {
                    ts.worsensInto = Gangrene;
                    done.Add($"{t.StageName(i)}: leads to gangrene");
                }
            }
        }

        for (int i = 0; i < defaults.Count; i++)
        {
            InjuryTypeDefinition d = defaults[i];
            if (s.FindType(d.id) != null)
                continue;
            // Before the first built-in type that comes after it by default (the order of the list matters for hits).
            int at = s.injuryTypes.Count;
            for (int k = i + 1; k < defaults.Count && at == s.injuryTypes.Count; k++)
            {
                int idx = s.injuryTypes.FindIndex(x => x != null && x.id == defaults[k].id);
                if (idx >= 0)
                    at = idx;
            }
            s.injuryTypes.Insert(at, d);
            s.InvalidateLookups();
            done.Add($"Added {d.Name}");
        }

        if (s.illness == null)
            s.illness = new IllnessRules();
        if (s.illness.exposures == null || s.illness.exposures.Count == 0)
        {
            s.illness.exposures = Exposures();
            done.Add("Exposures: the cold gives colds and frostbitten hands and feet, the heat heatstroke");
        }
        if (s.falls == null)
            s.falls = new FallRules();
        s.InvalidateLookups();
        return done;
    }
}
