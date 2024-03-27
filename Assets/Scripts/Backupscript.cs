//using System.Collections;
//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using System;

////public class CoroutineInfo
////{
////    public Coroutine coroutine;
////    public string effectName;
////    public bool isBuff = true;
////}

//public class PlayerStatusController : MonoBehaviour
//{
//    // Models
//    private PlayerStatusModel model;
//    private PlayerMovementModel movementModel;

//    private void Awake()
//    {
//        // Assign models
//        model = GetComponent<PlayerStatusModel>();
//        movementModel = GetComponent<PlayerMovementModel>();
//    }

//    private void Start()
//    {
//        // Initialize player status variables
//        model.Stamina = model.MaxStamina;
//        model.Hp = model.MaxHp;
//        model.IsRegeneratingStamina = false;
//        model.IsRegeneratingHp = false;
//        model.Food = model.MaxFood;
//        model.Drink = model.MaxDrink;
//    }

//    private void Update()
//    {
//        if (model.Drink <= 0 && model.DrinkPenaltyRoutine == null)
//        {
//            ApplyThirstPenalty();
//        }

//        if (model.Food <= 0 && model.FoodPenaltyRoutine == null)
//        {
//            ApplyHungerPenalty();
//        }
//        // Update status  bars and initiate stamina regeneration if conditions are met
//        UpdateStatusBars();
//        if (!model.IsRegeneratingStamina && model.Stamina < model.MaxStamina && !model.IsConsumingStamina)
//        {
//            model.IsRegeneratingStamina = true;
//            model.StaminaRegenerationRoutine = StartCoroutine(RegenerateStaminaRoutine());
//        }
//        if (!model.IsRegeneratingHp && model.Hp < model.MaxHp)
//        {
//            model.IsRegeneratingHp = true;
//            model.HpRegenerationRoutine = StartCoroutine(RegenerateHpRoutine());
//        }
//        model.FoodConsumptionRoutine = model.ShouldConsumeFood && !model.IsConsumingFood ? StartCoroutine(ConsumeFoodRoutine()) : null;
//        model.DrinkConsumptionRoutine = model.ShouldConsumeDrink && !model.IsConsumingDrink ? StartCoroutine(ConsumeDrinkRoutine()) : null;

//    }

//    private void UpdateStatusBars()
//    {
//        if (model.StaminaImage) UpdateStaminaBar();
//        if (model.HpImage) UpdateHpBar();
//        if (model.FoodImage) UpdateFoodBar();
//        if (model.DrinkImage) UpdateDrinkBar();
//        if (model.WeightImage) UpdateWeightBar();
//    }
//    private void UpdateBar(float value, float maxValue, UnityEngine.UI.Image barImage)
//    {
//        // Ensure that the value does not exceed limits
//        value = Mathf.Clamp(value, 0f, maxValue);

//        // Calculate the percentage of the remaining value
//        float percentage = value / maxValue;
//        barImage.fillAmount = percentage;
//    }

//    // Usage example:

//    private void UpdateStaminaBar()
//    {
//        UpdateBar(model.Stamina, model.MaxStamina, model.StaminaImage);
//        model.Stamina = Mathf.Clamp(model.Stamina, 0, model.MaxStamina);
//    }

//    private void UpdateHpBar()
//    {
//        UpdateBar(model.Hp, model.MaxHp, model.HpImage);
//        model.Hp = Mathf.Clamp(model.Hp, 0, model.MaxHp);
//    }

//    private void UpdateFoodBar()
//    {
//        UpdateBar(model.Food, model.MaxFood, model.FoodImage);
//        model.Food = Mathf.Clamp(model.Food, 0, model.MaxFood);
//    }

//    private void UpdateDrinkBar()
//    {
//        UpdateBar(model.Drink, model.MaxDrink, model.DrinkImage);
//        model.Drink = Mathf.Clamp(model.Drink, 0, model.MaxDrink);
//    }

//    private void UpdateWeightBar()
//    {
//        UpdateBar(model.Weight, model.MaxWeight, model.WeightImage);
//    }

//    // Coroutine to regenerate stamina over time
//    public IEnumerator RegenerateStaminaRoutine()
//    {
//        // Check if stamina consumption is active, exit coroutine if true
//        if (model.IsConsumingStamina)
//        {
//            model.IsRegeneratingStamina = false;
//            yield break; // Exit the coroutine
//        }

//        model.IsRegeneratingStamina = true;

//        // Regenerate stamina while it is below the maximum
//        while (model.Stamina < model.MaxStamina)
//        {
//            model.Stamina += model.StaminaRegen;

//            // Check the flag and exit the coroutine if needed
//            if (model.ShouldStopStaminaRegeneration)
//            {
//                model.ShouldStopStaminaRegeneration = false;
//                break;
//            }

