using UnityEngine;

/// <summary>
/// Somewhere to fill a watering can or a bucket: a well, a river bank, a lake, a swamp, a spring, an alchemical font, a
/// magical pool - or a rain barrel that fills with the rain falling on a roof. The water carries its kind into the soil
/// (minerals, salt, dirt, magic: see the Agriculture settings' Water Quality). Interact with it (verb "Fill") to fill the
/// container in hand, else the emptiest one carried.
/// </summary>
[AddComponentMenu("SimpleMovements/Agriculture/Water Source")]
public class WaterSource : MonoBehaviour, IInteractable
{
    public WaterSourceKind kind = WaterSourceKind.Well;
    public string displayName = "Well";
    [Tooltip("Litres it holds (0 = it never runs dry).")]
    [Min(0f)] public float capacity;
    [Tooltip("Litres in it at the start (with a capacity).")]
    [Min(0f)] public float startLitres = 50f;
    [Tooltip("Rain barrel: square metres of roof whose rain it collects (0 = none).")]
    [Min(0f)] public float catchment;
    [Min(0.5f)] public float range = 2.5f;
    public Transform interactionPoint;

    private float litres;
    private double lastRain = -1.0;
    private float rainTimer;

    public float Litres => capacity <= 0f ? float.PositiveInfinity : litres;

    private void Awake()
    {
        litres = Mathf.Min(startLitres, capacity > 0f ? capacity : startLitres);
        if (GetComponentInChildren<Collider>() == null)
        {
            var go = new GameObject("Water Source Trigger") { layer = 2 };
            go.transform.SetParent(transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.2f, 1.4f, 1.2f);
        }
    }

    private void Update()
    {
        if (catchment <= 0f || capacity <= 0f || !GameNet.NetAuthority.RunsRules(gameObject)) return;
        rainTimer += Time.unscaledDeltaTime;
        if (rainTimer < 2f) return;
        rainTimer = 0f;
        double now = FarmClock.Now;
        if (lastRain < 0.0) { lastRain = now; return; }
        double hours = now - lastRain;
        lastRain = now;
        if (hours <= 0.0) return;
        AgriWeather w = AgriEnvironment.Climate(AgricultureDatabase.Instance.Settings, now);
        litres = Mathf.Min(capacity, litres + w.rain * (float)hours * catchment);
    }

    public string InteractionName => displayName;
    public string InteractionVerb => "Fill";
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public float InteractionRange => range;
    public int InteractionPriority => 3;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        CombatEntity player = interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;
        if (player == null) return false;
        InventoryManager inv = InventoryManager.For(player);
        bool any = false;
        if (inv != null)
            foreach (InventoryItem it in AgriToolkit.Stacks(inv))
                if (it != null && it.itemScriptableObject is WaterContainerSO) { any = true; break; }
        if (!any)
        {
            reason = "Nothing to carry water in";
            return false;
        }
        if (capacity > 0f && litres < 0.1f)
        {
            reason = "Empty";
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        CombatEntity player = interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;
        if (player == null || !GameNet.NetAuthority.RunsRules(gameObject)) return;
        float taken = AgriToolkit.Fill(player, kind, capacity > 0f ? litres : float.PositiveInfinity, out string container);
        if (capacity > 0f) litres = Mathf.Max(0f, litres - taken);
        InventoryManager inv = InventoryManager.For(player);
        if (container == null) inv?.ShowMessage("You have nothing to carry water in.", InventoryFeedback.Kind.Info);
        else if (taken < 0.05f) inv?.ShowMessage($"The {container.ToLowerInvariant()} is already full.", InventoryFeedback.Kind.Info);
        else inv?.ShowMessage($"You fill the {container.ToLowerInvariant()} with {AgriText.Water(kind).ToLowerInvariant()}.", InventoryFeedback.Kind.Info);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }
}
