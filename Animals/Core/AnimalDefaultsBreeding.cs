using System.Collections.Generic;

/// <summary>
/// The built-in breeding, behaviour and affinity perks (Phase 4). Time is game hours, scaled to the game's animals (a hen's
/// clutch hatches in three days, a cow calves in a fortnight: they live a few months).
/// </summary>
public static partial class AnimalDefaults
{
    /// <summary>How a built-in species breeds.</summary>
    public static AnimalBreedingSpec BreedingOf(string species)
    {
        switch (species)
        {
            case "chicken": return Breeding(72f, 2, 5, 96f, 12f, false, true, "hatched a clutch", "broody");
            case "duck": return Breeding(84f, 2, 5, 96f, 12f, false, true, "hatched a clutch", "broody");
            case "rabbit": return Breeding(48f, 3, 6, 72f, 12f, true, true, "kindled", "expecting");
            case "sheep": return Breeding(240f, 1, 2, 336f, 24f, true, true, "lambed", "in lamb");
            case "goat": return Breeding(240f, 1, 3, 336f, 24f, true, true, "kidded", "in kid");
            case "pig": return Breeding(192f, 3, 6, 240f, 24f, true, true, "farrowed", "in pig");
            case "cow": return Breeding(336f, 1, 1, 480f, 24f, true, true, "calved", "in calf");
            case "horse": return Breeding(480f, 1, 1, 720f, 48f, true, true, "foaled", "in foal");
            default: return new AnimalBreedingSpec();
        }
    }

    /// <summary>How a built-in species behaves.</summary>
    public static AnimalBehaviourSpec BehaviourOf(string species)
    {
        switch (species)
        {
            case "chicken": return Behaviour(0.6f, 0.4f, 0.6f, 0.4f, true);
            case "duck": return Behaviour(0.7f, 0.5f, 0.5f, 0.3f, true);
            case "rabbit": return Behaviour(0.5f, 0.6f, 0.8f, 0.2f, false);
            case "sheep": return Behaviour(0.8f, 0.1f, 0.6f, 0.2f, true);
            case "goat": return Behaviour(0.6f, 0.2f, 0.3f, 0.8f, true);
            case "pig": return Behaviour(0.6f, 0.1f, 0.3f, 0.6f, false);
            case "cow": return Behaviour(0.7f, 0.3f, 0.4f, 0.4f, true);
            case "horse": return Behaviour(0.6f, 0.3f, 0.5f, 0.5f, true);
            default: return new AnimalBehaviourSpec();
        }
    }

    /// <summary>What a bond with its owner unlocks for a built-in species.</summary>
    public static List<AnimalAffinityPerk> PerksOf(string species)
    {
        var list = new List<AnimalAffinityPerk>();
        switch (species)
        {
            case "chicken":
                list.Add(Perk("greet", "Comes running", "It runs to the fence when it sees you.", AnimalPerkKind.Greet, 40f));
                list.Add(Perk("follow", "Follows you about", "It keeps to your side of the run.", AnimalPerkKind.Follow, 60f));
                break;
            case "duck":
                list.Add(Perk("greet", "Quacks a welcome", "It hurries over, quacking, when it sees you.", AnimalPerkKind.Greet, 40f));
                list.Add(Perk("follow", "Waddles after you", "It follows you along the fence.", AnimalPerkKind.Follow, 60f));
                break;
            case "rabbit":
                list.Add(Perk("greet", "Hops to the fence", "It hops over to see you.", AnimalPerkKind.Greet, 40f));
                list.Add(Perk("calm", "Lets you hold it", "Your handling no longer frightens it.", AnimalPerkKind.CalmHandling, 70f));
                break;
            case "sheep":
                list.Add(Perk("calm", "Stands quiet for shearing", "Your handling and shearing no longer stress it.", AnimalPerkKind.CalmHandling, 60f));
                list.Add(Perk("follow", "Follows you like a lamb", "It follows you along the fence.", AnimalPerkKind.Follow, 70f));
                break;
            case "goat":
                list.Add(Perk("greet", "Comes to see what you bring", "It trots over whenever you come.", AnimalPerkKind.Greet, 30f));
                list.Add(Perk("follow", "Trots after you", "It follows you along the fence.", AnimalPerkKind.Follow, 50f));
                list.Add(Perk("calm", "Stands for milking", "Your handling and milking no longer stress it.", AnimalPerkKind.CalmHandling, 60f));
                break;
            case "pig":
                list.Add(Perk("greet", "Grunts a welcome", "It comes to the fence grunting when you come.", AnimalPerkKind.Greet, 40f));
                list.Add(Perk("follow", "Follows you about", "It follows you along the fence.", AnimalPerkKind.Follow, 60f));
                break;
            case "cow":
                list.Add(Perk("greet", "Lows a welcome", "It lows and comes over when it sees you.", AnimalPerkKind.Greet, 40f));
                list.Add(Perk("calm", "Lets down her milk for you", "Your handling and milking no longer stress it.", AnimalPerkKind.CalmHandling, 60f));
                list.Add(Perk("follow", "Follows you to the gate", "It follows you along the fence.", AnimalPerkKind.Follow, 75f));
                break;
            case "horse":
                list.Add(Perk("greet", "Whickers when it sees you", "It whickers and comes over when you come.", AnimalPerkKind.Greet, 30f));
                list.Add(Perk("call", "Comes when you call", "Call it from the pen's window and it comes to the fence where you stand.", AnimalPerkKind.ComeWhenCalled, 50f));
                list.Add(Perk("calm", "Trusts your hands", "Your handling no longer stresses it.", AnimalPerkKind.CalmHandling, 60f));
                list.Add(Perk("follow", "Follows you along the fence", "It keeps pace with you along the fence.", AnimalPerkKind.Follow, 70f));
                break;
        }
        if (list.Count > 0) list.Add(Perk("ribbon", "Devoted", "It wears a ribbon: your bond is plain for all to see.", AnimalPerkKind.Ribbon, 90f));
        return list;
    }

    private static AnimalBreedingSpec Breeding(float gestation, int min, int max, float cooldown, float sire, bool nurses, bool rival, string birth, string expecting) =>
        new AnimalBreedingSpec
        {
            enabled = true, gestationHours = gestation, litterMin = min, litterMax = max, cooldownHours = cooldown, sireCooldownHours = sire,
            nurses = nurses, nursesThrough = GrowthStage.Baby, malesRival = rival, birthVerb = birth, expectingWord = expecting,
        };

    private static AnimalBehaviourSpec Behaviour(float sociability, float grooming, float flightiness, float curiosity, bool herds) =>
        new AnimalBehaviourSpec { sociability = sociability, grooming = grooming, flightiness = flightiness, curiosity = curiosity, herds = herds, youngFollowMother = true };

    private static AnimalAffinityPerk Perk(string id, string name, string description, AnimalPerkKind kind, float from) =>
        new AnimalAffinityPerk { id = id, name = name, description = description, kind = kind, minAffinity = from };
}
