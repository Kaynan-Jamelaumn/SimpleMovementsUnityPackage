using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
//UnityEngine.InputSystem.InputAction.PlayerInput.PlayerActions
[System.Serializable]
public class AbilityAction
{
    public AbilityStateMachine abilityStateMachine;
    public InputActionReference abilityActionReference;
    public AbilityAction(InputActionReference abilityActionReference, AbilityStateMachine a)
    {
        this.abilityStateMachine = a;
        this.abilityActionReference = abilityActionReference;
    }
}

