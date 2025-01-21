using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
/// <summary>
/// Base class for managing the status of game entities.
/// Implements IAssignmentsValidator for assignment validation.
/// </summary>
public abstract class BaseStatusController : MonoBehaviour, IAssignmentsValidator
{
    /// <summary>
    /// Called during the Awake phase to cache necessary components.
    /// </summary>
    protected virtual void Awake() => CacheComponents();

    /// <summary>
    /// Called during the Start phase to validate assignments.
    /// </summary>
    protected virtual void Start() => ValidateAssignments();

    /// <summary>
    /// Abstract method for caching necessary components. Must be implemented by derived classes.
    /// </summary>
    protected abstract void CacheComponents();

    /// <summary>
    /// Abstract method for validating assignments. Must be implemented by derived classes.
    /// </summary>
    public abstract void ValidateAssignments();

    /// <summary>
    /// Handles the death of the entity by destroying the game object.
    /// </summary>
    protected virtual void HandleDeath() => Destroy(gameObject);

    /// <summary>
    /// Caches a manager component of type <typeparamref name="T"/>.
    /// Logs an error if the manager is not found.
    /// </summary>
    /// <typeparam name="T">Type of the manager component.</typeparam>
    /// <param name="manager">Reference to the manager.</param>
    /// <param name="fieldName">Name of the field for error logging.</param>
    protected void CacheManager<T>(ref T manager, string fieldName) where T : Component
    {
        manager = GetComponentOrLogError(ref manager, fieldName);
    }

    /// <summary>
    /// Gets a component of type <typeparamref name="T"/> and logs an error if not found.
    /// </summary>
    /// <typeparam name="T">Type of the component.</typeparam>
    /// <param name="field">Reference to the field to assign the component to.</param>
    /// <param name="fieldName">Name of the field for error logging.</param>
    /// <returns>The found component, or null if not found.</returns>
    protected T GetComponentOrLogError<T>(ref T field, string fieldName) where T : Component
    {
        field = GetComponent<T>();
        Assert.IsNotNull(field, $"{fieldName} is not assigned.");
        return field;
    }

    /// <summary>
    /// Applies an effect to the entity.
    /// </summary>
    /// <param name="effect">The effect to apply.</param>
    /// <param name="amount">The effect amount.</param>
    /// <param name="timeBuffEffect">The duration of the effect.</param>
    /// <param name="tickCooldown">Cooldown between ticks for procedural effects.</param>
    public abstract void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown);
}