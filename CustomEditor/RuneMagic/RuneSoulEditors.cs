#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace RuneMagic.EditorTools
{
    /// <summary>Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic to Selected Player.</summary>
    public static class RuneSoulMenus
    {
        private const string Menu = SMMenu.Root + "Rune Magic/";
        private const int Section = SMMenu.Agriculture + 1;

        [MenuItem(Menu + "Add Rune Magic to Selected Player", false, Section + 20)]
        private static void AddToSelectedMenu()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Rune Magic", "Select the player object (or prefab root) first.", "OK");
                return;
            }
            string added = AddToPlayer(go);
            EditorUtility.DisplayDialog("Rune Magic", added.Length > 0 ? $"Added to '{RootOf(go).name}': {added}." : "The player already has Rune Magic.", "OK");
        }

        [MenuItem(Menu + "Add Rune Magic to Selected Player", true)]
        private static bool AddToSelectedValidate() => Selection.activeGameObject != null;

        /// <summary>
        /// Adds the Rune Soul (with its Soul Integrity Manager), the Meditation Controller and, in a FishNet project with a
        /// NetworkObject on the player, NetRuneMagic. Returns what was added (empty when nothing was missing). Non-Mage
        /// players can carry them: they stay inert.
        /// </summary>
        public static string AddToPlayer(GameObject anyPart, bool useUndo = true)
        {
            GameObject root = RootOf(anyPart);
            var added = new System.Collections.Generic.List<string>();
            if (root.GetComponentInChildren<RuneSoul>(true) == null)
            {
                AddComponent(root, typeof(RuneSoul), useUndo);
                added.Add("Rune Soul + Soul Integrity Manager");
            }
            if (root.GetComponentInChildren<MeditationController>(true) == null)
            {
                AddComponent(root, typeof(MeditationController), useUndo);
                added.Add("Meditation Controller");
            }
            Type net = Type.GetType("GameNet.NetRuneMagic, Assembly-CSharp");
            Type netObject = Type.GetType("FishNet.Object.NetworkObject, FishNet.Runtime");
            if (net != null && netObject != null && root.GetComponent(netObject) != null && root.GetComponent(net) == null)
            {
                AddComponent(root, net, useUndo);
                added.Add("NetRuneMagic");
            }
            if (added.Count > 0)
                EditorUtility.SetDirty(root);
            return string.Join(", ", added);
        }

        private static void AddComponent(GameObject root, Type type, bool useUndo)
        {
            if (useUndo) Undo.AddComponent(root, type);
            else root.AddComponent(type);
        }

        private static GameObject RootOf(GameObject go)
        {
            CombatEntity e = go.GetComponentInParent<CombatEntity>();
            if (e != null) return e.gameObject;
            PlayerStatusController ps = go.GetComponentInParent<PlayerStatusController>();
            return ps != null ? ps.gameObject : go.transform.root.gameObject;
        }
    }

    [CustomEditor(typeof(RuneSoul))]
    public class RuneSoulEditor : Editor
    {
        private const string DebugSource = "debug";
        private float amount = 10f;
        private int kindIndex;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var soul = (RuneSoul)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("A rune user's soul: Soul Integrity, deep damage, Soul Rune Capacity and recovery. " +
                    "It is inert on players who are not rune users (Mage by default). Values and rates are in the Rune Magic Database ▸ Balance.", MessageType.Info);
                if (soul.GetComponentInParent<MeditationController>() == null && soul.GetComponentInChildren<MeditationController>(true) == null &&
                    GUILayout.Button("Add Meditation Controller (Meditate input)"))
                    Undo.AddComponent<MeditationController>(soul.gameObject);
                DrawDefaultInspector();
                return;
            }

            RuneSoulView v = soul.View;
            EditorGUILayout.LabelField("Rules", soul.IsAuthority ? "run here" : "network client (read-only)");
            EditorGUILayout.LabelField("Rune user", v.runeUser ? "yes" : "no (inert)");
            if (!v.runeUser)
            {
                DrawDefaultInspector();
                return;
            }
            EditorGUILayout.LabelField("Soul Integrity", $"{v.integrity:0.##} / {v.maxIntegrity:0.#}  ({v.Percent:0.#}%)");
            EditorGUILayout.LabelField("Deep damage", $"{v.deepDamage:0.##}  (ordinary recovery up to {v.Ceiling:0.#})");
            EditorGUILayout.LabelField("Condition", string.IsNullOrEmpty(v.castingDebuff) ? v.conditionName : $"{v.conditionName} — {v.castingDebuff}");
            if (v.collapseLocked) EditorGUILayout.HelpBox($"Collapse lock: no casting or synthesis until {v.lockUntilPercent:0}%.", MessageType.Warning);
            EditorGUILayout.LabelField("Capacity", $"{v.usedSlots} / {v.capacity} ({v.capacityStage})" + (v.nextStageLevel > 0 ? $", {v.nextStageCapacity} at level {v.nextStageLevel}" : ""));
            EditorGUILayout.LabelField("Recovery", $"{v.recoveryPerMinute:0.##}/min — {v.recoverySources}");
            EditorGUILayout.LabelField("Focus", v.channel == SoulChannel.None ? "none" : $"{v.channelName} {v.channelProgress * 100f:0}%");
            EditorGUILayout.LabelField("Saved", v.kept ? "yes" : "not yet (no player key, or the world is not open)");
            RuneCatalog cat = RuneMagicDatabase.Instance.Catalog;
            EditorGUILayout.LabelField("Pending energy", v.pendingEnergy.ToString("N0"));
            EditorGUILayout.LabelField("Runes", soul.Runes.Count.ToString());
            foreach (RuneInstance r in soul.Runes)
                if (r != null)
                    EditorGUILayout.LabelField("   " + RuneRules.DisplayName(cat, r),
                        $"R{r.rank} · Q{r.quality:0} · {r.energy:N0}/{cat.Balance.MaxEnergy(r.rank, r.quality):N0} · {soul.KnownConcepts(r.runeId).Count} concept(s) known");

            if (soul.IsAuthority)
            {
                EditorGUILayout.Space();
                var kinds = SoulRules.DamageKindIds(RuneMagicDatabase.Instance.Catalog.Balance);
                kinds.Insert(0, "(ordinary)");
                kindIndex = EditorGUILayout.Popup("Damage kind", Mathf.Clamp(kindIndex, 0, kinds.Count - 1), kinds.ToArray());
                amount = EditorGUILayout.FloatField("Amount", amount);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Damage")) soul.Damage(amount, kindIndex == 0 ? "" : kinds[kindIndex], DebugSource);
                    if (GUILayout.Button("Restore")) soul.Restore(amount, DebugSource);
                    if (GUILayout.Button("Heal deep")) soul.HealDeep(amount);
                    if (GUILayout.Button("Full")) soul.RestoreFully();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(v.IsChannelling ? "Stop focus" : "Meditate")) soul.RequestMeditation(!v.IsChannelling);
                    int bonus = soul.CapacityBonusOf(DebugSource);
                    if (GUILayout.Button("Capacity +1")) soul.SetCapacityBonus(DebugSource, bonus + 1);
                    if (GUILayout.Button("Capacity −1")) soul.SetCapacityBonus(DebugSource, bonus - 1);
                    if (GUILayout.Button("Log state")) Debug.Log(soul.ExportState(), soul);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Energy +" + amount.ToString("0"))) soul.AddPendingEnergy(amount, DebugSource);
                    RuneInstance first = soul.Runes.Count > 0 ? soul.Runes[0] : null;
                    using (new EditorGUI.DisabledScope(first == null))
                        if (GUILayout.Button("Give energy to first rune") && first != null) soul.AssignEnergy(first.instanceId, 0f);
                    if (GUILayout.Button("Open Rune Soul window")) RuneSoulWindow.Toggle();
                }
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
#endif
