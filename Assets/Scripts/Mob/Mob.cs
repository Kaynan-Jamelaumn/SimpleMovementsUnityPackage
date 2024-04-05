using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;


// Enum representing the possible states of the Animal.
public enum MobState
{
    Idle,
    Moving,
    Chase,
    Patrol,
}

public enum MobType
{
    Player,
    Sheep,
    Wolf,
    Fox,
}


// Require the presence of a NavMeshAgent component on the GameObject.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobStatusController))]
public class Mob : MonoBehaviour
{

    [SerializeField] private MobStatusController statusController;
    [Header("Wander")]
    [SerializeField] private float wanderDistance = 50f; // How far the animal can move in one go.
    [SerializeField] private float maxWalkTime = 6f;
    [SerializeField] protected Vector3[] patrolPoints;
    [SerializeField] protected int currentPatrolPoint = 0;

    [Header("Idle")]
    [SerializeField] private float idleTime = 5f; // How long the animal takes a break for.

    [Header("Chase")]

    [Header("Attributes")]

    protected NavMeshAgent navMeshAgent;
    protected Animator animator;
    [SerializeField] protected MobState currentState = MobState.Idle;




    [SerializeField] protected MobType type;   // The maximum distance the prey can escape from the predator.
    [SerializeField] protected float detectionRange = 10f;       // The range within which the prey can detect predators.
    [SerializeField] protected Cast detectionCast;
    private void OnDrawGizmosSelected()
    {
        detectionCast.DrawGizmos(transform);
    }

    [Header("Prey Variables")]
    [SerializeField] protected float escapeMaxDistance = 80f;   // The maximum distance the prey can escape from the predator.

    [SerializeField] protected MobActionsController currentPredator = null;   // Reference to the current predator pursuing the prey.

    [Header("Predator Variables")]
    [SerializeField] protected float maxChaseTime = 10f;          // The maximum time the predator will chase prey.
    [SerializeField] protected int biteDamage = 3;               // The damage inflicted when the predator catches prey.
    [SerializeField] protected bool isPartialWait = false;               //If after biting it should stop moving or keep chasing and then attack?
    [SerializeField] protected float biteCooldown = 1f;          // The cooldown time between consecutive bites.
    [SerializeField] protected float attackDistance = 2f;
    [SerializeField] protected MobActionsController currentChaseTarget;   // Reference to the current prey being chased.
    [Header("Player Chase Variables")]
    [SerializeField] protected bool playerHasMaxChaseTime = false;          // The maximum time the predator will chase prey.
    [SerializeField] protected PlayerStatusController currentPlayerTarget;
    [SerializeField] protected List<MobType> Preys;
    [SerializeField] private float stoppingMargin = 0;

    public PlayerStatusController CurrentPlayerTarget { get => currentPlayerTarget; set => currentPlayerTarget = value; }

    private void Awake()
    {
        statusController = GetComponent<MobStatusController>();
    }
    private void Start()
    {
        ValidateAssignments();
        // Initialize the Animal.
        InitializeAnimal();
    }
    private void ValidateAssignments()
    {
        Assert.IsNotNull(statusController, "MobStatusController is not assigned in statusController.");
    }
    // Initializes the Animal by setting up the NavMeshAgent and initial state
    // 
    private bool HasReachedDestinationWithMargin()
{
    // Verifique se o mob está perto o suficiente do destino, considerando a margem de erro.
    return navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + stoppingMargin;
}
    protected virtual void InitializeAnimal()
    {
        // Get the NavMeshAgent component and set its speed.
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = transform.GetChild(0).GetChild(0).GetComponent<Animator>();
        navMeshAgent.speed = statusController.SpeedManager.Speed;

        // Set the initial state to Idle and update the state.
        currentState = MobState.Idle;
        UpdateState();
    }

    // Updates the state of the Animal based on the current state.
    protected virtual void UpdateState()
    {
        // Switch between different states and call respective handling methods.
        switch (currentState)
        {
            case MobState.Idle:
                HandleIdleState();
                break;
            case MobState.Moving:
                HandleMovingState();
                break;
            case MobState.Chase:
                HandleChaseState();
                break;
            case MobState.Patrol:
                HandlePatrolState();
                break;
        }
    }

