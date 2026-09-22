using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The <see cref="MobActionsController"/> class manages the behavior of animals in the game, including wandering, detecting predators,
/// and being pursued by predators.
/// </summary>
/// <remarks>
/// This class handles the animal's state transitions and interactions with other game objects.
///  with utility-based target selection and threat assessment.
/// </remarks>
public class MobActionsController : Mob
{
    [SerializeField] Vector3 detectionDistance;
    [SerializeField] Vector3 offSetDetectionDistance;
    [SerializeField] Transform mobTransform;

    // Utility AI weights for target selection
    private const float DISTANCE_WEIGHT = 0.45f;
    private const float HEALTH_WEIGHT = 0.3f;
    private const float THREAT_WEIGHT = 0.35f;

    /// <summary>
    /// Gets or sets the Transform of the mob.
    /// </summary>
    public Transform MobTransform
    {
        get => mobTransform;
        set => mobTransform = value;
    }

    /// <summary>
    /// Gets or sets the offset detection distance.
    /// </summary>
    public Vector3 OffSetDetectionDistance
    {
        get => offSetDetectionDistance;
        set => offSetDetectionDistance = value;
    }

    /// <summary>
    /// Gets or sets the detection distance.
    /// </summary>
    public Vector3 DetectionDistance
    {
        get => detectionDistance;
        set => detectionDistance = value;
    }

    /// <summary>
    /// Determines the available target for the mob using utility-based scoring.
    /// </summary>
    /// <returns>Returns the Transform of the available target, or null if no target is found.</returns>
    public Transform AvailableTarget()
    {
        // Detects objects within the detection distance.
        Collider[] detectedObjects = detectionCast.DetectObjects(transform);

        Transform bestTarget = null;
        float bestScore = float.MinValue;

        // Iterate through detected colliders to find and score potential targets.
        foreach (var collider in detectedObjects)
        {
            // Check if the collider belongs to a PlayerStatusController.
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            // If the detected object is a player and is in prey list.
            if (player != null && Preys.Contains("Player"))
            {
                if (player.gameObject != currentPlayerTarget)
                {
                    float score = ScorePlayerTarget(player);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = player.transform;
                    }
                }
            }

            // Check if the collider belongs to a MobActionsController.
            MobActionsController prey = collider.GetComponent<MobActionsController>();

            // If the detected object is a prey.
            if (prey != null && Preys.Contains(prey.type))
            {
                if (prey.gameObject != currentChaseTarget)
                {
                    float score = ScoreMobTarget(prey);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = prey.transform;
                    }
                }
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Scores a player target based on distance, health, and threat level.
    /// </summary>
    /// <param name="player">The player to score.</param>
    /// <returns>Utility score for targeting this player.</returns>
    private float ScorePlayerTarget(PlayerStatusController player)
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        float distanceScore = 1f - Mathf.Clamp01(distance / DetectionRange);

        // Players are typically higher threat/priority
        float threatScore = 0.8f;

        // Prefer targets that are closer and represent good opportunities
        return (distanceScore * DISTANCE_WEIGHT) + (threatScore * THREAT_WEIGHT) + (0.5f * HEALTH_WEIGHT);
    }

    /// <summary>
    /// Scores a mob target based on distance, health, and relative threat.
    /// </summary>
    /// <param name="mob">The mob to score.</param>
    /// <returns>Utility score for targeting this mob.</returns>
    private float ScoreMobTarget(MobActionsController mob)
    {
        float distance = Vector3.Distance(transform.position, mob.transform.position);
        float distanceScore = 1f - Mathf.Clamp01(distance / DetectionRange);

        // Calculate relative threat based on whether they're already fleeing or have a predator
        float threatScore = mob.CurrentPredator != null ? 0.3f : 0.6f;

        // Prefer weaker or isolated targets
        float healthScore = 0.5f;

        return (distanceScore * DISTANCE_WEIGHT) + (healthScore * HEALTH_WEIGHT) + (threatScore * THREAT_WEIGHT);
    }

