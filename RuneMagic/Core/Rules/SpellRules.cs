using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// Spells made by synthesis and the runes they anchor (decision D3): a spell keeps the runes it was made from in the
    /// soul and draws their energy when cast, so a rune that anchors a spell cannot be consumed (rank advancement,
    /// composite runes) or released until the spell is dismissed. If an anchored rune is destroyed anyway (a catastrophic
    /// failure), the spells that depended on it are lost with it. Pure functions over <see cref="RuneSoulState"/>.
    /// </summary>
    public static class SpellRules
    {
        /// <summary>The spells that anchor the rune <paramref name="runeInstanceId"/>.</summary>
        public static List<SpellRecord> AnchoredBy(RuneSoulState s, string runeInstanceId)
        {
            var list = new List<SpellRecord>();
            if (s?.spells == null || string.IsNullOrEmpty(runeInstanceId)) return list;
            foreach (SpellRecord sp in s.spells)
                if (sp != null && sp.anchors != null && sp.anchors.Contains(runeInstanceId)) list.Add(sp);
            return list;
        }

        public static bool IsAnchored(RuneSoulState s, string runeInstanceId)
        {
            if (s?.spells == null || string.IsNullOrEmpty(runeInstanceId)) return false;
            foreach (SpellRecord sp in s.spells)
                if (sp != null && sp.anchors != null && sp.anchors.Contains(runeInstanceId)) return true;
            return false;
        }

        /// <summary>The spell with this id (null when none).</summary>
        public static SpellRecord Find(RuneSoulState s, string spellId)
        {
            if (s?.spells == null || string.IsNullOrEmpty(spellId)) return null;
            foreach (SpellRecord sp in s.spells)
                if (sp != null && sp.spellId == spellId) return sp;
            return null;
        }

        /// <summary>Dismisses a spell: its runes are free again (they keep their energy). Returns the spell, or null when there is none.</summary>
        public static SpellRecord Dismiss(RuneSoulState s, string spellId)
        {
            SpellRecord sp = Find(s, spellId);
            if (sp == null) return null;
            s.spells.Remove(sp);
            return sp;
        }

        /// <summary>
        /// Removes the spells whose anchored runes are no longer all in the soul (after runes were destroyed). Returns their names.
        /// </summary>
        public static List<string> RemoveBroken(RuneSoulState s)
        {
            var lost = new List<string>();
            if (s?.spells == null) return lost;
            for (int i = s.spells.Count - 1; i >= 0; i--)
            {
                SpellRecord sp = s.spells[i];
                bool broken = false;
                if (sp.anchors != null)
                    foreach (string id in sp.anchors)
                        if (s.Rune(id) == null) { broken = true; break; }
                if (!broken) continue;
                lost.Add(sp.name);
                s.spells.RemoveAt(i);
            }
            lost.Reverse();
            return lost;
        }

        /// <summary>Energy the runes anchoring <paramref name="sp"/> hold together (what its casts can draw on).</summary>
        public static float AnchoredEnergy(RuneSoulState s, SpellRecord sp)
        {
            float sum = 0f;
            if (s == null || sp?.anchors == null) return 0f;
            foreach (string id in sp.anchors)
            {
                RuneInstance r = s.Rune(id);
                if (r != null) sum += r.energy;
            }
            return sum;
        }
    }
}
