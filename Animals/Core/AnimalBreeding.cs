using System;
using System.Collections.Generic;
using UnityEngine;

// ==================================================================== data
/// <summary>
/// How a species breeds (FR-11), as data: how long from mating to birth (or hatching), how many young, how long before
/// a mother (or a sire) may breed again, how likely a typical, well kept pair are to conceive, how well a parent must be
/// to breed, what breeding takes from the player, whether the young nurse, and the words for it all.
/// </summary>
[Serializable]
public class AnimalBreedingSpec
{
    [Tooltip("Its animals breed at all.")]
    public bool enabled = true;
    [Tooltip("Game hours from mating to birth (or hatching).")]
    [Min(1f)] public float gestationHours = 240f;
    [Tooltip("Young in a birth (a clutch, a litter): from this many...")]
    [Min(1)] public int litterMin = 1;
    [Tooltip("...to this many (a fertile mother leans to more).")]
    [Min(1)] public int litterMax = 1;
    [Tooltip("Game hours after a birth before the mother may breed again.")]
    [Min(0f)] public float cooldownHours = 336f;
    [Tooltip("Game hours after mating before the sire may breed again.")]
    [Min(0f)] public float sireCooldownHours = 24f;
    [Tooltip("The chance a typical, well kept pair conceive (their fertility, welfare, age and kinship move it).")]
    [Range(0f, 1f)] public float conception = 0.75f;
    [Tooltip("Least health, nutritional condition and welfare a parent needs to breed.")]
    [Range(0f, 100f)] public float minHealth = 60f;
    [Range(0f, 100f)] public float minNutrition = 55f;
    [Range(0f, 100f)] public float minWelfare = 50f;
    [Tooltip("An item breeding takes from the player (its key, as \"animal:feed/grain\"; empty: none), and how many.")]
    public string costItem = "";
    [Min(0)] public int costCount;
    [Tooltip("The young suckle from their mother while she is in their pen, up to and including Nurses Through (a chick feeds itself).")]
    public bool nurses = true;
    public GrowthStage nursesThrough = GrowthStage.Baby;
    [Tooltip("Two grown males of its kind in one pen are rivals (bulls, rams, boars, roosters, stallions).")]
    public bool malesRival;
    [Tooltip("The words: a birth (\"calved\", \"hatched a clutch\") and a mother expecting (\"in calf\", \"broody\").")]
    public string birthVerb = "gave birth";
    public string expectingWord = "expecting";

    public int LitterMax => Math.Max(Math.Max(1, litterMin), litterMax);

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (!enabled) return;
        if (litterMax < litterMin) warnings?.Add($"{owner}: breeding: Litter Max is below Litter Min (it is taken as Litter Min).");
        if (gestationHours < 1f) errors?.Add($"{owner}: breeding: a gestation under an hour.");
        if (conception <= 0f) warnings?.Add($"{owner}: breeding: Conception is 0: no pair of it ever conceives.");
        if (costCount > 0 && string.IsNullOrWhiteSpace(costItem)) warnings?.Add($"{owner}: breeding takes {costCount} of no item.");
    }
}

/// <summary>A mother expecting: her mate, his genes at the mating, when the young are due and how many.</summary>
[Serializable]
public class AnimalPregnancy
{
    public bool active;
    public string father = "", fatherName = "", fatherBreed = "";
    [Tooltip("The sire's parents (the young's grandparents on his side) and generation.")]
    public string fatherMother = "", fatherFather = "";
    public int fatherGeneration;
    [Tooltip("The sire's genes as they were at the mating (he may be gone by the birth).")]
    public AnimalGenes fatherGenes = new AnimalGenes();
    [Tooltip("How closely the pair are related (0 none, 0.25 parent and young or full siblings).")]
    public float kinship;
    public double conceivedAt, dueAt;
    public int litter, seed;
    [Tooltip("The young's ids, taken from the world's serial at the mating (so a catch-up and a watched stretch give the same ids, whatever order births come in).")]
    public List<string> young = new List<string>();
}

/// <summary>How inheritance and breeding work everywhere (Animal Rules ▸ Breeding).</summary>
[Serializable]
public class AnimalBreedingRules
{
    public bool enabled = true;

