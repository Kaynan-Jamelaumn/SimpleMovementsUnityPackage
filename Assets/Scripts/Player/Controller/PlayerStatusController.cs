using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    }

    private void Update()
    {
        // Update stamina bar and initiate regeneration if conditions are met
        UpdateStaminaBar();
        if (!model.IsRegeneratingStamina && model.Stamina < model.MaxStamina && !model.IsConsumingStamina && !modelMovement.IsCrouching && !modelMovement.IsRunning && !modelMovement.IsJumping)
        {
            model.IsRegeneratingStamina = true;
            model.StaminaRegenerationRoutine = StartCoroutine(RegenerateStaminaRoutine());
        }
    }

    private void UpdateStaminaBar()
    {
        // Ensure that stamina does not exceed limits
        model.Stamina = Mathf.Clamp(model.Stamina, 0f, model.MaxStamina);

        // Calculate the percentage of the remaining stamina
        float staminaPercentage = model.Stamina / model.MaxStamina;
        model.StaminaImage.fillAmount = staminaPercentage;
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

            yield return new WaitForSeconds(0.5f); // Delay between regen updates
        }

        model.IsRegeneratingStamina = false;
    }

    // Method to signal the regeneration coroutine to stop
    public void StopStaminaRegeneration()
    {
        model.ShouldStopStaminaRegeneration = true;
    }

    // Coroutine to consume stamina over time
    public IEnumerator ConsumeStaminaRoutine(float amount = 0)
    {
        model.IsConsumingStamina = true;

        // Signal the regeneration coroutine to stop
        StopStaminaRegeneration();

        // Consume stamina while there is enough stamina available
        while (model.Stamina >= 0 && model.Stamina - amount >= 0)
        {
            model.Stamina -= amount;
            yield return new WaitForSeconds(0.5f); // Delay between regen updates
        }

        model.IsConsumingStamina = false;
    }

    // Check if there is enough stamina for consumption
    public bool HasEnoughStamina(float amount)
    {
        return model.Stamina - amount > 0;
    }

    // Method to directly consume stamina
    public void ConsumeStamina(float amount)
    {
        if (model.Stamina < 0) return;
        model.Stamina -= amount;
    }

    public void RegenHp(float amount)
    {
        if (model.Hp + amount>  model.MaxHp) model.Hp = model.MaxHp;
        model.Hp += amount;
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

            yield return new WaitForSeconds(0.5f); // Delay between regen updates
        }

        model.IsRegeneratingHp = false;
    }
}
