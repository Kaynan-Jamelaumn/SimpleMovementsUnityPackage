using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A severity level shared by every injury type (Minor, Moderate, Severe, Critical...).</summary>
[Serializable]
public class InjurySeverityLevel
{
    [Tooltip("Name shown in the health panel (\"Severe\").")]
    public string name = "Minor";
    [Tooltip("Colour of the severity in the health panel.")]
    public Color color = new Color(0.95f, 0.8f, 0.3f);

    public InjurySeverityLevel() { }

    public InjurySeverityLevel(string name, Color color)
    {
        this.name = name;
        this.color = color;
    }
}

/// <summary>"From this impairment on, the part stops these actions" (a leg at 50% stops sprinting).</summary>
[Serializable]
public class BodyActionRule
{
    [Tooltip("Actions stopped. Hand On This Side = the main or off hand, by the part's side and the Main Hand Side setting.")]
    public BodyAction actions = BodyAction.Sprint;
    [Tooltip("From this impairment of the part (0 = healthy, 1 = useless or missing). 1 = only when the part is useless or lost.")]
    [Range(0f, 1f)] public float fromImpairment = 0.5f;

    public BodyActionRule() { }

    public BodyActionRule(BodyAction actions, float fromImpairment)
    {
        this.actions = actions;
        this.fromImpairment = fromImpairment;
    }

    public string Describe() => $"{BodyActions.DescribeAll(actions)} from {fromImpairment * 100f:0}% impairment";
}

/// <summary>When and how a body part can be lost, and what the lost limb becomes.</summary>
[Serializable]
public class LimbLossRules
{
    [Tooltip("This part can be cut off (arms, hands, legs, feet). Off for the head and torso.")]
    public bool canBeLost = false;
    [Tooltip("A single hit that removes at least this fraction of the part's max condition cuts it off (0.5 = half its condition " +
             "in one blow). 0 = never by one hit.")]
    [Range(0f, 2f)] public float singleHitThreshold = 0.6f;
    [Tooltip("The hit that brings the part's condition to 0 cuts it off (worn down by many hits).")]
    public bool atZeroCondition = true;
    [Tooltip("Chance (0-1) when one of the conditions above is met.")]
    [Range(0f, 1f)] public float chance = 0.5f;
    [Tooltip("Kinds of damage that can cut it off (Physical = blades, claws, bullets).")]
    public InjuryDamageKinds damageKinds = InjuryDamageKinds.Physical;
    [Tooltip("Area damage (explosions, slams) can cut it off too.")]
    public bool areaDamageCanSever = true;
    [Tooltip("Only weapons of these categories cut it off (empty = any weapon, ability or claw).")]
    public List<WeaponCategory> onlyWeapons = new List<WeaponCategory>();
    [Tooltip("How likely each form of hit cuts it off, on top of Chance: blades sever, blunt blows almost never do (a crushed hand or foot " +
             "now and then). 0 = never from that form.")]
    public DamageFormChances formChances = DefaultSevering();

    /// <summary>The default severing odds by form: slashes 1, bites 0.6, crushing 0.5, claws 0.4, piercing 0.15, blunt 0.1.</summary>
    public static DamageFormChances DefaultSevering() => new DamageFormChances(1f, 1f, 0.15f, 0.1f, 0.6f, 0.4f, 0.5f);

    [Header("The lost limb")]
    [Tooltip("Seconds the lost limb can still be reattached. Negative = the Reattachment rules' time limit.")]
    public float reattachTimeLimit = -1f;
    [Tooltip("Item the lost limb becomes in the world (a Severed Limb item asset: needed in network sessions and saves). " +
             "Empty = one is made at runtime (offline only).")]
    public SeveredLimbItemSO limbItem;
    [Tooltip("Model of the lost limb in the world. Empty = the Limb Item's prefab, else a generated capsule.")]
    public GameObject severedPrefab;
    [Tooltip("Icon of the lost limb in the inventory. Empty = the Limb Item's icon, else a generated one.")]
    public Sprite icon;
    [Tooltip("Injury left on the stump (bleeding until it is treated). Empty = none.")]
    public string stumpInjury = InjuryDefaults.Amputation;
    [Tooltip("Severity index of the stump injury.")]
    [Min(0)] public int stumpSeverity = 3;
    public GameObject vfx;
    public AudioClip sound;
}

/// <summary>
/// One tracked body part (Head, Torso, Left Arm, Right Hand...): its condition, how hits reach it, what impairing or
/// losing it does, what it can lose and what it wears. Lives in <see cref="InjurySettings.bodyParts"/>.
/// </summary>
[Serializable]
public class InjuryBodyPart
{
    [Tooltip("Unique id used by treatments, saves and the network (LeftArm). Change it only before players have saves.")]
    public string id = "Torso";
    [Tooltip("Name shown to the player (\"Left Arm\").")]
    public string displayName = "Torso";
    [Tooltip("Other names of this part: scripts, treatments, abilities and saves that use them still find it (LeftArm for the " +
             "Left Upper Arm, Torso for the Upper Torso).")]
    public List<string> aliases = new List<string>();
    [Tooltip("What it is: arms and hands hold things, legs and feet carry the character (used by filters and the defaults).")]
    public BodyPartKind kind = BodyPartKind.Torso;
    [Tooltip("Which side of the body it is on (left / right parts pick the side a hit came from).")]
    public BodyPartSide side = BodyPartSide.Center;
    [Tooltip("The part's health. Hits remove condition; resting restores it.")]
    [Min(1f)] public float maxCondition = 100f;

