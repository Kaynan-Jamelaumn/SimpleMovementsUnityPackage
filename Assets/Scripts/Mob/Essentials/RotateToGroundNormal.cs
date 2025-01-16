using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class rotates a GameObject to align with the ground's normal vector.
/// </summary>
public class RotateToGroundNormal : MonoBehaviour
{
    /// <summary>
    /// Speed at which the GameObject will rotate to match the ground's normal.
    /// </summary>
    [SerializeField] private float rotationSpeed = 5f;

    /// <summary>
    /// The Transform of the model that will be rotated.
    /// </summary>
    [SerializeField] private Transform modelTransform;

    /// <summary>
    /// Called once per frame.
    /// </summary>
    private void Update()
    {
        /// <summary>
        /// Casts a ray downward from the GameObject's position to detect the ground.
        /// </summary>
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit))
        {
            /// <summary>
            /// Calculates the target rotation to align the GameObject's up vector with the ground's normal vector.
            /// </summary>
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;

            /// <summary>
            /// Smoothly interpolates the GameObject's rotation towards the target rotation.
            /// </summary>
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}
