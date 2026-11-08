using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Ids of housing things that were never given one in the editor (a scene saved before, a chest added at run time): made
/// from the object's scene and place in the hierarchy, so they are the same in every session and on every machine (the
/// server and its clients agree on them). Ids made in the editor (saved with the scene) are better: renaming or moving
/// an object changes these.
/// </summary>
public static class HousingIds
{
#if UNITY_EDITOR
    // Unity's Reset command puts every field back to its default before it calls Reset(): the ids an object had are
    // remembered here each time the editor validates it (the scene opened, the inspector changed it), so Reset can give
    // them back - a tenant's lease, orders and chest records are named by them.
    private static readonly Dictionary<string, string> remembered = new Dictionary<string, string>();
#endif

    /// <summary>Editor: remembers an object's id of a kind ("property", "chest"...), from its OnValidate, so the Reset command keeps it.</summary>
    public static void Remember(UnityEngine.Object o, string kind, string id)
    {
#if UNITY_EDITOR
        if (o != null && !string.IsNullOrEmpty(id)) remembered[kind + ":" + o.GetInstanceID()] = id;
#endif
    }

    /// <summary>Editor: the id of that kind the object had before the Reset command (null when none is known).</summary>
    public static string Recall(UnityEngine.Object o, string kind)
    {
#if UNITY_EDITOR
        if (o != null && remembered.TryGetValue(kind + ":" + o.GetInstanceID(), out string id)) return id;
#endif
        return null;
    }

    public static string Stable(Component c, string kind)
    {
        if (c == null) return Guid.NewGuid().ToString("N").Substring(0, 12);
        var path = new StringBuilder();
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            int same = 0;
            if (t.parent != null)
                for (int i = 0; i < t.GetSiblingIndex(); i++)
                    if (t.parent.GetChild(i).name == t.name) same++;
            path.Insert(0, "/" + t.name + (same > 0 ? "[" + same + "]" : ""));
        }
        string scene = c.gameObject.scene.IsValid() ? c.gameObject.scene.name : "";
        using (SHA256 sha = SHA256.Create())
        {
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes($"{kind}|{scene}|{path}"));
            var b = new StringBuilder(12);
            for (int i = 0; i < 6; i++) b.Append(h[i].ToString("x2"));
            return b.ToString();
        }
    }
}
