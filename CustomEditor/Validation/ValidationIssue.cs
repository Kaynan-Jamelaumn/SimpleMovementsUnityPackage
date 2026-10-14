#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleMovements.Validation
{
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// One problem found by a validator: what is wrong, where (an object to select, or a prefab asset and the path of
    /// the object inside it), and optionally how to fix it.
    /// </summary>
    public sealed class ValidationIssue
    {
        public IssueSeverity severity;
        /// <summary>Short group name ("Missing script", "Reference", "Network"...).</summary>
        public string category;
        public string message;
        /// <summary>Object to select or ping (a scene object, or the prefab asset).</summary>
        public Object context;
        /// <summary>Path of the object inside its scene or prefab ("Player/Model/Hand_R"), for display.</summary>
        public string objectPath;
        /// <summary>Fixes the issue; returns true when something changed. Null = no automatic fix.</summary>
        public Func<bool> fix;
        /// <summary>Label of the fix button ("Remove", "Assign Animator"...).</summary>
        public string fixLabel;

        public bool CanFix => fix != null;

        public ValidationIssue(IssueSeverity severity, string category, string message, Object context = null, string objectPath = null)
        {
            this.severity = severity;
            this.category = category;
            this.message = message;
            this.context = context;
            this.objectPath = objectPath;
        }

        public ValidationIssue WithFix(string label, Func<bool> action)
        {
            fixLabel = label;
            fix = action;
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(severity).Append("] ").Append(category).Append(": ").Append(message);
            if (!string.IsNullOrEmpty(objectPath))
                sb.Append("  (").Append(objectPath).Append(')');
            return sb.ToString();
        }

        /// <summary>Writes the issue to the console (clicking the line selects the object).</summary>
        public void Log()
        {
            switch (severity)
            {
                case IssueSeverity.Error: Debug.LogError(ToString(), context); break;
                case IssueSeverity.Warning: Debug.LogWarning(ToString(), context); break;
                default: Debug.Log(ToString(), context); break;
            }
        }
    }

    /// <summary>Small helpers shared by the validators.</summary>
    public static class ValidationUtility
    {
        /// <summary>"Root/Child/Grandchild".</summary>
        public static string PathOf(Transform t)
        {
            if (t == null)
                return "";
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, '/').Insert(0, p.name);
            return sb.ToString();
        }

        /// <summary>The asset path of a prefab asset object, or null for scene objects.</summary>
        public static string PrefabAssetPath(Object o)
        {
            if (o == null || !PrefabUtility.IsPartOfPrefabAsset(o))
                return null;
            string path = AssetDatabase.GetAssetPath(o);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>
        /// Opens a prefab asset for editing, runs <paramref name="edit"/> on its contents and saves it when the edit
        /// reports a change. Returns whether it changed.
        /// </summary>
        public static bool EditPrefab(string path, Func<GameObject, bool> edit)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = edit(root);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Every prefab under Assets/ (not packages).</summary>
        public static string[] ProjectPrefabPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            return paths;
        }

        /// <summary>True for scripts of the project (not Unity's, Fish-Net's or TextMesh Pro's own components).</summary>
        public static bool IsProjectScript(Type type)
        {
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
                return false;
            string assembly = type.Assembly.GetName().Name;
            if (assembly.StartsWith("Unity", StringComparison.Ordinal) || assembly.StartsWith("FishNet", StringComparison.Ordinal)
                || assembly.StartsWith("GameKit", StringComparison.Ordinal) || assembly.StartsWith("Cinemachine", StringComparison.Ordinal)
                || assembly.StartsWith("TextMeshPro", StringComparison.Ordinal) || assembly.StartsWith("nunit", StringComparison.OrdinalIgnoreCase))
                return false;
            string ns = type.Namespace ?? "";
            return !ns.StartsWith("UnityEngine", StringComparison.Ordinal) && !ns.StartsWith("UnityEditor", StringComparison.Ordinal)
                && !ns.StartsWith("TMPro", StringComparison.Ordinal) && !ns.StartsWith("FishNet", StringComparison.Ordinal);
        }
    }
}
#endif
