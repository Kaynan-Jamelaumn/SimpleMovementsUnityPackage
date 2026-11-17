using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Optional, on an older Interactable (dungeon chests, shrines, altars, levers...): what the player's interaction prompt
/// says about it and whether it can be used right now. The <see cref="PlayerInteractor"/> reads it through
/// <see cref="LegacyInteractableTarget"/>, so the object is used with the player's Interact input action (whatever it
/// is bound to), shows a proper prompt ("Open  Chest", "Pull  Lever") and knows which player used it. The object stays
/// an older Interactable for everything else (<c>Player.OnInteract</c>, the network relay).
/// </summary>
public interface IInteractionPrompt
{
    /// <summary>Name shown in the prompt ("Chest"). Empty = the object's name.</summary>
    string PromptName { get; }

    /// <summary>What using it does ("Open", "Pray", "Pull"). Empty = "Interact".</summary>
    string PromptVerb { get; }

    /// <summary>Seconds the Interact input must be held (0 = a press).</summary>
    float PromptHold { get; }

    /// <summary>Can <paramref name="interactor"/>'s player use it now? <paramref name="reason"/> says why not ("Empty").</summary>
    bool CanInteract(PlayerInteractor interactor, out string reason);

    /// <summary><paramref name="interactor"/>'s player uses it (called instead of the parameterless Interact).</summary>
    void Interact(PlayerInteractor interactor);
}

/// <summary>
/// Lets the <see cref="PlayerInteractor"/> use the older interaction objects - Interactable (doors, dungeon shrines,
/// chests), ItemPickable (items on the ground) and <see cref="Storage"/> - the way <c>Player.OnInteract</c> does, with a
/// prompt and its key. Used when the interactor's Prompt Older Interactables (or Handle Legacy Interactables) is on.
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
    private readonly IInteractionPrompt prompt;
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
        prompt = owner as IInteractionPrompt;
        Type t = owner != null ? owner.GetType() : null;
        pickable = Is(t, PickableTypeName);
        if (t != null)
        {
            interact = t.GetMethod("Interact", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            // (An object with its own prompt says itself how long the input is held.)
            if (prompt == null)
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
                if (IsOwnerComponent(mb))
                    return mb;
        }
        return null;
    }

    /// <summary>Is <paramref name="component"/> an older interaction component (an Interactable or derived, or a Storage)?</summary>
    public static bool IsOwnerComponent(Component component) =>
        component is MonoBehaviour mb && mb != null && (mb is Storage || Is(mb.GetType(), InteractableTypeName));

    /// <summary>An item on the ground (picked up through the inventory, which asks the server itself in a network session).</summary>
    public bool IsPickup => pickable;

    /// <summary>A storage (opened through the inventory, which asks the server itself in a network session).</summary>
    public bool IsStorage => storage != null;

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
            if (prompt != null && Owner != null && !string.IsNullOrWhiteSpace(prompt.PromptName))
                return prompt.PromptName;
            return Owner != null ? Owner.gameObject.name : "";
        }
    }

    public string InteractionVerb => pickable ? "Pick up" : storage != null ? "Open"
        : prompt != null && Owner != null && !string.IsNullOrWhiteSpace(prompt.PromptVerb) ? prompt.PromptVerb : "Interact";
    public Transform InteractionPoint => Owner != null ? Owner.transform : null;
    public float InteractionRange => range;
    public int InteractionPriority => pickable ? -2 : -1; // NPCs first

    public float HoldDuration =>
        Owner == null ? 0f
        : prompt != null ? Mathf.Max(0f, prompt.PromptHold)
        : interactionTime != null && interactionTime.GetValue(Owner) is float f ? Mathf.Max(0f, f) : 0f;

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
        // A chest that is not this player's (a rented room's): its owner's hook says why (the server checks again).
        string denied = storage != null && !interactor.Inventory.IsStorageOpened ? storage.Denied(interactor.Inventory) : null;
        if (denied != null)
        {
            reason = denied;
            return false;
        }
        if (prompt != null)
            return prompt.CanInteract(interactor, out reason);
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
        else if (prompt != null)
        {
            prompt.Interact(interactor);
        }
        else
        {
            interact?.Invoke(Owner, null);
        }
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }
}