    [Header("Inheritance")]
    [Tooltip("How far a young's trait may stray from its parents' mean (a normal spread).")]
    [Range(0f, 0.3f)] public float inheritSpread = 0.06f;
    [Tooltip("The chance, per trait, that it mutates...")]
    [Range(0f, 1f)] public float mutationChance = 0.04f;
    [Tooltip("...by this much (a normal spread).")]
    [Range(0f, 1f)] public float mutationSize = 0.2f;
    [Tooltip("The chance a young of a parent with a rare trait carries it.")]
    [Range(0f, 1f)] public float rareInheritChance = 0.5f;
    [Tooltip("The chance a young carries a rare trait neither parent had.")]
    [Range(0f, 1f)] public float rareMutationChance = 0.02f;

    [Header("Kin")]
    [Tooltip("Parent and young, or full siblings, may be bred (off: refused).")]
    public bool allowCloseKin;
    [Tooltip("Inbreeding weakens the young: resistance, fertility and longevity less this x the pair's kinship x 4.")]
    [Range(0f, 1f)] public float inbreedingPenalty = 0.1f;

    [Header("Mating, pregnancy, nursing")]
    [Tooltip("Game hours before a pair whose mating did not take may try again.")]
    [Range(1f, 500f)] public float retryHours = 24f;
    [Tooltip("How much fertility moves the chance to conceive (the mother's; the sire's at 60 %).")]
    [Range(0f, 1f)] public float fertilitySpread = 0.5f;
    [Tooltip("A mother expecting eats and needs this much more.")]
    [Range(1f, 2f)] public float pregnancyAppetite = 1.15f;
    [Tooltip("A nursing mother eats and needs this much more for each young suckling.")]
    [Range(0f, 1f)] public float nursingAppetite = 0.15f;
    [Tooltip("A mother whose health falls below this loses her young.")]
    [Range(0f, 100f)] public float miscarryHealthBelow = 20f;

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (mutationChance > 0.5f) warnings?.Add("Breeding: Mutation Chance above 0.5: most traits mutate in every young.");
    }
}

// ==================================================================== rules
/// <summary>
/// Breeding and inheritance (FR-11), pure C# and deterministic (hashes of the parents and their tries, never randomness).
/// <list type="bullet">
/// <item><b>Who</b>: two animals of one species, a female and a male (both, for a species that is not sexed), in a stage of
/// life that breeds, healthy, fed and well enough (the species' least), she not expecting, neither in their cooldown, not
/// close kin (parent and young, full siblings: unless the rules allow it).</item>
/// <item><b>Conceiving</b>: the species' chance, x each parent's fertility, her welfare, age and the pair's kinship. A mating
/// that does not take lets them try again after Retry Hours.</item>
/// <item><b>Pregnancy</b>: she eats and needs more; she loses her young if her health falls too low; when they are due the
/// young are born (or hatch) in her pen, or at the boarding stable with her.</item>
/// <item><b>Inheritance</b>: each trait of a young is its parents' mean, a little either way, sometimes mutated (configurable);
/// its coat and pattern one parent's; a rare trait passed on half the time (or, rarely, new); its breed one parent's; close
/// kin weaken it. Its generation is one past its elder parent's, and it knows its parents and grandparents.</item>
/// <item><b>Nursing</b>: the young suckle from their mother while she is in their pen (she eats more for them), so a calf
/// needs no milk replacer while its mother is with it.</item>
/// </list>
/// Nothing is born twice: a birth happens once, in the step its young are due, and empties the pregnancy; the young take
/// fresh ids from the world's serial.
/// </summary>
public static class AnimalBreeding
{
    /// <summary>Young on the way to a player's living mothers (their litters), for the limit of how many they keep.</summary>
    public static int ExpectedFor(AnimalBook book, string owner)
    {
        int n = 0;
        if (book == null || string.IsNullOrEmpty(owner)) return 0;
        foreach (AnimalRecord a in book.All)
            if (a != null && a.owner == owner && a.Alive && Expecting(a)) n += Math.Max(1, a.pregnancy.litter);
        return n;
    }

