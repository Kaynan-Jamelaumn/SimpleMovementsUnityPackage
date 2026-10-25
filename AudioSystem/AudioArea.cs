using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;

[System.Serializable]
public class AudioArea : MonoBehaviour
{
    [Header("Area Settings")]
    public string areaName = "AudioArea";
    public AreaShape areaShape = AreaShape.Sphere;

    [Header("Shape Properties")]
    public float radius = 10f; // For sphere and cylinder
    public Vector3 boxSize = Vector3.one; // For box
    public float height = 5f; // For cylinder

    [Header("Area Audio")]
    public AudioClip enterSound;
    [Range(0f, 1f)]
    public float enterSoundVolume = 1f;

    public AudioClip exitSound;
    [Range(0f, 1f)]
    public float exitSoundVolume = 1f;

    public AudioClip ambientSound; // Loops while in area
    [Range(0f, 1f)]
    public float ambientVolume = 0.5f;

    [Header("Area Behavior")]
    public bool exclusiveArea = false; // Only one sound can play in this area
    [Range(1, 10)]
    public int maxSimultaneousSounds = 3;
    public bool fadeOnTransition = true;
    [Range(0.1f, 5f)]
    public float fadeTime = 1f;

    [Header("Trigger Settings")]
    public LayerMask playerLayers = -1;
    public bool requiresLineOfSight = false;
    public LayerMask obstacleLayers = -1;

    // Check if player is within the area
    public bool IsPlayerInArea(Vector3 playerPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(playerPosition);

        switch (areaShape)
        {
            case AreaShape.Sphere:
                return localPosition.magnitude <= radius;

            case AreaShape.Box:
                return Mathf.Abs(localPosition.x) <= boxSize.x * 0.5f &&
                       Mathf.Abs(localPosition.y) <= boxSize.y * 0.5f &&
                       Mathf.Abs(localPosition.z) <= boxSize.z * 0.5f;

            case AreaShape.Cylinder:
                float horizontalDistance = Mathf.Sqrt(localPosition.x * localPosition.x + localPosition.z * localPosition.z);
                return horizontalDistance <= radius && Mathf.Abs(localPosition.y) <= height * 0.5f;

            default:
                return false;
        }
    }

    // Check line of sight if required
    public bool HasLineOfSight(Vector3 playerPosition)
    {
        if (!requiresLineOfSight) return true;

        Vector3 direction = transform.position - playerPosition;
        float distance = direction.magnitude;

        return !ScenePhysics.Raycast(playerPosition, direction.normalized, distance, obstacleLayers);
    }

    // Gizmo drawing for area visualization
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        DrawAreaGizmo(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        DrawAreaGizmo(true);
    }

    private void DrawAreaGizmo(bool selected)
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        switch (areaShape)
        {
            case AreaShape.Sphere:
                if (selected)
                    Gizmos.DrawSphere(Vector3.zero, radius);
                else
                    Gizmos.DrawWireSphere(Vector3.zero, radius);
                break;

            case AreaShape.Box:
                if (selected)
                    Gizmos.DrawCube(Vector3.zero, boxSize);
                else
                    Gizmos.DrawWireCube(Vector3.zero, boxSize);
                break;

            case AreaShape.Cylinder:
                // Draw cylinder as combination of circles and lines
                DrawWireCylinder(Vector3.zero, radius, height);
                break;
        }

        Gizmos.matrix = oldMatrix;
    }

    private void DrawWireCylinder(Vector3 center, float radius, float height)
    {
        // Draw top and bottom circles
        for (int i = 0; i < 32; i++)
        {
            float angle = i * 2 * Mathf.PI / 32;
            float nextAngle = (i + 1) * 2 * Mathf.PI / 32;

            Vector3 point1 = center + new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius);
            Vector3 point2 = center + new Vector3(Mathf.Cos(nextAngle) * radius, height * 0.5f, Mathf.Sin(nextAngle) * radius);
            Vector3 point3 = center + new Vector3(Mathf.Cos(angle) * radius, -height * 0.5f, Mathf.Sin(angle) * radius);
            Vector3 point4 = center + new Vector3(Mathf.Cos(nextAngle) * radius, -height * 0.5f, Mathf.Sin(nextAngle) * radius);

            // Top circle
            Gizmos.DrawLine(point1, point2);
            // Bottom circle
            Gizmos.DrawLine(point3, point4);
            // Vertical lines
            if (i % 8 == 0)
                Gizmos.DrawLine(point1, point3);
        }
    }
}
