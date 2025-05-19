using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

/// <summary>
/// The <see cref="Mob"/> class manages the behavior of animals in the game, including wandering, detecting predators, 
/// and being pursued by predators.
/// </summary>
/// <remarks>
/// This class handles the animal's state transitions and interactions with other game objects.
/// </remarks>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobStatusController))]
public class Mob : MonoBehaviour
{
    /// <summary>
    /// Delegate for the event that is triggered when the mob is destroyed.
    /// </summary>
    public delegate void MobDestroyedHandler();
    public event MobDestroyedHandler OnMobDestroyed; // Event called when the mob is destroyed.

    /// <summary>
    /// Status controller for the mob.
    /// </summary>
    [SerializeField] private MobStatusController statusController;

    [Header("Wander")]
    /// <summary>
    /// How far the animal can move in one go.
    /// </summary>
    [Tooltip("How far the animal can move in one go.")]
    [SerializeField] private float wanderDistance = 50f;

    /// <summary>
    /// Maximum time the animal will wander.
    /// </summary>
    [Tooltip("Maximum time the animal will wander.")]
    [SerializeField] private float maxWalkTime = 6f;

    /// <summary>
    /// Points that the animal will patrol.
    /// </summary>
    [Tooltip("Points that the animal will patrol.")]
    [SerializeField] protected Vector3[] patrolPoints;

    /// <summary>
    /// Current patrol point index.
    /// </summary>
    [SerializeField] protected int currentPatrolPoint = 0;

    [Header("Idle")]
    /// <summary>
    /// How long the animal takes a break for.
    /// </summary>
    [Tooltip("How long the animal takes a break for.")]
    [SerializeField] private float idleTime = 5f;

    [Header("Chase")]
    [Header("Attributes")]

    /// <summary>
    /// Reference to the NavMeshAgent component.
    /// </summary>
    protected NavMeshAgent navMeshAgent;

    /// <summary>
    /// Reference to the Animator component.
    /// </summary>
    protected Animator animator;

    /// <summary>
    /// Types of mobs.
    /// </summary>
    [Tooltip("Types of mobs.")]
    [SerializeField] public List<string> mobTypes = new List<string> { "Player", "Sheep", "Wolf", "Fox" };

    /// <summary>
    /// The type of mob as a string.
    /// </summary>
    [Tooltip("The type of mob as a string.")]
    [SerializeField] public string type;

    /// <summary>
    /// The range within which the prey can detect predators.
    /// </summary>
    [Tooltip("The range within which the prey can detect predators.")]
    [SerializeField] protected float detectionRange = 10f;

    /// <summary>
    /// Reference to the cast detection.
    /// </summary>
    [Tooltip("Reference to the cast detection.")]
    [SerializeField] protected Cast detectionCast;

    [Header("Prey Variables")]
    /// <summary>
    /// The maximum distance the prey can escape from the predator.
    /// </summary>
    [Tooltip("The maximum distance the prey can escape from the predator.")]
    [SerializeField] protected float escapeMaxDistance = 80f;

    /// <summary>
    /// Reference to the current predator pursuing the prey.
    /// </summary>
    [Tooltip("Reference to the current predator pursuing the prey.")]
    [SerializeField] protected MobActionsController currentPredator = null;

    [Header("Predator Variables")]
    /// <summary>
    /// The maximum time the predator will chase prey.
    /// </summary>
    [Tooltip("The maximum time the predator will chase prey.")]
    [SerializeField] protected float maxChaseTime = 10f;

    /// <summary>
    /// The damage inflicted when the predator catches prey.
    /// </summary>
    [Tooltip("The damage inflicted when the predator catches prey.")]
    [SerializeField] protected int biteDamage = 3;

    /// <summary>
    /// If after biting it should stop moving or keep chasing and then attack.
    /// </summary>
    [Tooltip("If after biting it should stop moving or keep chasing and then attack.")]
    [SerializeField] protected bool isPartialWait = false;

    /// <summary>
    /// The cooldown time between consecutive bites.
    /// </summary>
    [Tooltip("The cooldown time between consecutive bites.")]
    [SerializeField] protected float biteCooldown = 1f;

    /// <summary>
    /// The distance within which the predator can attack prey.
    /// </summary>
    [Tooltip("The distance within which the predator can attack prey.")]
    [SerializeField] protected float attackDistance = 2f;

    /// <summary>
    /// Reference to the current prey being chased.
    /// </summary>
    [Tooltip("Reference to the current prey being chased.")]
    [SerializeField] protected MobActionsController currentChaseTarget;

    [Header("Player Chase Variables")]
    /// <summary>
    /// Determines if the player has a maximum chase time.
    /// </summary>
    [Tooltip("Determines if the player has a maximum chase time.")]
    [SerializeField] protected bool playerHasMaxChaseTime = false;

    /// <summary>
    /// Reference to the current player target.
    /// </summary>
    [Tooltip("Reference to the current player target.")]
    [SerializeField] protected PlayerStatusController currentPlayerTarget;

    /// <summary>
    /// List of prey types.
    /// </summary>
    [Tooltip("List of prey types.")]
    [SerializeField] protected List<string> Preys;

