using System;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Ability", menuName = "Scriptable Objects/Ability/Player Ability")]

public class PlayerAbilitySO : AbilityEffectSO
{
    [SerializeField] public float MaxRange;
    [SerializeField] public GameObject isSelfTarget;
}

