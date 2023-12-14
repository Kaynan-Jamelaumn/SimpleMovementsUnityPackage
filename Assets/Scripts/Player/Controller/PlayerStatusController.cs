using System.Collections;
using System.Diagnostics.Tracing;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;
using UnityEngine.UI;

public class Buff
{
    public float amount;
    public float duration;
    public float tickCooldown;
    public bool isProcedural;

    public Buff(float amount, float duration, float tickCooldown, bool isProcedural)
    {
        this.amount = amount;
        this.duration = duration;
        this.tickCooldown = tickCooldown;
        this.isProcedural = isProcedural;
    }
}
public class PlayerStatusController : MonoBehaviour
{
    // Models
    private PlayerStatusModel model;
    private PlayerMovementModel modelMovement;

    private void Awake()
    {
        // Assign models
        model = GetComponent<PlayerStatusModel>();
        modelMovement = GetComponent<PlayerMovementModel>();
    }

    private void Start()
    {
        // Initialize player status variables
        model.Stamina = model.MaxStamina;
        model.Hp = model.MaxHp;
        model.IsRegeneratingStamina = false;
        model.IsRegeneratingHp = false;
        model.Food = model.MaxFood;
        model.Drink = model.MaxDrink;
        model.ShouldConsumeDrink = true;
        model.ShouldConsumeFood = true;
    }

    private void Update()
    {
        // Update status  bars and initiate stamina regeneration if conditions are met
        UpdateStatusBars();
        if (!model.IsRegeneratingStamina && model.Stamina < model.MaxStamina && !model.IsConsumingStamina)
        {
            model.IsRegeneratingStamina = true;
            model.StaminaRegenerationRoutine = StartCoroutine(RegenerateStaminaRoutine());
        }
        model.FoodConsumptionRoutine = model.ShouldConsumeFood && !model.IsConsumingFood ? StartCoroutine(ConsumeFoodRoutine()) : null;
        model.DrinkConsumptionRoutine = model.ShouldConsumeDrink && !model.IsConsumingDrink ?  StartCoroutine(ConsumeDrinkRoutine()) : null;

    }

    private void UpdateStatusBars()
    {
        if (model.StaminaImage) UpdateStaminaBar();
        if (model.HpImage) UpdateHpBar();
        if (model.FoodImage) UpdateFoodBar();
        if (model.DrinkImage) UpdateDrinkBar();
    }

    private void UpdateStaminaBar()
    {
        // Ensure that stamina does not exceed limits
        model.Stamina = Mathf.Clamp(model.Stamina, 0f, model.MaxStamina);

        // Calculate the percentage of the remaining stamina
        float staminaPercentage = model.Stamina / model.MaxStamina;
        model.StaminaImage.fillAmount = staminaPercentage;
    }

    private void UpdateHpBar()
    {
        // Ensure that stamina does not exceed limits
        model.Hp = Mathf.Clamp(model.Hp, 0f, model.MaxHp);

        // Calculate the percentage of the remaining stamina
        float hpPercentage = model.Hp / model.MaxHp;
        model.HpImage.fillAmount = hpPercentage;
    }
    private void UpdateFoodBar()
    {
        // Ensure that stamina does not exceed limits
        model.Food = Mathf.Clamp(model.Food, 0f, model.MaxFood);

        // Calculate the percentage of the remaining stamina
        float foodPercentage = model.Food / model.MaxFood;
        model.FoodImage.fillAmount = foodPercentage;
    }

    private void UpdateDrinkBar()
    {
        // Ensure that stamina does not exceed limits
        model.Drink = Mathf.Clamp(model.Drink, 0f, model.MaxDrink);

        // Calculate the percentage of the remaining stamina
        float drinkPercentage = model.Drink / model.MaxDrink;
        model.DrinkImage.fillAmount = drinkPercentage;
    }

    // Coroutine to regenerate stamina over time
    public IEnumerator RegenerateStaminaRoutine()
    {
        // Check if stamina consumption is active, exit coroutine if true
        if (model.IsConsumingStamina)
        {
            model.IsRegeneratingStamina = false;
            yield break; // Exit the coroutine
        }

        model.IsRegeneratingStamina = true;

        // Regenerate stamina while it is below the maximum
        while (model.Stamina < model.MaxStamina)
        {
            model.Stamina += model.StaminaRegen;

            // Check the flag and exit the coroutine if needed
            if (model.ShouldStopStaminaRegeneration)
            {
                model.ShouldStopStaminaRegeneration = false;
                break;
            }

            yield return new WaitForSeconds(model.StaminaTickRegen); // Delay between regen updates
        }

        model.IsRegeneratingStamina = false;
    }

