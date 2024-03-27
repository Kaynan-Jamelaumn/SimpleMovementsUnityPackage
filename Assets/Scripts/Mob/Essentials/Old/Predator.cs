//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//// Class representing a predator animal that inherits from the base Animal class.
//public class Predator : Mob
//{
//    [Header("Predator Variables")]
//    [SerializeField] private float detectionRange = 20f;        // The range within which the predator can detect prey.
//    [SerializeField] private float maxChaseTime = 10f;          // The maximum time the predator will chase prey.
//    [SerializeField] private int biteDamage = 3;               // The damage inflicted when the predator catches prey.
//    [SerializeField] private float biteCooldown = 1f;          // The cooldown time between consecutive bites.

//    private Prey currentChaseTarget;   // Reference to the current prey being chased.

//    // Override the base class method to implement custom conditions for starting a chase.
//    protected override void CheckChaseConditions()
//    {
//        // If already chasing a target, do nothing.
//        if (currentChaseTarget != null)
//            return;

//        // Use physics overlap to find potential prey within the detection range.
//        Collider[] colliders = new Collider[10];
//        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, colliders);

//        // Iterate through detected colliders to find prey.
//        for (int i = 0; i < numColliders; i++)
//        {
//            Prey prey = colliders[i].GetComponent<Prey>();

//            // If prey is found, start the chase and return.
//            if (prey != null)
//            {
//                StartChase(prey);
//                return;
//            }
//        }

//        currentChaseTarget = null;  // Reset the chase target if no prey is found.
//    }

//    // Method to initiate a chase with a specific prey target.
//    private void StartChase(Prey prey)
//    {
//        currentChaseTarget = prey;         // Set the current chase target.
//        SetState(MobState.Chase);        // Set the state to Chase.
//    }

//    // Override the base class method to handle logic specific to the Chase state for predators.
//    protected override void HandleChaseState()
//    {
//        // If there is a current prey target, alert the prey and start chasing.
//        if (currentChaseTarget != null)
//        {
//            currentChaseTarget.AlertPrey(this);    // Alert the prey about the chase.
//            StartCoroutine(ChasePrey());           // Start the coroutine to handle the chase.
//        }
//        else
//        {
//            SetState(MobState.Idle);   // If no prey target, return to Idle state.
//        }
//    }

//    // Coroutine to manage the chase behavior.
//    private IEnumerator ChasePrey()
//    {
//        float startTime = Time.time;

//        // Continue chasing until the max chase time is reached or the prey is caught.
//        while (currentChaseTarget != null && Vector3.Distance(transform.position, currentChaseTarget.transform.position) > navMeshAgent.stoppingDistance)
//        {
//            // If max chase time is exceeded or prey is lost, stop the chase.
//            if (Time.time - startTime >= maxChaseTime || currentChaseTarget == null)
//            {
//                StopChase();
//                yield break;
//            }

//            SetState(MobState.Chase);  // Update the state to Chase.
//            navMeshAgent.SetDestination(currentChaseTarget.transform.position);  // Set the destination to the prey.

//            yield return null;
//        }

//        // If the prey is caught, inflict damage, wait for the bite cooldown, and resume the chase.
//        if (currentChaseTarget != null)
//            currentChaseTarget.ReceiveDamage(biteDamage);

//        yield return new WaitForSeconds(biteCooldown);

//        currentChaseTarget = null;       // Reset the prey target.
//        HandleChaseState();              // Resume chasing or return to Idle state.
//        CheckChaseConditions();          // Check for new chase conditions.
//    }

//    // Method to stop the chase and return to Idle state.
//    private void StopChase()
//    {
//        navMeshAgent.ResetPath();           // Reset the path to stop the chase movement.
//        currentChaseTarget = null;          // Reset the prey target.
//        SetState(MobState.Idle);         // Set the state back to Idle.
//    }

//    // Gizmos for visualization purposes in the Unity Editor.
//    private void OnDrawGizmos()
//    {
//        Gizmos.color = Color.red;
//        Gizmos.DrawWireSphere(transform.position, detectionRange);  // Draw a wire sphere to represent the detection range.
//    }
//}
