using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

public class EmoteWheel : MonoBehaviour
{
    // UI and visual references
    [Header("References")]
    [SerializeField] private RectTransform wheelTransform; // The wheel's transform
    [SerializeField] private RectTransform cursorTransform; // Cursor indicator
    [SerializeField] private Animator characterAnimator; // Animator for emote playback
    [SerializeField] private GameObject buttonPrefab; // Prefab for each emote button
    [SerializeField] private float radius = 200f; // Radius of the wheel

    // Configuration settings
    [Header("Settings")]
    [SerializeField] private float deadzoneRadiusPercent = 0.2f; // Deadzone as percentage of radius
    [SerializeField] private float initialAngle = -90f; // Starting angle (top position)

    // Emote data
    [Header("Emotes")]
    [SerializeField] private List<Emote> emotes = new List<Emote>();

    // Runtime variables
    private PlayerInput inputActions;
    private Vector2 mousePosition;
    private bool wheelActive;
    private int currentSelection = -1; // Currently selected emote index
    private int previousSelection = -1; // Previously selected emote index
    private CursorLockMode previousLockState;
    private bool hadPreviousCursorVisibility;
    private List<InputAction> disabledPlayerActions = new List<InputAction>();
    private List<Image> buttonImages = new List<Image>();
    private List<RectTransform> buttonRects = new List<RectTransform>();
    private List<Vector2> buttonPositions = new List<Vector2>(); // Store button positions for selection
    private InputActionMap playerActionMap;
    private float angleStep; // Angle between each emote button
    private float deadzoneRadiusSquared; // Deadzone radius squared for distance comparison
    private Vector2 wheelCenter; // Center position of the wheel in screen space

    [System.Serializable]
    public class Emote
    {
        public string name;
        public Sprite icon;
        public string triggerName; // Animation trigger name
        public UnityEvent onSelected; // Events to fire when selected
    }

    private void Awake()
    {
        // Initialize input system
        inputActions = new PlayerInput();
        playerActionMap = inputActions.asset.FindActionMap("Player");

        // Calculate deadzone as squared value for optimized distance comparison
        deadzoneRadiusSquared = Mathf.Pow(radius * deadzoneRadiusPercent, 2);

        CreateWheelUI();

        // Hide UI elements initially
        wheelTransform.gameObject.SetActive(false);
        cursorTransform.gameObject.SetActive(false);

        // Set up input callbacks
        inputActions.UI.Point.performed += ctx => mousePosition = ctx.ReadValue<Vector2>();
        inputActions.Player.OpenEmoteWheel.performed += ctx => ShowWheel();
        inputActions.Player.OpenEmoteWheel.canceled += ctx => HideWheel();
    }

    private void CreateWheelUI()
    {
        // Clean up existing buttons
        buttonImages.Clear();
        buttonRects.Clear();
        buttonPositions.Clear();
        foreach (Transform child in wheelTransform)
            Destroy(child.gameObject);

        if (emotes.Count == 0) return;

        // Calculate angle between each emote button
        angleStep = 360f / emotes.Count;

        // Convert initial angle to radians for positioning calculations
        float currentAngle = initialAngle * Mathf.Deg2Rad;

        // Create buttons for each emote
        for (int i = 0; i < emotes.Count; i++)
        {
            // Instantiate and configure button
            GameObject button = Instantiate(buttonPrefab, wheelTransform);
            Image img = button.GetComponent<Image>();
            img.sprite = emotes[i].icon;
            buttonImages.Add(img);

            RectTransform rt = button.GetComponent<RectTransform>();
            buttonRects.Add(rt);

            // Position button using polar coordinates (angle and radius)
            Vector2 buttonPos = new Vector2(
                Mathf.Cos(currentAngle) * radius,
                Mathf.Sin(currentAngle) * radius
            );
            rt.anchoredPosition = buttonPos;

            // Store button position for selection calculation
            buttonPositions.Add(buttonPos);

            // Rotate button to face outward from center
            rt.localRotation = Quaternion.Euler(0, 0, (initialAngle + i * angleStep) % 360f);

            // Move to next angle position (convert angleStep to radians)
            currentAngle += angleStep * Mathf.Deg2Rad;
        }
    }

