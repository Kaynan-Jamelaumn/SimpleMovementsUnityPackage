using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

public class MobStatusController : MonoBehaviour, IAssignmentsValidator
{
    [SerializeField] private HealthManager healthManager;
    [SerializeField] private SpeedManager speedManager;

    public HealthManager HealthManager { get => healthManager; }
    public SpeedManager SpeedManager { get => speedManager; }

    private AbilitySpawner abilitySpawner;
    private ItemSpawner itemSpawner;
    private MobActionsController mobActionsController;
    private MobAbilityController mobAbilityController;

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
        speedManager = GetComponent<SpeedManager>();

        // Cache components to avoid repeated GetComponent calls
        abilitySpawner = GetComponent<AbilitySpawner>();
        itemSpawner = GetComponent<ItemSpawner>();
        mobActionsController = GetComponent<MobActionsController>();
        mobAbilityController = GetComponent<MobAbilityController>();
    }

    private void Start()
    {
        ValidateAssignments();
    }

    private void Update()
    {
        if (healthManager.Hp <= 0)
        {
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        if (abilitySpawner) abilitySpawner.SpawnAbility();
        if (itemSpawner) itemSpawner.SpawnItem(transform.position);
        if (mobActionsController) mobActionsController.StopAllCoroutines();
        if (mobAbilityController) mobAbilityController.StopAllCoroutines();
        healthManager.StopAllCoroutines();
        speedManager.StopAllCoroutines();
        Destroy(gameObject);
    }

    public void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "HealthManager is not assigned.");
        Assert.IsNotNull(speedManager, "SpeedManager is not assigned.");
    }
}
