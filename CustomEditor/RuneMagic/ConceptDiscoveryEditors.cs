#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuneMagic.EditorTools
{
    /// <summary>Tools ▸ SimpleMovements ▸ Rune Magic: knowledge sources for dungeons and the concept coverage report.</summary>
    public static class ConceptDiscoveryMenus
    {
        private const string Menu = SMMenu.Root + "Rune Magic/";
        private const int Section = SMMenu.Agriculture + 1;

        [MenuItem(Menu + "Add Concept Discovery Source to Selected", false, Section + 30)]
        private static void AddToSelectedMenu()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null) return;
            Undo.RegisterCompleteObjectUndo(go, "Add Concept Discovery Source");
            Prepare(go);
            EditorUtility.SetDirty(go);
        }

        [MenuItem(Menu + "Add Concept Discovery Source to Selected", true)]
        private static bool AddToSelectedValidate() => Selection.activeGameObject != null;

        [MenuItem(Menu + "Create Concept Discovery Source Prefab", false, Section + 31)]
        private static void CreatePrefabMenu()
        {
            string path = EditorUtility.SaveFilePanelInProject("Concept Discovery Source Prefab", "AncientInscription", "prefab",
                "Where to save the knowledge-source prefab (list it in a Dungeon Prop Table to spawn it in dungeons).");
            if (string.IsNullOrEmpty(path)) return;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = System.IO.Path.GetFileNameWithoutExtension(path);
            go.transform.localScale = new Vector3(1.4f, 2f, 0.3f); // an upright stone slab
            Prepare(go);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log($"[Rune Magic] Saved {path}. Add it to a Dungeon Prop Table entry (kind Interactable, areas such as Library, Shrine, Crypt, Secret) to have dungeons spawn it.");
        }

        /// <summary>Adds the Concept Discovery Source and a collider (the player's interaction ray needs one) to <paramref name="go"/>.</summary>
        public static ConceptDiscoverySource Prepare(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>(true) == null)
                go.AddComponent<BoxCollider>();
            if (!go.TryGetComponent(out ConceptDiscoverySource s))
            {
                s = go.AddComponent<ConceptDiscoverySource>();
                s.holdToUse = 0f;
                s.uses = 0;
            }
            return s;
        }

        [MenuItem(Menu + "Report Concept Coverage", false, Section + 32)]
        private static void CoverageMenu()
        {
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            string report = Coverage(db.Catalog);
            Debug.Log(report, db);
            EditorUtility.DisplayDialog("Rune Magic", "The concept coverage report is in the Console.", "OK");
        }

        /// <summary>Which concepts each difficulty band can teach, with the concepts nothing teaches and the ones a single source teaches.</summary>
        public static string Coverage(RuneCatalog c)
        {
            var sb = new StringBuilder("[Rune Magic] Concept discovery coverage\n");
            DiscoveryBalance d = c.Discovery;
            for (int t = 0; t < d.tiers.Count; t++)
            {
                int teachable = 0, total = 0;
                foreach (RuneDef r in c.Runes)
                    foreach (ConceptDef k in r.concepts)
                    {
                        if (k == null) continue;
                        total++;
                        if (DiscoveryCoverage.SourcesOf(c, k, t).Count > 0) teachable++;
                    }
                sb.Append($"  {d.tiers[t].Name}: {teachable} of {total} concepts can be taught here ({d.tiers[t].minigameNodes} glyphs, {d.tiers[t].steps} riddle step(s), {d.tiers[t].options} options)\n");
            }
            var unreachable = new List<ConceptDef>();
            var fragile = new List<ConceptDef>();
            DiscoveryCoverage.Find(c, unreachable, fragile);
            sb.Append($"  Never taught by any source: {unreachable.Count}");
            foreach (ConceptDef k in unreachable.GetRange(0, Mathf.Min(10, unreachable.Count))) sb.Append("\n    · ").Append(k.id).Append(" (").Append(k.rarity).Append(')');
            sb.Append($"\n  Taught by a single source (one wrong answer locks them): {fragile.Count}");
            foreach (ConceptDef k in fragile.GetRange(0, Mathf.Min(10, fragile.Count))) sb.Append("\n    · ").Append(k.id);
            int written = 0;
            foreach (RuneDef r in c.Runes) foreach (ConceptDef k in r.concepts) if (c.RiddlesOf(k.id).Count > 0) written++;
            sb.Append($"\n  Written riddles: {c.Riddles.Count} for {written} concept(s); the other concepts get generated riddles.");
            return sb.ToString();
        }
    }

    [CustomEditor(typeof(ConceptDiscoverySource))]
    public class ConceptDiscoverySourceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var src = (ConceptDiscoverySource)target;
            RuneCatalog c = RuneMagicDatabase.Instance.Catalog;
            DiscoverySourceDef def = c.DiscoverySource(src.SourceId);
            if (def == null)
                EditorGUILayout.HelpBox($"The Rune Magic Database has no discovery source '{src.SourceId}': this object teaches nothing.", MessageType.Error);
            else
            {
                string tier = src.TierOverride >= 0 ? $"band {src.TierOverride} ({c.Discovery.Tier(src.TierOverride)?.Name})" : "the dungeon's tier";
                EditorGUILayout.HelpBox($"{def.Name}: {def.description}\nTeaches {(def.runes.Count == 0 ? "any rune's" : string.Join(", ", def.runes) + "'")} concepts at {tier}. " +
                    "Rune users interact with it (the player's Interact input); everyone else finds nothing.", MessageType.Info);
            }
            if (src.GetComponentInChildren<Collider>(true) == null)
                EditorGUILayout.HelpBox("It has no collider: the player's interaction ray cannot find it.", MessageType.Warning);
            if (src.FixedConcept.Length > 0 && c.Concept(src.FixedConcept) == null)
                EditorGUILayout.HelpBox($"The fixed concept '{src.FixedConcept}' does not exist.", MessageType.Error);

            var ids = new List<string>();
            foreach (DiscoverySourceDef s in c.DiscoverySources) if (s != null) ids.Add(s.id);
            int index = Mathf.Max(0, ids.IndexOf(src.SourceId));
            if (ids.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                int picked = EditorGUILayout.Popup("Pick a source kind", index, ids.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(src, "Change Source Kind");
                    src.SourceId = ids[picked];
                    EditorUtility.SetDirty(src);
                }
            }
            DrawDefaultInspector();
        }
    }
}
#endif
