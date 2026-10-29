using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The body of one kind of creature, as an asset: its plan, size, regions (where hits land, weak points, what each is
/// made of, where it lies on the hide) and every material it gives (how each is taken, its difficulty, tools and skill,
/// its yield, value and rarity). New creatures reuse every mechanic: they only describe their own body. Start from a
/// built-in one (the inspector's Start From) and register it in the Harvest Database.
/// </summary>
[CreateAssetMenu(fileName = "CreatureAnatomy", menuName = "SimpleMovements/Harvesting/Creature Anatomy", order = 1)]
public class CreatureAnatomy : ScriptableObject
{
    [Tooltip("The creature's body and what it gives.")]
    public HarvestAnatomyData data = new HarvestAnatomyData();

    [Tooltip("Mobs of these Mob Profiles use this anatomy (besides the anatomy's Mob Types words).")]
    public List<MobProfile> mobProfiles = new List<MobProfile>();

    public string Id => data != null ? data.id : "";

    /// <summary>Fills the asset with a built-in creature (bear, wolf, deer, boar, rabbit, sheep, drake, troll, eagle, salmon, giant_spider, giant_serpent).</summary>
    public bool StartFrom(string builtInId)
    {
        HarvestAnatomyData d = HarvestDefaults.Anatomy(builtInId);
        if (d == null)
            return false;
        data = d;
        return true;
    }

    private void Reset() => StartFrom("wolf");

    /// <summary>Setup problems: the anatomy's own, and its mob profiles.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (data == null)
        {
            errors.Add("No anatomy.");
            return;
        }
        data.Validate(errors, warnings);
        if ((data.mobTypes == null || data.mobTypes.Count == 0) && (mobProfiles == null || mobProfiles.Count == 0))
            warnings.Add("No Mob Types and no Mob Profiles: mobs only use it when their Harvestable Creature names it.");
    }
}