//            yield return new WaitForSeconds(model.StaminaTickRegen); // Delay between regen updates
//        }

//        model.IsRegeneratingStamina = false;
//    }

//    // Method to signal the regeneration coroutine to stop
//    public void StopStaminaRegeneration()
//    {
//        model.ShouldStopStaminaRegeneration = true;
//    }

//    // Coroutine to consume stamina over time
//    public IEnumerator ConsumeStaminaRoutine(float amount = 0, float staminaTickConsuption = 0.5f)
//    {
//        model.IsConsumingStamina = true;

//        // Signal the regeneration coroutine to stop
//        StopStaminaRegeneration();

//        // Consume stamina while there is enough stamina available
//        while (model.Stamina >= 0 && model.Stamina - amount >= 0)
//        {
//            model.Stamina -= amount;
//            yield return new WaitForSeconds(staminaTickConsuption); // Delay between Consume Stamina
//        }

//        model.IsConsumingStamina = false;
//    }

//    public IEnumerator RegenerateHpRoutine()
//    {
//        // Check if stamina consumption is active, exit coroutine if true
//        if (model.IsConsumingHp)
//        {
//            model.IsRegeneratingHp = false;
//            yield break; // Exit the coroutine
//        }

//        model.IsRegeneratingHp = true;

//        // Regenerate stamina while it is below the maximum
//        while (model.Hp < model.MaxHp)
//        {
//            model.Hp += model.HpRegen;

//            // Check the flag and exit the coroutine if needed
//            if (model.ShouldStopHpRegeneration)
//            {
//                model.ShouldStopHpRegeneration = false;
//                break;
//            }

//            yield return new WaitForSeconds(model.HpTickRegen); // Delay between regen updates
//        }

//        model.IsRegeneratingHp = false;
//    }

//    public IEnumerator ConsumeFoodRoutine()
//    {
//        if (model.IsRegeneratingFood)
//        {
//            model.IsRegeneratingFood = false;
//            yield break;
//        }
//        model.IsConsumingFood = true;

//        //consume food while it's not below 0
//        while (model.Food > 0)
//        {
//            model.Food -= model.FoodConsumption;

//            if (!model.IsConsumingFood)
//            {
//                model.IsConsumingDrink = true;
//                break;
//            }
//            yield return new WaitForSeconds(model.FoodTickConsumption);
//        }
//    }

//    public IEnumerator ConsumeDrinkRoutine()
//    {
//        if (model.IsRegeneratingDrink)
//        {
//            model.IsRegeneratingDrink = false;
//            yield break;
//        }
//        model.IsConsumingDrink = true;

//        while (model.Drink >= 0)
//        {
//            model.Drink -= model.DrinkConsumption;

//            if (!model.IsConsumingDrink)
//            {
//                model.IsConsumingDrink = true;
//                break;
//            }
//            yield return new WaitForSeconds(model.DrinkTickConsumption);
//        }
//    }

//    public IEnumerator HungerPenalty()
//    {
//        while (model.Food <= 0)
//        {
//            model.Hp -= model.HpPenaltyWhileHungry;
//            yield return new WaitForSeconds(model.HpTickPenaltyCooldownWhileHungry);
//        }

//        model.FoodPenaltyRoutine = null; // Libera a referência para que a rotina possa ser reiniciada
//    }

//    public IEnumerator ThirstPenalty()
//    {
//        while (model.Drink <= 0)
//        {
//            model.Hp -= model.HpPenaltyWhileThirsty;
//            yield return new WaitForSeconds(model.HpTickPenaltyCooldownWhileThirsty);
//        }

//        model.DrinkPenaltyRoutine = null; // Libera a referência para que a rotina possa ser reiniciada
//    }

//    public void ApplyThirstPenalty()
//    {
//        model.DrinkPenaltyRoutine = StartCoroutine(ThirstPenalty());
//    }

//    public void ApplyHungerPenalty()
//    {
//        model.FoodPenaltyRoutine = StartCoroutine(HungerPenalty());
//    }


//    public void AddStamina(float amount)
//    {
//        model.Stamina = Mathf.Min(model.Stamina + StaminaAddFactor(amount), model.MaxDrink);
//    }

//    public void AddHp(float amount)
//    {
//        model.Hp = Mathf.Min(model.Hp + HpAddFactor(amount), model.MaxHp);
//    }

//    public void AddFood(float amount)
//    {
//        model.Food = Mathf.Min(model.Food + FoodAddFactor(amount), model.MaxFood);
//    }

//    public void AddDrink(float amount)
//    {
//        model.Drink = Mathf.Min(model.Drink + DrinkAddFactor(amount), model.MaxDrink);
//    }
//    public void AddWeight(float amount)
//    {
//        model.Weight += amount;// Mathf.Min(model.Weight + amount, model.MaxWeight);
//    }



