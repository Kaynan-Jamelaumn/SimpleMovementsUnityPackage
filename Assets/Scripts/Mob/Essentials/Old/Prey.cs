//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//// Class representing a prey animal that inherits from the base Animal class.
//public class Prey : Mob
//{
//    [Header("Prey Variables")]
//    [SerializeField] private float detectionRange = 10f;       // The range within which the prey can detect predators.
//    [SerializeField] private float escapeMaxDistance = 80f;   // The maximum distance the prey can escape from the predator.

//    private Predator currentPredator = null;   // Reference to the current predator pursuing the prey.

//    // Alert the prey and initiate a chase when a predator is detected.
//    public void AlertPrey(Predator predator)
//    {
//        SetState(MobState.Chase);    // Set the state to Chase.
//        currentPredator = predator;      // Store the reference to the current predator.
//        StartCoroutine(RunFromPredator());  // Start the coroutine to handle running away from the predator.
//    }

//    // Coroutine that manages the prey's behavior while being chased by a predator.
//    private IEnumerator RunFromPredator()
//    {
//        // Wait until the predator is within detection range.
//        while (currentPredator == null || Vector3.Distance(transform.position, currentPredator.transform.position) > detectionRange)
//        {
//            yield return null;
//        }

//        // Predator detected, so we should run away.
//        while (currentPredator != null && Vector3.Distance(transform.position, currentPredator.transform.position) <= detectionRange)
//        {
//            RunAwayFromPredator();   // Execute the method to run away from the predator.

//            yield return null;
//        }

//        // Predator out of range, run to our final location and go back to idle.
//        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
//        {
//            yield return null;
//            SetState(MobState.Idle);  // Set the state back to Idle.
//        }

//    }

//    // Method responsible for determining the escape destination and updating the NavMeshAgent.
//    private void RunAwayFromPredator()
//    {
//        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
//        {
//            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance)
//            {
//                // Calculate the direction opposite to the predator and set the escape destination.
//                Vector3 runDirection = transform.position - currentPredator.transform.position;
//                Vector3 escapeDestination = transform.position + runDirection.normalized * (escapeMaxDistance * 2);
//                navMeshAgent.SetDestination(GetRandomNavMeshPosition(escapeDestination, escapeMaxDistance));
//            }
//        }
//    }

//    // Override the Die method to stop all coroutines before calling the base class's Die method.
//    protected override void Die()
//    {
//        StopAllCoroutines();  // Stop all coroutines specific to the prey.
//        base.Die();           // Call the base class's Die method.
//    }

//    // Gizmos for visualization purposes in the Unity Editor.
//    private void OnDrawGizmos()
//    {
//        Gizmos.color = Color.green;
//        Gizmos.DrawWireSphere(transform.position, detectionRange);  // Draw a wire sphere to represent the detection range.
//    }
//}
