using System.Collections.Generic;
using UnityEngine;

/// <summary>The kinds of harvesting tool the presets make.</summary>
public enum HarvestToolPreset
{
    SkinningKnife,
    ButcherKnife,
    Cleaver,
    HuntingKnife,
    Lancet,
    BoneSaw,
    Scraper,
    Pliers,
}

/// <summary>
/// A harvesting tool carried in the inventory: a skinning knife, a butcher's knife, a lancet, a bone saw, a scraper,
/// pliers... It is not held: the harvest picks the fittest tool the player carries for each piece of work. How fit it
/// is depends on what it is made for, its edge and how well it is made; its edge is its
/// durability, worn a little by every harvest it is used for (a dull knife makes the margins narrow). A knife or an axe
/// in the bag works too, after a fashion; the bare hands can only pull and pry.
/// </summary>
[CreateAssetMenu(fileName = "Harvesting Tool", menuName = "SimpleMovements/Harvesting/Harvesting Tool", order = 4)]
public class HarvestToolSO : ItemSO
{
    [Header("Harvesting tool")]
    [Tooltip("How well it does each kind of work (0 = not at all .. 1 = made for it).")]
    [SerializeField] private HarvestToolFit fit = new HarvestToolFit(1f, 0.4f, 0.4f, 0f, 0.3f, 0f, 0f);
    [Tooltip("How well made (0 crude .. 1 masterwork): steadier, cleaner work.")]
    [SerializeField, Range(0f, 1f)] private float quality = 0.6f;
    [Tooltip("How fast it works (× progress).")]
    [SerializeField, Range(0.25f, 3f)] private float speed = 1f;

    public HarvestToolFit Fit => fit ??= new HarvestToolFit();
    public float Quality => quality;
    public float Speed => speed;

    public HarvestToolSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 0.3f;
        maxDurability = 80;
        durability = 80;
        durabilityReductionPerUse = 1;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    /// <summary>The tool as the harvest sees it, for the copy <paramref name="copy"/> (its edge from its durability).</summary>
    public HarvestToolProfile Profile(InventoryItem copy, string id)
    {
        float edge = 1f;
        if (copy != null && MaxDurability > 1)
            edge = Mathf.Clamp01(copy.durability / MaxDurability);
        return new HarvestToolProfile { id = id ?? "", displayName = Name, fit = Fit.Clone(), sharpness = edge, quality = quality, speed = speed };
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Harvesting tool (carried)");
        lines.Add(new HarvestToolProfile { fit = Fit }.DescribeFit());
        if (quality >= 0.8f) lines.Add("Finely made");
        else if (quality < 0.35f) lines.Add("Crudely made");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (Fit.Kinds(0.2f) == HarvestToolKind.None)
            warnings.Add("It is fit for no harvesting work (every Fit value is under 0.2).");
    }

    /// <summary>Fills it as a typical tool of <paramref name="kind"/> (the editor's Create Tools).</summary>
    public void ApplyPreset(HarvestToolPreset kind)
    {
        switch (kind)
        {
            case HarvestToolPreset.SkinningKnife:
                Set("Skinning knife", "Short, curved, keen, with a rounded tip that slides under a hide without piercing it.", new HarvestToolFit(1f, 0.45f, 0.45f, 0f, 0.35f, 0f, 0f), 0.6f, 1f, 6f, 0.25f, 80);
                break;
            case HarvestToolPreset.ButcherKnife:
                Set("Butcher's knife", "A long, stiff blade for cutting meat off the bone and finding the joints.", new HarvestToolFit(0.4f, 1f, 0.2f, 0f, 0.25f, 0f, 0.35f), 0.6f, 1f, 6f, 0.35f, 90);
                break;
            case HarvestToolPreset.Cleaver:
                Set("Cleaver", "A heavy chopping blade: fast through joints and small bones, crude everywhere else.", new HarvestToolFit(0.1f, 0.6f, 0f, 0.35f, 0.1f, 0f, 1f), 0.5f, 1.2f, 7f, 0.8f, 120);
                break;
            case HarvestToolPreset.HuntingKnife:
                Set("Hunting knife", "The hunter's all-round blade: it skins, butchers and does fine work - none as well as the proper tool.", new HarvestToolFit(0.75f, 0.7f, 0.55f, 0f, 0.3f, 0f, 0.2f), 0.55f, 1f, 8f, 0.3f, 70);
                break;
            case HarvestToolPreset.Lancet:
                Set("Lancet", "A small, very keen blade for organs, glands and tendons.", new HarvestToolFit(0.3f, 0f, 1f, 0f, 0f, 0f, 0f), 0.7f, 0.9f, 10f, 0.05f, 50);
                break;
            case HarvestToolPreset.BoneSaw:
                Set("Bone saw", "A fine-toothed saw: bones, horns and antlers come off clean.", new HarvestToolFit(0f, 0.2f, 0f, 1f, 0f, 0f, 0.2f), 0.6f, 1f, 9f, 0.5f, 120);
                break;
            case HarvestToolPreset.Scraper:
                Set("Fleshing scraper", "A dull curved blade with two handles: cleans bones and scrapes fat and scales.", new HarvestToolFit(0.15f, 0.1f, 0f, 0f, 1f, 0f, 0f), 0.55f, 1.1f, 4f, 0.3f, 150);
                break;
            default:
                Set("Pliers", "Iron pliers: teeth, claws and scales come loose; tendons and organs come out.", new HarvestToolFit(0f, 0f, 0.15f, 0f, 0f, 1f, 0f), 0.6f, 1f, 5f, 0.3f, 200);
                break;
        }
    }

    private void Set(string toolName, string text, HarvestToolFit f, float q, float s, float value, float mass, int uses)
    {
        name = toolName;
        description = text;
        fit = f;
        quality = q;
        speed = s;
        price = value;
        weight = mass;
        maxDurability = uses;
        durability = uses;
        durabilityReductionPerUse = 1;
        shouldBeDestroyedOn0UsesLeft = false;
        stackMax = 1;
        itemType = ItemType.Miscellaneous;
    }
}
