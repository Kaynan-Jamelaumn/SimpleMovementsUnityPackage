using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Names an object of a scene without a network identity (a chest or an item placed in the City or World scene) by
    /// its place in the hierarchy: "Root/Child/Item", with "[k]" after a name when k earlier siblings share it. The
    /// server and every client load the same scene files, so the same path finds the same object on each machine,
    /// inside the scene the player is in (stacked dungeon runs each have their own scene).
    /// </summary>
    public static class SceneObjectPath
    {
        /// <summary>Deepest hierarchy accepted (longer paths are refused).</summary>
        public const int MaxDepth = 32;

        /// <summary>The path of <paramref name="t"/> in its scene, or null.</summary>
        public static string Of(Transform t)
        {
            if (t == null)
                return null;
            var parts = new List<string>();
            for (Transform p = t; p != null; p = p.parent)
            {
                parts.Add(Segment(p));
                if (parts.Count > MaxDepth)
                    return null;
            }
            var sb = new StringBuilder();
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                sb.Append(parts[i]);
                if (i > 0)
                    sb.Append('/');
            }
            return sb.ToString();
        }

        /// <summary>The object at <paramref name="path"/> in <paramref name="scene"/>, or null.</summary>
        public static Transform Find(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path) || !scene.IsValid() || !scene.isLoaded)
                return null;
            string[] parts = path.Split('/');
            if (parts.Length == 0 || parts.Length > MaxDepth + 1)
                return null;
            Transform current = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int depth = 0; depth < parts.Length; depth++)
            {
                Parse(parts[depth], out string name, out int occurrence);
                Transform next = null;
                int seen = 0;
                if (depth == 0)
                {
                    foreach (GameObject root in roots)
                    {
                        if (Escape(root.name) != name)
                            continue;
                        if (seen++ == occurrence)
                        {
                            next = root.transform;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < current.childCount; i++)
                    {
                        Transform c = current.GetChild(i);
                        if (Escape(c.name) != name)
                            continue;
                        if (seen++ == occurrence)
                        {
                            next = c;
                            break;
                        }
                    }
                }
                if (next == null)
                    return null;
                current = next;
            }
            return current;
        }

        private static string Segment(Transform t)
        {
            string name = Escape(t.name);
            int occurrence = 0;
            if (t.parent != null)
            {
                for (int i = 0; i < t.parent.childCount; i++)
                {
                    Transform c = t.parent.GetChild(i);
                    if (c == t)
                        break;
                    if (Escape(c.name) == name)
                        occurrence++;
                }
            }
            else if (t.gameObject.scene.IsValid())
            {
                foreach (GameObject root in t.gameObject.scene.GetRootGameObjects())
                {
                    if (root.transform == t)
                        break;
                    if (Escape(root.name) == name)
                        occurrence++;
                }
            }
            return occurrence > 0 ? name + "[" + occurrence + "]" : name;
        }

        // Names may contain '/' or '[': those are replaced so the path can be split again.
        private static string Escape(string name) => string.IsNullOrEmpty(name) ? "_" : name.Replace('/', '_').Replace('[', '(').Replace(']', ')');

        private static void Parse(string segment, out string name, out int occurrence)
        {
            occurrence = 0;
            name = segment;
            if (string.IsNullOrEmpty(segment) || segment[segment.Length - 1] != ']')
                return;
            int open = segment.LastIndexOf('[');
            if (open <= 0)
                return;
            if (int.TryParse(segment.Substring(open + 1, segment.Length - open - 2), out int k) && k >= 0)
            {
                occurrence = k;
                name = segment.Substring(0, open);
            }
        }
    }
}