    /// <summary>Square metres the young on the way to a pen's mothers will take (as adults).</summary>
    public static float ExpectedSpace(AnimalBook book, AnimalHerd herd)
    {
        float space = 0f;
        if (book == null || herd == null) return 0f;
        foreach (AnimalRecord a in herd.animals)
            if (a != null && a.Alive && Expecting(a)) space += Math.Max(1, a.pregnancy.litter) * AnimalBook.SpaceOf(book.Catalog?.Species(a.species), a);
        return space;
    }

    /// <summary>Young on the way to a pen's mothers.</summary>
    public static int ExpectedIn(AnimalHerd herd)
    {
        int n = 0;
        if (herd == null) return 0;
        foreach (AnimalRecord a in herd.animals)
            if (a != null && a.Alive && Expecting(a)) n += Math.Max(1, a.pregnancy.litter);
        return n;
    }

    /// <summary>Its species' breeding (a species with none: not enabled).</summary>
    public static AnimalBreedingSpec SpecOf(AnimalSpeciesData sp) => sp?.breeding ?? Disabled;

    private static readonly AnimalBreedingSpec Disabled = new AnimalBreedingSpec { enabled = false };

    public static bool Expecting(AnimalRecord a) => a?.pregnancy != null && a.pregnancy.active;

    /// <summary>
    /// Can it breed now (on its own account)? Why not, in words, when it cannot: its kind, its stage, health, nutrition,
    /// welfare, being with young, or its cooldown (and how long it has left).
    /// </summary>
    public static bool Ready(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double now, out string why)
    {
        why = null;
        AnimalBreedingSpec s = SpecOf(sp);
        string name = AnimalSim.NameOf(a, sp);
        if (a == null || !a.Alive) { why = "It is dead."; return false; }
        if (rules?.breeding != null && !rules.breeding.enabled) { why = "Breeding is off."; return false; }
        if (!s.enabled) { why = $"{(sp != null ? sp.plural : "These animals")} are not bred here."; return false; }
        if (string.IsNullOrEmpty(a.pen)) { why = $"{name} is at the boarding stable."; return false; }
        AnimalStageSpec st = sp.StageOrNearest(a.stage);
        if (st == null || !st.breedingEnabled) { why = $"{name} is {(a.stage < GrowthStage.Adult ? "too young" : "past breeding")}."; return false; }
        if (Expecting(a)) { why = $"{name} is {s.expectingWord}."; return false; }
        if (a.breedReadyAt > now + 1e-6) { why = $"{name} is not ready again yet ({AnimalObservation.Wait(a.breedReadyAt - now)})."; return false; }
        if (a.health < s.minHealth) { why = $"{name} is not well enough to breed."; return false; }
        if (a.nutrition < s.minNutrition) { why = $"{name} is too poorly fed to breed."; return false; }
        if (a.welfare < s.minWelfare) { why = $"{name} is not kept well enough to breed."; return false; }
        if (a.satiety <= 0f || a.hydration <= 0f) { why = $"{name} is starving."; return false; }
        return true;
    }

    /// <summary>
    /// Can these two be bred together (each must also be <see cref="Ready"/>)? Of one species, a female and a male (a species
    /// that is not sexed: any two), not themselves, and not close kin unless the rules allow it. <paramref name="mother"/> and
    /// <paramref name="father"/>: which is which.
    /// </summary>
    public static bool Compatible(AnimalRecord a, AnimalRecord b, AnimalSpeciesData sp, AnimalRules rules, out AnimalRecord mother, out AnimalRecord father, out string why)
    {
        mother = father = null;
        why = null;
        if (a == null || b == null || a.id == b.id) { why = "It needs a partner."; return false; }
        if (a.species != b.species) { why = "They are not of one kind."; return false; }
        if (sp != null && sp.sexed)
        {
            if (a.sex == b.sex) { why = a.sex == AnimalSex.Female ? "They are both female." : "They are both male."; return false; }
            mother = a.sex == AnimalSex.Female ? a : b;
            father = a.sex == AnimalSex.Female ? b : a;
        }
        else
        {
            mother = a;
            father = b;
        }
        float kin = Kinship(a, b);
        if (kin >= 0.25f - 1e-4f && (rules?.breeding == null || !rules.breeding.allowCloseKin)) { why = "They are too closely related."; return false; }
        return true;
    }

