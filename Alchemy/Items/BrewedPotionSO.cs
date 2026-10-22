using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A bottle from an alchemy station: one quality band (tier) of one outcome of the Alchemy Database (Healing Draught,
/// Potent). Drinking it applies its effects (it is a <see cref="ConsumableSO"/>); poured into another vessel it
/// releases what it is made of, so brews can be ingredients of further brews. Create the assets with
/// Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Potion Items (needed for network sessions and saves); offline, a bottle
/// without an asset gets an item made at runtime.
/// </summary>
[CreateAssetMenu(fileName = "Brewed Potion", menuName = "SimpleMovements/Alchemy/Brewed Potion", order = 3)]
public class BrewedPotionSO : ConsumableSO, IAlchemyIngredient
{
    [Header("Alchemy")]
    [Tooltip("The Alchemy Database outcome this bottle is.")]
    [SerializeField] private string outcomeId = "";
    [Tooltip("Its quality band (index into the outcome's tiers).")]
    [SerializeField] private int tier;
    [Tooltip("Shown in the tooltip (\"Potent\").")]
    [SerializeField] private string tierLabel = "";
    [Tooltip("Colour of the liquid (icon and model).")]
    [SerializeField] private Color liquidColor = new Color(0.8f, 0.3f, 0.4f);
    [Tooltip("What it releases when poured into a vessel (one bottle).")]
    [SerializeField] private AlchemyIngredientProfile composition = new AlchemyIngredientProfile { dissolveSeconds = 0f, canBeGround = false };

    public string OutcomeId => outcomeId;
    public int Tier => tier;
    public string TierLabel => tierLabel;
    public Color LiquidColor => liquidColor;

    public string AlchemyId => ((Object)this).name;
    public string AlchemyName => string.IsNullOrWhiteSpace(Name) ? ((Object)this).name : Name;
    public AlchemyIngredientProfile AlchemyProfile => composition ?? (composition = new AlchemyIngredientProfile { dissolveSeconds = 0f, canBeGround = false });

    public BrewedPotionSO()
    {
        itemType = ItemType.Potion;
        stackMax = 10;
        weight = 0.3f;
        cooldown = 1f;
    }

    /// <summary>
    /// Fills the item from an outcome and tier: name, description, effects × the tier's multiplier, price, colour, and
    /// a composition of one bottle at that tier's strength (so pouring it back into a vessel gives what it was).
    /// </summary>
    public void Setup(AlchemyOutcome outcome, int tierIndex, AlchemyRules rules, Color color, Sprite itemIcon = null, GameObject model = null)
    {
        if (outcome == null)
            return;
        AlchemyOutcomeTier t = outcome.tiers != null && tierIndex >= 0 && tierIndex < outcome.tiers.Count ? outcome.tiers[tierIndex] : null;
        float multiplier = t != null ? t.effectMultiplier : 1f;
        outcomeId = outcome.id;
        tier = Mathf.Max(0, tierIndex);
        tierLabel = t != null ? t.label : "";
        name = outcome.TierName(tierIndex);
        description = outcome.description;
        itemType = outcome.kind == AlchemyOutcomeKind.Potion ? ItemType.Potion : ItemType.Miscellaneous;
        stackMax = 10;
        weight = 0.3f;
        price = Mathf.Max(0f, outcome.basePrice * Mathf.Max(0.2f, multiplier));
        liquidColor = color;
        var list = new List<ConsumableEffect>();
        if (outcome.effects != null)
            foreach (ConsumableEffect e in outcome.effects)
            {
                if (e == null) continue;
                var c = new ConsumableEffect();
                Copy(e, c);
                c.amount = e.amount * multiplier;
                c.effectName = string.IsNullOrWhiteSpace(e.effectName) ? $"{outcome.Name} {e.effectType}" : e.effectName;
                list.Add(c);
            }
        SetEffects(list);
        composition = Composition(outcome, t, rules);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    /// <summary>One bottle's content at the tier's strength: the potency substance, the rest water.</summary>
    public static AlchemyIngredientProfile Composition(AlchemyOutcome outcome, AlchemyOutcomeTier t, AlchemyRules rules)
    {
        var p = new AlchemyIngredientProfile { dissolveSeconds = 0f, temperature = 20f, canBeGround = false, observation = "the bottle empties into the vessel" };
        float units = rules != null ? (float)rules.UnitsPerBottle(outcome) : 5f;
        string solvent = rules != null && rules.IndexOf(AlchemyDefaults.Water) >= 0 ? AlchemyDefaults.Water : "";
        if (outcome.HasPotency && outcome.potency.mode == SubstanceSelectorMode.Substance && (rules == null || rules.IndexOf(outcome.potency.id) >= 0))
        {
            float strength = t != null ? Mathf.Clamp(t.minQuality + 0.15f, 0.2f, 1f) : 0.6f;
            float active = Mathf.Min(units, units * outcome.fullPotencyAt * strength);
            p.releases.Add(new IngredientRelease(outcome.potency.id, active));
            if (!string.IsNullOrEmpty(solvent) && units - active > 0.01f)
                p.releases.Add(new IngredientRelease(solvent, units - active));
        }
        else if (!string.IsNullOrEmpty(solvent))
        {
            p.releases.Add(new IngredientRelease(solvent, units));
        }
        return p;
    }

    private static void Copy(ConsumableEffect from, ConsumableEffect to)
    {
        to.effectType = from.effectType;
        to.itemType = from.itemType;
        to.effectName = from.effectName;
        to.amount = from.amount;
        to.timeBuffEffect = from.timeBuffEffect;
        to.tickCooldown = from.tickCooldown;
        to.isProcedural = from.isProcedural;
        to.isStackable = from.isStackable;
        to.randomAmount = from.randomAmount;
        to.randomTimeBuffEffect = from.randomTimeBuffEffect;
        to.randomTickCooldown = from.randomTickCooldown;
        to.minAmount = from.minAmount;
        to.maxAmount = from.maxAmount;
        to.minTimeBuffEffect = from.minTimeBuffEffect;
        to.maxTimeBuffEffect = from.maxTimeBuffEffect;
        to.minTickCooldown = from.minTickCooldown;
        to.maxTickCooldown = from.maxTickCooldown;
        to.applicationChance = from.applicationChance;
        to.minimumLevel = from.minimumLevel;
    }

    public override void AppendTooltip(List<string> lines)
    {
        AlchemyOutcome o = AlchemyDatabase.Instance.Rules.Outcome(outcomeId);
        string kind = o == null ? "Brewed" : o.kind switch
        {
            AlchemyOutcomeKind.Potion => "Potion",
            AlchemyOutcomeKind.Material => "Alchemical material",
            AlchemyOutcomeKind.Unstable => "<color=#ffcc55>Unstable compound</color>",
            AlchemyOutcomeKind.Incomplete => "<color=#9aa0a6>Incomplete preparation</color>",
            AlchemyOutcomeKind.Degraded => "<color=#9aa0a6>Spoiled preparation</color>",
            _ => "<color=#9aa0a6>Failed preparation</color>",
        };
        lines.Add(string.IsNullOrWhiteSpace(tierLabel) ? kind : $"{kind} · {tierLabel}");
        if (Effects != null)
            foreach (ConsumableEffect e in Effects)
                if (e != null)
                    lines.Add(DescribeEffect(e));
        lines.Add("<color=#9aa0a6>Can be poured into an alchemy vessel.</color>");
        base.AppendTooltip(lines);
    }

    /// <summary>"+35 health", "+2 health regeneration over 12 s", "−25% damage taken for 60 s".</summary>
    public static string DescribeEffect(ConsumableEffect e)
    {
        string what = EffectName(e.effectType, out bool percent);
        float amount = e.amount;
        string sign = amount >= 0 ? "+" : "−";
        var sb = new StringBuilder();
        sb.Append(amount >= 0 ? "<color=#8fd68f>" : "<color=#f0a090>");
        sb.Append(sign).Append(Mathf.Abs(amount).ToString(Mathf.Abs(amount) >= 10f ? "0" : "0.#"));
        if (percent) sb.Append('%');
        sb.Append(' ').Append(what);
        if (e.timeBuffEffect > 0f)
            sb.Append(e.isProcedural && !percent ? $" over {e.timeBuffEffect:0} s" : $" for {e.timeBuffEffect:0} s");
        sb.Append("</color>");
        return sb.ToString();
    }

    private static string EffectName(ConsumableEffectType t, out bool percent)
    {
        percent = t.ToString().Contains("Factor");
        switch (t)
        {
            case ConsumableEffectType.Hp: return "health";
            case ConsumableEffectType.HpRegeneration: return "health regeneration";
            case ConsumableEffectType.HpDamageFactor: return "damage taken";
            case ConsumableEffectType.Stamina: return "stamina";
            case ConsumableEffectType.StaminaRegeneration: return "stamina regeneration";
            case ConsumableEffectType.Mana: return "mana";
            case ConsumableEffectType.ManaRegeneration: return "mana regeneration";
            case ConsumableEffectType.Sanity: return "sanity";
            case ConsumableEffectType.BodyHeat: return "body heat";
            case ConsumableEffectType.Oxygen: return "breath";
            case ConsumableEffectType.OxygenDamageFactor: return "breath loss";
            case ConsumableEffectType.SpeedFactor: return "speed";
            case ConsumableEffectType.Drink: return "thirst quenched";
            case ConsumableEffectType.Food: return "hunger sated";
            default:
                var sb = new StringBuilder();
                string s = t.ToString();
                for (int i = 0; i < s.Length; i++)
                {
                    if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(s[i]));
                }
                return sb.ToString();
        }
    }

    // ------------------------------------------------------------------ runtime potions (no asset)
    /// <summary>A bottle item made at runtime for an outcome tier (offline projects without potion assets).</summary>
    internal static BrewedPotionSO CreateRuntime(AlchemyOutcome outcome, int tierIndex, AlchemyRules rules, Color color)
    {
        var s = CreateInstance<BrewedPotionSO>();
        ((Object)s).name = $"{outcome.id}_{tierIndex}";
        s.hideFlags = HideFlags.DontSave;
        s.Setup(outcome, tierIndex, rules, color, AlchemyArt.Icon(AlchemyArt.Shape.Vial, color), AlchemyArt.Template(AlchemyArt.Shape.Vial, color));
        return s;
    }
}
