using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
/// <summary>
/// Base class for managing the status of game entities.
/// Implements IAssignmentsValidator for assignment validation.
/// </summary>
public abstract class BaseStatusController : MonoBehaviour
{



    /// <summary>
    /// Abstract method for caching necessary components. Must be implemented by derived classes.
    /// </summary>


    /// <summary>
    /// Handles the death of the entity by destroying the game object.
    /// </summary>
    protected virtual void HandleDeath() => Destroy(gameObject);




    /// <summary>
    /// Applies an effect to the entity.
    /// </summary>
    /// <param name="effect">The effect to apply.</param>
    /// <param name="amount">The effect amount.</param>
    /// <param name="timeBuffEffect">The duration of the effect.</param>
    /// <param name="tickCooldown">Cooldown between ticks for procedural effects.</param>
    public abstract void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown);
}