    /// <summary>
    /// Determines the available targets for the mob with utility-based ranking.
    /// </summary>
    /// <returns>Returns a list of Transforms of available targets sorted by utility score, or null if no targets are found.</returns>
    public List<Transform> AvailableTargets()
    {
        // Detects objects within the detection distance.
        Collider[] detectedObjects = detectionCast.DetectObjects(transform);
        List<(Transform transform, float score)> scoredTargets = new List<(Transform, float)>();

        // Iterate through detected colliders to find and score prey.
        foreach (var collider in detectedObjects)
        {
            // Check if the collider belongs to a PlayerStatusController.
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            // If the detected object is a player and is not currently targeted.
            if (player != null && Preys.Contains("Player"))
            {
                if (player.gameObject != currentPlayerTarget)
                {
                    float score = ScorePlayerTarget(player);
                    scoredTargets.Add((player.transform, score));
                }
            }

            // Check if the collider belongs to a MobActionsController.
            MobActionsController prey = collider.GetComponent<MobActionsController>();

            // If the detected object is a prey and is not currently targeted.
            if (prey != null && Preys.Contains(prey.type))
            {
                if (prey.gameObject != currentChaseTarget)
                {
                    float score = ScoreMobTarget(prey);
                    scoredTargets.Add((prey.transform, score));
                }
            }
        }

        // Sort by score descending and return transforms
        if (scoredTargets.Count > 0)
        {
            return scoredTargets.OrderByDescending(t => t.score)
                               .Select(t => t.transform)
                               .ToList();
        }

        return null;
    }

    /// <summary>
    /// Calculates the best escape position away from a threat using tactical positioning.
    /// </summary>
    /// <param name="threatPosition">Position of the threat to escape from.</param>
    /// <param name="maxDistance">Maximum distance to consider for escape.</param>
    /// <returns>Best escape position on NavMesh.</returns>
    public Vector3 CalculateBestEscapePosition(Vector3 threatPosition, float maxDistance)
    {
        Vector3 awayDirection = (transform.position - threatPosition).normalized;

        // Try multiple angles to find the best escape route
        float bestScore = float.MinValue;
        Vector3 bestPosition = transform.position;

        for (int angle = -45; angle <= 45; angle += 15)
        {
            Vector3 rotatedDirection = Quaternion.Euler(0, angle, 0) * awayDirection;
            Vector3 testPosition = transform.position + rotatedDirection * maxDistance;

            if (NavMesh.SamplePosition(testPosition, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                float score = ScoreEscapePosition(hit.position, threatPosition);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = hit.position;
                }
            }
        }

        return bestPosition;
    }

    /// <summary>
    /// Scores an escape position based on distance from threat and path viability.
    /// </summary>
    /// <param name="escapePosition">Position to evaluate.</param>
    /// <param name="threatPosition">Position of the threat.</param>
    /// <returns>Score for the escape position.</returns>
    private float ScoreEscapePosition(Vector3 escapePosition, Vector3 threatPosition)
    {
        float distanceFromThreat = Vector3.Distance(escapePosition, threatPosition);
        float distanceScore = Mathf.Clamp01(distanceFromThreat / EscapeMaxDistance);

        // Prefer positions that maintain some distance from the original position
        float movementScore = Vector3.Distance(transform.position, escapePosition) > 5f ? 1f : 0.5f;

        return distanceScore * 0.7f + movementScore * 0.3f;
    }

    /// <summary>
    /// Overrides the Die method to stop all coroutines before calling the base class's Die method.
    /// </summary>
    protected override void Die()
    {
        StopAllCoroutines(); // Stop all coroutines specific to the prey.
        base.Die(); // Call the base class's Die method.
    }

    /// <summary>
    /// Draws gizmos to visualize the detection distance in the editor.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue; // Set gizmo color to blue.

        // Saving the current Matrix
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Applying the gizmos to work with the mob rotation
        Gizmos.matrix = Matrix4x4.TRS(mobTransform.position, mobTransform.rotation, Vector3.one);

        // Draw gizmos
        Vector3 boxPosition = offSetDetectionDistance;
        Vector3 size = new Vector3(detectionDistance.x, detectionDistance.y, detectionDistance.z);
        Gizmos.DrawWireCube(boxPosition, size);

        // Restoring the previous transformation matrix
        Gizmos.matrix = oldMatrix;
    }
}