//    public void ModifyMaxStamina(float amount)
//    {
//        model.MaxStamina += amount;
//    }

//    public void ModifyStaminaRegeneration(float amount)
//    {
//        model.StaminaRegen += amount;
//    }

//    public void ModifySpeed(float amount)
//    {
//        movementModel.Speed += amount;
//    }

//    public void ModifyMaxHp(float amount)
//    {
//        model.MaxHp += amount;
//    }
//    public void ModifyHpRegeneration(float amount)
//    {
//        model.HpRegen += amount;
//    }

//    public void ModifyMaxFood(float amount)
//    {
//        model.MaxFood += amount;
//    }

//    public void ModifyMaxDrink(float amount)
//    {
//        model.MaxDrink += amount;
//    }
//    public void ModifyMaxWeight(float amount)
//    {
//        model.MaxWeight += amount;
//    }


//    public void ModifyStaminaHealFactor(float amount)
//    {
//        model.StaminaHealFactor += amount;
//    }


//    public void ModifyStaminaDamageFactor(float amount)
//    {
//        model.StaminaDamageFactor += amount;
//    }


//    public void ModifyHpHealFactor(float amount)
//    {
//        model.HpHealFactor += amount;
//    }


//    public void ModifyHpDamageFactor(float amount)
//    {
//        model.HpDamageFactor += amount;
//    }


//    public void ModifyFoodFactor(float amount)
//    {
//        model.FoodFactor += amount;
//    }
//    public void ModifyFoodRemoveFactor(float amount)
//    {
//        model.FoodRemoveFactor += amount;
//    }
//    public void ModifyDrinkFactor(float amount)
//    {
//        model.DrinkFactor += amount;
//    }
//    public void ModifyDrinkRemoveFactor(float amount)
//    {
//        model.DrinkRemoveFactor += amount;
//    }


//    // Check if there is enough stamina for consumption
//    public bool HasEnoughStamina(float amount)
//    {
//        return model.Stamina - amount >= 0;
//    }

//    public bool HasEnoughHp(float amount)
//    {
//        return model.Hp - amount >= 0;
//    }

//    public bool HasEnoughFood(float amount)
//    {
//        return model.Food - amount >= 0;
//    }

//    public bool HasEnoughDrink(float amount)
//    {
//        return model.Drink - amount >= 0;
//    }

//    public void ConsumeStamina(float amount)
//    {
//        if (model.Stamina <= 0) return;
//        model.Stamina -= StaminaRemoveFactor(amount);
//    }
//    public void ConsumeHP(float amount)
//    {
//        if (model.Hp <= 0) return;
//        model.Hp -= HpRemoveFactor(amount);
//    }
//    public void ConsumeFood(float amount)
//    {
//        if (model.Food <= 0) return;
//        model.Food -= FoodRemoveFactor(amount);
//    }
//    public void ConsumeDrink(float amount)
//    {
//        if (model.Drink <= 0) return;
//        model.Drink -= DrinkRemoveFactor(amount);
//    }

//    public void ConsumeWeight(float amount)
//    {
//        if (model.Weight <= 0) return;
//        model.Weight -= amount;
//    }

//    private float ApplyFactor(float amount, float factor)
//    {
//        float percentageFactor = 1.0f + factor / 100.0f; // Convert percentage to a factor

//        amount *= percentageFactor;

//        return amount;
//    }


//    private float HpAddFactor(float amount)
//    {
//        return ApplyFactor(amount, model.HpHealFactor);
//    }
//    private float HpRemoveFactor(float amount)
//    {
//        return ApplyFactor(amount, model.HpDamageFactor);
//    }

//    private float StaminaAddFactor(float amount)
//    {
//        return ApplyFactor(amount, model.StaminaHealFactor);
//    }
//    private float StaminaRemoveFactor(float amount)
//    {
//        return ApplyFactor(amount, model.StaminaDamageFactor);
//    }
//    private float FoodAddFactor(float amount)
//    {
//        return ApplyFactor(amount, model.FoodFactor);
//    }
//    private float FoodRemoveFactor(float amount)
//    {
//        return ApplyFactor(amount, model.FoodRemoveFactor);
//    }
//    private float DrinkAddFactor(float amount)
//    {
//        return ApplyFactor(amount, model.DrinkFactor);
//    }

//    private float DrinkRemoveFactor(float amount)
//    {
//        return ApplyFactor(amount, model.DrinkRemoveFactor);
//    }
//    public IEnumerator ApplyStaminaEffectRoutine(string effectName, float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = staminaAmount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            float newAmount = amount;
//            if (newAmount > 0) newAmount = StaminaAddFactor(newAmount);
//            else newAmount = StaminaRemoveFactor(newAmount);
//            model.Stamina += newAmount;

//            // Wait for the specified tickCooldown duration
//            yield return new WaitForSeconds(tickCooldown);
//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.Stamina = Mathf.Min(model.Stamina, model.MaxStamina);
//    }

