using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts skinned meshes made for a character's skeleton on that character: each bone of the mesh is matched to the
/// character's bone of the same name, else the same name without rig prefixes ("mixamorig:Spine", "Bip001 Spine",
/// "DEF-spine"), else the same humanoid bone (when both models are Humanoid). The mesh then moves with the
/// character's animation. Used for custom hair, body parts and whole body models (<see cref="CharacterAppearance"/>).
/// </summary>
public static class SkeletonBinder
{
    /// <summary>
    /// Binds every skinned mesh under <paramref name="instance"/> to <paramref name="character"/>'s skeleton.
    /// Returns how many bones were not found (0 = everything bound).
    /// </summary>
    public static int Bind(GameObject instance, Animator character)
    {
        if (instance == null || character == null)
            return 0;
        Transform skeletonRoot = character.transform;
        var byName = new Dictionary<string, Transform>();
        var byNormal = new Dictionary<string, Transform>();
        foreach (Transform t in skeletonRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.IsChildOf(instance.transform)) continue; // never the piece's own bones
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            string k = Normalize(t.name);
            if (k.Length > 0 && !byNormal.ContainsKey(k)) byNormal[k] = t;
        }
        Dictionary<Transform, Transform> human = HumanMap(instance, character);

        Transform Match(Transform theirs)
        {
            if (theirs == null) return null;
            if (byName.TryGetValue(theirs.name, out Transform t)) return t;
            if (byNormal.TryGetValue(Normalize(theirs.name), out t)) return t;
            return human != null && human.TryGetValue(theirs, out t) ? t : null;
        }

        int missing = 0;
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform[] bones = smr.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                Transform mine = Match(bones[i]);
                if (mine != null) bones[i] = mine;
                else missing++;
            }
            smr.bones = bones;
            Transform root = Match(smr.rootBone);
            if (root != null) smr.rootBone = root;
            smr.updateWhenOffscreen = true; // its bounds follow the character's animation
        }
        return missing;
    }

    /// <summary>A bone name without rig prefixes, separators and case ("mixamorig:LeftArm" → "leftarm").</summary>
    public static string Normalize(string n)
    {
        if (string.IsNullOrEmpty(n)) return "";
        int colon = n.LastIndexOf(':');
        if (colon >= 0) n = n.Substring(colon + 1);
        n = n.ToLowerInvariant();
        foreach (string prefix in new[] { "bip001", "bip01", "mixamorig", "def-", "def_", "b_", "bone_" })
            if (n.StartsWith(prefix)) n = n.Substring(prefix.Length);
        var sb = new System.Text.StringBuilder(n.Length);
        foreach (char c in n)
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>The piece's humanoid bones → the character's (both Animators Humanoid), or null.</summary>
    private static Dictionary<Transform, Transform> HumanMap(GameObject piece, Animator character)
    {
        if (!character.isHuman)
            return null;
        Animator theirs = piece.GetComponentInChildren<Animator>(true);
        if (theirs == null || theirs.avatar == null || !theirs.avatar.isHuman)
            return null;
        var byName = new Dictionary<string, Transform>();
        foreach (Transform t in piece.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;
        var map = new Dictionary<Transform, Transform>();
        string[] humanNames = HumanTrait.BoneName;
        foreach (HumanBone hb in theirs.avatar.humanDescription.human)
        {
            int index = System.Array.IndexOf(humanNames, hb.humanName);
            if (index < 0 || index >= (int)HumanBodyBones.LastBone || !byName.TryGetValue(hb.boneName, out Transform t)) continue;
            Transform mine = character.GetBoneTransform((HumanBodyBones)index);
            if (mine != null) map[t] = mine;
        }
        return map;
    }
}
