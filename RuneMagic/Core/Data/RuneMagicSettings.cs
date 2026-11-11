using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Who uses the Rune Magic System and how it sits next to the ability (essence) absorption of the other classes.
    /// Rune users are players of the Mage class (design directive 2); they can never absorb abilities (directive 3) -
    /// that rule is not a setting.
    /// </summary>
    [Serializable]
    public class RuneMagicSettings
    {
        [Tooltip("Turn the whole Rune Magic System on or off (off: nobody is a rune user, everyone absorbs abilities as before).")]
        public bool enabled = true;

        [Tooltip("Classes whose players use Rune Magic, by name: a class matches by its asset name, its display name or the " +
                 "name of its Class archetype (case does not matter). The Rune Magic Database can also list class assets.")]
        public List<string> runeUserClassNames = new List<string> { "Mage" };

        [Tooltip("Also match the Class archetypes a character has (classes defined only as an archetype, or a class whose " +
                 "archetype is the Mage preset).")]
        public bool matchClassArchetypes = true;

        [Tooltip("When a rune user kills a mob, its absorbable abilities still roll and drop as pickups for the other " +
                 "players (the rune user is never the one who takes them). Off: a rune user's kills roll no absorption at all.")]
        public bool essenceRollsOnRuneUserKills = true;

        [Tooltip("A rune user whose player object has no Rune Soul gets one when the game starts (with a warning): their soul " +
                 "is kept and its rules run. Add Rune Magic to the player prefab (Tools ▸ SimpleMovements ▸ Rune Magic) for the " +
                 "HUD gauge, meditation input and network replication.")]
        public bool addMissingSoulComponents = true;

        [Tooltip("Development builds and the editor: the Debug Panel input action shows the Rune Magic debug panel (the local " +
                 "player's soul; tools to damage, heal and test it where the rules run).")]
        public bool debugPanel = true;

        [Header("Input actions (names in the player's Input Actions asset)")]
        [Tooltip("Rune Magic reads its inputs only from these actions of the Input Actions asset: no key is fixed in code. " +
                 "Add them to the Player map and bind them there (Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input " +
                 "Actions adds missing ones without a key); players rebind them in Key Bindings. An action that is missing does nothing.")]
        public string meditateAction = "Meditate";
        [Tooltip("Opens and closes the Rune Soul window.")]
        public string runeSoulAction = "RuneSoul";
        [Tooltip("Opens and closes the debug panel (development builds and the editor).")]
        public string debugPanelAction = "RuneMagicDebug";
        [Tooltip("Opens and closes the Rune Synthesis window (the Rune Soul window also has a button for it).")]
        public string synthesisAction = "Synthesis";
        [Tooltip("Opens and closes the Spellbook (the Rune Soul window also has a button for it). Spells are CAST with the ability keys " +
                 "(Ability1, Ability2... of the Input Actions asset): this action only opens the book where they are put on those keys.")]
        public string spellbookAction = "Spellbook";
        [Tooltip("Opens and closes the Grimoire window (the Rune Soul window also has a button for it): the book a Mage starts with, where runes of " +
                 "the soul are imprinted and prints absorbed.")]
        public string grimoireAction = "Grimoire";

        /// <summary>The input action names Rune Magic reads (none empty).</summary>
        public IEnumerable<string> InputActionNames()
        {
            if (!string.IsNullOrWhiteSpace(meditateAction)) yield return meditateAction.Trim();
            if (!string.IsNullOrWhiteSpace(runeSoulAction)) yield return runeSoulAction.Trim();
            if (!string.IsNullOrWhiteSpace(synthesisAction)) yield return synthesisAction.Trim();
            if (!string.IsNullOrWhiteSpace(spellbookAction)) yield return spellbookAction.Trim();
            if (!string.IsNullOrWhiteSpace(grimoireAction)) yield return grimoireAction.Trim();
            if (!string.IsNullOrWhiteSpace(debugPanelAction)) yield return debugPanelAction.Trim();
        }
    }

    /// <summary>The rule that decides from a character's class whether it is a rune user (pure, testable).</summary>
    public static class RuneUserRules
    {
        /// <summary>
        /// Is a class with these names a rune-user class? <paramref name="classAssetName"/> is the class asset's name (what
        /// character saves store), <paramref name="classDisplayName"/> its shown name, <paramref name="classArchetypeNames"/>
        /// the names of the character's Class archetypes (used when <see cref="RuneMagicSettings.matchClassArchetypes"/>).
        /// </summary>
        public static bool IsRuneUserClass(RuneMagicSettings settings, string classAssetName, string classDisplayName,
            IEnumerable<string> classArchetypeNames = null)
        {
            if (settings == null || !settings.enabled || settings.runeUserClassNames == null)
                return false;
            if (Listed(settings, classAssetName) || Listed(settings, classDisplayName))
                return true;
            if (settings.matchClassArchetypes && classArchetypeNames != null)
                foreach (string a in classArchetypeNames)
                    if (Listed(settings, a)) return true;
            return false;
        }

        /// <summary>Is <paramref name="name"/> one of the rune-user class names (trimmed, case-insensitive)?</summary>
        public static bool Listed(RuneMagicSettings settings, string name)
        {
            if (settings == null || settings.runeUserClassNames == null || string.IsNullOrWhiteSpace(name))
                return false;
            string n = name.Trim();
            foreach (string s in settings.runeUserClassNames)
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s.Trim(), n, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
