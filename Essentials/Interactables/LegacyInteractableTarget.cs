using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Lets the <see cref="PlayerInteractor"/> use the older interaction objects - Interactable (doors, dungeon shrines,
/// chests), ItemPickable (items on the ground) and <see cref="Storage"/> - the way <c>Player.OnInteract</c> does. Only
/// used when the interactor's Handle Legacy Interactables is on.
/// <para>Interactable and ItemPickable are found by their type names at runtime, so this file compiles whether or not
/// (and in whatever form) those classes exist in the project.</para>
/// </summary>
public sealed class LegacyInteractableTarget : IInteractable
{
    private const string InteractableTypeName = "Interactable";
    private const string PickableTypeName = "ItemPickable";

    /// <summary>The Interactable (or derived) or Storage component.</summary>
    public Component Owner { get; }

    private readonly bool pickable;
    private readonly Storage storage;
    private readonly float range;
    private readonly MethodInfo interact;
    private readonly PropertyInfo interactionTime;
    private readonly FieldInfo pickableItem;
    private readonly FieldInfo pickableQuantity;

    public LegacyInteractableTarget(Component owner, float range)
    {
        Owner = owner;
        this.range = range;
        storage = owner as Storage;
        Type t = owner != null ? owner.GetType() : null;
        pickable = Is(t, PickableTypeName);
        if (t != null)
        {
            interact = t.GetMethod("Interact", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            interactionTime = t.GetProperty("InteractionTime", BindingFlags.Public | BindingFlags.Instance);
            pickableItem = t.GetField("itemScriptableObject", BindingFlags.Public | BindingFlags.Instance);
            pickableQuantity = t.GetField("quantity", BindingFlags.Public | BindingFlags.Instance);
        }
    }

    /// <summary>The older interaction component behind a collider (on it or a parent), or null.</summary>
    public static Component FindOwner(Collider col)
    {
        for (Transform t = col != null ? col.transform : null; t != null; t = t.parent)
        {
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                if (mb != null && (mb is Storage || Is(mb.GetType(), InteractableTypeName)))
                    return mb;
        }
        return null;
    }

    /// <summary>Is <paramref name="type"/> (or one of its base classes) named <paramref name="name"/>?</summary>
    private static bool Is(Type type, string name)
    {
        for (; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            if (type.Name == name)
                return true;
        return false;
    }

    public string InteractionName
    {
        get
        {
            if (pickable && pickableItem?.GetValue(Owner) is ItemSO item && item != null)
            {
                int qty = pickableQuantity?.GetValue(Owner) is int q ? q : 1;
                return qty > 1 ? $"{item.Name} ×{qty}" : item.Name;
            }
            if (storage != null && !string.IsNullOrWhiteSpace(storage.name))
                return storage.name;
            return Owner != null ? Owner.gameObject.name : "";
        }
    }

    public string InteractionVerb => pickable ? "Pick up" : storage != null ? "Open" : "Use";
    public Transform InteractionPoint => Owner != null ? Owner.transform : null;
    public float InteractionRange => range;
    public int InteractionPriority => pickable ? -2 : -1; // NPCs first

    public float HoldDuration =>
        Owner != null && interactionTime != null && interactionTime.GetValue(Owner) is float f ? Mathf.Max(0f, f) : 0f;

    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (Owner == null || (Owner is Behaviour b && !b.isActiveAndEnabled))
            return false;
        if ((pickable || storage != null) && interactor.Inventory == null)
        {
            reason = "No inventory";
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        InventoryManager inventory = interactor.Inventory;
        if (pickable)
        {
            inventory?.ItemPicked(Owner.gameObject);
        }
        else if (storage != null)
        {
            if (inventory == null) return;
            if (inventory.IsStorageOpened) inventory.CloseStorage(storage);
            else inventory.OpenStorage(storage);
        }
        else
        {
            interact?.Invoke(Owner, null);
        }
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }
}
