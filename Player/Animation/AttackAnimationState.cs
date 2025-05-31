using UnityEngine;

[System.Serializable]
public class AttackAnimationState
{
    public AnimationClip CurrentAnimation;
    public float Speed = 1.0f;
    public float Duration = 0f;
    public bool IsLocked = false;
    public bool IsWeaponManaged = false;

    public void Reset()
    {
        CurrentAnimation = null;
        Speed = 1.0f;
        Duration = 0f;
        IsLocked = false;
        IsWeaponManaged = false;
    }
}