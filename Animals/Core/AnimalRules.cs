using System;
using UnityEngine;

/// <summary>
/// The rules of animal husbandry: time and simulation budget, what happens while an owner is away, nutrition, feeding by
/// hand, limits, history, boarding and saving; and (Phase 2) care, the habitat's cleanliness, comfort, welfare, health,
/// the veterinarian and the remains; and (Phase 3) production. Held by the <c>AnimalDatabase</c>; every number is a setting, so the system is
/// balanced without touching code.
/// </summary>
[Serializable]
public class AnimalRules
{
    [Header("Time")]
    [Tooltip("Multiplies growth speed (2 = animals grow up twice as fast). Hunger and thirst are not changed by it.")]
    [Min(0.01f)] public float growthSpeed = 1f;
    [Tooltip("Game hours between two updates of a pen (its animals live in lockstep). Longer gaps are lived in steps.")]
    [Range(0.25f, 12f)] public float tickHours = 1f;
    [Tooltip("Longest step of the simulation, game hours: a day away is lived as this many steps (hunger, meals, thirst and harm stay right).")]
    [Range(0.1f, 3f)] public float stepHours = 1f;
    [Tooltip("Most animal-steps worked out in one frame, over every pen: a long catch-up is spread over frames.")]
    [Range(1, 20000)] public int stepsPerFrame = 400;
    [Tooltip("Real milliseconds a frame for the simulation (a catch-up after a long absence stops at it and goes on next frame).")]
    [Range(0.25f, 20f)] public float millisPerFrame = 2f;

    [Header("Owners away")]
    [Tooltip("While its owner is logged out an animal's hunger and thirst grow at this share of their pace: an evening offline does not starve a herd.")]
    [Range(0f, 1f)] public float awayNeedsRate = 0.35f;
    [Tooltip("While its owner is logged out, hunger, thirst, poor food and toxins never take an animal's health below this (old age still does). 0 = no protection.")]
    [Range(0f, 100f)] public float awayHealthFloor = 25f;

    [Header("Nutrition")]
    [Tooltip("Game hours over which nutrition is judged: what it ate over about this long, not its last meal, makes its condition.")]
    [Range(6f, 480f)] public float nutritionWindowHours = 48f;
    [Tooltip("How much too much of a nutrient hurts (0 = not at all; 0.35: twice the need loses a third of that nutrient's score).")]
    [Range(0f, 1f)] public float excessPenalty = 0.35f;
    [Tooltip("Nutritional condition below which an animal is malnourished (health suffers, growth slows).")]
    [Range(0f, 100f)] public float malnourishedBelow = 35f;
    [Tooltip("Nutritional condition at and above which it heals and grows at full pace.")]
    [Range(0f, 100f)] public float wellNourishedAbove = 75f;
    [Tooltip("Growth pace of a badly nourished animal (a well nourished one grows at 1).")]
    [Range(0f, 1f)] public float poorGrowth = 0.15f;
    [Tooltip("A favourite food is absorbed this much better.")]
    [Range(1f, 1.5f)] public float favoriteAbsorption = 1.1f;
    [Tooltip("A poor food is absorbed only this much.")]
    [Range(0.1f, 1f)] public float poorAbsorption = 0.55f;

    [Header("Eating")]
    [Tooltip("An animal eats harmful food from a feeder only when its satiety is below this (and nothing better is there).")]
    [Range(0f, 100f)] public float harmfulBelow = 35f;
    [Tooltip("It eats toxic food only when its satiety is below this (starving, nothing better there).")]
    [Range(0f, 100f)] public float toxicBelow = 12f;
    [Tooltip("It eats until its satiety reaches this.")]
    [Range(50f, 100f)] public float fullAt = 96f;
    [Tooltip("Digestive upset (0-100) of one portion of harmful food before the species' own irritation.")]
    [Min(0f)] public float harmfulUpset = 18f;
    [Tooltip("Toxin dose of one portion of a food that is toxic to the species (on top of the food's own toxin).")]
    [Min(0f)] public float toxicDose = 6f;
    [Tooltip("From a hand, an animal takes a harmful food only below this satiety (it trusts a hand more than a trough).")]
    [Range(0f, 100f)] public float harmfulFromHandBelow = 75f;
    [Tooltip("From a hand, it takes a toxic food only below this satiety.")]
    [Range(0f, 100f)] public float toxicFromHandBelow = 45f;
    [Tooltip("Share of the upset that fades per game hour.")]
    [Range(0f, 1f)] public float upsetRecovery = 0.06f;