//    public IEnumerator ApplyHpEffectRoutine(string effectName, float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = hpAmount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            float newAmount = amount;
//            if (newAmount > 0) newAmount = HpAddFactor(newAmount);
//            else newAmount = HpRemoveFactor(newAmount);
//            model.Hp += newAmount;

//            // Wait for the specified tickCooldown duration
//            yield return new WaitForSeconds(tickCooldown);
//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.Hp = Mathf.Min(model.Hp, model.MaxHp);
//    }

//    public IEnumerator ApplyFoodEffectRoutine(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = foodAmount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            float newAmount = amount;
//            if (newAmount > 0) newAmount = FoodAddFactor(newAmount);
//            else newAmount = FoodRemoveFactor(newAmount);
//            model.Food += newAmount;

//            // Wait for the specified tickCooldown duration
//            yield return new WaitForSeconds(tickCooldown);
//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.Food = Mathf.Min(model.Food, model.MaxFood);
//    }


//    public IEnumerator ApplyDrinkEffectRoutine(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = drinkAmount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            float newAmount = amount;
//            if (newAmount > 0) newAmount = DrinkAddFactor(newAmount);
//            else newAmount = DrinkRemoveFactor(newAmount);
//            model.Drink += amount;

//            // Wait for the specified tickCooldown duration
//            yield return new WaitForSeconds(tickCooldown);
//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.Drink = Mathf.Min(model.Drink, model.MaxDrink);
//    }



//    public IEnumerator ApplyWeightIncreaseEffectRoutine(string effectName, float weightIncreaseAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = weightIncreaseAmount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

//        float maxWeightOriginalAmount = model.MaxWeight;


//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.MaxWeight += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.MaxWeight = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.MaxWeight = maxWeightOriginalAmount;
//    }

//    public IEnumerator ApplyStaminaHealFactorEffectRoutine(string effectName, float staminaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = staminaHealFactorAmount;
//        float staminaHealFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.StaminaHealFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.StaminaHealFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.StaminaHealFactor -= staminaHealFactorOriginalAmount;
//    }
//    public IEnumerator ApplyStaminaDamageFactorEffectRoutine(string effectName, float staminaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = staminaDamageFactorAmount;
//        float staminaDamageFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.StaminaHealFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.StaminaDamageFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.StaminaDamageFactor -= staminaDamageFactorOriginalAmount;
//    }

//    public IEnumerator ApplyHpHealFactorEffectRoutine(string effectName, float hpHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = hpHealFactorAmount;
//        float hpHealFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.HpHealFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.HpHealFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.HpHealFactor -= hpHealFactorOriginalAmount;
//    }
//    public IEnumerator ApplyHpDamageFactorEffectRoutine(string effectName, float hpDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = hpDamageFactorAmount;
//        float hpDamageFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.HpDamageFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.HpDamageFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.HpDamageFactor -= hpDamageFactorOriginalAmount;
//    }
//    public IEnumerator ApplyFoodAddFactorEffectRoutine(string effectName, float foodAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = foodAddFactorAmount;
//        float foodAddFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.FoodFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.FoodFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.FoodFactor -= foodAddFactorOriginalAmount;
//    }

//    public IEnumerator ApplyFoodRemoveFactorEffectRoutine(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = foodRemoveFactorAmount;
//        float foodRemoveFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.FoodRemoveFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.FoodRemoveFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.FoodRemoveFactor -= foodRemoveFactorOriginalAmount;
//    }

//    public IEnumerator ApplyDrinkAddFactorEffectRoutine(string effectName, float drinkAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = drinkAddFactorAmount;
//        float drinkAddFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.DrinkFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.DrinkFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.DrinkFactor -= drinkAddFactorOriginalAmount;
//    }


//    public IEnumerator ApplyDrinkRemoveFactorEffectRoutine(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = drinkRemoveFactorAmount;
//        float drinkRemoveFactorOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.DrinkRemoveFactor += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.DrinkRemoveFactor = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.DrinkRemoveFactor -= drinkRemoveFactorOriginalAmount;
//    }

//    public IEnumerator ApplyStaminaRegenEffectRoutine(string effectName, float staminaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = staminaRegenAmount;
//        float staminaRegenOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.StaminaRegen += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.StaminaRegen = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.StaminaRegen -= staminaRegenOriginalAmount;
//    }
//    public IEnumerator ApplyHpRegenEffectRoutine(string effectName, float hpRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = hpRegenAmount;
//        float hpRegenOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                model.HpRegen += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                model.HpRegen = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        model.HpRegen -= hpRegenOriginalAmount;
//    }

//    public IEnumerator ApplySpeedEffectRoutine(string effectName, float speedAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        float amount = speedAmount;
//        float speedOriginalAmount = amount;
//        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;