    // Returns a random position on the NavMesh within a specified distance from the given origin.
    protected Vector3 GetRandomNavMeshPosition(Vector3 origin, float distance)
    {
        // Try finding a valid random position within the specified distance multiple times.
        for (int i = 0; i < 5; i++)
        {
            // Generate a random direction and add it to the origin.
            Vector3 randomDirection = Random.insideUnitSphere * distance;
            randomDirection += origin;

            // Check if the random position is on the NavMesh and return it if true.
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navMeshHit, distance, NavMesh.AllAreas))
            {
                return navMeshHit.position;
            }
        }

        // Return the original position if no valid random position is found.
        return origin;
    }

    // Placeholder method for checking conditions to enter the Chase state.
    protected virtual void CheckChaseConditions()
    {
        // To be implemented in derived classes.
    }

    // Placeholder method for handling the Chase state.
    protected virtual void HandleChaseState()
    {
        // Stop all coroutines when entering the Chase state.
        StopAllCoroutines();
    }

    // Initiates the Idle state by waiting for a random amount of time, then setting a random destination.
    protected virtual void HandleIdleState()
    {
        // Start the coroutine to wait and then move to a random destination.
        StartCoroutine(WaitToMove());
    }

    protected virtual void HandlePatrolState()
    {
        if (patrolPoints.Length == 0) return; // No patrol points set

        navMeshAgent.SetDestination(patrolPoints[currentPatrolPoint]);
        StartCoroutine(WaitToReachDestination());

        if (HasReachedDestinationWithMargin())
        {
            currentPatrolPoint = (currentPatrolPoint + 1) % patrolPoints.Length;
           // SetState(MobState.Idle); // Continue to the next patrol point
        }
    }
    // Coroutine that makes the Animal wait for a random amount of time and then move to a random destination.
    //private float offsetMargin = 1.0f; // Adjust this value as needed.

    private IEnumerator WaitToMove()
    {
        // Check if the player is within the detection range.
        if (currentPlayerTarget != null && Vector3.Distance(transform.position, currentPlayerTarget.transform.position) <= detectionRange)
        {
            // Set the destination to the player's position.
            navMeshAgent.SetDestination(currentPlayerTarget.transform.position);
            HandleMovingState();
        }
        else
        {
            // If the player is not in range, move to a random destination.
            float waitTime = Random.Range(idleTime / 2, idleTime * 2);
            yield return new WaitForSeconds(waitTime);

            // Get a random destination on the NavMesh and set it for the Animal.
            if (patrolPoints.Length ==0)
            {
                Vector3 randomDestination = GetRandomNavMeshPosition(transform.position, wanderDistance);
                navMeshAgent.SetDestination(randomDestination);
                if (!currentPlayerTarget) SetState(MobState.Moving);
            }
            else SetState(MobState.Patrol);
        }
    }

    // Placeholder method for handling the Moving state.
    protected virtual void HandleMovingState()
    {
        // Start the coroutine to wait until reaching the destination or a maximum walk time is exceeded.
        StartCoroutine(WaitToReachDestination());
    }

    // Coroutine that makes the Animal wait until it reaches its destination or a maximum walk time is exceeded.
    private IEnumerator WaitToReachDestination()
    {
        // Record the start time for measuring the walk time.
        float startTime = Time.time;

        // Continue the loop until the destination is reached or the maximum walk time is exceeded.
        //while (navMeshAgent.pathPending || (navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance))
        while (navMeshAgent.pathPending || (navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh &&
           !HasReachedDestinationWithMargin()))

            {
            // If the maximum walk time is exceeded, reset the path and set the state to Idle.
            if (currentPlayerTarget != null && playerHasMaxChaseTime && Time.time - startTime >= maxWalkTime || currentPlayerTarget == null && Time.time - startTime >= maxWalkTime)
            {
                if (currentPredator) currentPredator = null;
                navMeshAgent.ResetPath();
                SetState(MobState.Idle);
                yield break;
            }

            // Check conditions to enter the Chase state during movement.
            CheckChaseConditions();

            yield return null;
        }

        // Destination has been reached, set the state to Idle.
        if (!currentPlayerTarget) SetState(MobState.Idle);
        else CheckChaseConditions();
    }

    // Sets the state of the Animal and invokes the OnStateChanged method.
    protected void SetState(MobState newState)
    {
        // Check if the new state is different from the current state.
        if (currentState == newState)
            return;

        // Update the current state and invoke the OnStateChanged method.
        currentState = newState;
        OnStateChanged(newState);
    }

    // Invoked when the state of the Animal changes, adjusts the NavMeshAgent speed accordingly, and updates the state.
    protected virtual void OnStateChanged(MobState newState)
    {
        animator?.CrossFadeInFixedTime(newState.ToString(), 0.5f);
        // Adjust NavMeshAgent speed based on the new state.
        if (newState == MobState.Moving)
            navMeshAgent.speed = statusController.SpeedManager.Speed;

        if (newState == MobState.Chase)
            navMeshAgent.speed = statusController.SpeedManager.Speed * statusController.SpeedManager.SpeedWhileRunningMultiplier;

        // Update the state based on the new state.
        UpdateState();
    }

    // Reduces the health of the Animal based on received damage and triggers the Die method if health is depleted.
    public virtual void ReceiveDamage(int damage)
    {
        // Reduce health based on the received damage.
        statusController.HealthManager.ConsumeHP(damage);
        // If health is depleted, trigger the Die method.
        if (statusController.HealthManager.Hp <= 0)
            Die();
    }

    // Placeholder method for handling the death of the Animal.
    protected virtual void Die()
    {
        // Stop all coroutines and destroy the GameObject.
        StopAllCoroutines();
        Destroy(gameObject);
    }
}