    /// <summary>
    /// How closely two animals are related, from the parents and grandparents each knows: 0.25 parent and young or full
    /// siblings; 0.125 half siblings, grandparent and grandchild, aunt or uncle; 0.0625 first cousins (and the like).
    /// </summary>
    public static float Kinship(AnimalRecord a, AnimalRecord b)
    {
        if (a == null || b == null || a.id == b.id) return 0f;
        if (Is(a.id, b.mother) || Is(a.id, b.father) || Is(b.id, a.mother) || Is(b.id, a.father)) return 0.25f;
        bool sameMother = !string.IsNullOrEmpty(a.mother) && a.mother == b.mother;
        bool sameFather = !string.IsNullOrEmpty(a.father) && a.father == b.father;
        if (sameMother && sameFather) return 0.25f;
        float k = sameMother || sameFather ? 0.125f : 0f;
        // A grandparent and its grandchild.
        if (Has(b.grandparents, a.id) || Has(a.grandparents, b.id)) k = Mathf.Max(k, 0.125f);
        // An aunt or uncle: one's parents are the other's grandparents.
        int au = Count(b.grandparents, a.mother, a.father), ua = Count(a.grandparents, b.mother, b.father);
        k = Mathf.Max(k, Mathf.Max(au, ua) * 0.0625f);
        // Cousins: shared grandparents.
        int shared = 0;
        if (a.grandparents != null && b.grandparents != null)
            foreach (string g in a.grandparents)
                if (!string.IsNullOrEmpty(g) && b.grandparents.Contains(g)) shared++;
        k = Mathf.Max(k, shared * 0.03125f);
        return k;
    }

    private static bool Is(string id, string parent) => !string.IsNullOrEmpty(parent) && id == parent;

    private static bool Has(List<string> list, string id) => list != null && !string.IsNullOrEmpty(id) && list.Contains(id);

    private static int Count(List<string> list, string x, string y) => (Has(list, x) ? 1 : 0) + (Has(list, y) ? 1 : 0);

    /// <summary>
    /// The chance a pair conceive: the species' Conception x her fertility (1 ± Fertility Spread) x his (± 60 % of it) x her
    /// welfare (Excellent x1.1, Good x1.05) x age (a senior parent x0.6) x (1 - their kinship x 2).
    /// </summary>
    public static float ConceptionChance(AnimalRecord mother, AnimalRecord father, AnimalSpeciesData sp, AnimalRules rules)
    {
        AnimalBreedingSpec s = SpecOf(sp);
        AnimalBreedingRules r = rules?.breeding ?? new AnimalBreedingRules();
        float her = AnimalGenes.Scale(mother?.genes != null ? mother.genes.fertility : 0.5f, r.fertilitySpread);
        float his = AnimalGenes.Scale(father?.genes != null ? father.genes.fertility : 0.5f, r.fertilitySpread * 0.6f);
        AnimalWelfareBand band = AnimalWelfare.BandOf(mother != null ? mother.welfare : 60f, rules?.welfare);
        float well = band == AnimalWelfareBand.Excellent ? 1.1f : band == AnimalWelfareBand.Good ? 1.05f : band == AnimalWelfareBand.Poor ? 0.8f : band == AnimalWelfareBand.Critical ? 0.5f : 1f;
        float age = (mother != null && mother.stage == GrowthStage.Senior) || (father != null && father.stage == GrowthStage.Senior) ? 0.6f : 1f;
        float kin = 1f - 2f * Kinship(mother, father);
        return Mathf.Clamp(s.conception * her * his * well * age * kin, 0f, 0.98f);
    }

