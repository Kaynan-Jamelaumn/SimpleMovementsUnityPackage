using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
//UnityEngine.InputSystem.InputAction.PlayerInput.PlayerActions
[System.Serializable]
public class AbilityAction 
{
    [SerializeField]private AbilityStateMachine abilityStateMachine;
    public InputActionReference abilityActionReference;

    public AbilityStateMachine AbilityStateMachine { get => abilityStateMachine; set => abilityStateMachine = value; }

    public AbilityAction(InputActionReference abilityActionReference, AbilityStateMachine a)
    {
        this.abilityStateMachine = a;
        this.abilityActionReference = abilityActionReference;
    }
}