    [Header("How hits reach it")]
    [Tooltip("Body Part Profile parts (Head, Torso, Arms, Legs...) whose hits can land on this part. The side of the hit picks " +
             "left or right; the height (and Hit Weight) picks between e.g. an arm and its hand.")]
    public List<string> hitParts = new List<string>();
    [Tooltip("Height band on the body (0 = feet, 1 = top of the head) where hits land on this part when no Body Part Profile part " +
             "says where (characters without a Body Part Controller), and to choose between parts that share a profile part.")]
    public Vector2 heightRange = new Vector2(0.45f, 0.83f);
    [Tooltip("Share of the hits when several parts could be hit (an arm 0.7, its hand 0.3).")]
    [Min(0f)] public float hitWeight = 1f;
    [Tooltip("Condition lost per point of damage of hits on this part (after the global Condition Per Damage).")]
    [Min(0f)] public float conditionDamageMultiplier = 1f;
    [Tooltip("Condition regained per second while the part has no injury (natural recovery).")]
    [Min(0f)] public float conditionRegenPerSecond = 0.25f;

    [Header("Structure")]
    [Tooltip("The part this one is attached to (a hand to its arm, an arm to the torso): losing the parent loses this one too, " +
             "and hits on this part go there while it is lost.")]
    public string attachedTo = "";
    [Tooltip("What happens when its condition reaches 0.")]
    public ZeroConditionRule atZeroCondition = ZeroConditionRule.Nothing;
    [Tooltip("Seconds of stun for Zero Condition = Stun.")]
    [Min(0f)] public float zeroConditionStunSeconds = 2f;

    [Header("While impaired (scaled by the impairment 0-1)")]
    [Tooltip("What an impaired part does to the character, scaled by how impaired it is (a leg: Limping, less move speed; an arm: " +
             "less weapon damage). Its label is the status shown in the character panel.")]
    public StatusConsequence impaired = new StatusConsequence("Injured");
    [Tooltip("What the missing part does instead (empty = Impaired at full strength).")]
    public StatusConsequence lost = new StatusConsequence("");
    [Tooltip("Actions the part stops from some impairment (a leg at 50%: no sprinting; a missing arm: no two-handed weapons).")]
    public List<BodyActionRule> actionRules = new List<BodyActionRule>();

    [Header("Limb loss")]
    public LimbLossRules limbLoss = new LimbLossRules();

    [Header("Equipment")]
    [Tooltip("Armour slots worn on this part: they fall off with the limb (gloves with a hand, boots with a foot).")]
    public List<ArmorSlotType> armorSlots = new List<ArmorSlotType>();

    [Header("Look")]
    [Tooltip("Hide the bone of a Humanoid rig when the part is lost (its children go with it).")]
    public bool useHumanoidBone = false;
    public HumanBodyBones humanoidBone = HumanBodyBones.LeftUpperArm;
    [Tooltip("Bones collapsed when the part is lost, by name (rigs that are not Humanoid).")]
    public List<string> bones = new List<string>();
    [Tooltip("Renderers turned off when the part is lost, by object name (limbs that are separate meshes).")]
    public List<string> renderers = new List<string>();
    [Tooltip("Where the part is drawn on the health panel's body diagram (0-1 inside the diagram, from the bottom-left).")]
    public Rect diagramRect = new Rect(0.38f, 0.45f, 0.24f, 0.3f);