    /// <summary>
    /// Mates a pair (the commit of a Breed request; both checked already). The roll is a hash of the pair and her tries,
    /// so the same world gives the same answer. Conceived: she is expecting (the young, their number, their seed and - given
    /// the <paramref name="book"/> - their ids are fixed now), due after the species' gestation; he may breed again after
    /// his cooldown. Not: both may try again after
    /// Retry Hours. Either way they are mates. Returns whether she conceived.
    /// </summary>
    public static bool Mate(AnimalRecord mother, AnimalRecord father, AnimalSpeciesData sp, AnimalRules rules, double now, string world, string by, AnimalBook book = null)
    {
        AnimalBreedingSpec s = SpecOf(sp);
        AnimalBreedingRules r = rules?.breeding ?? new AnimalBreedingRules();
        mother.breedAttempts++;
        int seed = (int)(AnimalMath.Hash(AnimalMath.Hash(world ?? ""), AnimalMath.Hash(mother.id), AnimalMath.Hash(father.id)) & 0x7FFFFFFF);
        float roll = AnimalMath.Noise(seed, mother.breedAttempts, 1);
        bool conceived = roll < ConceptionChance(mother, father, sp, rules);
        AnimalSocial.Bond(mother, father, AnimalBondKind.Mate, rules?.social != null ? rules.social.mateBond : 20f, now);
        AnimalSocial.Bond(father, mother, AnimalBondKind.Mate, rules?.social != null ? rules.social.mateBond : 20f, now);
        string sire = AnimalSim.NameIn(father, sp), dam = AnimalSim.NameIn(mother, sp);
        if (!conceived)
        {
            mother.breedReadyAt = now + r.retryHours;
            father.breedReadyAt = Math.Max(father.breedReadyAt, now + Math.Min(r.retryHours, s.sireCooldownHours));
            mother.Log(AnimalEventKind.Mated, now, $"Bred with {sire}: it did not take.", by, rules?.keepEvents ?? 32);
            father.Log(AnimalEventKind.Mated, now, $"Bred with {dam}: it did not take.", by, rules?.keepEvents ?? 32);
            mother.revision++;
            father.revision++;
            return false;
        }
        float u = AnimalMath.Clamp01(AnimalMath.Noise(seed, mother.breedAttempts, 2) + ((mother.genes != null ? mother.genes.fertility : 0.5f) - 0.5f) * 0.4f);
        int min = Math.Max(1, s.litterMin), max = s.LitterMax;
        int litter = Math.Min(max, min + (int)(u * (max - min + 1)));
        mother.pregnancy = new AnimalPregnancy
        {
            active = true,
            father = father.id,
            fatherName = father.name ?? "",
            fatherBreed = father.breed ?? "",
            fatherMother = father.mother ?? "",
            fatherFather = father.father ?? "",
            fatherGeneration = father.generation,
            fatherGenes = father.genes != null ? father.genes.Clone() : new AnimalGenes(),
            kinship = Kinship(mother, father),
            conceivedAt = now,
            dueAt = now + Math.Max(1f, s.gestationHours),
            litter = litter,
            seed = (int)(AnimalMath.Hash(seed, mother.breedAttempts, 3) & 0xFFFFFF) | 1,
        };
        // The young's ids now, in the order commands come (the same in every run), never at the birth.
        if (book != null)
            for (int i = 0; i < litter; i++) mother.pregnancy.young.Add(book.NewId(sp != null ? sp.id : mother.species));
        mother.breedReadyAt = mother.pregnancy.dueAt;
        father.breedReadyAt = now + s.sireCooldownHours;
        father.lastBredAt = mother.lastBredAt = now;
        mother.Log(AnimalEventKind.Mated, now, $"Bred with {sire}: {s.expectingWord}.", by, rules?.keepEvents ?? 32);
        father.Log(AnimalEventKind.Mated, now, $"Bred with {dam}.", by, rules?.keepEvents ?? 32);
        mother.revision++;
        father.revision++;
        return true;
    }

