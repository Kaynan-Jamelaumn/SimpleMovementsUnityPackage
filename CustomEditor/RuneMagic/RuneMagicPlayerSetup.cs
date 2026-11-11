#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Check = PlayerSetupValidator.Check;
using Importance = PlayerSetupValidator.Importance;

namespace RuneMagic.EditorTools
{
    /// <summary>
    /// Tells the project's player validators which Rune Magic scripts a player prefab needs, so the Player Setup Validator (the inspector's
    /// Add buttons and Add All Recommended), the Scene &amp; Prefab Setup window and the Project Validator find a prefab that lacks them and
    /// fix it in one click:
    /// <list type="bullet">
    /// <item><b>Rune Soul</b> (with its Soul Integrity Manager: the HUD's Soul gauge) and the <b>Meditation Controller</b> (the Meditate input).</item>
    /// <item><b>NetRuneMagic</b> on a networked prefab (a Fish-Net project): replicates the soul, the runes, the spell sequence and the open grimoire to the owner.</item>
    /// </list>
    /// The Add button of each runs the same tool as <i>Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic to Selected Player</i>: all of them at once.
    /// Nothing is listed while Rune Magic is switched off in its database. (The windows, the Spelling overlay and the grimoire are made at run
    /// time by the system itself: nothing to assign for them.) The network validator checks NetRuneMagic too, through Network ▸ Make Networked.
    /// </summary>
    [InitializeOnLoad]
    public static class RuneMagicPlayerSetup
    {
        private const string Group = "Rune Magic";

        static RuneMagicPlayerSetup() => Register();

        /// <summary>Registers the Rune Magic scripts with the Player Setup Validator (again is harmless).</summary>
        public static void Register()
        {
            PlayerSetupValidator.Register(new Check
            {
                type = typeof(RuneSoul), importance = Importance.Recommended, group = Group, applies = Enabled, add = AddAll,
                purpose = "Rune Magic: the Mage's soul (Soul Integrity, runes, spells, the grimoire). Inert on other classes. A Mage without one gets one when the game starts, " +
                          "but the prefab needs it for the HUD's Soul gauge and for network replication.",
            });
            PlayerSetupValidator.Register(new Check
            {
                type = typeof(MeditationController), importance = Importance.Recommended, group = Group, applies = Enabled, add = AddAll,
                purpose = "Rune Magic: the Meditate input and screen (the soul recovers while the Mage meditates). Inert on other classes.",
            });
            PlayerSetupValidator.Register(new Check
            {
                type = typeof(SoulIntegrityManager), importance = Importance.Automatic, group = Group, applies = Enabled,
                purpose = "Rune Magic: the Soul Integrity bar of the HUD. Added with the Rune Soul.",
            });
            Type net = Type.GetType("GameNet.NetRuneMagic, Assembly-CSharp");
            Type networkObject = Type.GetType("FishNet.Object.NetworkObject, FishNet.Runtime");
            if (net != null && networkObject != null)
                PlayerSetupValidator.Register(new Check
                {
                    type = net, importance = Importance.Recommended, group = Group, add = AddAll,
                    applies = root => Enabled(root) && root != null && root.GetComponent(networkObject) != null,
                    purpose = "Rune Magic on the network: sends the Mage's soul, runes, spell sequence and open grimoire to its owner and carries the owner's requests to the " +
                              "server. Network ▸ Make Selected Prefab Networked ▸ Player adds it too.",
                });
        }

        private static bool Enabled(GameObject root) => RuneMagicDatabase.Instance.Catalog.Settings.enabled;

        /// <summary>The validators' Add: everything Rune Magic needs on the player at once (no Undo: this also runs on prefab contents).</summary>
        private static void AddAll(GameObject root) => RuneSoulMenus.AddToPlayer(root, false);
    }
}
#endif