    // Method to signal the regeneration coroutine to stop
    public void StopStaminaRegeneration()
    {
        model.ShouldStopStaminaRegeneration = true;
    }

    // Coroutine to consume stamina over time
    public IEnumerator ConsumeStaminaRoutine(float amount = 0, float staminaTickConsuption = 0.5f)
    {
        model.IsConsumingStamina = true;

        // Signal the regeneration coroutine to stop
        StopStaminaRegeneration();

        // Consume stamina while there is enough stamina available
        while (model.Stamina >= 0 && model.Stamina - amount >= 0)
        {
            model.Stamina -= amount;
            yield return new WaitForSeconds(staminaTickConsuption); // Delay between Consume Stamina
        }

        model.IsConsumingStamina = false;
    }

    public IEnumerator RegenerateHpRoutine()
    {
        // Check if stamina consumption is active, exit coroutine if true
        if (model.IsConsumingHp)
        {
            model.IsRegeneratingHp = false;
            yield break; // Exit the coroutine
        }

        model.IsRegeneratingHp = true;

        // Regenerate stamina while it is below the maximum
        while (model.Hp < model.MaxHp)
        {
            model.Hp += model.HpRegen;

            // Check the flag and exit the coroutine if needed
            if (model.ShouldStopHpRegeneration)
            {
                model.ShouldStopHpRegeneration = false;
                break;
            }

            yield return new WaitForSeconds(model.HpTickRegen); // Delay between regen updates
        }

        model.IsRegeneratingHp = false;
    }

    public IEnumerator ConsumeFoodRoutine()
    {
        if (model.IsRegeneratingFood)
        {
            model.IsRegeneratingFood = false; 
            yield break;
        }
        model.IsConsumingFood = true;

        //consume food while it's not below 0
        while (model.Food >0)
        {
            model.Food -= model.FoodConsumption;

            if (!model.IsConsumingFood)
            {
                model.IsConsumingDrink = true;
                break;
            }
            yield return new WaitForSeconds(model.FoodTickConsumption);
        }
    }

    public IEnumerator ConsumeDrinkRoutine()
    {
        if (model.IsRegeneratingDrink)
        {
            model.IsRegeneratingDrink = false;
            yield break;
        }
        model.IsConsumingDrink = true;

        while (model.Drink >= 0)
        {
            model.Drink -= model.DrinkConsumption;

            if (!model.IsConsumingDrink)
            {
                model.IsConsumingDrink = true;
                break;
            }
            yield return new WaitForSeconds(model.DrinkTickConsumption);
        }
    }




    public void AddStamina(float amount)
    {
       model.Stamina = Mathf.Min(model.Stamina + amount, model.MaxDrink);
    }

    public void AddHp(float amount)
    {
        model.Hp = Mathf.Min(model.Hp + amount, model.MaxHp);
    }

    public void AddFood(float amount)
    {
        model.Food = Mathf.Min(model.Food + amount, model.MaxFood);
    }

    public void AddDrink(float amount)
    {
        model.Drink = Mathf.Min(model.Drink + amount, model.MaxDrink);
    }



    // Check if there is enough stamina for consumption
    public bool HasEnoughStamina(float amount)
    {
        return model.Stamina - amount >= 0;
    }
    
    public bool HasEnoughHp(float amount)
    {
        return model.Hp - amount >= 0;
    }
    
    public bool HasEnoughFood(float amount)
    {
        return model.Food - amount >= 0;
    }
    
    public bool HasEnoughDrink(float amount)
    {
        return model.Drink - amount >= 0;
    }
    public void ConsumeStamina(float amount)
    {
        if (model.Stamina <= 0) return;
        model.Stamina -= amount;
    }
        public void ConsumeHP(float amount)
    {
        if (model.Stamina <= 0) return;
        model.Hp -= amount;
    }
        public void ConsumeFood(float amount)
    {
        if (model.Stamina <= 0) return;
        model.Food -= amount;
    }
        public void ConsumeDrink(float amount)
    {
        if (model.Stamina <= 0) return;
        model.Drink -= amount;
    }

