using System.Collections.Generic;

namespace RuneMagic
{
    // Built-in Spelling System data (design spec 16.5): the keys, the score, the numbers a spell's difficulty is generated from and
    // a few example rules. The default global mode is Selected (decision D9): only the spells the rules mark use it, and the rules
    // mark the heavier forms. Switch the mode to None to remove the mechanic, to All to make every spell use it.
    public static partial class RuneDefaults
    {
        public static SpellingBalance Spelling()
        {
            var s = new SpellingBalance
            {
                mode = SpellingMode.Selected,
                failureModel = SpellingFailureModel.Lenient,
                candidateKeys = new List<string>(),
                // Keys the game reads outside the Input System's actions (digits and quick slots, Y/N prompts, T, F-keys, Esc, Tab, Enter, Space...).
                reservedKeys = new List<string>
                {
                    "Y", "N", "T", "Space", "Tab", "Enter", "Escape", "Backspace", "LeftShift", "RightShift", "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt",
                    "Digit0", "Digit1", "Digit2", "Digit3", "Digit4", "Digit5", "Digit6", "Digit7", "Digit8", "Digit9",
                    "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                },
                movementActions = new List<string> { "Move", "Movement", "Walk" },
                weights = new List<SpellingWeight>
                {
                    new SpellingWeight("accuracy", 0.5f), new SpellingWeight("speed", 0.2f),
                    new SpellingWeight("precision", 0.2f), new SpellingWeight("rhythm", 0.1f),
                },
            };
            for (char c = 'A'; c <= 'Z'; c++) s.candidateKeys.Add(c.ToString());
            return s;
        }

        /// <summary>Example rules: the heavier forms use the Spelling System in Selected mode.</summary>
        public static List<SpellingRule> SpellingRules() => new List<SpellingRule>
        {
            new SpellingRule { scope = SpellingScope.Form, id = "blast", use = SpellingUse.Yes, damageMode = SpellingDamageChoice.OnceOnFailure, damageAmount = 3f },
            new SpellingRule { scope = SpellingScope.Form, id = "vortex", use = SpellingUse.Yes, damageMode = SpellingDamageChoice.OnceOnFailure, damageAmount = 3f },
            new SpellingRule { scope = SpellingScope.Form, id = "beam", use = SpellingUse.Yes, rhythm = SpellingUse.Yes },
            new SpellingRule { scope = SpellingScope.Form, id = "wall", use = SpellingUse.Yes, failureModel = SpellingFailureChoice.Strict, windowMultiplier = 1.3f },
            new SpellingRule { scope = SpellingScope.Form, id = "cage", use = SpellingUse.Yes, damageMode = SpellingDamageChoice.FlatPerMistake, damageAmount = 1f },
        };

        /// <summary>
        /// How a Soul Integrity condition changes the Spelling System (design spec 16.5: a Strained or worse soul increases the key
        /// count, shortens the windows or increases the penalties): extra letters, window multiplier, penalty multiplier.
        /// </summary>
        internal static void SpellingOfCondition(SoulCondition c)
        {
            switch (c.id)
            {
                case "strained": c.spellingExtraLetters = 1; c.spellingWindowMultiplier = 0.95f; c.spellingPenaltyMultiplier = 1.1f; break;
                case "damaged": c.spellingExtraLetters = 1; c.spellingWindowMultiplier = 0.9f; c.spellingPenaltyMultiplier = 1.25f; break;
                case "critical": c.spellingExtraLetters = 2; c.spellingWindowMultiplier = 0.8f; c.spellingPenaltyMultiplier = 1.5f; break;
                case "near_collapse": c.spellingExtraLetters = 2; c.spellingWindowMultiplier = 0.7f; c.spellingPenaltyMultiplier = 2f; break;
            }
        }
    }
}
