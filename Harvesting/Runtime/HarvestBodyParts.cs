using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Body-part profiles made from creature anatomies: a part for every region - where hits land on it (its height and,
/// for horizontal bodies, length bands, and its side), how much damage they deal there (weak points: a bear's head ×1.5,
/// a deer's ribs over the heart ×1.3, a drake's soft belly), its natural armour (a drake's scales, a spider's shell) and
/// what a hit there does (a head hit staggers, a leg or wing hit slows). Hits are then located on the regions harvesting
/// reads, so the body remembers exactly where it was struck.
/// </summary>
public static class HarvestBodyParts
{
    /// <summary>Defense one point of a region's natural armour (0-1) adds (with Defense Half Value 100, armour 0.5 takes 23% off).</summary>
    public const float ArmorDefense = 60f;

    private static readonly Dictionary<string, BodyPartProfile> cache = new Dictionary<string, BodyPartProfile>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    /// <summary>
    /// Forgets the profiles made so far (made again on demand from the creatures as they are then); outside Play Mode they
    /// are destroyed. For tests and tools that change the creatures between uses.
    /// </summary>
    public static void ClearCaches()
    {
        if (!Application.isPlaying)
            foreach (BodyPartProfile p in cache.Values)
                if (p != null) UnityEngine.Object.DestroyImmediate(p);
        cache.Clear();
    }

    /// <summary>The part name of a region: its first combat body part, else a name from its id ("foreleg_l" → "LeftForeleg").</summary>
    public static string PartName(HarvestRegion r)
    {
        if (r == null)
            return "";
        if (r.bodyParts != null)
            foreach (string b in r.bodyParts)
                if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        string n = HarvestNames.FromId(r.id).Replace(" ", "");
        return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : r.id;
    }

    /// <summary>A profile made at runtime for <paramref name="a"/> (cached; not saved).</summary>
    public static BodyPartProfile ProfileFor(HarvestAnatomyData a)
    {
        if (a == null)
            return null;
        if (cache.TryGetValue(a.id, out BodyPartProfile p) && p != null)
            return p;
        p = ScriptableObject.CreateInstance<BodyPartProfile>();
        p.name = $"{a.Name} (from its anatomy)";
        p.hideFlags = HideFlags.DontSave;
        Fill(p, a);
        cache[a.id] = p;
        return p;
    }

    /// <summary>Fills <paramref name="p"/> with a part for each region of <paramref name="a"/>.</summary>
    public static void Fill(BodyPartProfile p, HarvestAnatomyData a)
    {
        p.parts = new List<BodyPartDefinition>();
        if (a?.regions == null)
            return;
        foreach (HarvestRegion r in a.regions)
        {
            if (r == null) continue;
            string name = PartName(r);
            var part = new BodyPartDefinition
            {
                name = name,
                aliases = new List<string>(),
                heightRange = new Vector2(Mathf.Clamp01(r.height.x), Mathf.Clamp01(r.height.y)),
                lengthRange = IsWholeLength(r.length) ? Vector2.zero : new Vector2(Mathf.Clamp01(r.length.x), Mathf.Clamp01(r.length.y)),
                side = r.side,
                sideOffset = r.sideOffset,
                damageMultiplier = Mathf.Max(0f, r.damageMultiplier),
                bonusDefense = r.bonusDefense + r.armor * ArmorDefense,
                bonusMagicResistance = r.armor * ArmorDefense * 0.5f,
                protectedBy = new List<ArmorSlotType>(),
                effects = new List<BodyPartEffect>(),
                redirectWhenSevered = string.IsNullOrWhiteSpace(r.redirectWhenSevered) ? "" : r.redirectWhenSevered,
            };
            if (r.bodyParts != null)
                foreach (string b in r.bodyParts)
                    if (!string.IsNullOrWhiteSpace(b) && !string.Equals(b.Trim(), name, StringComparison.OrdinalIgnoreCase) && !part.aliases.Contains(b.Trim()))
                        part.aliases.Add(b.Trim());
            if (!string.Equals(r.id, name, StringComparison.OrdinalIgnoreCase))
                part.aliases.Add(r.id);
            switch (r.reaction)
            {
                case HarvestRegionReaction.Stagger:
                    part.effects.Add(new StaggerBodyPartEffect { chance = 0.35f, minDamageFraction = 0.05f, duration = 0.4f });
                    break;
                case HarvestRegionReaction.Slow:
                    part.effects.Add(new ImpairMovementBodyPartEffect { chance = 0.3f, minDamageFraction = 0.05f, slow = 0.3f, duration = 2f });
                    break;
            }
            p.parts.Add(part);
        }
        int d = a.DefaultRegionIndex;
        p.defaultPart = d >= 0 ? PartName(a.regions[d]) : (p.parts.Count > 0 ? p.parts[0].name : "Torso");
        p.otherArmorShare = 0.25f;
        p.areaDamageUsesDefaultPart = true;
        p.whenSeveralParts = SeveralPartsRule.NearestToContact;
    }

    private static bool IsWholeLength(Vector2 l) => (l.x <= 0.001f && l.y >= 0.999f) || (l.x == 0f && l.y == 0f);
}
