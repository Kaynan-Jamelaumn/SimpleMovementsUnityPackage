#if UNITY_EDITOR
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEditor;
using UnityEngine;

namespace ProceduralCaves.EditorTools
{
    /// <summary>What a cave holds at each depth: the most likely deposits of an ordinary chamber, per band (inspectors).</summary>
    internal static class CaveDepthPreview
    {
        private static readonly Dictionary<int, bool> open = new Dictionary<int, bool>();

        public static void Draw(CaveRules rules, int key)
        {
            if (rules == null)
                return;
            open.TryGetValue(key, out bool shown);
            shown = EditorGUILayout.Foldout(shown, "What each depth holds (ordinary chamber)", true);
            open[key] = shown;
            if (!shown)
                return;
            EditorGUI.indentLevel++;
            foreach (CaveDepthBand band in System.Enum.GetValues(typeof(CaveDepthBand)))
            {
                float depth = CaveSetupMenu.BandMiddle(rules.Depth, band);
                var weights = new List<(string name, float w, CaveRarity rarity)>();
                float total = 0f;
                foreach (CaveResourceEntry e in rules.Resources)
                {
                    if (e == null || !e.enabled || e.specialChambersOnly)
                        continue;
                    float w = e.weight * CaveDepth.Suitability(e, depth);
                    if (w <= 0f)
                        continue;
                    weights.Add((e.name, w, e.rarity));
                    total += w;
                }
                weights.Sort((a, b) => b.w.CompareTo(a.w));
                EditorGUILayout.LabelField($"{band} (depth {depth:0.0})", EditorStyles.boldLabel);
                if (weights.Count == 0)
                {
                    EditorGUILayout.HelpBox("Nothing can be placed at this depth (outside special chambers).", MessageType.Warning);
                    continue;
                }
                for (int i = 0; i < weights.Count && i < 6; i++)
                    EditorGUILayout.LabelField($"{weights[i].name}", $"{weights[i].w / total * 100f:0.#}%  ({weights[i].rarity})");
                if (weights.Count > 6)
                    EditorGUILayout.LabelField($"... and {weights.Count - 6} more");
            }
            EditorGUI.indentLevel--;
        }
    }