    public IEnumerator ApplyStaminaBuffRoutine(float staminaAmount, float timeBuffEffect, float tickCooldown, bool IsProcedural = false)
    {
        float amount = staminaAmount;
        amount = IsProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        while (Time.time < Time.time + timeBuffEffect)
        {
            model.Stamina += amount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        model.Stamina = Mathf.Min(model.Stamina, model.MaxStamina);
    }

    public IEnumerator ApplyHpBuffRoutine(float hpAmount, float timeBuffEffect, float tickCooldown, bool IsProcedural = false)
    {
        float amount = hpAmount;
        amount = IsProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        while (Time.time < Time.time + timeBuffEffect)
        {
            model.Hp += amount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        model.Hp = Mathf.Min(model.Hp, model.MaxHp);
    }

    public IEnumerator ApplyFoodBuffRoutine(float foodAmount, float timeBuffEffect, float tickCooldown, bool IsProcedural = false)
    {
        float amount = foodAmount;
        amount = IsProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        while (Time.time < Time.time + timeBuffEffect)
        {
            model.Food += amount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        model.Food = Mathf.Min(model.Food, model.MaxFood);
    }


    public IEnumerator ApplyDrinkBuffRoutine(float drinkAmount, float timeBuffEffect, float tickCooldown, bool IsProcedural = false)
    {
        float amount = drinkAmount;
        amount = IsProcedural ? amount / (timeBuffEffect / tickCooldown): amount;

        while (Time.time < Time.time + timeBuffEffect)
        {
            model.Drink += amount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        model.Drink = Mathf.Min(model.Drink, model.MaxDrink);
    }














    public void AddStaminaBuff(float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false)
    {
        Coroutine buffRoutine = StartCoroutine(ApplyStaminaBuffRoutine(staminaAmount, timeBuffEffect, tickCooldown, isProcedural));
        model.StaminaBuffRoutines.Add(buffRoutine);
    }


    public void AddHpBuff(float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false)
    {
        Coroutine buffRoutine = StartCoroutine(ApplyHpBuffRoutine(hpAmount, timeBuffEffect, tickCooldown, isProcedural));
        model.HpBuffRoutines.Add(buffRoutine);
    }
    public void AddFoodBuff(float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false)
    {
        Coroutine buffRoutine = StartCoroutine(ApplyFoodBuffRoutine(foodAmount, timeBuffEffect, tickCooldown, isProcedural));
        model.FoodBuffRoutines.Add(buffRoutine);
    }

    public void AddDrinkBuff(float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false)
    {
        Coroutine buffRoutine = StartCoroutine(ApplyDrinkBuffRoutine(drinkAmount, timeBuffEffect, tickCooldown, isProcedural));
        model.DrinkBuffRoutines.Add(buffRoutine);
    }

    public void RemoveStaminaBuff(Coroutine buffRoutine)
    {
        if (model.StaminaBuffRoutines.Contains(buffRoutine))
        {
            StopCoroutine(buffRoutine);
            model.StaminaBuffRoutines.Remove(buffRoutine);
        }
    }


    public void RemoveHpBuff(Coroutine buffRoutine)
    {
        if (model.HpBuffRoutines.Contains(buffRoutine))
        {
            StopCoroutine(buffRoutine);
            model.HpBuffRoutines.Remove(buffRoutine);
        }
    }

    public void RemoveFoodBuff(Coroutine buffRoutine)
    {
        if (model.FoodBuffRoutines.Contains(buffRoutine))
        {
            StopCoroutine(buffRoutine);
            model.FoodBuffRoutines.Remove(buffRoutine);
        }
    }


    public void RemoveDrinkBuff(Coroutine buffRoutine)
    {
        if (model.DrinkBuffRoutines.Contains(buffRoutine))
        {
            StopCoroutine(buffRoutine);
            model.DrinkBuffRoutines.Remove(buffRoutine);
        }
    }

    public void StopAllStaminaBuffs()
    {
        foreach (Coroutine routine in model.StaminaBuffRoutines)
        {
            StopCoroutine(routine);
        }
        model.StaminaBuffRoutines.Clear();
    }

    public void StopAllHpBuffs()
    {
        foreach (Coroutine routine in model.HpBuffRoutines)
        {
            StopCoroutine(routine);
        }
        model.HpBuffRoutines.Clear();
    }

    public void StopAllFoodBuffs()
    {
        foreach (Coroutine routine in model.FoodBuffRoutines)
        {
            StopCoroutine(routine);
        }
        model.FoodBuffRoutines.Clear();
    }

    public void StopAllDrinkBuffs()
    {
        foreach (Coroutine routine in model.DrinkBuffRoutines)
        {
            StopCoroutine(routine);
        }
        model.DrinkBuffRoutines.Clear();
    }

}
