using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackExecutor
{
    private WeaponController controller;
    private AttackState attackState = new AttackState();
    private Coroutine currentAttackCoroutine;
    private readonly HashSet<Collider> hitTargets = new HashSet<Collider>();

    // Dependencies
    private ComboSystem comboSystem;
    private VariationSystem variationSystem;
    private InputBufferSystem inputBufferSystem;
    private AttackAnimationHandler animationHandler;
    private WeaponEffectsManager effectsManager;

    // Properties
    public bool IsAttacking => attackState.IsActive || (controller.GetAnimController()?.Model?.IsAttacking ?? false);
    public AttackAction CurrentAttackAction => attackState.Action;
    public AttackVariation CurrentAttackVariation => attackState.Variation;
    public IAttackComponent CurrentAttackComponent => attackState.CurrentComponent;

    public AttackExecutor(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetDependencies(ComboSystem comboSystem, VariationSystem variationSystem,
        InputBufferSystem inputBufferSystem, AttackAnimationHandler animationHandler,
        WeaponEffectsManager effectsManager)
    {
        this.comboSystem = comboSystem;
        this.variationSystem = variationSystem;
        this.inputBufferSystem = inputBufferSystem;
        this.animationHandler = animationHandler;
        this.effectsManager = effectsManager;
    }

    public void PerformAttack(GameObject player, AttackType attackType)
    {
        controller.LogDebug($"PerformAttack called with type: {attackType}");

        if (!CanPerformAttack(attackType, player))
        {
            controller.LogDebug("Attack preconditions failed", true);
            return;
        }

        if (attackState.IsActive && controller.EnableInputBuffer)
        {
            controller.LogDebug("Currently attacking, buffering input");
            inputBufferSystem.BufferInput(attackType, player);
            return;
        }

        ExecuteAttack(player, attackType);
    }

    public void ExecuteAttack(GameObject player, AttackType attackType)
    {
        controller.LogDebug($"ExecuteAttack: {attackType}");

        // Check if combo should be triggered
        if (controller.EnableComboSystem && controller.EquippedWeapon.HasCombos() &&
            comboSystem.TryExecuteCombo(player, attackType))
        {
            return;
        }

        // Get the appropriate action and variation
        var (action, variation) = variationSystem.GetAttackActionWithVariation(attackType);
        if (action == null)
        {
            controller.LogDebug($"No action available for type: {attackType}", true);
            return;
        }

        // Check trait requirements for the action
        var traitManager = player.GetComponent<TraitManager>();
        if (!action.CanPerformWithTraits(traitManager, controller.EquippedWeapon))
        {
            controller.LogDebug($"Trait requirements not met for action: {action.actionName}", true);
            return;
        }

        // Apply weapon traits to the action
        var playerController = player.GetComponent<PlayerStatusController>();
        if (playerController != null)
        {
            controller.EquippedWeapon.ApplyWeaponTraitsToAttack(action, playerController);
        }

        // Update variation state
        variationSystem.UpdateVariationState(attackType, action);

        // Update combo sequence
        comboSystem.UpdateComboSequence(attackType);

        // Start the attack
        StartAttack(player, action, variation);
    }

    public void StartAttack(GameObject player, AttackAction action, AttackVariation variation = null, ComboSequence combo = null)
    {
        string attackName = variation?.variationName ?? action.actionName;
        controller.LogDebug($"Starting attack: {attackName}");

        attackState.IsActive = true;
        attackState.SetAttackComponent(action, variation);
        attackState.AnimationTime = 0f;
        attackState.IsComboFinisher = combo != null;

        var component = attackState.CurrentComponent;

        // Consume stamina
        var playerController = player.GetComponent<PlayerStatusController>();
        if (playerController != null)
        {
            float staminaCost = controller.EquippedWeapon.CalculateTraitModifiedStaminaCost(component.StaminaCost);
            playerController.StaminaManager.ConsumeStamina(staminaCost);
        }

        ApplyMovementRestrictions(player, component);
        animationHandler.TriggerAttackAnimation(component);

        if (component.ForwardMovement != Vector3.zero)
            controller.StartCoroutine(ApplyForwardMovement(player, component));

        StartAttackCoroutine(player, component);
    }

    private bool CanPerformAttack(AttackType attackType, GameObject player)
    {
        if (controller.EquippedWeapon == null || controller.GetAnimController() == null || attackState.IsActive)
            return false;

        // Check stamina requirement
        var weapon = controller.EquippedWeapon;
        var action = weapon.GetAction(attackType);
        if (action == null) return false;

        float staminaCost = weapon.CalculateTraitModifiedStaminaCost(action.staminaCost);
        var playerController = player.GetComponent<PlayerStatusController>();

        if (playerController != null && playerController.StaminaManager.CurrentValue < staminaCost)
        {
            controller.LogDebug($"Not enough stamina. Required: {staminaCost}, Current: {playerController.StaminaManager.CurrentValue}");
            return false;
        }

        return true;
    }

    private IEnumerator HandleAttackExecution(GameObject player, IAttackComponent component)
    {
        float totalDuration = component.GetTotalDuration();
        float elapsedTime = 0f;

        controller.LogDebug($"Starting attack execution. Duration: {totalDuration}s");

        yield return null; // Wait one frame
        yield return WaitForAnimationLock();

        hitTargets.Clear();

        while (elapsedTime < totalDuration && attackState.IsActive)
        {
            elapsedTime += Time.deltaTime;
            attackState.AnimationTime = elapsedTime / totalDuration;

            if (component.IsInActiveFrames(attackState.AnimationTime))
            {
                PerformCollisionDetection(player, component);
            }

            yield return null;
        }

        EndAttack();
    }

    private void PerformCollisionDetection(GameObject player, IAttackComponent component)
    {
        if (controller.EquippedWeapon?.attackCast == null) return;

        var colliders = controller.EquippedWeapon.attackCast.DetectObjects(controller.HandGameObject.transform);
        foreach (var collider in colliders)
        {
            if (IsValidTarget(collider, player) && !hitTargets.Contains(collider))
            {
                ProcessHit(collider, player, component);
                hitTargets.Add(collider);

                // Set current target for combo system
                comboSystem.SetCurrentTarget(collider.gameObject);
            }
        }
    }

    private bool IsValidTarget(Collider collider, GameObject playerObject)
    {
        return collider != null && collider.gameObject != playerObject;
    }

    private void ProcessHit(Collider collider, GameObject player, IAttackComponent component)
    {
        var weapon = controller.EquippedWeapon;
        var playerController = player.GetComponent<PlayerStatusController>();
        var traitManager = player.GetComponent<TraitManager>();

        // Calculate base damage
        float baseDamage = weapon.CalculateDamage(component as AttackAction);

        // Apply weapon trait damage modifiers
        float weaponTraitDamage = weapon.CalculateTraitModifiedDamage(baseDamage);

        // Apply combo damage multiplier
        float comboDamage = weaponTraitDamage * comboSystem.GetCurrentComboDamageMultiplier();

        // Apply player trait damage modifiers
        float finalDamage = ApplyPlayerTraitDamageModifiers(comboDamage, traitManager);

        // Apply elemental damage based on traits
        if (weapon.ElementType != ElementType.None)
        {
            var targetElement = GetTargetElement(collider.gameObject);
            float elementMultiplier = CalculateTraitBasedElementalMultiplier(weapon, traitManager, weapon.ElementType, targetElement);
            finalDamage *= elementMultiplier;

            // Apply elemental visual effects
            PlayElementalEffects(weapon.ElementType, collider.transform.position);

            // Check for elemental reactions
            CheckElementalReactions(weapon.ElementType, targetElement, collider.transform.position, player, collider.gameObject);
        }

        // Track damage for combo scoring
        comboSystem.AddDamageDealt(Mathf.RoundToInt(finalDamage));

        // Process trait interactions
        ProcessTraitInteractions(weapon, traitManager, collider.gameObject, finalDamage, playerController);

        // Original hit processing
        controller.EquippedWeapon.ApplyEffectsToTarget(collider.gameObject, player, component);
        effectsManager.PlayHitEffects(component, collider.transform.position);

        controller.LogDebug($"Hit target: {collider.name} for {finalDamage:F1} damage");
    }

    private float ApplyPlayerTraitDamageModifiers(float baseDamage, TraitManager traitManager)
    {
        float modifiedDamage = baseDamage;

        if (traitManager == null) return modifiedDamage;

        foreach (var trait in traitManager.ActiveTraits)
        {
            if (trait == null) continue;

            foreach (var effect in trait.effects)
            {
                if (effect.targetStat.ToLower() == "damage" || effect.targetStat.ToLower() == "attack")
                {
                    switch (effect.effectType)
                    {
                        case TraitEffectType.StatMultiplier:
                            modifiedDamage *= effect.value;
                            break;
                        case TraitEffectType.StatAddition:
                            modifiedDamage += effect.value;
                            break;
                    }
                }
            }
        }

        return modifiedDamage;
    }

    private ElementType GetTargetElement(GameObject target)
    {
        // Check if target has elemental component
        var elementalComponent = target.GetComponent<IElemental>();
        if (elementalComponent != null)
        {
            return elementalComponent.ElementType;
        }

        // Check for elemental trait on target
        var targetTraitManager = target.GetComponent<TraitManager>();
        if (targetTraitManager != null)
        {
            foreach (var trait in targetTraitManager.ActiveTraits)
            {
                // Check if trait provides elemental affinity
                foreach (var effect in trait.effects)
                {
                    if (effect.targetStat.ToLower().StartsWith("element_"))
                    {
                        string elementName = effect.targetStat.Substring(8); // Remove "element_" prefix
                        if (System.Enum.TryParse<ElementType>(elementName, true, out ElementType element))
                        {
                            return element;
                        }
                    }
                }
            }
        }

        return ElementType.None;
    }

    private float CalculateTraitBasedElementalMultiplier(WeaponSO weapon, TraitManager playerTraits, ElementType attackElement, ElementType targetElement)
    {
        float multiplier = 1.0f;

        // Get weapon's elemental damage multiplier from traits
        multiplier *= weapon.GetElementalDamageMultiplier(attackElement, targetElement);

        // Check player traits for elemental damage modifiers
        if (playerTraits != null)
        {
            foreach (var trait in playerTraits.ActiveTraits)
            {
                if (trait == null) continue;

                foreach (var effect in trait.effects)
                {
                    // Check for specific elemental matchup modifiers
                    string statKey = $"{attackElement.ToString().ToLower()}_vs_{targetElement.ToString().ToLower()}";
                    if (effect.targetStat.ToLower() == statKey)
                    {
                        if (effect.effectType == TraitEffectType.StatMultiplier)
                        {
                            multiplier *= effect.value;
                        }
                    }

                    // Check for general elemental damage boost
                    if (effect.targetStat.ToLower() == $"elemental_{attackElement.ToString().ToLower()}_damage")
                    {
                        if (effect.effectType == TraitEffectType.StatMultiplier)
                        {
                            multiplier *= effect.value;
                        }
                    }
                }
            }
        }

        // Check if ElementalSystem is available for additional calculations
        if (ElementalSystem.Instance != null)
        {
            multiplier *= ElementalSystem.Instance.GetElementalDamageMultiplier(attackElement, targetElement);
        }

        return multiplier;
    }

    private void CheckElementalReactions(ElementType attackElement, ElementType targetElement, Vector3 position, GameObject source, GameObject target)
    {
        if (ElementalSystem.Instance != null && targetElement != ElementType.None)
        {
            ElementalSystem.Instance.TriggerElementalReaction(attackElement, targetElement, position, source, target);
        }
    }

    private void PlayElementalEffects(ElementType element, Vector3 position)
    {
        if (ElementalSystem.Instance != null)
        {
            var particles = ElementalSystem.Instance.GetElementParticles(element);
            if (particles != null)
            {
                var instance = Object.Instantiate(particles, position, Quaternion.identity);
                instance.Play();
                Object.Destroy(instance.gameObject, instance.main.duration + 1f);
            }
        }
    }

    private void ProcessTraitInteractions(WeaponSO weapon, TraitManager traitManager, GameObject target, float damage, PlayerStatusController player)
    {
        // Process weapon trait interactions
        foreach (var weaponTrait in weapon.WeaponTraits)
        {
            if (weaponTrait == null) continue;

            ProcessSingleTraitInteraction(weaponTrait, target, damage, player);
        }

        // Process player trait interactions
        if (traitManager != null)
        {
            foreach (var playerTrait in traitManager.ActiveTraits)
            {
                if (playerTrait == null) continue;

                ProcessSingleTraitInteraction(playerTrait, target, damage, player);
            }
        }
    }

    private void ProcessSingleTraitInteraction(Trait trait, GameObject target, float damage, PlayerStatusController player)
    {
        foreach (var effect in trait.effects)
        {
            // Life steal effect
            if (effect.targetStat.ToLower() == "lifesteal" || effect.targetStat.ToLower() == "vampiric")
            {
                float healAmount = damage * effect.value;
                player?.HpManager.AddCurrentValue(healAmount);
                controller.LogDebug($"{trait.Name} healing: {healAmount:F1}");
            }

            // AOE damage effect
            if (effect.targetStat.ToLower() == "aoe" || effect.targetStat.ToLower() == "explosive")
            {
                float procChance = effect.value;
                if (Random.value < procChance)
                {
                    float aoeRange = 3f; // Could be another trait effect value
                    var nearbyEnemies = Physics.OverlapSphere(target.transform.position, aoeRange);
                    foreach (var enemy in nearbyEnemies)
                    {
                        if (enemy.gameObject != target && enemy.gameObject != controller.gameObject)
                        {
                            float aoeDamage = damage * 0.5f; // Could be defined in trait
                            controller.EquippedWeapon.ApplyEffectsToTarget(enemy.gameObject, controller.gameObject, CurrentAttackComponent);
                            controller.LogDebug($"{trait.Name} AOE hit: {enemy.name} for {aoeDamage:F1}");
                        }
                    }
                }
            }

            // Apply on-hit debuffs
            if (effect.effectType == TraitEffectType.Special && effect.targetStat.ToLower().Contains("on_hit"))
            {
                ApplyOnHitEffect(trait, effect, target);
            }
        }
    }

    private void ApplyOnHitEffect(Trait trait, TraitEffect effect, GameObject target)
    {
        var targetController = target.GetComponent<BaseStatusController>();
        if (targetController == null) return;

        // Parse the on_hit effect type from targetStat
        // Format: "on_hit_slow", "on_hit_poison", etc.
        string[] parts = effect.targetStat.ToLower().Split('_');
        if (parts.Length < 3) return;

        string effectType = parts[2]; // Get the effect type after "on_hit_"

        var debuffEffect = new AttackEffect
        {
            effectName = trait.Name + "_" + effectType,
            amount = effect.value,
            timeBuffEffect = 3f // Default duration, could be specified in trait
        };

        // Map effect type to AttackEffectType
        switch (effectType)
        {
            case "slow":
                debuffEffect.effectType = AttackEffectType.Speed;
                debuffEffect.amount = -effect.value;
                break;
                // Add more as needed
        }

        targetController.ApplyEffect(debuffEffect, debuffEffect.amount, debuffEffect.timeBuffEffect, 0);
    }

    private bool CanPerformAttack()
    {
        return controller.EquippedWeapon != null && controller.GetAnimController() != null && !attackState.IsActive;
    }

    private void ApplyMovementRestrictions(GameObject player, IAttackComponent component)
    {
        var playerMovement = player.GetComponent<PlayerMovement>();
        if (playerMovement == null) return;

        controller.LogDebug(component.LockMovement ? "Locking player movement" :
                $"Setting movement speed multiplier: {component.MovementSpeedMultiplier}");
        // Implement movement restrictions based on your PlayerMovement component
    }

    private IEnumerator ApplyForwardMovement(GameObject player, IAttackComponent component)
    {
        var rigidbody = player.GetComponent<Rigidbody>();
        if (rigidbody == null) yield break;

        float duration = component.StartupFrames + component.ActiveFrames;
        float elapsed = 0f;
        Vector3 frameMovement = component.ForwardMovement * Time.fixedDeltaTime / duration;

        while (elapsed < duration)
        {
            rigidbody.MovePosition(rigidbody.position + player.transform.forward * frameMovement.magnitude);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    private void StartAttackCoroutine(GameObject player, IAttackComponent component)
    {
        if (currentAttackCoroutine != null) controller.StopCoroutine(currentAttackCoroutine);
        currentAttackCoroutine = controller.StartCoroutine(HandleAttackExecution(player, component));
    }

    private IEnumerator WaitForAnimationLock()
    {
        float lockWaitTime = 0f;
        var animController = controller.GetAnimController();
        while (lockWaitTime < 0.1f && !animController.IsAttackAnimationLocked())
        {
            lockWaitTime += Time.deltaTime;
            yield return null;
        }

        controller.LogDebug(animController.IsAttackAnimationLocked() ?
            "Attack animation successfully locked" :
            "Warning: Attack animation may not be properly locked");
    }

    public void ResetAttackState()
    {
        attackState.Reset();
        hitTargets.Clear();
    }

    public void EndAttack()
    {
        controller.LogDebug("Ending attack");
        ResetAttackState();
        currentAttackCoroutine = null;
    }

    public void CleanupAllCoroutines()
    {
        if (currentAttackCoroutine != null)
        {
            controller.StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }
    }

    public void DrawDebugInfo(Transform handTransform)
    {
        if (attackState.CurrentComponent == null) return;

        Gizmos.color = Color.green;
        Vector3 labelPos = handTransform.position + Vector3.up * 2f;

#if UNITY_EDITOR
        string attackName = (attackState.CurrentComponent as AttackVariation)?.variationName ??
                           (attackState.CurrentComponent as AttackAction)?.actionName ?? "Unknown";
        string info = $"Attack: {attackName}\n" +
                     $"Progress: {attackState.AnimationTime:F2}\n" +
                     $"Weapon: {(attackState.IsActive ? "ACTIVE" : "IDLE")}\n" +
                     $"Animation: {(controller.GetAnimController()?.IsAttackAnimationLocked() == true ? "LOCKED" : "UNLOCKED")}\n" +
                     $"Model: {(controller.GetAnimController()?.Model?.IsAttacking == true ? "ATTACKING" : "IDLE")}\n" +
                     $"Variation: {(attackState.Variation != null ? "YES" : "NO")}";

        UnityEditor.Handles.Label(labelPos, info);
#endif
    }

    // Nested Classes
    private class AttackState
    {
        public bool IsActive;
        public AttackAction Action;
        public AttackVariation Variation;
        public IAttackComponent CurrentComponent;
        public float AnimationTime;
        public bool IsComboFinisher;

        public void Reset()
        {
            IsActive = false;
            Action = null;
            Variation = null;
            CurrentComponent = null;
            AnimationTime = 0f;
            IsComboFinisher = false;
        }

        public void SetAttackComponent(AttackAction action, AttackVariation variation = null)
        {
            Action = action;
            Variation = variation;
            CurrentComponent = variation ?? (IAttackComponent)action;
        }
    }
}

// Interface for elemental components
public interface IElemental
{
    ElementType ElementType { get; }
    float ElementalResistance { get; }
}