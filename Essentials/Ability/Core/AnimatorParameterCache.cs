using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets Animator parameters only if they exist, so abilities and AI can name parameters that some characters do not
/// have without errors or warnings. Parameter lists are cached per controller.
/// </summary>
public static class AnimatorParameterCache
{
    private struct Entry
    {
        public Dictionary<int, AnimatorControllerParameterType> parameters;
    }

    private static readonly Dictionary<RuntimeAnimatorController, Entry> byController =
        new Dictionary<RuntimeAnimatorController, Entry>(ReferenceComparer<RuntimeAnimatorController>.Instance);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => byController.Clear();

    private static Dictionary<int, AnimatorControllerParameterType> Get(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
            return null;
        RuntimeAnimatorController key = animator.runtimeAnimatorController;
        if (!byController.TryGetValue(key, out Entry e))
        {
            e.parameters = new Dictionary<int, AnimatorControllerParameterType>();
            AnimatorControllerParameter[] ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
                e.parameters[ps[i].nameHash] = ps[i].type;
            byController[key] = e;
        }
        return e.parameters;
    }

    public static bool Has(Animator animator, int hash, AnimatorControllerParameterType type)
    {
        Dictionary<int, AnimatorControllerParameterType> p = Get(animator);
        return p != null && p.TryGetValue(hash, out AnimatorControllerParameterType t) && t == type;
    }

    public static bool Has(Animator animator, string name, AnimatorControllerParameterType type) =>
        !string.IsNullOrEmpty(name) && Has(animator, Animator.StringToHash(name), type);

    public static void SetTrigger(Animator animator, string name)
    {
        if (string.IsNullOrEmpty(name))
            return;
        int h = Animator.StringToHash(name);
        if (Has(animator, h, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(h);
    }

    public static void ResetTrigger(Animator animator, string name)
    {
        if (string.IsNullOrEmpty(name))
            return;
        int h = Animator.StringToHash(name);
        if (Has(animator, h, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(h);
    }

    public static void SetBool(Animator animator, string name, bool value)
    {
        if (string.IsNullOrEmpty(name))
            return;
        SetBool(animator, Animator.StringToHash(name), value);
    }

    public static void SetBool(Animator animator, int hash, bool value)
    {
        if (Has(animator, hash, AnimatorControllerParameterType.Bool))
            animator.SetBool(hash, value);
    }

    public static void SetFloat(Animator animator, int hash, float value, float damp = 0f)
    {
        if (!Has(animator, hash, AnimatorControllerParameterType.Float))
            return;
        if (damp > 0f)
            animator.SetFloat(hash, value, damp, Time.deltaTime);
        else
            animator.SetFloat(hash, value);
    }

    public static void SetInteger(Animator animator, int hash, int value)
    {
        if (Has(animator, hash, AnimatorControllerParameterType.Int))
            animator.SetInteger(hash, value);
    }

    /// <summary>True if the controller has a state with this name on <paramref name="layer"/>.</summary>
    public static bool HasState(Animator animator, int layer, int stateHash) =>
        animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled && animator.HasState(layer, stateHash);
}
