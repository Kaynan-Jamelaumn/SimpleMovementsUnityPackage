using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class GetableAbility
{
    [SerializeField] public AbilityEffectSO ability;
    [Tooltip("change of the ability to spawn")][Range(0f, 1f)] public float spawnChance;
    [SerializeField] public GameObject particleEffect;
    [Tooltip("after the ability is spawned it has a cartain time to be acquired")][SerializeField] public float waitingTimeToAbilityBecomeAvailable;
    [Tooltip("after the ability is ready to be acquired the time to get it untiil it vanishes")][SerializeField] public float abilityLifeSpan;
}

public class PickableAbility : MonoBehaviour
{
    [SerializeField] public AbilityEffectSO ability;
    [Tooltip("after the ability is spawned it has a cartain time to be acquired")][SerializeField] public float waitingTimeToAbilityBecomeAvailable;
    [Tooltip("after the ability is ready to be acquired the time to get it untiil it vanishes")][SerializeField] public float abilityLifeSpan;
    [Tooltip("controlls when the ability countdown to became available should be")] public bool startCountDown;
    private new Collider collider; // Reference to the collider component

    private void Start()
    {
        collider = GetComponent<Collider>(); // Get the collider component
        collider.enabled = false; // Disable the collider initially
    }


    private void Update()
    {
        if (startCountDown)
            StartCoroutine(WaitToBecomeAvailableRoutine());
    }

    private IEnumerator WaitToBecomeAvailableRoutine()
    {
        float startTime = Time.time;
        while (Time.time < startTime + waitingTimeToAbilityBecomeAvailable)
            yield return null;

        collider.enabled = true;
        StartCoroutine(AbilityPickableRoutine());

    }
    private IEnumerator AbilityPickableRoutine()
    {
        float startTime = Time.time;
        while (Time.time < startTime + abilityLifeSpan) 
            yield return null;
        Destroy(this.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
           PlayerAbilityController playerAbilityController = other.GetComponent<PlayerAbilityController>(); 
            if (playerAbilityController != null)
            foreach (var abilityHolderReference in playerAbilityController.Abilities)
                if (abilityHolderReference.abilityEffect == null) 
                {
                    abilityHolderReference.abilityEffect = ability;
                    abilityHolderReference.attackCast = ability.effects[0].attackCast;
                    abilityHolderReference.particle = ability.particle;
                    StopAllCoroutines();
                    Destroy(this.gameObject);
                    return;
                }
               
            


        }
    }
}

public class AbilitySpawner : MonoBehaviour
{
    [SerializeField] List<GetableAbility> spawnedAbilities;

    public void SpawnAbility()
    {
        if (spawnedAbilities != null)
        foreach (var abilityCase in spawnedAbilities)
        {
            if (Random.value <= abilityCase.spawnChance)
                {
                    Vector3 spawnOffset = GetSpawnOffSet();
                    Vector3 spawnPosition = this.transform.position + spawnOffset;

                    GameObject newItem = Instantiate(abilityCase.particleEffect, spawnPosition, Quaternion.identity);
                    PickableAbility pickableAbility = newItem.AddComponent<PickableAbility>();
                    pickableAbility.ability = abilityCase.ability;
                    pickableAbility.waitingTimeToAbilityBecomeAvailable = abilityCase.waitingTimeToAbilityBecomeAvailable;
                    pickableAbility.abilityLifeSpan = abilityCase.abilityLifeSpan;
                    pickableAbility.startCountDown = true;

                }
            }
    }

    private static Vector3 GetSpawnOffSet()
    {
        return new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
    }
}

