using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A food animals can be given, as data: what one portion fills (satiety), what it waters, what it feeds (nutrients),
/// and what it does besides (a toxin dose, an upset stomach). What the food is to each species is the species' diet
/// (<see cref="AnimalDiet"/>): the same hay is a favourite of one and inedible to another. Items count as a food through
/// <see cref="items"/> (the item ids that are it) - feed items of this system, a farm's produce, harvested meat.
/// <para>The rules keep apart <b>how full</b> it makes an animal (satiety), <b>how well</b> it feeds it (nutrients: an
/// animal can be full and badly nourished), <b>whether it likes it</b> (the diet) and <b>what else it does</b>
/// (toxins, irritation).</para>
/// </summary>
[Serializable]
public class AnimalFoodData
{
    public string id = "";
    public string name = "";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("What kind of food it is (\"grain\", \"forage\", \"meat\", \"poisonous\"...): diets name tags as #grain.")]
    public List<string> tags = new List<string>();
    [Tooltip("Item ids that are this food (\"animal:feed/hay\", \"agri:produce/wheat\").")]
    public List<string> items = new List<string>();

    [Header("One portion")]
    [Tooltip("Portions in one item (one copy of the item in a stack).")]
    [Min(1)] public int portionsPerItem = 1;
    [Tooltip("Satiety (0-100) one portion gives an adult of an average species, before its appetite.")]
    [Min(0f)] public float satiety = 12f;
    [Tooltip("Hydration (0-100) one portion gives (fresh greens carry water).")]
    [Min(0f)] public float hydration;
    [Tooltip("The nutrients in one portion.")]
    public NutrientProfile nutrients = new NutrientProfile(3f, 1f, 2f, 0.1f, 0.1f);
    [Tooltip("A copy's quality (farm produce, Phase 3 products) scales its nutrients.")]
    public bool qualityMatters = true;

    [Header("Besides")]
    [Tooltip("Toxin dose of one portion (poisonous plants, spoiled feed).")]
    [Min(0f)] public float toxin;
    [Tooltip("Digestive irritation (0-100) of one portion.")]
    [Min(0f)] public float irritation;
    [Tooltip("Game hours it stays fresh in a feeder (0 = keeps). After it, its quality falls by half every as many hours.")]
    [Min(0f)] public float freshHours;

    public bool HasTag(string tag)
    {
        if (tags == null || string.IsNullOrEmpty(tag)) return false;
        for (int i = 0; i < tags.Count; i++)
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool IsItem(string itemId)
    {
        if (items == null || string.IsNullOrEmpty(itemId)) return false;
        for (int i = 0; i < items.Count; i++)
            if (string.Equals(items[i], itemId, StringComparison.Ordinal)) return true;
        return false;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? id : name;

    /// <summary>The quality of a lot of this food that has lain in a feeder for <paramref name="hours"/> (it started at <paramref name="quality"/>).</summary>
    public float QualityAfter(float quality, double hours)
    {
        if (freshHours <= 0f || hours <= freshHours) return quality;
        return quality * (float)Math.Pow(0.5, (hours - freshHours) / Math.Max(1.0, freshHours));
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        string who = string.IsNullOrEmpty(id) ? "(a food without an id)" : $"food '{id}'";
        if (string.IsNullOrWhiteSpace(id)) errors?.Add($"{who}: no id.");
        if (satiety <= 0f && hydration <= 0f) warnings?.Add($"{who}: it neither fills nor waters.");
        if (items == null || items.Count == 0) warnings?.Add($"{who}: no item is this food (it can never be given).");
    }
}

/// <summary>
/// A lot of food lying in a pen's feeder: one food, a number of portions, their quality and when they were put there
/// (fresh food wilts). Lots of the same food and nearly the same quality are kept together.
/// </summary>
[Serializable]
public class FeederLot
{
    public string food = "";
    [Min(0f)] public float portions;
    [Range(0f, 1f)] public float quality = 0.6f;
    public double since;
    [Tooltip("Who put it there (their lasting key).")]
    public string by = "";
}

/// <summary>What an animal took in at one meal or drink (the simulation's answer to the pen).</summary>
public struct AnimalIntake
{
    public string food;
    public float portions;
    public float quality;
    public FoodAffinity affinity;
}
