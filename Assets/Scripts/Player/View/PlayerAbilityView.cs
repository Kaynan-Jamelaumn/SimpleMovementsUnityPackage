﻿using System;
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
        if (!playerAbilityController) playerAbilityController = GetComponent<PlayerAbilityController>();

    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(playerAbilityController, "PlayerAbilityController is not assigned in playerAbilityController.");
        Assert.IsNotNull(inventoryManager, "InventoryManager is not assigned in inventoryManager.");
    }
    public void OnAttackClick(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        if (playerAbilityController.abilityStillInProgress) playerAbilityController.isWaitingForClick = false;
        //else inventoryManager.OnUseItem(value);

    }
    public void OnAbility1(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        //playerAbilityController.CheckAbilities(value.action);

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
