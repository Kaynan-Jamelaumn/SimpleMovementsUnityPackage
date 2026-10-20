using UnityEngine;

/// <summary>
/// PLACEHOLDER city healer: put it on a healer NPC or a hospital in the hub city. It registers as an
/// <see cref="ICityTreatmentProvider"/> and can be interacted with, but city treatment is not implemented yet: it only
/// tells the player so (Injury Settings ▸ City Treatment ▸ Placeholder Note). Replace <see cref="Treat"/> when the
/// mechanics are added; nothing else in the injury system needs to change.
/// </summary>
[DisallowMultipleComponent]
public class CityHealer : MonoBehaviour, ICityTreatmentProvider, IInteractable
{
    [Tooltip("Name shown in the prompt and the health panel.")]
    [SerializeField] private string healerName = "City Healer";
    [Tooltip("How close the player must be (metres).")]
    [SerializeField, Min(0.5f)] private float range = 3f;

    public string ProviderName => healerName;

    private void OnEnable() => CityInjuryTreatment.Register(this);
    private void OnDisable() => CityInjuryTreatment.Unregister(this);

    // ------------------------------------------------------------------ ICityTreatmentProvider (placeholder)
    public bool IsAvailableTo(InjuryController patient, out string reason)
    {
        reason = CityInjuryTreatment.StatusLine;
        if (string.IsNullOrEmpty(reason))
            reason = "City treatment is not available yet.";
        return false;
    }

    public float PriceFor(InjuryController patient, InjuryPartState part, Injury injury)
    {
        CityTreatmentSettings s = InjurySettings.Instance.cityTreatment;
        if (injury == null)
            return s.reattachPrice;
        return s.pricePerSeverity * (injury.Severity + 1);
    }

    public bool Treat(InjuryController patient, InjuryPartState part, Injury injury, out string message)
    {
        IsAvailableTo(patient, out message);
        return false;
    }

    // ------------------------------------------------------------------ IInteractable
    public string InteractionName => healerName;
    public string InteractionVerb => "Ask for Treatment";
    public Transform InteractionPoint => transform;
    public float InteractionRange => range;
    public int InteractionPriority => 5;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        return InjurySettings.Instance.enabled && interactor != null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null)
            return;
        IsAvailableTo(null, out string message);
        InventoryManager.For(interactor)?.ShowMessage($"{healerName}: {message}", InventoryFeedback.Kind.Info);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }
}