//        while (Time.time < Time.time + timeBuffEffect)
//        {
//            if (isProcedural)
//            {
//                movementModel.Speed += amount;
//                yield return new WaitForSeconds(tickCooldown);
//                // Wait for the specified tickCooldown duration
//            }
//            else
//            {
//                movementModel.Speed = amount;
//            }

//        }

//        // Ensure the final stamina value is within the maximum limit
//        movementModel.Speed -= speedOriginalAmount;
//    }


//    private void AddEffect(List<CoroutineInfo> effectList, string effectName, float amount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false, Func<string, float, float, float, bool, bool, IEnumerator> applyEffectRoutine = null)
//    {
//        if (!isStackable)
//        {
//            // Check if there is already an effect with the same effectName
//            CoroutineInfo existingEffect = effectList.Find(e => e.effectName == effectName);

//            if (existingEffect != null)
//            {
//                // Stop the existing coroutine and remove it from the list
//                StopCoroutine(existingEffect.coroutine);
//                effectList.Remove(existingEffect);
//            }
//        }

//        Coroutine effectRoutine = null;
//        effectRoutine = StartCoroutine(applyEffectRoutine(effectName, amount, timeBuffEffect, tickCooldown, isProcedural, isStackable));
//        CoroutineInfo coroutineInfo = new CoroutineInfo
//        {
//            coroutine = effectRoutine,
//            effectName = effectName,
//            isBuff = (amount > 0) ? true : false
//        };

//        effectList.Add(coroutineInfo);

//        // Order list elements by effectName
//        effectList = effectList.OrderBy(c => c.effectName).ToList();
//    }

//    public void AddStaminaEffect(string effectName, float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.StaminaEffectRoutines, effectName, staminaAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaEffectRoutine);
//    }

//    public void AddHpEffect(string effectName, float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.HpEffectRoutines, effectName, hpAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpEffectRoutine);
//    }
//    public void AddFoodEffect(string effectName, float foodAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.FoodEffectRoutines, effectName, foodAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodEffectRoutine);
//    }

//    public void AddDrinkEffect(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.DrinkEffectRoutines, effectName, drinkAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkEffectRoutine);
//    }

//    public void AddWeightEffect(string effectName, float weightAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.WeightIncreaseEffectRoutines, effectName, weightAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyWeightIncreaseEffectRoutine);
//    }
//    public void AddStaminaHealFactorEffect(string effectName, float staminaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.StaminaEffectRoutines, effectName, staminaHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaHealFactorEffectRoutine);
//    }
//    public void AddStaminaDamageFactorEffect(string effectName, float staminaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.StaminaEffectRoutines, effectName, staminaDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaDamageFactorEffectRoutine);
//    }
//    public void AddHpHealFactorEffect(string effectName, float hpHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.HpEffectRoutines, effectName, hpHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpHealFactorEffectRoutine);
//    }
//    public void AddHpDamageFactorEffect(string effectName, float hpDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.HpEffectRoutines, effectName, hpDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpDamageFactorEffectRoutine);
//    }
//    public void AddFoodAddFactorEffect(string effectName, float foodFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.FoodEffectRoutines, effectName, foodFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodAddFactorEffectRoutine);
//    }

//    public void AddFoodRemoveFactorEffect(string effectName, float foodRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.FoodEffectRoutines, effectName, foodRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyFoodRemoveFactorEffectRoutine);
//    }

//    public void AddDrinkAddFactorEffect(string effectName, float drinkFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.DrinkEffectRoutines, effectName, drinkFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkAddFactorEffectRoutine);
//    }

//    public void AddDrinkRemoveFactorEffect(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.DrinkEffectRoutines, effectName, drinkRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkRemoveFactorEffectRoutine);
//    }

//    public void AddHpRegenEffect(string effectName, float hpRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.HpEffectRoutines, effectName, hpRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkRemoveFactorEffectRoutine);
//    }

//    public void AddStaminaRegenEffect(string effectName, float staminaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
//    {
//        AddEffect(model.StaminaEffectRoutines, effectName, staminaRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkRemoveFactorEffectRoutine);
//    }

//    private void RemoveEffect(List<CoroutineInfo> effectList, string effectName = null, Coroutine effectRoutine = null)
//    {
//        // Remove effect based on effectName or effectRoutine
//        CoroutineInfo effectToRemove = null;

//        if (effectName != null)
//        {
//            effectToRemove = effectList.Find(e => e.effectName == effectName);
//        }
//        else if (effectRoutine != null)
//        {
//            effectToRemove = effectList.Find(e => e.coroutine == effectRoutine);
//        }

//        if (effectToRemove != null)
//        {
//            StopCoroutine(effectToRemove.coroutine);
//            effectList.Remove(effectToRemove);
//        }
//    }


