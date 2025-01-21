using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

public abstract class BaseStatusController : MonoBehaviour, IAssignmentsValidator
{
    protected virtual void Awake() => CacheComponents();
    protected virtual void Start() => ValidateAssignments();
    protected abstract void CacheComponents();
    public abstract void ValidateAssignments();
    protected virtual void HandleDeath() => Destroy(gameObject);
    protected void CacheManager<T>(ref T manager, string fieldName) where T : Component
    {
        manager = GetComponentOrLogError(ref manager, fieldName);
    }
    protected T GetComponentOrLogError<T>(ref T field, string fieldName) where T : Component
    {
        field = GetComponent<T>();
        Assert.IsNotNull(field, $"{fieldName} is not assigned.");
        return field;
    }
    public abstract void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown);
}
