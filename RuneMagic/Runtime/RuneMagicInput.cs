using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneMagic
{
    /// <summary>
    /// Every Rune Magic input comes from the Input System: an action of the player's Input Actions asset (the generated
    /// PlayerInput), found by the name the Rune Magic Database gives it (Settings ▸ Input actions), or an Input Action
    /// assigned on the component. No key is fixed in code: an action that is not in the asset is simply not available (a
    /// warning says so, once). Keys are chosen in the Input Actions editor and rebound by players in Key Bindings, like
    /// every other action.
    /// <para>Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions adds the missing actions to the asset,
    /// without a key, ready to bind.</para>
    /// </summary>
    public static class RuneMagicInput
    {
        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string[] NoControls = new string[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => warned.Clear();

        private static RuneMagicSettings Settings => RuneMagicDatabase.Instance.Catalog.Settings;

        /// <summary>The Meditate action's name.</summary>
        public static string MeditateAction => Name(Settings.meditateAction, "Meditate");
        /// <summary>The Rune Soul window's action name.</summary>
        public static string RuneSoulAction => Name(Settings.runeSoulAction, "RuneSoul");
        /// <summary>The debug panel's action name.</summary>
        public static string DebugPanelAction => Name(Settings.debugPanelAction, "RuneMagicDebug");
        /// <summary>The Rune Synthesis window's action name.</summary>
        public static string SynthesisAction => Name(Settings.synthesisAction, "Synthesis");
        /// <summary>The Spellbook's action name.</summary>
        public static string SpellbookAction => Name(Settings.spellbookAction, "Spellbook");
        /// <summary>The Grimoire window's action name.</summary>
        public static string GrimoireAction => Name(Settings.grimoireAction, "Grimoire");

        private static string Name(string configured, string fallback) => string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

        /// <summary>
        /// A gameplay input of one player (paused with the gameplay keys): <paramref name="assigned"/> when set, else the
        /// action named <paramref name="actionName"/> in the player's input actions. Never a default key. Dispose it.
        /// </summary>
        public static CombatInputBinding Player(Component owner, InputActionReference assigned, string actionName, string label)
        {
            CombatInputBinding b = CombatInputBinding.Create(owner, assigned, new[] { actionName }, NoControls, label);
            if (b != null && !b.HasAny)
                Warn(actionName, label, owner);
            return b;
        }

        /// <summary>
        /// An input that is not one player's (a window, a debug toggle): the action named <paramref name="actionName"/> in
        /// the input actions, read from a copy pausing never turns off. Never created, never a default key. Dispose it.
        /// </summary>
        public static GlobalInputBinding Global(string actionName, string label, UnityEngine.Object context, bool quietWhilePaused = true)
        {
            GlobalInputBinding b = GlobalInputBinding.Create(new[] { actionName }, NoControls, label, false, quietWhilePaused, context);
            if (b != null && b.Action == null)
                Warn(actionName, label, context);
            return b;
        }

        /// <summary>The key shown for an input ("V", "Shift+R"), or <paramref name="unbound"/> when it has none.</summary>
        public static string KeyLabel(InputAction action, string unbound = "unbound")
        {
            if (action == null) return unbound;
            string s;
            try { s = action.GetBindingDisplayString(0); }
            catch (ArgumentException) { s = ""; }
            return string.IsNullOrWhiteSpace(s) ? unbound : HudStyle.ShortKey(s);
        }

        private static void Warn(string actionName, string label, UnityEngine.Object context)
        {
            if (string.IsNullOrEmpty(actionName) || !warned.Add(actionName))
                return;
            Debug.LogWarning($"[Rune Magic] {label}: the Input Actions have no '{actionName}' action, so this input is not available. " +
                             "Add it to the Player map of your Input Actions asset and bind it there (Tools ▸ SimpleMovements ▸ Rune Magic ▸ " +
                             "Add Rune Magic Input Actions adds it without a key); the action name is set in the Rune Magic Database ▸ Settings.", context);
        }
    }
}