    [Header("Mood")]
    [Tooltip("Happiness a portion of a favourite food gives (at most twice a game hour).")]
    [Range(0f, 30f)] public float favoriteHappiness = 4f;
    [Tooltip("Game hours for happiness and stress to settle where the animal's life puts them.")]
    [Range(1f, 96f)] public float moodHours = 12f;
    [Tooltip("Affinity with its owner a hand-given meal adds, within Affinity Per Day (it never replaces care: a fond animal can still be hungry).")]
    [Range(0f, 10f)] public float handFeedAffinity = 0.8f;

    [Header("Feeding by hand")]
    [Tooltip("How far from the player the hands reach, metres (from the pen's fence when feeding the pen's feeder).")]
    [Range(0.5f, 8f)] public float workRange = 2.6f;
    [Tooltip("Game hours an animal waits after being fed by hand before it takes food from a hand again.")]
    [Range(0f, 12f)] public float handFeedCooldownHours = 0.5f;
    [Tooltip("Most portions an average animal takes from a hand at once (a cow takes more, a hen less, by their size).")]
    [Range(1, 50)] public int maxHandPortions = 6;

    [Header("Limits")]
    [Tooltip("Most living animals one player may own in a world.")]
    [Range(1, 2000)] public int maxAnimalsPerPlayer = 60;
    [Tooltip("Longest name an animal may be given.")]
    [Range(4, 40)] public int maxNameLength = 24;

    [Header("History")]
    [Tooltip("Events kept per animal.")]
    [Range(4, 200)] public int keepEvents = 32;
    [Tooltip("Game days of care history kept per animal (the quality of its products is judged on them, Phase 3).")]
    [Range(2, 60)] public int keepDays = 14;
    [Tooltip("Hand-given meals kept per animal.")]
    [Range(2, 100)] public int keepFeedings = 16;
    [Tooltip("Real hours a done request is remembered (a retry within it is answered, never done again).")]
    [Range(0.1f, 168f)] public float rememberRequestsHours = 24f;

    [Header("Boarding")]
    [Tooltip("An animal whose owner can no longer reach its pen (the lease ended) is boarded: kept fed on a plain diet until brought home.")]
    public bool boardWhenUnreachable = true;
    [Tooltip("The nutritional value (0-1) of the boarding stable's plain diet.")]
    [Range(0f, 1f)] public float boardingDiet = 0.8f;
    [Tooltip("Happiness a boarded animal settles at (it misses its home).")]
    [Range(0f, 100f)] public float boardingHappiness = 35f;

    [Header("Bodies")]
    [Tooltip("Metres from a pen within which its animals are shown (bodies made), and beyond which they are put away.")]
    [Min(5f)] public float showRadius = 70f;
    [Min(5f)] public float hideRadius = 90f;

    [Header("Saving")]
    [Tooltip("The animals of a world are kept on disk where the rules run (offline, the server).")]
    public bool saveAnimals = true;
    [Tooltip("Real seconds between two saves while something changed (a trade - a release, a move - is saved at once).")]
    [Range(2f, 600f)] public float autosaveSeconds = 30f;

    // ================================================================ Phase 2: care, welfare and health
    [Header("Care")]
    [Tooltip("How well a worn-out tool (no uses left) still does its work, flat (0.4: a poor job; the player is told).")]
    [Range(0f, 1f)] public float wornToolFactor = 0.4f;
    [Tooltip("Game hours before an animal can be looked over again (Examine).")]
    [Range(0f, 24f)] public float examineCooldownHours = 0.25f;
    [Tooltip("Entries of care history kept per animal.")]
    [Range(4, 200)] public int keepCareLog = 24;
    [Tooltip("Most affinity with its owner care, medicine and meals from its hand together give an animal in one game day (a bond is not bought by petting or feeding it all day).")]
    [Range(0f, 100f)] public float affinityPerDay = 8f;