//    public void RemoveStaminaEffect(string effectName = null, Coroutine effectRoutine = null)
//    {
//        RemoveEffect(model.StaminaEffectRoutines, effectName, effectRoutine);
//    }

//    public void RemoveHpEffect(string effectName = null, Coroutine effectRoutine = null)
//    {
//        RemoveEffect(model.HpEffectRoutines, effectName, effectRoutine);
//    }

//    public void RemoveFoodEffect(string effectName = null, Coroutine effectRoutine = null)
//    {
//        RemoveEffect(model.FoodEffectRoutines, effectName, effectRoutine);
//    }


//    public void RemoveDrinkEffect(string effectName = null, Coroutine effectRoutine = null)
//    {
//        RemoveEffect(model.DrinkEffectRoutines, effectName, effectRoutine);
//    }
//    private void StopAllEffects(List<CoroutineInfo> effectRoutines)
//    {
//        foreach (CoroutineInfo routine in effectRoutines)
//        {
//            StopCoroutine(routine.coroutine);
//        }
//        effectRoutines.Clear();
//    }

//    public void StopAllStaminaEffects()
//    {
//        StopAllEffects(model.StaminaEffectRoutines);
//    }

//    public void StopAllHpEffects()
//    {
//        StopAllEffects(model.HpEffectRoutines);
//    }

//    public void StopAllFoodEffects()
//    {
//        StopAllEffects(model.FoodEffectRoutines);
//    }

//    public void StopAllDrinkEffects()
//    {
//        StopAllEffects(model.DrinkEffectRoutines);
//    }


//    private void StopAllEffectsByType(List<CoroutineInfo> effectRoutines, bool isBuff = true)
//    {
//        List<CoroutineInfo> routinesToRemove = effectRoutines
//            .Where(info => (isBuff && info.isBuff) || (!isBuff && !info.isBuff))
//            .ToList();

//        foreach (CoroutineInfo coroutineInfo in routinesToRemove)
//        {
//            StopCoroutine(coroutineInfo.coroutine);
//            effectRoutines.Remove(coroutineInfo);
//        }
//    }

//    public void StopAllStaminaEffectsByType(bool isBuff = true)
//    {
//        StopAllEffectsByType(model.StaminaEffectRoutines, isBuff);
//    }

//    public void StopAllHpEffectsByType(bool isBuff = true)
//    {
//        StopAllEffectsByType(model.HpEffectRoutines, isBuff);
//    }

//    public void StopAllFoodEffectsByType(bool isBuff = true)
//    {
//        StopAllEffectsByType(model.FoodEffectRoutines, isBuff);
//    }

//    public void StopAllDrinkEffectsByType(bool isBuff = true)
//    {
//        StopAllEffectsByType(model.DrinkEffectRoutines, isBuff);
//    }
//    public void StopAllDebuffEffects()
//    {
//        StopAllEffectsByType(model.StaminaEffectRoutines, false);
//        StopAllEffectsByType(model.HpEffectRoutines, false);
//        StopAllEffectsByType(model.FoodEffectRoutines, false);
//        StopAllEffectsByType(model.DrinkEffectRoutines, false);
//    }
//    public void StopAllBuffEffects()
//    {
//        StopAllEffectsByType(model.StaminaEffectRoutines, true);
//        StopAllEffectsByType(model.HpEffectRoutines, true);
//        StopAllEffectsByType(model.FoodEffectRoutines, true);
//        StopAllEffectsByType(model.DrinkEffectRoutines, true);
//    }
//    public float CalculateSpeedBasedOnWeight(float speed)
//    {
//        if (!model.ShouldHaveWeight) return speed;
//        float weightPercentage = model.Weight / model.MaxWeight;
//        float speedReductionByWeight = -model.SpeedReductionByWeightConstant;
//        if (weightPercentage >= 0.82f && weightPercentage <= 1.0f) speedReductionByWeight -= 0.255f;
//        else if (model.Weight > model.MaxWeight) speedReductionByWeight -= 0.5f;
//        float currentSpeedBasedOnWeightCarried = speed * Mathf.Exp(speedReductionByWeight * weightPercentage);
//        return currentSpeedBasedOnWeightCarried;
//    }


//}


////public void RemoveHpEffect(string effectName = null, Coroutine effectRoutine = null)
////    {
////        // Remove effect based on effectName
////        if (effectName != null)
////        {
////            CoroutineInfo effectToRemove = model.StaminaEffectRoutines.Find(e => e.effectName == effectName);

////            if (effectToRemove != null)
////            {
////                StopCoroutine(effectToRemove.coroutine);
////                model.StaminaEffectRoutines.Remove(effectToRemove);
////            }
////        }

////        // Remove effect based on effectRoutine
////        if (effectRoutine != null)
////        {
////            CoroutineInfo effectToRemove = model.StaminaEffectRoutines.Find(e => e.coroutine == effectRoutine);

