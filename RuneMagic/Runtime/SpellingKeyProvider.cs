using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RuneMagic
{
    /// <summary>
    /// The player's machine's side of the Spelling System's keys: which letters may be asked for, worked out from the player's LIVE key
    /// bindings (design spec 16.5, directive 9: movement keys, default WASD or rebound, must never be casting keys), and how a key is
    /// shown and read. The bindings come from a private copy of the Input Actions with the saved rebinds applied (the same way every
    /// other input of the project reads them), looked at again whenever the player rebinds a key.
    /// <para>The keys a sequence asks for are DATA (the balance's candidate keys, names of the Input System's Key enum); the keys of the
    /// game's own actions are only ever read to be left out. Keys are logic by their physical <see cref="Key"/> and shown by the
    /// layout's own display name (an AZERTY keyboard shows the letters it has).</para>
    /// </summary>
    public static class SpellingKeyProvider
    {
        private static readonly List<string> movementKeys = new List<string>();
        private static readonly List<string> gameplayKeys = new List<string>();
        private static readonly List<string> abilityKeys = new List<string>();
        private static bool dirty = true;
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            movementKeys.Clear();
            gameplayKeys.Clear();
            abilityKeys.Clear();
            dirty = true;
            hooked = false;
        }

        private static void OnBindingsChanged() => dirty = true;

        /// <summary>Is there a keyboard? Without one the Spelling System cannot be played (see <see cref="SpellingBalance.skipWithoutKeyboard"/>).</summary>
        public static bool HasKeyboard => Keyboard.current != null;

        /// <summary>
        /// The keys this machine can ask for now: the candidates minus the reserved keys, the keys of abilities, the keys of every other
        /// gameplay action (when the balance says so) and the movement keys (unless the spell roots the caster).
        /// </summary>
        /// <param name="rooted">The spell roots the caster, so the movement keys may be used.</param>
        /// <param name="abilityActions">The actions of the player's ability keys (their keys are never used).</param>
        public static SpellingKeyPool BuildPool(SpellingBalance b, bool rooted, IEnumerable<InputAction> abilityActions)
        {
            if (!hooked)
            {
                hooked = true;
                InputBindingStore.Changed += OnBindingsChanged;
            }
            if (dirty) Recompute(b);
            var ability = new List<string>(abilityKeys);
            if (abilityActions != null)
                foreach (InputAction a in abilityActions)
                    if (a != null) AddKeysOf(a, ability);
            return SpellingKeyPool.Build(b, movementKeys, gameplayKeys, ability, rooted);
        }

        private static void Recompute(SpellingBalance b)
        {
            movementKeys.Clear();
            gameplayKeys.Clear();
            abilityKeys.Clear();
            global::PlayerInput copy = null;
            try
            {
                copy = new global::PlayerInput();
                InputBindingStore.Apply(copy.asset);
                foreach (InputActionMap map in copy.asset.actionMaps)
                {
                    foreach (InputAction a in map.actions)
                    {
                        if (a == null) continue;
                        List<string> into = IsMovement(a, b) ? movementKeys
                            : a.name.StartsWith("Ability", StringComparison.OrdinalIgnoreCase) ? abilityKeys : gameplayKeys;
                        AddKeysOf(a, into);
                    }
                }
                dirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rune Magic] The Spelling System could not read the key bindings ({e.Message}): only the reserved keys are left out.");
                dirty = false;
            }
            finally
            {
                copy?.Dispose();
            }
        }

        // A movement action: one the balance names, or any action with a keyboard composite (2D vector / D-pad: WASD and its rebinds).
        private static bool IsMovement(InputAction a, SpellingBalance b)
        {
            if (b?.movementActions != null)
                foreach (string n in b.movementActions)
                    if (!string.IsNullOrWhiteSpace(n) && string.Equals(a.name, n.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            foreach (InputBinding bd in a.bindings)
                if (bd.isComposite && bd.path != null && (bd.path.IndexOf("Vector", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                           bd.path.IndexOf("Dpad", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            return false;
        }

        private static void AddKeysOf(InputAction a, List<string> into)
        {
            foreach (InputBinding bd in a.bindings)
            {
                if (bd.isComposite) continue;
                string path = bd.effectivePath;
                if (string.IsNullOrEmpty(path) || !TryKeyName(path, out string name)) continue;
                if (!into.Contains(name)) into.Add(name);
            }
        }

        /// <summary>The Key a keyboard control path stands for ("&lt;Keyboard&gt;/w", "&lt;Keyboard&gt;/leftShift", "&lt;Keyboard&gt;/#(w)"), as the name of the Key enum.</summary>
        public static bool TryKeyName(string controlPath, out string name)
        {
            name = "";
            const string prefix = "<Keyboard>/";
            if (string.IsNullOrEmpty(controlPath) || !controlPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string rest = controlPath.Substring(prefix.Length);
            Key key;
            if (rest.StartsWith("#(", StringComparison.Ordinal) && rest.EndsWith(")", StringComparison.Ordinal))
            {
                // A key named by what it types on this layout.
                string typed = rest.Substring(2, rest.Length - 3);
                Keyboard kb = Keyboard.current;
                if (kb == null) return false;
                foreach (KeyControl k in kb.allKeys)
                    if (k != null && string.Equals(k.displayName, typed, StringComparison.OrdinalIgnoreCase)) { name = k.keyCode.ToString(); return true; }
                return false;
            }
            if (Enum.TryParse(rest, true, out key) && key != Key.None) { name = key.ToString(); return true; }
            Keyboard device = Keyboard.current;
            if (device != null)
            {
                try
                {
                    if (device[rest] is KeyControl control) { name = control.keyCode.ToString(); return true; }
                }
                catch (Exception) { /* not a control of the keyboard */ }
            }
            return false;
        }

        /// <summary>The Key with this name (a name of the Key enum).</summary>
        public static bool TryKey(string name, out Key key) => Enum.TryParse(name, true, out key) && key != Key.None;

        /// <summary>The letter to show for a key on this keyboard's layout ("A", "Q" on AZERTY for the physical A key...).</summary>
        public static string Label(string name)
        {
            if (!TryKey(name, out Key key)) return name ?? "";
            try
            {
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    string shown = kb[key].displayName;
                    if (!string.IsNullOrWhiteSpace(shown)) return shown.ToUpperInvariant();
                }
            }
            catch (Exception) { /* no keyboard */ }
            return name.ToUpperInvariant();
        }

        /// <summary>Was the key pressed this frame (a letter of the sequence)?</summary>
        public static bool PressedThisFrame(string name)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !TryKey(name, out Key key)) return false;
            try { return kb[key].wasPressedThisFrame; }
            catch (Exception) { return false; }
        }
    }
}