    // ---------------------------------------------------------------- inheritance
    /// <summary>
    /// The genes of a young of these parents (FR-11): each trait their mean a little either way (Inherit Spread), sometimes
    /// mutated (Mutation Chance, by Mutation Size); its coat and pattern one parent's (the coat kept within its breed's
    /// coats); a rare trait one parent's half the time (Rare Inherit Chance), or rarely a new one; kin weaken it.
    /// </summary>
    public static AnimalGenes Inherit(AnimalGenes mother, AnimalGenes father, int seed, AnimalBreedingRules r, AnimalBreedData breed, float kinship)
    {
        mother ??= new AnimalGenes();
        father ??= new AnimalGenes();
        r ??= new AnimalBreedingRules();
        var g = new AnimalGenes { seed = seed };
        g.size = Trait(mother.size, father.size, seed, 1, r);
        g.growth = Trait(mother.growth, father.growth, seed, 2, r);
        g.efficiency = Trait(mother.efficiency, father.efficiency, seed, 3, r);
        g.resistance = Trait(mother.resistance, father.resistance, seed, 4, r);
        g.fertility = Trait(mother.fertility, father.fertility, seed, 5, r);
        g.temperament = Trait(mother.temperament, father.temperament, seed, 6, r);
        g.productionQuality = Trait(mother.productionQuality, father.productionQuality, seed, 7, r);
        g.productionRate = Trait(mother.productionRate, father.productionRate, seed, 8, r);
        g.longevity = Trait(mother.longevity, father.longevity, seed, 9, r);
        // Inbreeding weakens it.
        float weaken = AnimalMath.Clamp01(kinship * 4f) * r.inbreedingPenalty;
        if (weaken > 0f)
        {
            g.resistance = AnimalMath.Clamp(g.resistance - weaken, 0.02f, 0.98f);
            g.fertility = AnimalMath.Clamp(g.fertility - weaken, 0.02f, 0.98f);
            g.longevity = AnimalMath.Clamp(g.longevity - weaken, 0.02f, 0.98f);
        }
        g.inbreeding = AnimalMath.Clamp01(kinship);
        // Its coat and pattern: one parent's each (a coat its breed has).
        int coats = breed?.coats != null ? breed.coats.Count : 0;
        int coat = AnimalMath.Noise(seed, 20) < 0.5f ? mother.coat : father.coat;
        g.coat = coats > 0 ? Math.Abs(coat) % coats : 0;
        g.pattern = AnimalMath.Noise(seed, 21) < 0.5f ? mother.pattern : father.pattern;
        if (AnimalMath.Noise(seed, 22) < r.mutationChance) g.pattern = AnimalMath.Noise(seed, 23) < 0.5f ? 0 : 1 + (int)(AnimalMath.Hash(seed, 24) % 3u);
        // A rare trait.
        int rareFrom = mother.rare != 0 && father.rare != 0 ? (AnimalMath.Noise(seed, 25) < 0.5f ? mother.rare : father.rare) : mother.rare != 0 ? mother.rare : father.rare;
        if (rareFrom != 0 && AnimalMath.Noise(seed, 26) < r.rareInheritChance) g.rare = rareFrom;
        else if (AnimalMath.Noise(seed, 27) < r.rareMutationChance) g.rare = 1 + (int)(AnimalMath.Hash(seed, 28) % 4u);
        return g;
    }

    private static float Trait(float m, float f, int seed, int channel, AnimalBreedingRules r)
    {
        float v = 0.5f * (AnimalMath.Clamp01(m) + AnimalMath.Clamp01(f)) + AnimalMath.Gauss(seed, channel) * r.inheritSpread;
        if (AnimalMath.Noise(seed, channel + 50) < r.mutationChance) v += AnimalMath.Gauss(seed, channel + 100) * r.mutationSize;
        return AnimalMath.Clamp(v, 0.02f, 0.98f);
    }

    // ---------------------------------------------------------------- pregnancy and birth (the simulation)
    /// <summary>
    /// How much more an animal eats and needs for its young: x Pregnancy Appetite while expecting, + Nursing Appetite for each
    /// young suckling from her (<paramref name="nursing"/>).
    /// </summary>
    public static float AppetiteFactor(AnimalRecord a, AnimalRules rules, int nursing)
    {
        AnimalBreedingRules r = rules?.breeding;
        if (a == null || r == null) return 1f;
        float k = Expecting(a) ? Mathf.Max(1f, r.pregnancyAppetite) : 1f;
        return k * (1f + Mathf.Max(0f, r.nursingAppetite) * Math.Max(0, nursing));
    }

