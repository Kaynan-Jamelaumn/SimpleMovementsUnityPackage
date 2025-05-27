using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections;

public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] private PlayerCameraModel model;

    // Camera switching system - maintains order and current index for cycling through cameras
    private List<GameObject> cameraOrder = new List<GameObject>();
    private int currentIndex = 0;

    // New features
    private Coroutine cameraTransitionCoroutine;
    private Coroutine cameraShakeCoroutine;
    private Vector3 originalCameraPosition;
    private bool isTransitioning = false;

    // Camera state preservation - saves positions/rotations for potential restoration
    private Dictionary<GameObject, Vector3> cameraPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Quaternion> cameraRotations = new Dictionary<GameObject, Quaternion>();

    // Each camera gets its own CinemachineBasicMultiChannelPerlin component for shake
    private Dictionary<GameObject, CinemachineBasicMultiChannelPerlin> shakeComponents = new Dictionary<GameObject, CinemachineBasicMultiChannelPerlin>();

    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        model.CameraTransform = this.CheckComponent(model.CameraTransform, nameof(model.CameraTransform), searchChildren: true);
        this.ValidateDict(model.CameraDictionary, nameof(model.CameraDictionary), allowEmpty: false);

        // Process each camera: add to order list, deactivate, store initial state, setup shake

        foreach (var kvp in model.CameraDictionary)
        {
            if (kvp.Key != null)
            {
                cameraOrder.Add(kvp.Key);              // Add to switching order
                kvp.Key.SetActive(false);              // Start inactive (only one active at a time)


                // Store initial positions and rotations
                cameraPositions[kvp.Key] = kvp.Key.transform.position;
                cameraRotations[kvp.Key] = kvp.Key.transform.rotation;

                // Cache Cinemachine shake components
                CacheShakeComponent(kvp.Key);
            }
        }
    }

    private void CacheShakeComponent(GameObject cameraObject)
    {
        if (cameraObject.TryGetComponent<CinemachineCamera>(out var cmCamera))
        {
            // Look for existing noise component (handles shake via Perlin noise)
            var noiseComponent = cmCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (noiseComponent == null)
            {
                // Add noise component if it doesn't exist
                noiseComponent = cmCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();

                if (model.DefaultNoiseSettings != null)
                {
                    noiseComponent.NoiseProfile = model.DefaultNoiseSettings;
                }
                else
                {
                    Debug.LogWarning($"No default noise settings assigned for camera shake on {cameraObject.name}");
                }
            }

            if (noiseComponent != null)
            {
                shakeComponents[cameraObject] = noiseComponent;

                // Always initialize to zero - this prevents residual shaking
                noiseComponent.AmplitudeGain = 0f;
                noiseComponent.FrequencyGain = 0f;
            }
        }
    }

    private void Start()
    {
        // Activate the first camera in the list as the starting cameras
        if (cameraOrder.Count > 0)
        {
            currentIndex = 0;
            ActivateCamera(cameraOrder[currentIndex]);
        }
    }

    private void Update()
    {
        if (model.CurrentCamera == null) return;

        // Update camera state based on current camera's configuration
        // This determines if player should rotate with camera (first person vs third person behavior)
        if (model.CameraDictionary.TryGetValue(model.CurrentCamera, out var cameraEntry))
        {
            model.IsFirstPerson = cameraEntry.isFirstPerson;
            // First person always rotates with camera, third person only when right-clicking
            model.PlayerShouldRotateByCameraAngle = cameraEntry.isFirstPerson || (model.IsRightPressed && !cameraEntry.isFirstPerson);
        }

        // Handle FOV transitions
        HandleFOVTransitions();
    }

    // FOV TRANSITION SYSTEM - Smoothly interpolates between default and zoomed FOV
    // This creates smooth zoom effects instead of instant FOV changes
    private void HandleFOVTransitions()
    {
        if (model.CurrentCamera == null) return;

        float targetFOV = model.IsZooming ? model.ZoomedFOV : model.DefaultFOV;
        float currentFOV = GetCameraFOV();

        // Only interpolate if there's a meaningful difference (avoids micro-adjustments)

        if (Mathf.Abs(currentFOV - targetFOV) > 0.1f)
        {
            float newFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * model.FOVTransitionSpeed);
            SetCameraFOV(newFOV);
            model.TriggerFOVChanged(newFOV);
        }
    }


    // CAMERA SWITCHING SYSTEM - Cycles through cameras in order
    // Can use either instant switching or smooth transitions based on camera settings
    public void ChangeCamera()
    {
        if (cameraOrder.Count == 0 || isTransitioning) return;

        var oldCamera = model.CurrentCamera;
        currentIndex = (currentIndex + 1) % cameraOrder.Count; // Wrap around to start
        var newCamera = cameraOrder[currentIndex];

        // Check if this camera wants smooth transitions
        var cameraEntry = model.GetCameraEntry(newCamera);
        if (cameraEntry?.transitionSettings?.useSmoothing == true)
        {
            StartSmoothTransition(oldCamera, newCamera, cameraEntry.transitionSettings);
        }
        else
        {
            ActivateCamera(newCamera); // Instant switch
        }
    }

    // Handles gradual camera switching with custom curves
    private void StartSmoothTransition(GameObject from, GameObject to, CameraTransitionSettings settings)
    {
        // Stop any existing transition to prevent conflicts
        if (cameraTransitionCoroutine != null)
            StopCoroutine(cameraTransitionCoroutine);

        cameraTransitionCoroutine = StartCoroutine(SmoothCameraTransition(from, to, settings));
    }


    private System.Collections.IEnumerator SmoothCameraTransition(GameObject from, GameObject to, CameraTransitionSettings settings)
    {
        isTransitioning = true;

        // Activate destination camera but keep it invisible/low priority initially
        to.SetActive(true);

        float elapsed = 0f;
        while (elapsed < settings.transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settings.transitionDuration;
            float curveValue = settings.transitionCurve.Evaluate(t); // Use custom animation curve
                // TODO: Add custom transition effects here (crossfade, position interpolation, etc.)
                // Currently just handles timing - can be extended for visual effects

            yield return null;
        }

        // Finalize transition - make new camera primary and clean up old one
        if (from != null) from.SetActive(false);
        model.CurrentCamera = to;
        model.CameraTransform = to.transform;

        // Update cursor and perspective state based on new camera

        if (model.CameraDictionary.TryGetValue(to, out var cameraEntry))
        {
            SetCursorLock(cameraEntry.isFirstPerson);
            model.TriggerFirstPersonChanged(cameraEntry.isFirstPerson);
        }

        model.TriggerCameraChanged(from, to); // Notify other systems
        isTransitioning = false;
    }

    // CAMERA SHAKE SYSTEM - Main entry points for triggering shake effects
    public void ShakeCamera(CameraShakeSettings shakeSettings)
    {
        // Stop any existing shake before starting new one
        // This prevents shake effects from stacking or interfering with each other
        if (cameraShakeCoroutine != null)
        {
            StopCoroutine(cameraShakeCoroutine);
            ResetCameraShake(); // Immediately reset to zero to prevent residual shake
        }

        cameraShakeCoroutine = StartCoroutine(CinemachineCameraShakeCoroutine(shakeSettings));
    }

    // Convenience overload for quick shake calls without creating settings object
    public void ShakeCamera(float intensity = 1f, float duration = 0.5f, float frequency = 10f)
    {
        var settings = new CameraShakeSettings
        {
            intensity = intensity,
            duration = duration,
            frequency = frequency
        };
        ShakeCamera(settings);
    }

    // Ensures camera completely stops shaking
    private void ResetCameraShake()
    {
        if (model.CurrentCamera != null && shakeComponents.TryGetValue(model.CurrentCamera, out var shakeComponent))
        {
            // Always reset to absolute zero - no more residual shake
            shakeComponent.AmplitudeGain = 0f;
            shakeComponent.FrequencyGain = 0f;
        }
    }

    // SHAKE ANIMATION COROUTINE - Handles the actual shake effect over time
    // Uses Cinemachine's Perlin noise system for realistic camera shake
    private System.Collections.IEnumerator CinemachineCameraShakeCoroutine(CameraShakeSettings settings)
    {
        if (model.CurrentCamera == null)
        {
            Debug.LogWarning("No current camera to shake!");
            yield break;
        }

        // Get or create shake component for current camera
        if (!shakeComponents.TryGetValue(model.CurrentCamera, out var shakeComponent))
        {
            Debug.LogWarning($"No shake component found for camera: {model.CurrentCamera.name}");
            CacheShakeComponent(model.CurrentCamera); // Try to create it
            if (!shakeComponents.TryGetValue(model.CurrentCamera, out shakeComponent))
            {
                Debug.LogError($"Could not create shake component for camera: {model.CurrentCamera.name}");
                yield break;
            }
        }

        Debug.Log($"Starting camera shake on {model.CurrentCamera.name} with intensity: {settings.intensity}, duration: {settings.duration}");

        float elapsed = 0f;

        // Animate shake over the specified duration
        while (elapsed < settings.duration)
        {
            elapsed += Time.deltaTime;

            float intensity = settings.intensity;

            // Diminish shake intensity over time for more realistic effect
            if (settings.diminishOverTime)
            {
                intensity *= (1f - elapsed / settings.duration);
            }

            // Apply shake values to Cinemachine noise component
            // AmplitudeGain controls the strength, FrequencyGain controls the speed/jitter
            shakeComponent.AmplitudeGain = intensity;
            shakeComponent.FrequencyGain = settings.frequency;

            yield return null;
        }

        // Ensure shake is completely stopped
        shakeComponent.AmplitudeGain = 0f;
        shakeComponent.FrequencyGain = 0f;

        cameraShakeCoroutine = null; // Clear reference
        Debug.Log("Camera shake completed and reset to zero");
    }

    // ALTERNATIVE SHAKE METHOD - Uses Cinemachine Impulse system instead of Perlin noise
    // Impulse creates more dramatic, physics-based shake effects
    public void ShakeCameraWithImpulse(float force = 1f, Vector3 velocity = default)
    {
        if (model.CurrentCamera == null) return;

        if (model.CurrentCamera.TryGetComponent<CinemachineCamera>(out var cmCamera))
        {
            // Get or create impulse source component
            var impulseSource = cmCamera.GetComponent<CinemachineImpulseSource>();
            if (impulseSource == null)
            {
                impulseSource = cmCamera.gameObject.AddComponent<CinemachineImpulseSource>();
            }

            // Use random direction if none specified
            if (velocity == default)
                velocity = UnityEngine.Random.insideUnitSphere;

            impulseSource.GenerateImpulse(velocity * force);
        }
    }

    // CAMERA SWITCHING BY NAME - Allows direct switching to specific cameras
    // Useful for scripted camera changes or UI-driven camera selection
    public bool SwitchToCamera(string cameraName)
    {
        foreach (var kvp in model.CameraDictionary)
        {
            if (kvp.Value.cameraName == cameraName)
            {
                var index = cameraOrder.IndexOf(kvp.Key);
                if (index >= 0)
                {
                    currentIndex = index;
                    ActivateCamera(kvp.Key);
                    return true;
                }
            }
        }
        return false; // Camera not found
    }

    // Get list of all available camera names for UI or debugging
    public List<string> GetAvailableCameraNames()
    {
        var names = new List<string>();
        foreach (var kvp in model.CameraDictionary)
        {
            names.Add(kvp.Value.cameraName);
        }
        return names;
    }

    // CAMERA STATE MANAGEMENT - Save and restore camera positions/rotations
    // Useful for cutscenes, special events, or resetting cameras to initial state
    public void SaveCameraState(GameObject camera)
    {
        if (camera != null)
        {
            cameraPositions[camera] = camera.transform.position;
            cameraRotations[camera] = camera.transform.rotation;
        }
    }
    public void RestoreCameraState(GameObject camera)
    {
        if (camera != null && cameraPositions.ContainsKey(camera))
        {
            camera.transform.position = cameraPositions[camera];
            camera.transform.rotation = cameraRotations[camera];
        }
    }

    // Enhanced zoom functionality
    public void SetZoom(bool isZooming)
    {
        model.IsZooming = isZooming;
    }

    public void ToggleZoom()
    {
        model.IsZooming = !model.IsZooming;
    }

    // Existing methods (enhanced)
    public Vector3 DirectionToMoveByCamera(Vector3 direction)
    {
        return Quaternion.AngleAxis(model.CameraTransform.rotation.eulerAngles.y, Vector3.up) * direction;
    }

    private void CursorLocker(bool shouldLock) => Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;

    public void SetCursorLock(bool shouldLock)
    {
        CursorLocker(shouldLock);
        Cursor.visible = !shouldLock;
    }

    private void ActivateCamera(GameObject camera)
    {
        if (camera == null) return;

        var oldCamera = model.CurrentCamera;
        SetCameraActive(camera, true);
        model.CurrentCamera = camera;

        if (camera.TryGetComponent<CinemachineCamera>(out var cmCamera))
        {
            // Modern Cinemachine configuration
        }

        if (model.CameraDictionary.TryGetValue(camera, out var cameraEntry))
        {
            SetCursorLock(cameraEntry.isFirstPerson);
            model.TriggerFirstPersonChanged(cameraEntry.isFirstPerson);
        }

        model.TriggerCameraChanged(oldCamera, camera);
    }

    public void SetCameraActive(GameObject camera, bool isActive)
    {
        if (camera != null)
        {
            camera.SetActive(isActive);
            if (isActive)
            {
                model.CameraTransform = camera.transform;
                var brain = CinemachineCore.FindPotentialTargetBrain(camera.GetComponent<CinemachineVirtualCameraBase>());
                brain?.ManualUpdate();
            }
        }
    }

    // FOV methods
    public void InterpolateFOV(float startingFOV, float playerFOV, float timeLapse, float timeToZoom)
    {
        float interpolatedFOV = Mathf.Lerp(startingFOV, playerFOV, timeLapse / timeToZoom);
        SetCameraFOV(interpolatedFOV);
    }

    public void SetCameraFOV(float fov)
    {
        if (model.CurrentCamera != null)
        {
            if (model.CurrentCamera.TryGetComponent<CinemachineCamera>(out var cmCamera))
            {
                cmCamera.Lens.FieldOfView = fov;
            }
            else if (model.CurrentCamera.TryGetComponent<Camera>(out var regularCamera))
            {
                regularCamera.fieldOfView = fov;
            }
        }
    }

    public float GetCameraFOV()
    {
        if (model.CurrentCamera != null)
        {
            if (model.CurrentCamera.TryGetComponent<CinemachineCamera>(out var cmCamera))
            {
                return cmCamera.Lens.FieldOfView;
            }
            else if (model.CurrentCamera.TryGetComponent<Camera>(out var regularCamera))
            {
                return regularCamera.fieldOfView;
            }
        }
        return 60f;
    }
}