using UnityEngine;
[System.Serializable]
public class WeaponAnimationSet
{
    [Header("Animation Set Info")]
    public string setName;
    [Tooltip("Animator Controller for this weapon")]
    public RuntimeAnimatorController animatorController;

    [Header("Basic Animations")]
    public AnimationClip idleAnimation;
    public AnimationClip walkAnimation;
    public AnimationClip runAnimation;
    public AnimationClip equipAnimation;
    public AnimationClip unequipAnimation;
}