    /// <summary>Does this young suckle (its kind nurses, it is young enough)? Its mother must be in its pen too.</summary>
    public static bool Suckles(AnimalRecord young, AnimalSpeciesData sp)
    {
        AnimalBreedingSpec s = SpecOf(sp);
        return young != null && young.Alive && s.nurses && !string.IsNullOrEmpty(young.mother) && young.stage <= s.nursesThrough;
    }

    /// <summary>
    /// A mother in poor health loses her young: the pregnancy ends, she may breed again after twice Retry Hours, and her owner
    /// is told. Returns whether she lost them.
    /// </summary>
    public static bool Miscarry(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double now, AnimalSimContext ctx)
    {
        AnimalBreedingRules r = rules?.breeding;
        if (!Expecting(a) || r == null || a.health >= r.miscarryHealthBelow && a.Alive) return false;
        a.pregnancy = new AnimalPregnancy();
        a.breedReadyAt = now + 2f * r.retryHours;
        if (a.Alive)
        {
            string text = $"{AnimalSim.NameOf(a, sp)} has lost her young.";
            a.Log(AnimalEventKind.Miscarried, now, text, "", rules.keepEvents);
            ctx?.Notice(a, AnimalEventKind.Miscarried, text);
        }
        a.revision++;
        return true;
    }

    /// <summary>
    /// The young of a mother whose time has come (her pregnancy due by <paramref name="at"/>): each made from its parents
    /// (see <see cref="Inherit"/>) with a fresh id from the world's serial, born into her pen (or the boarding stable with
    /// her), knowing its mother, sire and grandparents, its mother's owner's. Mother, young and litter-mates are kin. She may
    /// breed again after the species' cooldown; her owner is told. Returns the young born (none: not due, or not expecting).
    /// </summary>
    public static int Deliver(AnimalBook book, AnimalRecord mother, AnimalSpeciesData sp, AnimalSimContext ctx, double at, List<AnimalRecord> born = null)
    {
        if (book == null || mother == null || sp == null || !Expecting(mother) || !mother.Alive) return 0;
        AnimalPregnancy p = mother.pregnancy;
        if (at < p.dueAt - 1e-6) return 0;
        // Ended before anything is made: whatever happens next, these young are never born twice.
        mother.pregnancy = new AnimalPregnancy();
        AnimalRules rules = book.Rules;
        AnimalBreedingSpec s = SpecOf(sp);
        AnimalBreedingRules r = rules.breeding ?? new AnimalBreedingRules();
        GrowthStage first = sp.stages != null && sp.stages.Count > 0 && sp.stages[0] != null ? sp.stages[0].stage : GrowthStage.Baby;
        var litter = new List<AnimalRecord>();
        int n = Math.Max(1, p.litter);
        AnimalRecord sire = book.Find(p.father);
        for (int i = 0; i < n; i++)
        {
            int seed = (int)(AnimalMath.Hash(p.seed, i, 7) & 0xFFFFFF) | 1;
            string breedId = mother.breed ?? "";
            if (!string.IsNullOrEmpty(p.fatherBreed) && p.fatherBreed != breedId && AnimalMath.Noise(seed, 30) < 0.5f) breedId = p.fatherBreed;
            AnimalBreedData breed = sp.BreedOrDefault(breedId);
            AnimalSex sex = sp.sexed ? (AnimalMath.Noise(seed, 99) < sp.femaleShare ? AnimalSex.Female : AnimalSex.Male) : AnimalSex.None;
            string reserved = p.young != null && i < p.young.Count ? p.young[i] : null;
            var young = new AnimalRecord
            {
                id = !string.IsNullOrEmpty(reserved) && book.Find(reserved) == null ? reserved : book.NewId(sp.id),
                owner = mother.owner,
                ownerName = mother.ownerName ?? "",
                species = sp.id,
                breed = breed != null ? breed.id : breedId,
                sex = sex,
                genes = Inherit(mother.genes, p.fatherGenes, seed, r, breed, p.kinship),
                development = sp.DevelopmentAtStart(first),
                stage = first,
                bornAt = at,
                acquiredAt = at,
                origin = "born",
                pen = mother.pen,
                livedTo = at,
                satiety = 80f,
                hydration = 85f,
                nutrition = 72f,
                happiness = 60f,
                stress = 5f,
                hygiene = 70f,
                comfort = 65f,
                hooves = 100f,
                mother = mother.id,
                father = p.father ?? "",
                motherName = mother.name ?? "",
                fatherName = p.fatherName ?? "",
                generation = Math.Max(mother.generation, p.fatherGeneration) + 1,
            };
            young.grandparents = new List<string> { mother.mother ?? "", mother.father ?? "", p.fatherMother ?? "", p.fatherFather ?? "" };
            if (string.IsNullOrEmpty(mother.pen)) young.status = AnimalStatus.Boarded;
            AnimalSim.Prime(young, sp, rules);
            AnimalWelfare.Prime(young, rules);
            if (!book.Add(young)) continue;
            young.Log(AnimalEventKind.Born, at, $"Born to {AnimalSim.NameIn(mother, sp)} and {(string.IsNullOrWhiteSpace(p.fatherName) ? "a sire" : p.fatherName)}.", "", rules.keepEvents);
            litter.Add(young);
            born?.Add(young);
        }
        float kin = rules.social != null ? rules.social.kinAtBirth : 80f, mates = rules.social != null ? rules.social.littermatesAtBirth : 60f;
        foreach (AnimalRecord y in litter)
        {
            AnimalSocial.Bond(mother, y, AnimalBondKind.Kin, kin, at);
            AnimalSocial.Bond(y, mother, AnimalBondKind.Kin, kin, at);
            foreach (AnimalRecord z in litter)
                if (z != y) AnimalSocial.Bond(y, z, AnimalBondKind.Kin, mates, at);
        }
        int count = litter.Count;
        mother.offspring += count;
        if (sire != null)
        {
            sire.offspring += count;
            sire.revision++;
        }
        mother.breedReadyAt = at + s.cooldownHours;
        string young1 = string.IsNullOrWhiteSpace(sp.youngName) ? "young one" : sp.youngName.ToLowerInvariant();
        string what = count == 1 ? $"a {young1}" : $"{count} young";
        string text = $"{AnimalSim.NameOf(mother, sp)} has {s.birthVerb}: {what}.";
        mother.Log(AnimalEventKind.GaveBirth, at, text, "", rules.keepEvents);
        ctx?.Notice(mother, AnimalEventKind.GaveBirth, text);
        mother.revision++;
        book.Urgent = true;
        return count;
    }