////            if (effectToRemove != null)
////            {
////                StopCoroutine(effectToRemove.coroutine);
////                model.StaminaEffectRoutines.Remove(effectToRemove);
////            }
////        }
//////    }
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEditor.Playables;
//using UnityEngine;



//[System.Serializable]
//public class AbilityHolder
//{
//    public enum AbilityState
//    {
//        Ready,
//        Casting,
//        Launching,
//        Active,
//        InCooldown
//    }
//    [SerializeField] public AbilityEffect abilityEffect;
//    [SerializeField] public float activeTime;
//    [SerializeField] public float cooldownTime;
//    [SerializeField] public AbilityState abilityState = AbilityState.Ready;
//    [SerializeField] public List<AttackCast> attackCast;
//    [SerializeField] public GameObject particle;



//}

//public class MobAttackController : MonoBehaviour
//{
//    [SerializeField] private MobActionsController mobActionController;
//    [SerializeField] private Transform targetTransform;
//    private Transform oldTransform;
//    [SerializeField] private List<AbilityHolder> abilities;

//    private void Awake()
//    {
//        if (!mobActionController) transform.GetComponent<MobActionsController>();
//        if (!targetTransform) targetTransform = transform;
//        foreach (var ability in abilities)
//        {
//            if (ability.particle) ability.abilityEffect.particle = ability.particle;

//            foreach (var mobAttackEffect in ability.abilityEffect.effects)
//            {
//                mobAttackEffect.attackCast = ability.attackCast;
//            }

//            if (!ability.abilityEffect.targetTransform) ability.abilityEffect.targetTransform = targetTransform;
//            if (!ability.abilityEffect.statusController) ability.abilityEffect.statusController = transform.GetComponent<MobStatusController>();
//        }

//    }
//    private void OnDrawGizmos()
//    {
//        foreach (var ability in abilities)
//            foreach (var mobAttackEffect in ability.abilityEffect.effects)
//                foreach (var attackCast in mobAttackEffect.attackCast)
//                    attackCast.DrawGizmos(targetTransform);

//    }

//    private void Update()
//    {
//        foreach (var ability in abilities)
//        {
//            if (mobActionController.CurrentPlayerTarget == null || ability.abilityState != AbilityHolder.AbilityState.Ready)
//                continue;

//            Transform playerTransform = mobActionController.CurrentPlayerTarget.transform;
//            GameObject instantiatedParticle = Instantiate(ability.abilityEffect.particle);
//            SetParticleDuration(instantiatedParticle, ability);
//            instantiatedParticle.transform.position = targetTransform.position;
//            if (!ability.abilityEffect.isFixedPosition && ability.abilityEffect.shouldMarkAtCast)
//            {
//                ability.abilityEffect.targetTransform = GetPlayerTransform(playerTransform);
//                targetTransform = ability.abilityEffect.targetTransform;
//                //GameObject instantiatedParticle = Instantiate(ability.abilityEffect.particle);
//            }

//            if (ability.abilityEffect.castDuration != 0)
//            {
//                ability.abilityState = AbilityHolder.AbilityState.Casting;
//                if (ability.abilityEffect.isPartialPermanentTargetWhileCasting)
//                    StartCoroutine(SetPermanentTargetOnCastRoutine(ability, playerTransform, instantiatedParticle));
//                else
//                    StartCoroutine(SetTargetOnCastRoutine(ability, playerTransform, instantiatedParticle));
//            }
//            else
//            {
//                if (ability.abilityEffect.isPermanentTarget)
//                    StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle));
//                else
//                    StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle));
//            }
//        }
//    }
//    private void SetParticleDuration(GameObject particleInstantiated, AbilityHolder ability)
//    {
//        ParticleSystem particleSystem = particleInstantiated.GetComponent<ParticleSystem>();
//        ParticleSystem.MainModule mainModule = particleSystem.main;
//        mainModule.startDelay = 0;
//        //mainModule.startSizeX = ability.;
//        if (ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast)
//        {
//            Debug.Log("a");
//            mainModule.duration = ability.abilityEffect.castDuration + ability.abilityEffect.finalLaunchTime + ability.abilityEffect.activeTime;
//            mainModule.startLifetime = ability.abilityEffect.castDuration + ability.abilityEffect.finalLaunchTime + ability.abilityEffect.activeTime;
//            particleSystem.Play();
//            foreach (var particle in ability.abilityEffect.particle.GetComponentsInChildren<ParticleSystem>())
//            {
//                ParticleSystem.MainModule mainModuleSubParticle = particle.main;
//                mainModuleSubParticle.startDelay = ability.abilityEffect.finalLaunchTime;
//                mainModuleSubParticle.startLifetime = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.activeTime;
//                particle.Play();
//            }
//        }
//        else
//        {
//            mainModule.duration = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.activeTime;
//            mainModule.startLifetime = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.activeTime;
//            particleSystem.Play();
//            foreach (var particle in ability.abilityEffect.particle.GetComponentsInChildren<ParticleSystem>())
//            {
//                ParticleSystem.MainModule mainModuleSubParticle = particle.main;
//                mainModuleSubParticle.startDelay = ability.abilityEffect.finalLaunchTime;
//                mainModuleSubParticle.startLifetime = ability.abilityEffect.activeTime;
//                particle.Play();
//            }
//        }
//    }
//    private void SetGizmosAndColliderAndParticlePosition(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle, bool isPermanent = false)
//    {
//        if (!ability.abilityEffect.isFixedPosition)
//        {
//            if (isPermanent)
//            {
//                ability.abilityEffect.targetTransform = playerTransform;
//                targetTransform = playerTransform;
//            }
//            else
//            {
//                ability.abilityEffect.targetTransform = GetPlayerTransform(playerTransform);
//                targetTransform = GetPlayerTransform(playerTransform);
//            }
//            instantiatedParticle.transform.position = targetTransform.position;
//        }

