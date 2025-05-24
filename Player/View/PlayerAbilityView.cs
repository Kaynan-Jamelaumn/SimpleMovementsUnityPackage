using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem;
using UnityEngine.Assertions;
using System.Threading.Tasks;
public class PlayerAbilityView : MonoBehaviour
{
    [SerializeField] private PlayerAbilityController playerAbilityController;
    [SerializeField] private InventoryManager inventoryManager;

    private void Awake()
    {
        playerAbilityController = this.CheckComponent(playerAbilityController, nameof(playerAbilityController));
        inventoryManager = this.CheckComponent(inventoryManager, nameof(inventoryManager), isCritical: false);
    }

    public void OnAttackClick(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //if (playerAbilityController.abilityStillInProgress) playerAbilityController.isWaitingForClick = false;
        //else inventoryManager.OnUseItem(value);

    }

    public void OnAbility2(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);

    }
    public void OnAbility3(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);

    }
    public void OnAbility4(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);

    }
    public void OnAbility5(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);

    }
    public void OnAbility6(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);

    }
    public void OnAbility7(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);
    }
}