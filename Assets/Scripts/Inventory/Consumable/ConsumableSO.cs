using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;

[CreateAssetMenu(fileName = "Consumable", menuName = "Scriptable Objects/Consumable")]
public class ConsumableSO : ItemSO
{
    [Header("Consumable Buff")]
    [SerializeField] private float hpAmount;
    [SerializeField] private float staminaAmount;
    [SerializeField] private float foodAmount;
    [SerializeField] private float drinkAmount;
    [SerializeField] private float cooldown;
    [SerializeField] private float timeBuffEffect;
    [SerializeField] private float tickCooldown;
    [SerializeField] private bool isProcedural;



    public float HpAmount
    {
        get { return hpAmount; }
        set { hpAmount = value; }
    }

    public float StaminaAmount
    {
        get { return staminaAmount; }
        set { staminaAmount = value; }
    }

    public float FoodAmount
    {
        get { return foodAmount; }
        set { foodAmount = value; }
    }

    public float DrinkAmount
    {
        get { return drinkAmount; }
        set { drinkAmount = value; }
    }
    public float Cooldown
    {
        get { return cooldown; }
        set { cooldown = value; }
    }

    public float TimeBuffEffect
    {
        get { return timeBuffEffect; }
        set { timeBuffEffect = value; }
    }    
    
    public float TickCooldown
    {
        get { return tickCooldown; }
        set { tickCooldown = value; }
    }


    public bool IsProcedural
    {
        get { return isProcedural; }
        set { isProcedural = value; }
    }

    public override void UseItem()
    {
        base.UseItem();
        if (timeBuffEffect == 0)
        {
            // Add amounts to respective stats, clamping them to max values
            statusController.AddStamina(staminaAmount);
            statusController.AddHp(hpAmount);
            statusController.AddFood(foodAmount);
            statusController.AddDrink(drinkAmount);


            return;
        }
        if (hpAmount > 0) statusController.StartCoroutine(statusController.ApplyHpBuffRoutine(hpAmount, timeBuffEffect, tickCooldown, isProcedural));
        if (staminaAmount > 0) statusController.StartCoroutine(statusController.ApplyStaminaBuffRoutine(staminaAmount, timeBuffEffect, tickCooldown, isProcedural));
        if (foodAmount > 0) statusController.StartCoroutine(statusController.ApplyFoodBuffRoutine(foodAmount, timeBuffEffect, tickCooldown, isProcedural));
        if (drinkAmount > 0) statusController.StartCoroutine(statusController.ApplyDrinkBuffRoutine(drinkAmount, timeBuffEffect, tickCooldown, isProcedural));
        
    }



}