//    }

//    private Transform GetPlayerTransform(Transform playerTransform)
//    {
//        Transform newPlayerTransform = new GameObject("PlayerLastTransform").transform;
//        if (oldTransform == null) oldTransform = newPlayerTransform;
//        else
//        {
//            Destroy(oldTransform.gameObject);
//            oldTransform = newPlayerTransform;
//        }
//        newPlayerTransform.position = playerTransform.position;
//        newPlayerTransform.rotation = playerTransform.rotation;
//        return newPlayerTransform;
//    }

//    private IEnumerator CooldownAbilityRoutine(AbilityHolder ability)
//    {
//        yield return new WaitForSeconds(ability.abilityEffect.coolDown);
//        ability.abilityState = AbilityHolder.AbilityState.Ready;
//    }

//    private IEnumerator ActiveAbilityRoutine(AbilityHolder ability)
//    {
//        yield return new WaitForSeconds(ability.abilityEffect.duration);
//        ability.abilityState = AbilityHolder.AbilityState.InCooldown;
//        StartCoroutine(CooldownAbilityRoutine(ability));
//    }

//    private IEnumerator SetPermanentTargetOnCastRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle)
//    {
//        ability.abilityState = AbilityHolder.AbilityState.Casting;
//        float startTime = Time.time;
//        while (Time.time <= startTime + ability.abilityEffect.castDuration)
//        {
//            SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle, true);
//            yield return null;
//        }

//        SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle, false);
//        if (ability.abilityEffect.isPermanentTarget)
//            StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle));
//        else
//            StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle));
//    }

//    private IEnumerator SetTargetOnCastRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle)
//    {
//        ability.abilityState = AbilityHolder.AbilityState.Casting;
//        if (ability.abilityEffect.shouldMarkAtCast == true) SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle);


//        float startTime = Time.time;
//        while (Time.time <= startTime + ability.abilityEffect.castDuration)
//            yield return null;

//        if (ability.abilityEffect.isPermanentTarget)
//            StartCoroutine(SetPermanentTargetLaunchRoutine(ability, playerTransform, instantiatedParticle));
//        else
//            StartCoroutine(DelayedSetTargetLaunchRoutine(ability, playerTransform, instantiatedParticle));
//    }

//    private IEnumerator SetPermanentTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle)
//    {
//        ability.abilityState = AbilityHolder.AbilityState.Launching;
//        float startTime = Time.time;
//        while (Time.time <= startTime + ability.abilityEffect.finalLaunchTime)
//        {
//            SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle, true);
//            yield return null;
//        }

//        ApplyAbilityUse(ability);
//    }

//    private IEnumerator DelayedSetTargetLaunchRoutine(AbilityHolder ability, Transform playerTransform, GameObject instantiatedParticle)
//    {
//        if (!ability.abilityEffect.shouldMarkAtCast) SetGizmosAndColliderAndParticlePosition(ability, playerTransform, instantiatedParticle);
//        ability.abilityState = AbilityHolder.AbilityState.Launching;
//        yield return new WaitForSeconds(ability.abilityEffect.finalLaunchTime);
//        ApplyAbilityUse(ability);
//    }

//    private void ApplyAbilityUse(AbilityHolder ability)
//    {
//        //ability.abilityEffect.Use();
//        foreach (var effect in ability.abilityEffect.effects)
//        {
//            if (effect.enemyEffect == false) ability.abilityEffect.AbnormalUse(transform, effect);
//            else ability.abilityEffect.AbnormalUse(targetTransform, effect);
//        }
//        ability.abilityState = AbilityHolder.AbilityState.Active;
//        ability.activeTime = Time.time;
//        StartCoroutine(ActiveAbilityRoutine(ability));
//    }
//}

