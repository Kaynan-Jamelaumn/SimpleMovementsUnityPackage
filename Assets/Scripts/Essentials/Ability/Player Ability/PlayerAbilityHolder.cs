using UnityEngine;
using UnityEngine.InputSystem;
[System.Serializable]
public class PlayerAbilityHolder : AbilityHolder
{
    [SerializeField] private InputActionReference abilityActionReference;
    public InputActionReference AbilityActionReference { get => abilityActionReference; }
   // [SerializeField] public PlayerAbilitySO abilityEffect;
}