    /// <summary>
    /// Stopping margin for navigation.
    /// </summary>
    [SerializeField] private float stoppingMargin = 0;

    /// <summary>
    /// Coroutine reference for waiting to move.
    /// </summary>
    private Coroutine waitToMoveRoutine;

    /// <summary>
    /// Coroutine reference for waiting to reach destination.
    /// </summary>
    private Coroutine waitToReachDestinationRoutine;


// Properties
public float WanderDistance { get => wanderDistance; set => wanderDistance = value; }
    public float DetectionRange { get => detectionRange; }
    public Transform TransformReference { get => transform; }
    public Coroutine WaitToMoveRoutine { get => waitToMoveRoutine; set => waitToMoveRoutine = value; }
    public Coroutine WaitToReachDestinationRoutine { get => waitToReachDestinationRoutine; set => waitToReachDestinationRoutine = value; }
    public PlayerStatusController CurrentPlayerTarget { get => currentPlayerTarget; set => currentPlayerTarget = value; }
    public MobActionsController CurrentPredator { get => currentPredator; set => currentPredator = value; }
    public MobActionsController CurrentChaseTarget { get => currentChaseTarget; set => currentChaseTarget = value; }
    public Cast DetectionCast { get => detectionCast; }
    public List<string> PreysReference { get => Preys; }
    public bool PlayerHasMaxChaseTime { get => playerHasMaxChaseTime; }
    public bool IsPartialWait { get => isPartialWait; }
    public float MaxWalkTime { get => maxWalkTime; }
    public float MaxChaseTime { get => maxChaseTime; }
    public float IdleTime { get => idleTime; set => idleTime = value; }
    public int CurrentPatrolPoint { get => currentPatrolPoint; set => currentPatrolPoint = value; }
    public Vector3[] PatrolPoints { get => patrolPoints; set => patrolPoints = value; }
    public NavMeshAgent NavMeshAgentReference { get => navMeshAgent; set => navMeshAgent = value; }
    public int BiteDamage { get => biteDamage; }
    public float BiteCooldown { get => biteCooldown; }
    public float AttackDistance { get => attackDistance; }
    public float EscapeMaxDistance { get => escapeMaxDistance; }

    /// <summary>
    /// Initializes the <see cref="Mob"/> class, setting the references for statusController and NavMeshAgent.
    /// </summary>
    private void Awake()
    {
        statusController = GetComponent<MobStatusController>(); // Assign the MobStatusController component.
    }

    /// <summary>
    /// Starts the initialization process, validating assignments and setting up the animal's components.
    /// </summary>
    private void Start()
    {
        ValidateAssignments(); // Ensure all assignments are valid.
        InitializeAnimal(); // Initialize the animal.
    }

    /// <summary>
    /// Ensures that all necessary assignments are valid and not null.
    /// Throws an assertion if a required assignment is missing.
    /// </summary>
    private void ValidateAssignments()
    {
        Assert.IsNotNull(statusController, "MobStatusController is not assigned in statusController."); // Verify assignments.
    }

    /// <summary>
    /// Checks if the mob has reached its destination, considering a margin of error.
    /// </summary>
    /// <returns>True if the mob is close enough to the destination; otherwise, false.</returns>
    public bool HasReachedDestinationWithMargin()
    {
        return navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + stoppingMargin; // Check if the mob is close enough to the destination.
    }

    /// <summary>
    /// Initializes the animal, setting up the NavMeshAgent and Animator components, and adjusting movement speed.
    /// </summary>
    protected virtual void InitializeAnimal()
    {
        navMeshAgent = GetComponent<NavMeshAgent>(); // Get the NavMeshAgent component.
        animator = transform.GetChild(0).GetChild(0).GetComponent<Animator>(); // Get the Animator component.
        navMeshAgent.speed = statusController.SpeedManager.Speed; // Set the NavMeshAgent's speed.
    }

    /// <summary>
    /// Returns a random position on the NavMesh within a specified distance from the given origin.
    /// </summary>
    /// <param name="origin">The origin point from which to generate a random position.</param>
    /// <param name="distance">The distance within which to generate the random position.</param>
    /// <returns>A random position on the NavMesh, or the origin if no valid position is found.</returns>
    public Vector3 GetRandomNavMeshPosition(Vector3 origin, float distance)
    {
        // Try finding a valid random position within the specified distance multiple times.
        for (int i = 0; i < 5; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * distance; // Generate a random direction.
            randomDirection += origin; // Add it to the origin.

            // Check if the random position is on the NavMesh and return it if true.
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navMeshHit, distance, NavMesh.AllAreas))
            {
                return navMeshHit.position;
            }
        }

        // Return the original position if no valid random position is found.
        return origin;
    }

    public virtual void ReceiveDamage(int damage)
    {
        // Reduce health based on the received damage.
        statusController.HealthManager.ConsumeHP(damage);
        // If health is depleted, trigger the Die method.
        if (statusController.HealthManager.CurrentValue <= 0)
            Die();
    }

    // Placeholder method for handling the death of the Animal.
    protected virtual void Die()
    {
        // Stop all coroutines and destroy the GameObject.
        StopAllCoroutines();
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        OnMobDestroyed?.Invoke();
    }
}