    /// <summary>Is <paramref name="name"/> this part's id, display name or one of its aliases (case-insensitive)?</summary>
    public bool Matches(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string n = name.Trim();
        if (string.Equals(id, n, StringComparison.OrdinalIgnoreCase) || string.Equals(displayName, n, StringComparison.OrdinalIgnoreCase))
            return true;
        if (aliases != null)
            for (int i = 0; i < aliases.Count; i++)
                if (string.Equals(aliases[i], n, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    /// <summary>Is <paramref name="name"/> exactly this part's id or display name (no alias)?</summary>
    public bool IsNamed(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (string.Equals(id, name.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(displayName, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Is <paramref name="profilePart"/> named in this part's Hit Parts (or is it this part's own id) - by its name, not its
    /// aliases? A hit on the Left Forearm hitbox then goes to the Left Forearm, not to every part of the arm.
    /// </summary>
    public bool ReceivesHitsOfExactly(BodyPartDefinition profilePart)
    {
        if (profilePart == null || string.IsNullOrWhiteSpace(profilePart.name))
            return false;
        if (IsNamed(profilePart.name))
            return true;
        if (hitParts != null)
            for (int i = 0; i < hitParts.Count; i++)
                if (string.Equals(hitParts[i], profilePart.name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    /// <summary>Can hits on the Body Part Profile part <paramref name="profilePart"/> land on this part?</summary>
    public bool ReceivesHitsOf(BodyPartDefinition profilePart)
    {
        if (profilePart == null)
            return false;
        if (Matches(profilePart.name))
            return true;
        if (hitParts == null)
            return false;
        for (int i = 0; i < hitParts.Count; i++)
            if (profilePart.Matches(hitParts[i]))
                return true;
        return false;
    }

    public bool InHeight(float h01) => h01 >= Mathf.Min(heightRange.x, heightRange.y) - 1e-4f && h01 <= Mathf.Max(heightRange.x, heightRange.y) + 1e-4f;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    public bool HoldsAHand => kind == BodyPartKind.Arm || kind == BodyPartKind.Hand;

    /// <summary>Part of a leg (thigh, shin) or a foot: it carries the body's weight.</summary>
    public bool CarriesWeight => kind == BodyPartKind.Leg || kind == BodyPartKind.Foot;
}

/// <summary>
/// How the legs change the way the character moves: a limp from a hurt leg or foot, hopping on one leg when the other
/// cannot carry weight, crawling with the arms when no leg can. Also the body's height without legs and the hitboxes of
/// lost parts. Movement speed, the actions allowed, the animator parameters and the collider follow it.
/// </summary>
[Serializable]
public class LocomotionRules
{
    [Tooltip("Legs change how the character moves (limping, hopping, crawling). Off = hurt legs only slow it through their consequences.")]
    public bool enabled = true;

    [Header("Limping")]
    [Tooltip("A leg whose worst part (thigh, shin or foot) is this impaired makes the character limp (0-1).")]
    [Range(0.01f, 1f)] public float limpFromImpairment = 0.2f;
    [Tooltip("From this limp, walking hurts: Painful Walking runs while the character moves.")]
    [Range(0f, 1f)] public float painFromLimp = 0.6f;
    [Tooltip("A missing foot: the character walks on the stump with this limp.")]
    [Range(0f, 1f)] public float missingFootLimp = 0.9f;
    [Tooltip("Speed while walking on a stump (on top of the foot's own consequences).")]
    [Range(0.05f, 1f)] public float missingFootSpeed = 0.75f;
    [Tooltip("Actions stopped while walking on a stump.")]
    public BodyAction missingFootBlocks = BodyAction.Sprint | BodyAction.Dash | BodyAction.Roll;

    [Header("Hopping (one leg cannot carry weight)")]
    [Tooltip("A leg (thigh or shin) this impaired cannot carry weight - a compound fracture - and the character hops on the " +
             "other. 1 = only a missing leg.")]
    [Range(0.5f, 1f)] public float legUselessFromImpairment = 0.95f;
    [Tooltip("Average speed while hopping, compared to walking.")]
    [Range(0.05f, 1f)] public float hopSpeed = 0.45f;
    [Tooltip("Seconds of one hop.")]
    [Min(0.1f)] public float hopSeconds = 0.55f;
    [Tooltip("Share of each hop spent moving; the rest is the landing, standing still (inefficient).")]
    [Range(0.1f, 1f)] public float hopMoveShare = 0.6f;
    [Tooltip("Actions stopped while hopping.")]
    public BodyAction hopBlocks = BodyAction.Sprint | BodyAction.Jump | BodyAction.Dash | BodyAction.Roll | BodyAction.Crouch;

    [Header("Crawling (no leg can carry weight)")]
    [Tooltip("Speed while dragging the body with the arms, compared to walking (very inefficient).")]
    [Range(0.02f, 1f)] public float crawlSpeed = 0.2f;
    [Tooltip("Actions stopped while crawling.")]
    public BodyAction crawlBlocks = BodyAction.Sprint | BodyAction.Jump | BodyAction.Dash | BodyAction.Roll | BodyAction.Crouch;

    [Header("Strain while moving")]
    [Tooltip("Runs while the character moves with a painful limp or on a stump.")]
    public StatusConsequence painfulWalking = new StatusConsequence("Painful Walking") { staminaPerSecond = -0.4f, sanityPerSecond = -0.02f };
    [Tooltip("Runs while the character hops.")]
    public StatusConsequence hopping = new StatusConsequence("Hopping") { staminaPerSecond = -1.5f };
    [Tooltip("Runs while the character crawls.")]
    public StatusConsequence crawling = new StatusConsequence("Crawling") { staminaPerSecond = -2f };

    [Header("Body without legs")]
    [Tooltip("Height of the character's collider with no legs, compared to standing (0.55 = from the waist up): it touches " +
             "the ground with its waist and fits under lower obstacles.")]
    [Range(0.2f, 1f)] public float leglessHeight = 0.55f;
    [Tooltip("Lower the model with no legs so the waist rests on the ground. Turn it off when your crawl animations already " +
             "put the hips on the ground.")]
    public bool lowerModelWhenLegless = true;
    [Tooltip("How far the model is lowered with no legs, as a share of the body's height. Negative = the height of a Humanoid " +
             "rig's hips above its feet (measured when the legs are lost).")]
    public float leglessModelDrop = -1f;

    [Header("Hitboxes")]
    [Tooltip("Turn off the Body Part Hitbox colliders of lost parts until they are reattached: hits pass where the limb was.")]
    public bool disableLostHitboxes = true;
}

/// <summary>Removes lost limbs and the items that fell with them from the world after a while, so they do not pile up.</summary>
[Serializable]
public class LimbCleanupRules
{
    [Tooltip("Seconds a lost limb lies in the world before it is removed (one carried in an inventory stays). Negative = its " +
             "reattachment time limit plus 2 minutes; 0 = never.")]
    public float limbSeconds = -1f;
    [Tooltip("Seconds the armour and the items that fell with a limb lie in the world before they are removed. 0 = never.")]
    [Min(0f)] public float droppedItemSeconds = 600f;
}

/// <summary>What a hit must be to cause an injury of a type.</summary>
[Serializable]
public class InjuryTrigger
{
    [Tooltip("Chance (0-1) per qualifying hit (multiplied by Injury Chance Multiplier).")]
    [Range(0f, 1f)] public float chance = 0.5f;
    [Tooltip("Only hits that remove at least this fraction of the part's max condition (0.05 = a light hit, 0.3 = a heavy one).")]
    [Range(0f, 2f)] public float minHitFraction = 0.05f;
    [Tooltip("Kinds of damage that cause it.")]
    public InjuryDamageKinds damageKinds = InjuryDamageKinds.Physical;
    [Tooltip("How the hit must arrive (melee, projectile, area).")]
    public InjuryDeliveries deliveries = InjuryDeliveries.All;
    [Tooltip("Only hits of these elements (Fire for burns). Empty = any element, including none.")]
    public List<ElementType> elements = new List<ElementType>();
    [Tooltip("Only hits of weapons of these categories (Hammer and Mace for broken bones). Empty = anything.")]
    public List<WeaponCategory> weapons = new List<WeaponCategory>();
    [Tooltip("Only on these kinds of parts. Empty = every part.")]
    public List<BodyPartKind> partKinds = new List<BodyPartKind>();
    [Tooltip("Only on critical hits.")]
    public bool criticalOnly = false;

    [Header("By damage form")]
    [Tooltip("How likely each form of hit causes it, on top of Chance: a slash is far more likely to cut than a mace (Slash ×1.5, " +
             "Blunt ×0.15), a mace far more likely to break a bone. 0 = never from that form. Forms: Slash (swords, axes), Pierce " +
             "(spears, stabs, arrows), Blunt (maces, fists, pushes), Bite, Claw, Crush (stomps, falls); Formless = magic and elements.")]
    public DamageFormChances formChances = new DamageFormChances();
    [Tooltip("How hard each form of hit counts for it (multiplies the hit's fraction for Min Hit Fraction and the stage): a crushing blow " +
             "breaks a bone worse than a slash of the same damage. 1 = as hard as its damage.")]
    public DamageFormChances formStrength = new DamageFormChances();

    /// <summary>The chance of a matching hit of <paramref name="form"/> (Chance × the form's multiplier), before the global multiplier.</summary>
    public float ChanceFor(DamageForm form) => chance * (formChances != null ? formChances.For(form) : 1f);

    /// <summary>How hard a hit of <paramref name="form"/> counts (1 = its damage).</summary>
    public float StrengthFor(DamageForm form) => formStrength != null ? formStrength.For(form) : 1f;

    /// <summary>Does the hit match the filters (not the chance)?</summary>
    public bool Matches(in DamageInfo info, InjuryBodyPart part, float hitFraction)
    {
        if (hitFraction + 1e-5f < minHitFraction)
            return false;
        if ((damageKinds & BodyActions.KindOf(info.type)) == 0)
            return false;
        if ((deliveries & BodyActions.DeliveryOf(info.delivery)) == 0)
            return false;
        if (criticalOnly && !info.isCritical)
            return false;
        if (elements != null && elements.Count > 0 && !elements.Contains(info.element))
            return false;
        if (weapons != null && weapons.Count > 0 && (info.weapon == null || !weapons.Contains(info.weapon.Category)))
            return false;
        if (partKinds != null && partKinds.Count > 0 && (part == null || !partKinds.Contains(part.kind)))
            return false;
        return true;
    }
}

/// <summary>
/// One severity stage of an injury type: how bad a hit must be for it, how it bleeds, impairs and heals, how it gets
/// worse untreated, and what it does to the character.
/// </summary>
[Serializable]
public class InjuryStage
{
    [Tooltip("Name of this stage (\"Broken\", \"Compound\"). Empty = the type's name.")]
    public string name = "";
    [Tooltip("Severity level (index in the Severity Levels list: 0 Minor, 1 Moderate, 2 Severe, 3 Critical by default).")]
    [Min(0)] public int severity = 0;
    [Tooltip("A hit removing at least this fraction of the part's max condition creates this stage (the strongest stage the hit " +
             "reaches is chosen).")]
    [Range(0f, 2f)] public float minHitFraction = 0f;

    [Header("Bleeding")]
    [Tooltip("Health lost per second while it bleeds (0 = does not bleed).")]
    [Min(0f)] public float bleedingPerSecond = 0f;
    [Tooltip("Seconds after which it stops bleeding by itself (0 = never: it must be treated).")]
    [Min(0f)] public float clotsAfterSeconds = 0f;

    [Header("Body part")]
    [Tooltip("How much it impairs the part (0 = not at all, 1 = the part is useless). The part's worst injury counts.")]
    [Range(0f, 1f)] public float impairment = 0f;
    [Tooltip("Condition the part loses per second while the injury lasts (infections, untreated wounds).")]
    [Min(0f)] public float conditionLossPerSecond = 0f;

    [Header("Healing")]
    [Tooltip("Seconds it takes to heal once treated (or untreated at the Untreated Heal Rate).")]
    [Min(1f)] public float healSeconds = 300f;
    [Tooltip("It never heals until a treatment says it may (a broken bone needs a splint, a deep wound stitches).")]
    public bool needsTreatmentToHeal = false;
    [Tooltip("Healing speed while untreated (0.5 = half as fast). Ignored when it needs treatment to heal.")]
    [Range(0f, 2f)] public float untreatedHealRate = 0.5f;

    [Header("Getting worse (while untreated)")]
    [Tooltip("Seconds untreated before it gets worse (0 = never). Bandages and treatments that stop worsening stop it.")]
    [Min(0f)] public float worsenAfterSeconds = 0f;
    [Tooltip("Injury type it becomes (Infection). Empty = the next stage of the same type.")]
    public string worsensInto = "";

    [Header("Effects")]
    [Tooltip("What it does to the character while it lasts (stats, stamina or health per second, faint chance...). Leave the label " +
             "empty for no status line.")]
    public StatusConsequence consequence = new StatusConsequence("");
    [Tooltip("Actions it stops while it lasts (on its part's side for Hand On This Side).")]
    public BodyAction blocks = BodyAction.None;
}

/// <summary>
/// A kind of injury (Scratch, Laceration, Deep Wound, Fracture, Burn, Infection...): which hits cause it, its stages of
/// severity and what each does. Treatments find the injuries they treat by id or tag.
/// </summary>
[Serializable]
public class InjuryTypeDefinition
{
    [Tooltip("Unique id (treatments and progression refer to it).")]
    public string id = "Scratch";
    [Tooltip("Name shown to the player.")]
    public string displayName = "Scratch";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Tags treatments can target (wound, bleeding, fracture, burn, infection...).")]
    public List<string> tags = new List<string>();
    [Tooltip("Colour of the injury in the health panel.")]
    public Color color = new Color(0.9f, 0.35f, 0.3f);
    public Sprite icon;

    [Header("Caused by hits")]
    [Tooltip("Hits can cause it (off for injuries that only come from others: infections, stumps, reattachments).")]
    public bool causedByHits = true;
    public InjuryTrigger trigger = new InjuryTrigger();

    [Header("Stages (one per severity)")]
    [Tooltip("The stages from mildest to worst. A hit creates the strongest stage it reaches; untreated injuries can move up.")]
    public List<InjuryStage> stages = new List<InjuryStage>();

    [Tooltip("Healing a stage drops the injury one stage instead of removing it (a broken bone becomes cracked, then heals).")]
    public bool healStageByStage = false;

    [Header("Stacking")]
    [Tooltip("A new injury of this type on a part that already has one makes the existing one worse (if stronger) instead of " +
             "adding a second.")]
    public bool mergeIntoExisting = true;
    [Tooltip("Most injuries of this type on one part.")]
    [Min(1)] public int maxPerPart = 2;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    /// <summary>The name of a stage ("Broken Bone"): its own name, else the type's.</summary>
    public string StageName(int index)
    {
        InjuryStage s = Stage(index);
        return s != null && !string.IsNullOrWhiteSpace(s.name) ? s.name : Name;
    }

    /// <summary>The stage of <paramref name="severity"/> (the closest one below it, else the mildest).</summary>
    public int StageForSeverity(int severity)
    {
        if (stages == null || stages.Count == 0)
            return 0;
        int best = 0;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && stages[i].severity <= severity)
                best = i;
        return best;
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tags == null)
            return false;
        for (int i = 0; i < tags.Count; i++)
            if (string.Equals(tags[i], tag.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public InjuryStage Stage(int index) => stages != null && stages.Count > 0 ? stages[Mathf.Clamp(index, 0, stages.Count - 1)] : null;

    /// <summary>The strongest stage a hit of <paramref name="hitFraction"/> reaches (-1 = none).</summary>
    public int StageForHit(float hitFraction)
    {
        int best = -1;
        if (stages == null)
            return best;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && hitFraction + 1e-5f >= stages[i].minHitFraction)
                best = i;
        return best;
    }
}

/// <summary>Bleeding of the whole character (every bleeding injury together).</summary>
[Serializable]
public class BleedingRules
{
    [Tooltip("Multiplies every injury's bleeding.")]
    [Min(0f)] public float multiplier = 1f;
    [Tooltip("Most health lost per second from bleeding, all injuries together (0 = no cap).")]
    [Min(0f)] public float maxPerSecond = 4f;
    [Tooltip("Bleeding can bring health to 0 (unconsciousness and death rules apply). Off = it stops at 1 health.")]
    public bool canKill = true;
    [Tooltip("Bleeding this much per second counts as heavy bleeding (strength 1 of the Bleeding status).")]
    [Min(0.01f)] public float heavyBleedingPerSecond = 1f;
    [Tooltip("What bleeding does to the character, scaled by how much it bleeds (label = the status line).")]
    public StatusConsequence status = new StatusConsequence("Bleeding");
    [Tooltip("Blood drops spawned while bleeding (optional), every Drip Interval seconds.")]
    public GameObject dripVfx;
    [Min(0.1f)] public float dripInterval = 1.5f;
}

/// <summary>How lost limbs can be reattached.</summary>
[Serializable]
public class ReattachmentRules
{
    [Tooltip("Seconds after the loss during which the limb can be reattached with ordinary items and treatments. After it, only " +
             "abilities and treatments that ignore the time limit can.")]
    [Min(1f)] public float timeLimitSeconds = 300f;
    [Tooltip("Condition the lost limb loses per second while it is off the body (fraction of its max: 0.001 = 0.1% per second).")]
    [Range(0f, 0.05f)] public float limbDecayPerSecond = 0.0015f;
    [Tooltip("Least condition (fraction) a limb needs to be reattached (abilities may ignore it).")]
    [Range(0f, 1f)] public float minLimbCondition = 0.1f;
    [Tooltip("The reattached part gets this fraction of the limb's condition back.")]
    [Range(0f, 1f)] public float restoredCondition = 0.6f;
    [Tooltip("Injury added to a reattached part while it knits back (it heals with time). Empty = none.")]
    public string afterInjury = InjuryDefaults.Reattached;
    [Min(0)] public int afterSeverity = 2;
    [Tooltip("Only the character's own limbs can be reattached to it.")]
    public bool onlyOwnLimb = true;
    [Tooltip("Items and treatments need the limb in the inventory. Off = a limb lying within Ground Range counts too.")]
    public bool limbMustBeCarried = true;
    [Tooltip("Metres: limbs lying this close count for abilities (and items when Limb Must Be Carried is off).")]
    [Min(0f)] public float groundRange = 3f;
    [Tooltip("A respawned character gets every lost limb back (its limbs left in the world rot).")]
    public bool restoreLimbsOnRespawn = true;
    [Tooltip("Limbs past their time limit are renamed \"Rotten ...\" (they stay in the world / inventory as a reminder).")]
    public bool expiredLimbsRot = true;
}

/// <summary>
/// City treatment (healers, hospitals). PLACEHOLDER: the settings exist so city healers can be added later without
/// changing the injury system; nothing uses them yet (see <see cref="CityInjuryTreatment"/>).
/// </summary>
[Serializable]
public class CityTreatmentSettings
{
    [Tooltip("Placeholder: city healers are not implemented yet. Turning it on only lists the option in the health panel.")]
    public bool enabled = false;
    [Tooltip("Planned: price per severity level of each injury treated.")]
    [Min(0f)] public float pricePerSeverity = 25f;
    [Tooltip("Planned: price to reattach a limb.")]
    [Min(0f)] public float reattachPrice = 250f;
    [Tooltip("Planned: city surgeons can reattach limbs past their time limit.")]
    public bool canReattachExpiredLimbs = true;
    [Tooltip("Shown in the health panel while city treatment is a placeholder.")]
    public string placeholderNote = "City healers are coming in a future update.";
}

/// <summary>
/// The injury system's project settings (Project-Zomboid-like body damage): whether it is on, the tracked body parts,
/// severity levels, injury types and their stages, bleeding, limb loss and reattachment, treatments and the (placeholder)
/// city treatment. Optional: Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Injury Settings puts one at
/// Resources/SimpleMovements/Settings/InjurySettings - without one the system is OFF.
/// <para>Characters get an <see cref="InjuryController"/> automatically while it is on (players, and mobs when Mobs is on).</para>
/// </summary>
[CreateAssetMenu(fileName = "InjurySettings", menuName = "SimpleMovements/Combat/Injury Settings", order = 5)]
public class InjurySettings : ScriptableObject
{
    public const string ResourcesName = "InjurySettings";

    [Tooltip("Turns the whole injury system on. Off: no body-part condition, injuries, bleeding, limb loss or health panel.")]
    public bool enabled = true;
    [Tooltip("Players get injuries.")]
    public bool players = true;
    [Tooltip("Mobs get injuries too (bleeding, limping, losing limbs). Usually off: it costs a little per mob.")]
    public bool mobs = false;

    [Header("General")]
    [Tooltip("The side of the main hand (the hotbar item). The other side holds the off hand (shields, a second weapon).")]
    public BodyPartSide mainHandSide = BodyPartSide.Right;
    [Tooltip("Body-part condition lost per point of damage (before each part's own multiplier).")]
    [Min(0f)] public float conditionPerDamage = 1f;
    [Tooltip("Multiplies the chance of every injury from hits.")]
    [Min(0f)] public float injuryChanceMultiplier = 1f;
    [Tooltip("Most new injuries one hit can cause.")]
    [Range(1, 4)] public int maxInjuriesPerHit = 1;
    [Tooltip("Multiplies every injury's healing speed.")]
    [Min(0f)] public float healingSpeedMultiplier = 1f;
    [Tooltip("A respawned character starts healthy (no injuries, full condition).")]
    public bool clearOnRespawn = true;
    [Tooltip("Show the Health page in the inventory's character panel.")]
    public bool showHealthPanel = true;
    [Tooltip("Show a small warning on the screen while bleeding or missing a limb (outside the inventory).")]
    public bool showHudAlert = true;

    [Header("Treatment")]
    [Tooltip("Multiplies the time treatments take to apply (0.5 = twice as fast).")]
    [Min(0f)] public float treatmentTimeMultiplier = 1f;
    [Tooltip("Taking damage interrupts a treatment being applied (the item is not used).")]
    public bool damageInterruptsTreatment = true;
    [Tooltip("Treatments with items need at least one usable hand.")]
    public bool treatmentNeedsAHand = true;

    [Header("Body")]
    [Tooltip("Severity levels shared by every injury type (stages refer to them by index).")]
    public List<InjurySeverityLevel> severityLevels = new List<InjurySeverityLevel>();
    [Tooltip("The tracked body parts.")]
    public List<InjuryBodyPart> bodyParts = new List<InjuryBodyPart>();
    [Tooltip("Part hits go to when no other part is found (usually the Upper Torso).")]
    public string defaultPart = "UpperTorso";

    [Header("Injuries")]
    [Tooltip("Every kind of injury. Order matters for hits: types earlier in the list are tried first.")]
    public List<InjuryTypeDefinition> injuryTypes = new List<InjuryTypeDefinition>();
    public BleedingRules bleeding = new BleedingRules();

    [Header("Limbs")]
    public ReattachmentRules reattachment = new ReattachmentRules();
    public LimbCleanupRules cleanup = new LimbCleanupRules();

    [Header("Movement")]
    public LocomotionRules locomotion = new LocomotionRules();

    [Header("City treatment (placeholder)")]
    public CityTreatmentSettings cityTreatment = new CityTreatmentSettings();

    [Header("Debug")]
    public bool logInjuries = false;

    // ------------------------------------------------------------------ instance
    private static InjurySettings instance;
    private static bool searched;

    /// <summary>
    /// The settings (Resources/SimpleMovements/Settings/InjurySettings), or built-in defaults with the system OFF when
    /// the project has none.
    /// </summary>
    public static InjurySettings Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<InjurySettings>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<InjurySettings>();
                    instance.name = "InjurySettings (defaults, off)";
                    instance.hideFlags = HideFlags.DontSave;
                    InjuryDefaults.Fill(instance);
                    instance.enabled = false;
                }
            }
            return instance;
        }
    }

    /// <summary>Is the injury system on in this project (an Injury Settings asset with Enabled)?</summary>
    public static bool IsEnabled => Instance.enabled;

    /// <summary>Uses a specific settings asset (tests, bootstrap scripts). Null = find it again.</summary>
    public static void Override(InjurySettings settings)
    {
        instance = settings;
        searched = settings != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        searched = false;
    }

    private void Reset() => InjuryDefaults.Fill(this);

    // ------------------------------------------------------------------ lookups
    public InjuryBodyPart FindPart(string partId)
    {
        if (bodyParts == null || string.IsNullOrWhiteSpace(partId))
            return null;
        for (int i = 0; i < bodyParts.Count; i++)
            if (bodyParts[i] != null && bodyParts[i].Matches(partId))
                return bodyParts[i];
        return null;
    }

    public int IndexOfPart(InjuryBodyPart part) => bodyParts != null ? bodyParts.IndexOf(part) : -1;

    public InjuryTypeDefinition FindType(string typeId)
    {
        if (injuryTypes == null || string.IsNullOrWhiteSpace(typeId))
            return null;
        string t = typeId.Trim();
        for (int i = 0; i < injuryTypes.Count; i++)
        {
            InjuryTypeDefinition d = injuryTypes[i];
            if (d != null && (string.Equals(d.id, t, StringComparison.OrdinalIgnoreCase) || string.Equals(d.displayName, t, StringComparison.OrdinalIgnoreCase)))
                return d;
        }
        return null;
    }

    public InjurySeverityLevel Severity(int index)
    {
        if (severityLevels == null || severityLevels.Count == 0)
            return new InjurySeverityLevel("Severity " + index, Color.white);
        return severityLevels[Mathf.Clamp(index, 0, severityLevels.Count - 1)];
    }

    public string SeverityName(int index) => Severity(index).name;

    public float TimeLimitFor(InjuryBodyPart part) =>
        part != null && part.limbLoss != null && part.limbLoss.reattachTimeLimit >= 0f ? part.limbLoss.reattachTimeLimit : reattachment.timeLimitSeconds;

    /// <summary>Seconds the lost <paramref name="part"/> lies in the world before it is removed (0 = never).</summary>
    public float LimbDespawnSecondsFor(InjuryBodyPart part)
    {
        if (cleanup == null)
            return 0f;
        if (cleanup.limbSeconds >= 0f)
            return cleanup.limbSeconds;
        float limit = TimeLimitFor(part);
        return limit > 0f ? limit + 120f : 0f;
    }

    /// <summary>Parts attached to <paramref name="part"/> (its hand), and theirs, recursively.</summary>
    public void CollectAttached(InjuryBodyPart part, List<InjuryBodyPart> into)
    {
        if (part == null || bodyParts == null)
            return;
        for (int i = 0; i < bodyParts.Count; i++)
        {
            InjuryBodyPart p = bodyParts[i];
            if (p == null || p == part || into.Contains(p) || string.IsNullOrWhiteSpace(p.attachedTo) || !part.Matches(p.attachedTo))
                continue;
            into.Add(p);
            CollectAttached(p, into);
        }
    }

    // ------------------------------------------------------------------ validation
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (severityLevels == null || severityLevels.Count == 0)
            errors.Add("No severity levels: add at least one (Minor).");
        if (bodyParts == null || bodyParts.Count == 0)
        {
            errors.Add("No body parts: nothing can be injured.");
            return;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bodyParts.Count; i++)
        {
            InjuryBodyPart p = bodyParts[i];
            if (p == null) { warnings.Add($"Body part #{i + 1} is empty."); continue; }
            if (string.IsNullOrWhiteSpace(p.id)) errors.Add($"Body part #{i + 1} has no id.");
            else if (!ids.Add(p.id.Trim())) errors.Add($"Two body parts have the id '{p.id}'.");
            if (!string.IsNullOrWhiteSpace(p.attachedTo) && FindPart(p.attachedTo) == null)
                errors.Add($"{p.Name}: Attached To '{p.attachedTo}' is not a body part.");
            if (!string.IsNullOrWhiteSpace(p.attachedTo) && p.Matches(p.attachedTo))
                errors.Add($"{p.Name} is attached to itself.");
            if ((p.hitParts == null || p.hitParts.Count == 0) && p.heightRange.x == p.heightRange.y)
                warnings.Add($"{p.Name}: no Hit Parts and an empty Height Range: hits never land on it.");
            if (p.limbLoss != null && p.limbLoss.canBeLost)
            {
                if (!p.limbLoss.atZeroCondition && p.limbLoss.singleHitThreshold <= 0f)
                    warnings.Add($"{p.Name}: can be lost, but neither a single hit nor zero condition loses it.");
                if (!string.IsNullOrWhiteSpace(p.limbLoss.stumpInjury) && FindType(p.limbLoss.stumpInjury) == null)
                    warnings.Add($"{p.Name}: stump injury '{p.limbLoss.stumpInjury}' is not an injury type.");
            }
            if (p.atZeroCondition == ZeroConditionRule.LoseLimb && (p.limbLoss == null || !p.limbLoss.canBeLost))
                warnings.Add($"{p.Name}: At Zero Condition is Lose Limb, but the part cannot be lost.");
        }
        if (FindPart(defaultPart) == null)
            warnings.Add($"Default Part '{defaultPart}' is not a body part (the first part is used).");

        var typeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (injuryTypes != null)
        {
            for (int i = 0; i < injuryTypes.Count; i++)
            {
                InjuryTypeDefinition t = injuryTypes[i];
                if (t == null) { warnings.Add($"Injury type #{i + 1} is empty."); continue; }
                if (string.IsNullOrWhiteSpace(t.id)) errors.Add($"Injury type #{i + 1} has no id.");
                else if (!typeIds.Add(t.id.Trim())) errors.Add($"Two injury types have the id '{t.id}'.");
                if (t.stages == null || t.stages.Count == 0) { errors.Add($"{t.Name}: no stages."); continue; }
                for (int s = 0; s < t.stages.Count; s++)
                {
                    InjuryStage st = t.stages[s];
                    if (st == null) continue;
                    if (severityLevels != null && (st.severity < 0 || st.severity >= severityLevels.Count))
                        warnings.Add($"{t.Name} stage {s + 1}: severity {st.severity} is not a severity level.");
                    if (!string.IsNullOrWhiteSpace(st.worsensInto) && FindType(st.worsensInto) == null)
                        errors.Add($"{t.Name} stage {s + 1}: Worsens Into '{st.worsensInto}' is not an injury type.");
                    if (s > 0 && t.stages[s - 1] != null && st.minHitFraction < t.stages[s - 1].minHitFraction)
                        warnings.Add($"{t.Name}: stage {s + 1} needs a weaker hit than stage {s}: stages should go from mildest to worst.");
                }
                if (t.causedByHits && t.trigger != null && t.trigger.chance <= 0f)
                    warnings.Add($"{t.Name}: caused by hits with 0% chance (never happens).");
                if (t.causedByHits && t.trigger != null && t.trigger.formChances != null && t.trigger.formChances.IsZero)
                    warnings.Add($"{t.Name}: every damage form has odds 0 (By Damage Form): hits never cause it.");
            }
        }
        if (!string.IsNullOrWhiteSpace(reattachment.afterInjury) && FindType(reattachment.afterInjury) == null)
            warnings.Add($"Reattachment: After Injury '{reattachment.afterInjury}' is not an injury type.");
    }
}
