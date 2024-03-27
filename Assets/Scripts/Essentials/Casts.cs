using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class CastBase
{
    public enum CastType
    {
        Sphere,
        Box,
        Capsule,
        Ray,
    }

    [Header("Cast Settings")]
    [SerializeField] public CastType castType = CastType.Sphere;
    [SerializeField] public float castSize = 5f;
    [SerializeField] public Vector3 boxSize;
    [SerializeField] public Vector3 customOrigin = Vector3.zero;
    [SerializeField] public Vector3 customAngle = Vector3.zero;
    [SerializeField] public LayerMask targetLayers;

    public virtual void DrawGizmos(Transform transform)
    {
        Gizmos.color = Color.yellow;
        switch (castType)
        {
            case CastType.Sphere:
                Gizmos.DrawWireSphere(transform.position + customOrigin, castSize);
                break;
            case CastType.Box:
                Gizmos.DrawWireCube(transform.position + customOrigin, boxSize);
                break;
            case CastType.Capsule:
                DrawWireCapsule(transform.position + customOrigin, castSize, 0.5f, transform.forward);
                break;
            case CastType.Ray:
                Gizmos.DrawRay(transform.position + customOrigin, transform.forward * castSize);
                break;
        }
    }

    public virtual Collider[] DetectObjects(Transform transform)
    {
        if (!transform) return new Collider[0];
        int targetLayerMask = 0;
        //foreach (LayerMask mask in targetLayers)
        //{
            targetLayerMask |= targetLayers.value;
//        }

        switch (castType)
        {
            case CastType.Sphere:
                return Physics.OverlapSphere(transform.position + customOrigin, castSize, targetLayerMask);
            case CastType.Box:
                return Physics.OverlapBox(transform.position + customOrigin, boxSize / 2, transform.rotation, targetLayerMask);
            case CastType.Capsule:
                return Physics.OverlapCapsule(transform.position + customOrigin, transform.position + customOrigin + transform.forward * castSize, castSize / 2, targetLayerMask);
            case CastType.Ray:
                RaycastHit hit;
                if (Physics.Raycast(transform.position + customOrigin, transform.forward, out hit, castSize, targetLayerMask))
                {
                    return new Collider[] { hit.collider };
                }
                else
                {
                    return new Collider[0];
                }
            default:
                return new Collider[0];
        }
    }

    private void DrawWireCapsule(Vector3 position, float height, float radius, Vector3 direction)
    {
        int segments = 20;
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            Vector3 start = position + Quaternion.Euler(0, angle, 0) * direction * radius;
            Vector3 end = position + Quaternion.Euler(0, angle + angleStep, 0) * direction * radius;
            Gizmos.DrawLine(start, end);

            if (i == 0 || i == segments - 1)
            {
                Gizmos.DrawLine(position, start);
                Gizmos.DrawLine(position + direction * height, end);
            }
        }
    }
}
[System.Serializable]
public class Cast : CastBase
{
    //public override void DrawGizmos(Transform transform)
    //{
    //    base.DrawGizmos(transform, Color.yellow);
    //}
}
[System.Serializable]
public class AttackCast : CastBase
{
    //public float attackRange = 10f;

    public override void DrawGizmos(Transform transform)
    {

        // Set the color of the gizmos to red
        Gizmos.color = Color.red;

        // Store the current transformation matrix for later restoration
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Switch between different types of casting
        switch (castType)
        {
            case CastType.Sphere:
                // Calculate the transformation matrix for sphere casting
                Matrix4x4 sphereMatrix = Matrix4x4.TRS(transform.position, transform.rotation * Quaternion.Euler(customOrigin), Vector3.one);

                // Apply the transformation matrix and draw a wire sphere centered at the custom origin with the provided cast size
                Gizmos.matrix *= sphereMatrix;
                Gizmos.DrawWireSphere(customOrigin, castSize);
                break;

            case CastType.Box:
                // Calculate the transformation matrix for box casting
                Quaternion boxRotation = Quaternion.Euler(customAngle);
                Matrix4x4 boxMatrix = Matrix4x4.TRS(transform.position, transform.rotation * boxRotation, Vector3.one);

                // Apply the transformation matrix and draw a wire cube centered at the custom origin with the provided box size
                Gizmos.matrix *= boxMatrix;
                Gizmos.DrawWireCube(customOrigin, boxSize);
                break;

            case CastType.Capsule:
                // Draw a wire capsule with the provided parameters
                Gizmos.DrawWireSphere(transform.position + customOrigin, castSize);
                break;

            case CastType.Ray:
                // Calculate the transformation matrix for ray casting
                Matrix4x4 rayMatrix = Matrix4x4.TRS(transform.position + customOrigin, transform.rotation, Vector3.one);

                // Apply the transformation matrix and draw a ray starting from the custom origin and extending along the forward direction with the provided cast size
                Gizmos.matrix *= rayMatrix;
                Gizmos.DrawRay(Vector3.zero, Vector3.forward * castSize);
                break;
        }

        // Restore the previous transformation matrix to avoid affecting subsequent gizmos
        Gizmos.matrix = oldMatrix;
    }
}
