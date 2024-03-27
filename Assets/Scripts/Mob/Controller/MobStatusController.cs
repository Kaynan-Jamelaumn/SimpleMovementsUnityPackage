using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Assertions;

public class MobStatusController : MonoBehaviour
{
    [SerializeField] private HealthManager healthManager;
    [SerializeField] private SpeedManager speedManager;

    public HealthManager HealthManager { get => healthManager; }
    public SpeedManager SpeedManager { get => speedManager; }
    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
        speedManager = GetComponent<SpeedManager>();
    }
    private void Start()
    {
        ValidateAssignments();
    }
    private void Update()
    {
        if (healthManager.Hp <= 0)
        {
            AbilitySpawner abilitySpawner = GetComponent<AbilitySpawner>();
            ItemSpawnner itemSpawnner = GetComponent<ItemSpawnner>();
            MobActionsController mobActionsController = GetComponent<MobActionsController>();
            MobAbilityController mobAbilityController = GetComponent<MobAbilityController>();
            if (abilitySpawner) abilitySpawner.SpawnAbility();
            if (itemSpawnner) itemSpawnner.SpawnItem(this.transform.position);
            if (mobActionsController) mobActionsController.StopAllCoroutines();
            if (mobAbilityController) mobAbilityController.StopAllCoroutines();
            healthManager.StopAllCoroutines();
            speedManager.StopAllCoroutines();
            Destroy(this.gameObject);
        }
            
    }
    private void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "Target HealthManager is not Asigned");
        Assert.IsNotNull(speedManager, "Target SppedNanager is not Asigned");
    }

}