    [Header("Habitat")]
    [Tooltip("Dirt (0-100) the floor of a fully stocked pen gains per game hour (a typical species; messier ones more).")]
    [Range(0f, 20f)] public float dirtPerHourAtCapacity = 0.9f;
    [Tooltip("Share of the mess that fresh bedding takes up instead of the floor.")]
    [Range(0f, 1f)] public float beddingAbsorbs = 0.6f;
    [Tooltip("Bedding (0-100) spent per point of mess.")]
    [Range(0f, 5f)] public float beddingSoiling = 0.8f;
    [Tooltip("Game hours in which an open pen's ground (a pasture, a pond) clears itself of dung; rain hastens it. 0: never.")]
    [Range(0f, 2000f)] public float pastureCleanHours = 72f;
    [Tooltip("Fouling (0-100) a fully stocked pen gives a 100 L trough per game hour (a bigger trough fouls slower: x 100 / litres, between 0.25 and 2).")]
    [Range(0f, 10f)] public float troughFoulingPerHour = 0.15f;
    [Tooltip("Fouling per game hour a filthy floor (dirt 100) gives a 100 L trough (scaled like the fouling by the animals).")]
    [Range(0f, 10f)] public float troughDirtFouling = 0.2f;
    [Tooltip("°C above which algae grow in an open trough.")]
    [Range(-10f, 50f)] public float algaeAbove = 18f;
    [Tooltip("Fouling per game hour per °C above Algae Above, in an open trough (0.5 m² of water surface per 100 L or more; a covered tank grows none).")]
    [Range(0f, 2f)] public float algaePerDegree = 0.04f;
    [Tooltip("Trough fouling from which its water can be changed (it looks murky): mucking out can then scrub the trough.")]
    [Range(0f, 100f)] public float tipOutAbove = 15f;
    [Tooltip("How fast worm eggs build up in the ground of a soiled, stocked pasture in the warmth.")]
    [Range(0f, 10f)] public float wormRate = 1f;
    [Tooltip("Game hours over which worm eggs in the ground die off (in frost: Frost Fade Hours).")]
    [Range(1f, 5000f)] public float wormFadeHours = 150f;
    [Range(1f, 5000f)] public float frostFadeHours = 48f;
    [Tooltip("Dirt per game hour the remains of a dead animal give its pen (for the room it takes, over the pen's area).")]
    [Range(0f, 50f)] public float remainsMessPerHour = 2f;
    [Tooltip("Game hours the remains of an animal that died of a catching illness go on spreading it.")]
    [Range(0f, 1000f)] public float remainsContagionHours = 48f;
    [Tooltip("Game hours before a pen can be mucked out again.")]
    [Range(0f, 48f)] public float cleanCooldownHours = 0.25f;
    [Tooltip("Share of the floor's dirt one mucking out removes with an average tool (a good fork more, at most 95%).")]
    [Range(0f, 1f)] public float cleanShare = 0.75f;
    [Tooltip("Straw items for one square metre of fresh bedding.")]
    [Range(0.01f, 5f)] public float strawPerSquareMetre = 0.2f;
    [Tooltip("Game hours before fresh straw can be laid again in a pen that has nothing to muck out.")]
    [Range(0f, 96f)] public float beddingCooldownHours = 6f;
    [Tooltip("Bedding (0-100) a bedded pen has when the world first knows it.")]
    [Range(0f, 100f)] public float freshBedding = 60f;
    [Tooltip("Stress mucking out gives the animals of its pen (less for a quick clean; at most once in Disturb Hours).")]
    [Range(0f, 50f)] public float disturbStress = 3f;
    [Range(0f, 96f)] public float disturbHours = 6f;
    [Tooltip("The item mucking out gives (Agriculture's manure).")]
    public string manureItem = "agri:amend/manure";
    [Tooltip("Manure items per square metre of floor mucked out from filthy (dirt 100) to clean; fractions carry over.")]
    [Range(0f, 5f)] public float manurePerSquareMetre = 0.08f;