    [CustomEditor(typeof(CaveMiningStage))]
    public class CaveMiningStageEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var stage = (CaveMiningStage)target;
            EditorGUILayout.HelpBox(
                "List this asset in a Dungeon Profile's Custom Stages to make its dungeons caves to explore and mine: deposits by depth " +
                "and rarity (mined with a pickaxe into the inventory, used by crafting), rich veins, crystal grottos and guarded " +
                "hoards, rock formations, tougher creatures deeper down, natural passages and the cave's memory between visits.",
                MessageType.None);
            if (stage.resources == null)
                EditorGUILayout.HelpBox("No Resource Table: the built-in resources are used (every mineable material of the Crafting Database).", MessageType.Info);
            DrawDefaultInspector();
            EditorGUILayout.Space();
            CaveDepthPreview.Draw(stage.MakeRules(), stage.GetInstanceID());
            List<string> problems = CaveSetupMenu.CheckRules(stage.name, stage.MakeRules());
            foreach (string p in problems)
                EditorGUILayout.HelpBox(p, MessageType.Warning);
        }
    }

    [CustomEditor(typeof(CaveResourceTable))]
    public class CaveResourceTableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var table = (CaveResourceTable)target;
            EditorGUILayout.HelpBox(
                "Each entry is a kind of deposit: what a lump gives (a Crafting Database id), its depth range and peak, weight, rarity, " +
                "where it sits, vein size, lumps, work per lump and the pick it needs. Depth: about 1 per level down, more far from a " +
                "level's way in, in deeper caves and in harder caves (0-1 Shallow, 1-2.5 Medium, 2.5-4 Deep, 4+ Abyssal).",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Missing Built-in Resources"))
                {
                    Undo.RecordObject(table, "Add cave resources");
                    int added = 0;
                    foreach (CaveResourceEntry e in CaveDefaults.Resources())
                    {
                        if (table.entries.Exists(x => x != null && x.kind == e.kind && x.craftId == e.craftId))
                            continue;
                        table.entries.Add(e);
                        added++;
                    }
                    EditorUtility.SetDirty(table);
                    Debug.Log($"[Caves] {added} resource(s) added to {table.name}.");
                }
                if (GUILayout.Button("Reset To Built-in") && EditorUtility.DisplayDialog("Cave resources", "Replace every entry with the built-in resources?", "Replace", "Cancel"))
                {
                    Undo.RecordObject(table, "Reset cave resources");
                    table.entries = CaveDefaults.Resources();
                    EditorUtility.SetDirty(table);
                }
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
            EditorGUILayout.Space();
            CaveRules rules = CaveRules.Create(table.entries);
            CaveDepthPreview.Draw(rules, table.GetInstanceID());
            foreach (string p in CaveSetupMenu.CheckRules(table.name, rules))
                EditorGUILayout.HelpBox(p, MessageType.Warning);
        }
    }

    [CustomEditor(typeof(CaveEntrance))]
    public class CaveEntranceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var entrance = (CaveEntrance)target;
            EditorGUILayout.HelpBox(
                "A cave mouth: walking into its tunnel darkens the screen and takes the players into the cave behind it (generated from " +
                "its position, the same cave every time; what they did in it is kept for the next visit). +Z (the blue arrow) faces out " +
                "of the cave. It is built when play starts, fitted to the ground.",
                MessageType.None);
            foreach (string p in entrance.GetSetupProblems())
                EditorGUILayout.HelpBox(p, MessageType.Error);
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cave", entrance.EffectiveName);
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Rebuild Mouth"))
                    entrance.Build();
                CombatEntity local = PlayerLocator.Local;
                using (new EditorGUI.DisabledScope(local == null || entrance.Portal == null || !GameNet.NetAuthority.IsServer))
                    if (GUILayout.Button("Send The Local Player In"))
                        entrance.Portal.TryEnter(PlayerLocator.MovableRoot(local));
            }
        }
    }

    [CustomEditor(typeof(CaveInstance))]
    public class CaveInstanceEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var cave = (CaveInstance)target;
            EditorGUILayout.LabelField("Name", cave.DisplayName);
            EditorGUILayout.LabelField("Record", cave.Key ?? "-");
            EditorGUILayout.LabelField("Chain depth", cave.ChainDepth.ToString());
            EditorGUILayout.LabelField("Deposits", cave.DepositCount.ToString());
            EditorGUILayout.LabelField("Lumps mined (this visit)", cave.LumpsMined.ToString());
            EditorGUILayout.LabelField("Built", cave.IsBuilt ? "yes" : "building");
            EditorGUILayout.LabelField("Network copy", cave.IsReplica ? "yes" : "no");
            CaveStore.Record r = cave.Record;
            if (r != null)
            {
                EditorGUILayout.LabelField("Visits", r.visits.ToString());
                EditorGUILayout.LabelField("Deposits remembered", r.deposits.Count.ToString());
                EditorGUILayout.LabelField("Creatures slain (remembered)", r.slain.Count.ToString());
                EditorGUILayout.LabelField("Containers emptied", r.taken.Count.ToString());
                EditorGUILayout.LabelField("Lumps mined (all visits)", r.lumpsMined.ToString());
                if (GUILayout.Button("Forget This Cave"))
                {
                    CaveStore.Forget(cave.Key);
                    Debug.Log($"[Caves] '{cave.DisplayName}' forgotten: the next visit finds it untouched.");
                }
            }
            if (Application.isPlaying && PlayerLocator.Local != null)
                EditorGUILayout.LabelField("Depth here (local player)", $"{cave.DepthAt(PlayerLocator.Local.transform.position):0.00} ({cave.BandAt(PlayerLocator.Local.transform.position)})");
        }
    }
}
#endif
