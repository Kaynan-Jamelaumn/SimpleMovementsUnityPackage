using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Data shared by the states of one player ability slot (<see cref="AbilityStateMachine"/>).</summary>
public class AbilityContext : IDisposable
{
    private readonly PlayerInput playerInput;
    private readonly PlayerAnimationModel animationModel;
    private readonly PlayerAbilityController abilityController;
    private readonly PlayerAbilityHolder abilityHolder;
    private InputActionReference abilityActionReference;

    /// <summary>False while this ability blocks the player's other abilities (see the ability's Blocks Other Abilities).</summary>
    public bool cachedAvailability = true;
    /// <summary>Set by the abilities machine when the input was pressed; consumed by the Ready state.</summary>
    public bool triggered;
    /// <summary>True while a click-to-confirm preview is open for this slot.</summary>
    public bool abilityStillInProgress;
    public bool isWaitingForClick;

    // Kept for compatibility with older code; the new system does not use them.
    public Transform targetTransform;
    public GameObject instantiatedParticle;
    public AttackCast attackCast;

    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;
    public PlayerAbilityHolder AbilityHolder => abilityHolder;
    public InputActionReference AbilityActionReference { get => abilityActionReference; set => abilityActionReference = value; }

    /// <summary>The machine that owns this context.</summary>
    public AbilityStateMachine Machine { get; internal set; }

    /// <summary>Index of the slot in the PlayerAbilityController (-1 before registration).</summary>
    public int SlotIndex => Machine != null ? Machine.SlotIndex : -1;

    /// <summary>The slot this machine drives (null before registration).</summary>
    public AbilitySlot Slot => abilityController != null ? abilityController.GetSlot(SlotIndex) : null;

    public AbilityContext(PlayerInput playerInput, PlayerAnimationModel animationModel, PlayerAbilityController abilityController, PlayerAbilityHolder abilityHolder)
    {
        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
        this.abilityHolder = abilityHolder;
        abilityActionReference = abilityHolder != null ? abilityHolder.AbilityActionReference : null;
    }

    /// <summary>Raised when <see cref="cachedAvailability"/> changes.</summary>
    public event Action<bool> AvailabilityChanged;

    public void SetCachedAvailability(bool value)
    {
        if (cachedAvailability == value)
            return;
        cachedAvailability = value;
        AvailabilityChanged?.Invoke(value);
    }

    public void Dispose()
    {
        AvailabilityChanged = null;
    }
}