    // ---------------------------------------------------------------- words
    /// <summary>How long until it is due, in words ("about 9 days"), or "any time now".</summary>
    public static string DueIn(AnimalRecord a, double now)
    {
        if (!Expecting(a)) return "";
        double left = a.pregnancy.dueAt - now;
        return left <= 1.0 ? "any time now" : "due in about " + AnimalObservation.Wait(left);
    }

    /// <summary>What a keen owner knows of its inheritance, in words ("Fertility: good").</summary>
    public static void TraitWords(AnimalGenes g, List<string> into)
    {
        if (g == null || into == null) return;
        into.Add($"Size: {AnimalGenes.Word(g.size)}");
        into.Add($"Growth: {AnimalGenes.Word(g.growth)}");
        into.Add($"Thrift (feed): {AnimalGenes.Word(g.efficiency)}");
        into.Add($"Hardiness: {AnimalGenes.Word(g.resistance)}");
        into.Add($"Fertility: {AnimalGenes.Word(g.fertility)}");
        into.Add($"Temper: {(g.temperament >= 0.62f ? "docile" : g.temperament >= 0.38f ? "steady" : "skittish")}");
        into.Add($"What it gives: {AnimalGenes.Word(g.productionQuality)} quality, {AnimalGenes.Word(g.productionRate)} yield");
        into.Add($"Longevity: {AnimalGenes.Word(g.longevity)}");
        if (g.rare != 0) into.Add("It carries a rare trait.");
        if (g.inbreeding >= 0.125f) into.Add("Inbred: its parents were close kin.");
    }
}
