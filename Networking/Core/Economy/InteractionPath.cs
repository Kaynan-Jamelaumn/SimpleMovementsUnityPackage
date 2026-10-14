using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Names a child of an object by its place under it: "Body/Hand", with "[k]" after a name when k earlier siblings
    /// share it ("" = the object itself). Every machine builds the same object from the same prefab or scene, so the same
    /// path finds the same child - used to name an interactable inside a networked object (an NPC's talk volume, a lever
    /// on a gate) without a network identity of its own.
    /// </summary>
    public static class InteractionPath
    {
        /// <summary>Deepest path accepted.</summary>
        public const int MaxDepth = 16;
        /// <summary>Longest path accepted (characters).</summary>
        public const int MaxLength = 256;

        /// <summary>The path from <paramref name="root"/> down to <paramref name="t"/> ("" = the root), or null when it is not under it.</summary>
        public static string Of(Transform root, Transform t)
        {
            if (root == null || t == null)
                return null;
            var parts = new List<string>();
            for (Transform p = t; p != root; p = p.parent)
            {
                if (p == null || parts.Count >= MaxDepth)
                    return null;
                parts.Add(Segment(p));
            }
            var sb = new StringBuilder();
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                sb.Append(parts[i]);
                if (i > 0)
                    sb.Append('/');
            }
            return sb.Length <= MaxLength ? sb.ToString() : null;
        }

        /// <summary>The child of <paramref name="root"/> at <paramref name="path"/> ("" or null = the root), or null.</summary>
        public static Transform Find(Transform root, string path)
        {
            if (root == null)
                return null;
            if (string.IsNullOrEmpty(path))
                return root;
            if (path.Length > MaxLength)
                return null;
            string[] parts = path.Split('/');
            if (parts.Length > MaxDepth)
                return null;
            Transform current = root;
            foreach (string part in parts)
            {
                Parse(part, out string name, out int occurrence);
                Transform next = null;
                int seen = 0;
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
            Transform parent = t.parent;
            if (parent != null)
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform c = parent.GetChild(i);
                    if (c == t)
                        break;
                    if (Escape(c.name) == name)
                        occurrence++;
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
