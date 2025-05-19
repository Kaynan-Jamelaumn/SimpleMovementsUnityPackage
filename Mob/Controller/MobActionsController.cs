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
/// </remarks>
public class MobActionsController : Mob
{
    [SerializeField] Vector3 detectionDistance;
    [SerializeField] Vector3 offSetDetectionDistance;
    [SerializeField] Transform mobTransform;

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
    /// Determines the available target for the mob.
    /// </summary>
    /// <returns>Returns the Transform of the available target, or null if no target is found.</returns>
    public Transform AvailableTarget()
    {
        // Detects objects within the detection distance.
        Collider[] detectedObjects = detectionCast.DetectObjects(transform);
        MobActionsController backUpPrey = null;

        // Iterate through detected colliders to find prey.
        foreach (var collider in detectedObjects)
        {
            // Check if the collider belongs to a MobActionsController.
            MobActionsController prey = collider.GetComponent<MobActionsController>();
            // Check if the collider belongs to a PlayerStatusController.
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            // If the detected object is a player and is not currently targeted.
            if (player != null && Preys.Contains("Player"))
                if (player.gameObject != currentPlayerTarget)
                    // Return the player's transform as the available target.
                    return player.transform;

            // If the detected object is a prey.
            if (prey != null && Preys.Contains(prey.type))
                // Store the prey as a backup prey.
                backUpPrey = prey;
        }

        // If a backup prey was found and is not currently targeted, return the backup prey's transform.
        if (backUpPrey != null && backUpPrey.gameObject != currentChaseTarget)
            return backUpPrey.transform;

        // If no valid target was found, return null.
        return null;
    }

    /// <summary>
    /// Determines the available targets for the mob.
    /// </summary>
    /// <returns>Returns a list of Transforms of available targets, or null if no targets are found.</returns>
    public List<Transform> AvailableTargets()
    {
        // Detects objects within the detection distance.
        Collider[] detectedObjects = detectionCast.DetectObjects(transform);
        List<Transform> validPlayersTransforms = new List<Transform>();
        List<Transform> validMobsTransforms = new List<Transform>();

        // Iterate through detected colliders to find prey.
        foreach (var collider in detectedObjects)
        {
            // Check if the collider belongs to a MobActionsController.
            MobActionsController prey = collider.GetComponent<MobActionsController>();
            // Check if the collider belongs to a PlayerStatusController.
            PlayerStatusController player = collider.GetComponent<PlayerStatusController>();

            // If the detected object is a player and is not currently targeted.
            if (player != null && Preys.Contains("Player"))
                if (player.gameObject != currentPlayerTarget && !validMobsTransforms.Contains(player.transform))
                    // Add the player's transform to the list of valid player transforms.
                    validPlayersTransforms.Add(player.transform);

            // If the detected object is a prey and is not currently targeted.
            if (prey != null && Preys.Contains(prey.type) && !validMobsTransforms.Contains(prey.transform))
                // Add the prey's transform to the list of valid mob transforms.
                validMobsTransforms.Add(prey.transform);
        }

        // Combine the lists of valid player and mob transforms.
        validPlayersTransforms.AddRange(validMobsTransforms);

        // If there are any valid targets, return the list of valid targets.
        if (validPlayersTransforms.Count > 0)
            return validPlayersTransforms;

        // If no valid targets were found, return null.
        return null;
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
