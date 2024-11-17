using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class MobActionsController : Mob
{

    [SerializeField] Vector3 detectionDistance;
    [SerializeField] Vector3 offSetDetectionDistance;
    [SerializeField] Transform mobTransform;

    public Transform MobTransform { get => mobTransform; set => mobTransform = value; }
    public Vector3 OffSetDetectionDistance { get => offSetDetectionDistance; set => offSetDetectionDistance = value; }
    public Vector3 DetectionDistance { get => detectionDistance; set => detectionDistance = value; }
    // Alert the prey and initiate a chase when a predator is detected.
    //public void AlertPrey(MobActionsController predator)
    //{
    //   // SetState(MobState.Chase);    // Set the state to Chase.
    //    currentChaseTarget = null;
    //    currentPredator = predator;      // Store the reference to the current predator.
    //    StartCoroutine(RunFromPredator());  // Start the coroutine to handle running away from the predator.
    //}

    // Coroutine that manages the prey's behavior while being chased by a predator.
    //private IEnumerator RunFromPredator()
    //{
    //    // Wait until the predator is within detection range.
    //    while (currentPredator == null || Vector3.Distance(transform.position, currentPredator.transform.position) > detectionRange)
    //    {
    //        yield return null;
    //    }

    //    // Predator detected, so we should run away.
    //    while (currentPredator != null && Vector3.Distance(transform.position, currentPredator.transform.position) <= detectionRange)
    //    {
    //        RunAwayFromPredator();   // Execute the method to run away from the predator.

    //        yield return null;
    //    }

    //    // Predator out of range, run to our final location and go back to idle.
    //    if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
    //    {
    //        yield return null;
    //    }
    //    SetState(MobState.Idle);  // Set the state back to Idle.

    //}

    // Method responsible for determining the escape destination and updating the NavMeshAgent.
    //private void RunAwayFromPredator()
    //{
    //    if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
    //    {
    //        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance)
    //        {
    //            // Calculate the direction opposite to the predator and set the escape destination.
    //            Vector3 runDirection = transform.position - currentPredator.transform.position;
    //            Vector3 escapeDestination = transform.position + runDirection.normalized * (escapeMaxDistance * 2);
    //            navMeshAgent.SetDestination(GetRandomNavMeshPosition(escapeDestination, escapeMaxDistance));
    //        }
    //        if (currentPredator != null && Vector3.Distance(transform.position, currentPredator.transform.position) > detectionRange)
    //        {
    //            StopChase();
    //        }
    //    }
    //}
    // Override the base class method to implement custom conditions for starting a chase.
    //public override void CheckChaseConditions()
    //{
    //    // If already chasing a target or being chased by a predator, do nothing.
    //    if (currentChaseTarget || currentPredator || currentPlayerTarget)
    //        return;

    //    // Define as camadas desejadas (Player e Mob)
    //    Collider[] detectedObjects = detectionCast.DetectObjects(transform);

    //    MobActionsController backUpPrey = null;

    //    // Iterate through detected colliders to find prey.
    //    foreach (var collider in detectedObjects)
    //    {
    //        MobActionsController prey = collider.GetComponent<MobActionsController>();
    //        PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

    //        if (player != null && Preys.Contains(MobType.Player))
    //        {
    //            StartPlayerChase(player);
    //            return;
    //        }

    //        if (prey != null && Preys.Contains(prey.type))
    //        {
    //            backUpPrey = prey;
    //        }
    //    }

    //    if (backUpPrey != null)
    //    {
    //        StartChase(backUpPrey);
    //        return;
    //    }

    //    // Reset the chase target if no prey is found.
    //    currentPlayerTarget = null;
    //    currentChaseTarget = null;
    //}
    public Transform AvailableTarget()
    {
        Collider[] detectedObjects = detectionCast.DetectObjects(transform);

        MobActionsController backUpPrey = null;

        // Iterate through detected colliders to find prey.
        foreach (var collider in detectedObjects)
        {
            MobActionsController prey = collider.GetComponent<MobActionsController>();
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            if (player != null && Preys.Contains(MobType.Player))
                if (player.gameObject != currentPlayerTarget)
                    return player.transform;

            if (prey != null && Preys.Contains(prey.type))
                backUpPrey = prey;

        }

        if (backUpPrey != null && backUpPrey.gameObject != currentChaseTarget)
            return backUpPrey.transform;
        return null;

    }

    public List<Transform> AvailableTargets()
    {
        Collider[] detectedObjects = detectionCast.DetectObjects(transform);
        List<Transform> validPlayersTransforms = new List<Transform>();
        List<Transform> validMobsTransforms = new List<Transform>();

        // Iterate through detected colliders to find prey.
        foreach (var collider in detectedObjects)
        {
            MobActionsController prey = collider.GetComponent<MobActionsController>();
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            if (player != null && Preys.Contains(MobType.Player))
                if (player.gameObject != currentPlayerTarget && !validMobsTransforms.Contains(player.transform))
                    validPlayersTransforms.Append<Transform>(player.transform);

            if (prey != null && Preys.Contains(prey.type) &&!validMobsTransforms.Contains(player.transform))
                validMobsTransforms.Append<Transform>(prey.transform);

        }

        validPlayersTransforms.AddRange(validMobsTransforms);
        if (validPlayersTransforms.Count > 0) 
            return validPlayersTransforms;
        return null;

    }

    //// Method to initiate a chase with a specific prey target.
    //private void StartChase(MobActionsController prey)
    //{
    //    currentChaseTarget = prey;         // Set the current chase target.
    //    SetState(MobState.Chase);        // Set the state to Chase.
        
    //}    
    //private void StartPlayerChase(PlayerStatusController player)
    //{
    //    currentPlayerTarget = player;         // Set the current chase target.
    //    SetState(MobState.Chase);        // Set the state to Chase.
        
    //}

    //// Override the base class method to handle logic specific to the Chase state for predators.
    //public override void HandleChaseState()
    //{
    //    // If there is a current prey target, alert the prey and start chasing.
    //    if(currentPlayerTarget != null) 
    //    {
    //        StartCoroutine(ChasePlayerCoroutine());
    //        return;    
    //    }
    //    if (currentChaseTarget != null)
    //    {

    //        currentChaseTarget.AlertPrey(this);    // Alert the prey about the chase.
    //        StartCoroutine(ChasePrey());           // Start the coroutine to handle the chase.
    //    }

    //}

    //// Coroutine to manage the chase behavior.
    //private IEnumerator ChasePrey()
    //{
    //    float startTime = Time.time;

    //    // Continue chasing until the max chase time is reached or the prey is caught or the predator is being Preyd.
    //    while (currentChaseTarget != null && Vector3.Distance(transform.position, currentChaseTarget.transform.position) > navMeshAgent.stoppingDistance)
    //    {
    //        // If max chase time is exceeded or prey is lost, stop the chase.
    //        if (Time.time - startTime >= maxChaseTime || currentChaseTarget == null)
    //        {
    //            StopChase();
    //            yield break;
    //        }

    //        SetState(MobState.Chase);  // Update the state to Chase.
    //        navMeshAgent.SetDestination(currentChaseTarget.transform.position);  // Set the destination to the prey.
    //        if (currentChaseTarget != null && currentChaseTarget.isActiveAndEnabled)
    //        {
    //            float distance = Vector3.Distance(transform.position, currentChaseTarget.transform.position);
    //            // Adicione esta verificação para morder apenas quando o predador estiver perto o suficiente.
    //            if (distance <= attackDistance)
    //            {
    //                // Mordida
    //                currentChaseTarget.ReceiveDamage(biteDamage);
    //                yield return new WaitForSeconds(biteCooldown);

    //                // Resetar o alvo da presa, retomar a perseguição ou voltar ao estado Idle.
    //                currentChaseTarget = null;
    //                HandleChaseState();
    //                CheckChaseConditions();
    //            }
    //            // Se não estiver perto o suficiente, continue perseguindo.
    //            else navMeshAgent.SetDestination(currentChaseTarget.transform.position);
                
    //        }
    //        yield return null;
    //    }
    //}
    //public void ChasePlayer(PlayerStatusController player)
    //{
    //    SetState(MobState.Chase);        // Set the state to Chase.
    //    currentPlayerTarget = player;       // Set the current player target.
    //    StartCoroutine(ChasePlayerCoroutine());  // Start the coroutine to handle chasing the player.
    //}

    // Coroutine to manage the chase behavior with the player.
    //private IEnumerator ChasePlayerCoroutine()
    //{
    //    float startTime = Time.time;
    //    float timeSinceLastBit = Time.time;
    //    // Continue chasing until the max chase time is reached or the player is caught.
    //    while (currentPlayerTarget != null && Vector3.Distance(transform.position, currentPlayerTarget.transform.position) > navMeshAgent.stoppingDistance)
    //    {
    //        if (playerHasMaxChaseTime)
    //        {
    //            // If max chase time is exceeded, stop the chase.
    //            if (Time.time - startTime >= maxChaseTime)
    //            {
    //                StopChase();
    //                yield break;
    //            }
    //        }

    //        SetState(MobState.Chase);  // Update the state to Chase.
    //        navMeshAgent.SetDestination(currentPlayerTarget.transform.position);  // Set the destination to the player.

    //        // Check if the predator is close enough to bite the player.
    //        if (Time.time - timeSinceLastBit >= biteCooldown)
    //            {
    //            // Bite logic
    //            Vector3 boxPosition = mobTransform.position + offSetDetectionDistance;


    //            Vector3 size = new Vector3(detectionDistance.x, detectionDistance.y, detectionDistance.z); // Tamanho da caixa de detecção
    //            Collider[] hits = Physics.OverlapBox(boxPosition, size, mobTransform.rotation, LayerMask.NameToLayer("player"));; // Verificar colisões
              
    //            // Loop através de todas as colisões
    //            foreach (Collider hit in hits)
    //            {
    
    //                // Se o jogador está dentro da caixa de detecção
    //                if (hit.gameObject == currentPlayerTarget.gameObject)
    //                {
    //                    // Atacar o jogador
    //                    timeSinceLastBit = Time.time;
    //                    currentPlayerTarget.HpManager.ConsumeHP(biteDamage);
    //                    if (!isPartialWait) yield return new WaitForSeconds(biteCooldown);

    //                    // Reset the player target, resume chasing or return to the Idle state.
    //                    currentPlayerTarget = null;
    //                    CheckChaseConditions();
    //                    //HandleChaseState();
    //                    break; // Saia do loop
    //                }
    //            }
           
    //        }

    //        yield return null;
    //    }
    //    if (!currentPlayerTarget) StopChase();

    //    // Player caught or out of range, stop the chase and return to Idle state.
    //}

    // Method to stop the chase and return to Idle state.
    //private void StopChase()
    //{
    //    navMeshAgent.ResetPath();           // Reset the path to stop the chase movement.
    //    if (currentChaseTarget !=null)
    //    {

    //        currentChaseTarget.currentPredator = null;
    //        currentChaseTarget = null;          // Reset the prey target.
    //    }
    //    else currentPlayerTarget = null;
    //    SetState(MobState.Idle);         // Set the state back to Idle.
    //}


    // Override the Die method to stop all coroutines before calling the base class's Die method.
    protected override void Die()
    {
        StopAllCoroutines();  // Stop all coroutines specific to the prey.
        base.Die();           // Call the base class's Die method.
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        // Saving the current Matrix 
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Applying  the gizmos to work with the mob rotation
        Gizmos.matrix = Matrix4x4.TRS(mobTransform.position, mobTransform.rotation, Vector3.one);

        // Drawgizmos
        Vector3 boxPosition = offSetDetectionDistance;
        Vector3 size = new Vector3(detectionDistance.x, detectionDistance.y, detectionDistance.z);
        Gizmos.DrawWireCube(boxPosition, size);

        // Restaurando a matriz de transformação anterior
        Gizmos.matrix = oldMatrix;
    }
}

