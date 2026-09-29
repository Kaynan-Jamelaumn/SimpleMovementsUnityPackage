using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    /// <summary>Inspector for the world <see cref="Portal"/>: a status box listing what is missing, and a button to add the trigger collider.</summary>
    [CustomEditor(typeof(Portal))]
    public class PortalEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var portal = (Portal)target;
            List<string> problems = portal.GetSetupProblems();
            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox(portal.LeadsToDungeon
                    ? "Ready: the player walking into the trigger enters a dungeon. Add this prefab to EndlessTerrain > Portal Settings > Prefabs to spawn it on the terrain."
                    : "Ready: the player walking into the trigger loads Scene To Load.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
            }

            bool hasTrigger = false;
            foreach (Collider c in portal.GetComponentsInChildren<Collider>(true))
                hasTrigger |= c.isTrigger;
            if (!hasTrigger && GUILayout.Button("Add Trigger Collider"))
                AddTrigger(portal);

            if (Application.isPlaying && portal.HasSite)
                EditorGUILayout.HelpBox($"Spawned on {portal.Site} - dungeon difficulty {portal.Difficulty:0.00}.", MessageType.None);

            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }

        private static void AddTrigger(Portal portal)
        {
            var box = Undo.AddComponent<BoxCollider>(portal.gameObject);
            box.isTrigger = true;
            Renderer[] renderers = portal.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                Transform t = portal.transform;
                Vector3 s = t.lossyScale;
                box.center = t.InverseTransformPoint(b.center);
                box.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
            }
            else
            {
                box.center = new Vector3(0f, 1.5f, 0f);
                box.size = new Vector3(2f, 3f, 1.5f);
            }
            EditorUtility.SetDirty(portal.gameObject);
        }
    }
}