    [Header("Comfort")]
    [Tooltip("Game hours for comfort to settle where its surroundings put it.")]
    [Range(0.5f, 96f)] public float comfortHours = 6f;
    [Tooltip("Comfort lost per °C below its species' comfortable range (after shelter, warmth and wind).")]
    [Range(0f, 20f)] public float coldPenalty = 2.5f;
    [Tooltip("Comfort lost per °C above its comfortable range (after shade).")]
    [Range(0f, 20f)] public float heatPenalty = 3f;
    [Tooltip("°C of chill per m/s of wind on an animal without shelter, in the cold.")]
    [Range(0f, 5f)] public float windChill = 0.3f;
    [Tooltip("Comfort an animal that hates rain loses standing in it unsheltered (x its Rain Dislike; a storm x1.5).")]
    [Range(0f, 100f)] public float rainPenalty = 20f;
    [Tooltip("Comfort lost in a crowded pen.")]
    [Range(0f, 100f)] public float crowdingPenalty = 25f;
    [Tooltip("Comfort lost per point of floor dirt.")]
    [Range(0f, 2f)] public float dirtPenalty = 0.25f;
    [Tooltip("Comfort lost with no bedding in a bedded pen (x its Bedding Need).")]
    [Range(0f, 100f)] public float beddingPenalty = 15f;
    [Tooltip("Hooves below this hurt (Hoof Pain comfort per point below).")]
    [Range(0f, 100f)] public float hoofPainBelow = 50f;
    [Range(0f, 5f)] public float hoofPain = 0.4f;
    [Tooltip("A coat below this itches (Dirty Coat Penalty comfort per point below).")]
    [Range(0f, 100f)] public float dirtyCoatBelow = 50f;
    [Range(0f, 5f)] public float dirtyCoatPenalty = 0.2f;
    [Tooltip("Comfort, hygiene and hooves a boarded animal settles at (the stable keeps it clean and trims its hooves).")]
    [Range(0f, 100f)] public float boardingComfort = 60f;
    [Range(0f, 100f)] public float boardingHygiene = 65f;
    [Range(0f, 100f)] public float boardingHooves = 80f;
    [Tooltip("Game hours for a boarded animal's hooves to settle at Boarding Hooves.")]
    [Range(1f, 1000f)] public float boardingHoofHours = 48f;
    [Tooltip("Stress per point of comfort below 60.")]
    [Range(0f, 5f)] public float discomfortStress = 0.2f;
    [Tooltip("Stress of a crowded pen, and of being kept without the company its kind wants.")]
    [Range(0f, 100f)] public float crowdingStress = 20f;
    [Range(0f, 100f)] public float lonelinessStress = 15f;

    [Header("Welfare")]
    [Tooltip("How welfare is worked out, its five bands and what each band does.")]
    public AnimalWelfareSpec welfare = new AnimalWelfareSpec();
    [Tooltip("The owner is told when an animal's welfare falls to Critical or its health below Needs Help Below Health (at most once in Needs Help Every Hours).")]
    public bool tellWhenCritical = true;
    [Range(0f, 100f)] public float needsHelpBelowHealth = 30f;
    [Range(1f, 240f)] public float needsHelpEveryHours = 24f;