    private void Update()
    {
        if (!wheelActive) return;

        // Calculate vector from wheel center to cursor
        Vector2 cursorDirection = mousePosition - wheelCenter;

        // Use squared magnitude for performance (avoids sqrt calculation)
        float sqrDistance = cursorDirection.sqrMagnitude;

        // Only process selection if outside deadzone
        if (sqrDistance > deadzoneRadiusSquared)
        {
            // Find the closest button to the cursor position
            int closestButton = FindClosestButton(cursorDirection);

            // Handle selection changes
            if (closestButton != currentSelection)
            {
                previousSelection = currentSelection;
                currentSelection = closestButton;
                UpdateSelection();
            }
        }
        else if (currentSelection != -1) // Handle entering deadzone
        {
            previousSelection = currentSelection;
            currentSelection = -1;
            UpdateSelection();
        }

        // Update cursor position, clamping to wheel radius
        cursorTransform.position = wheelCenter + cursorDirection.normalized * Mathf.Min(Mathf.Sqrt(sqrDistance), radius);
    }

    private int FindClosestButton(Vector2 cursorDirection)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < buttonPositions.Count; i++)
        {
            // Calculate distance from cursor to button position
            float distance = Vector2.Distance(cursorDirection, buttonPositions[i]);
            // This uses: √[(x₂-x₁)² + (y₂-y₁)²]

            // Find the minimum distance
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private void UpdateSelection()
    {
        // No selection case (in deadzone)
        if (currentSelection == -1)
        {
            // Reset all buttons to default state
            foreach (Image img in buttonImages)
            {
                img.color = Color.white;
                img.transform.localScale = Vector3.one;
            }
            return;
        }

        // Reset previous selection if valid
        if (previousSelection != -1 && previousSelection < buttonImages.Count)
        {
            buttonImages[previousSelection].color = Color.white;
            buttonRects[previousSelection].localScale = Vector3.one;
        }

        // Highlight current selection if valid
        if (currentSelection < buttonImages.Count)
        {
            buttonImages[currentSelection].color = Color.yellow;
            buttonRects[currentSelection].localScale = Vector3.one * 1.2f;
        }
    }


    private void ShowWheel()
    {
        wheelActive = true;
        wheelCenter = wheelTransform.position; // Capture the wheel's screen-space position
        wheelTransform.gameObject.SetActive(true);
        cursorTransform.gameObject.SetActive(true);

        // Store and modify cursor visibility and locking
        previousLockState = Cursor.lockState;
        hadPreviousCursorVisibility = Cursor.visible;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Disable conflicting player actions
        DisableNonEssentialActions();
        inputActions.UI.Enable();
    }

    private void DisableNonEssentialActions()
    {
        disabledPlayerActions.Clear();
        foreach (InputAction action in playerActionMap.actions)
        {
            if (action.name != "OpenEmoteWheel" && action.enabled)
            {
                action.Disable();
                disabledPlayerActions.Add(action);
            }
        }
    }

    private void HideWheel()
    {
        wheelActive = false;
        wheelTransform.gameObject.SetActive(false);
        cursorTransform.gameObject.SetActive(false);

        // Restore cursor state
        Cursor.lockState = previousLockState;
        Cursor.visible = hadPreviousCursorVisibility;

        // Re-enable disabled player actions
        foreach (InputAction action in disabledPlayerActions)
            action.Enable();

        inputActions.UI.Disable();

        // Trigger the selected emote if one is selected
        if (currentSelection >= 0)
        {
            ExecuteEmote(currentSelection);
            currentSelection = -1;
        }
    }

    private void ExecuteEmote(int index)
    {
        if (index < 0 || index >= emotes.Count) return;

        // Trigger the emote animation and invoke its associated event
        characterAnimator.SetTrigger(emotes[index].triggerName);
        emotes[index].onSelected?.Invoke();
    }

    private void OnEnable() => inputActions?.Enable();
    private void OnDisable() => inputActions?.Disable();


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!wheelTransform) return;

        // Visualize wheel radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(wheelTransform.position, radius);

        // Visualize deadzone
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(wheelTransform.position, radius * deadzoneRadiusPercent);

        // Visualize button positions and selection areas
        if (Application.isPlaying && buttonPositions.Count > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < buttonPositions.Count; i++)
            {
                Vector3 worldPos = wheelTransform.position + (Vector3)buttonPositions[i];
                Gizmos.DrawWireSphere(worldPos, 30f); // Show button selection area
            }
        }
    }
#endif
}