    [Header("Health")]
    [Tooltip("Animals can fall ill (off: no exposure builds and illnesses they have do nothing).")]
    public bool healthEnabled = true;
    [Tooltip("Multiplies how fast every illness builds up (2 = twice as fast).")]
    [Range(0f, 10f)] public float exposureScale = 1f;
    [Tooltip("Entries of health history kept per animal.")]
    [Range(4, 200)] public int keepHealthLog = 32;
    [Tooltip("Medicines whose last dose each animal keeps (when the next may do good again). Never fewer than the game's medicines: none is forgotten while it is one.")]
    [Range(16, 1000)] public int keepDoses = 64;
    [Tooltip("An animal whose worst illness is above this severity does not heal.")]
    [Range(0f, 101f)] public float illnessHealBlockAbove = 40f;
    [Tooltip("Affinity an animal loses per game day once its owner has not cared for it in Affinity Neglect Hours.")]
    [Range(0f, 100f)] public float affinityFadePerDay = 0.5f;
    [Range(1f, 2000f)] public float affinityNeglectHours = 48f;
    [Tooltip("Game hours over which its life record averages how it lived (what its remains are judged by).")]
    [Range(24f, 20000f)] public float lifeHours = 480f;
    [Tooltip("Severity of its worst illness from which an animal counts as ill (its life record's ill hours).")]
    [Range(0f, 100f)] public float illFrom = 15f;

    [Header("Veterinarian")]
    [Tooltip("Illnesses past their Needs Vet Above severity are only held by a keeper's medicine while a veterinarian is in the world (with none, keeper medicine cures everything).")]
    public bool vetRequired = true;
    [Tooltip("Game hours before the veterinarian sees the same animal again.")]
    [Range(0f, 240f)] public float vetCooldownHours = 8f;
    [Tooltip("Severity the veterinarian takes off every illness at once, then per game hour for Vet Hours.")]
    [Range(0f, 100f)] public float vetImmediate = 40f;
    [Range(0f, 50f)] public float vetPerHour = 5f;
    [Range(0f, 240f)] public float vetHours = 24f;

    [Header("Remains")]
    [Tooltip("Owners may slaughter their animals (the carcass is butchered through Harvesting).")]
    public bool allowSlaughter = true;
    [Tooltip("The quality factor (x every piece of the carcass) of the worst and the best raised animal.")]
    [Range(0.1f, 2f)] public float rearingMin = 0.45f;
    [Range(0.1f, 2f)] public float rearingMax = 1.15f;
    [Tooltip("How much its lifelong welfare, nutrition, health and hygiene count in how it was raised (normalised).")]
    [Range(0f, 1f)] public float rearingWelfare = 0.45f;
    [Range(0f, 1f)] public float rearingNutrition = 0.3f;
    [Range(0f, 1f)] public float rearingHealth = 0.15f;
    [Range(0f, 1f)] public float rearingHygiene = 0.1f;
    [Tooltip("Rearing lost to its worst illness at death (x severity / 100), to starving or wasting, and to poison.")]
    [Range(0f, 1f)] public float illnessPenalty = 0.35f;
    [Range(0f, 1f)] public float starvationPenalty = 0.2f;
    [Range(0f, 1f)] public float poisonPenalty = 0.3f;
    [Tooltip("Real minutes a laid-out farm carcass lasts before it rots away (it is not saved).")]
    [Range(1f, 1440f)] public float carcassMinutes = 120f;

    [Header("Production")]
    [Tooltip("How products are made and judged (Phase 3): the quality formula and its limits, the tiers, when an animal makes nothing, the history and the ledger.")]
    public AnimalProductionRules production = new AnimalProductionRules();

    [Header("Breeding and relationships")]
    [Tooltip("How breeding works (Phase 4): inheritance and mutation, close kin, retries, pregnancy and nursing.")]
    public AnimalBreedingRules breeding = new AnimalBreedingRules();
    [Tooltip("How animals keep company (Phase 4): bonds between them, what company does, rivals, and the bond its owner's time near it builds.")]
    public AnimalSocialRules social = new AnimalSocialRules();

    [Header("Trading, upgrades and automation")]
    [Tooltip("How animals change hands (Phase 5): crating them to sell or trade, how long a crate waits, what a crated animal is worth.")]
    public AnimalTradeRules trade = new AnimalTradeRules();
    [Tooltip("How pens are improved and fitted with machines - a feed hopper, an automatic waterer, egg trays - and what keeping them up costs (Phase 5).")]
    public AnimalUpgradeRules upgrades = new AnimalUpgradeRules();

    public static AnimalRules Defaults() => new AnimalRules();
}
