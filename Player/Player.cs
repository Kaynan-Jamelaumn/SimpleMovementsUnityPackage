using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Assertions;
using UnityEngine.UI;
using System.Collections;

public class Player : MonoBehaviour
{
    [SerializeField] InventoryManager inventoryManager;
    [SerializeField] Camera cam;
    [SerializeField] TMPro.TextMeshProUGUI interactionText;
    [SerializeField] GameObject interactionUI;
    [SerializeField] Image fillingCircle;
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private string playerName;

    private ItemPickable item;
    private Storage storage;
    private Coroutine interactionCoroutine;
    private RaycastHit hitInfo;
    private const float MAX_INTERACTION_DISTANCE = 3.2f;

    // Properties with validation
    public AudioSource PlayerAudioSource
    {
        get => playerAudioSource;
        set => playerAudioSource = value ?? throw new System.ArgumentNullException(nameof(value));
    }

    public string PlayerName
    {
        get => playerName;
        set => playerName = !string.IsNullOrEmpty(value) ? value : "Unknown Player";
    }

    public Camera Cam => cam;
    public InventoryManager InventoryManager => inventoryManager;
    public TextMeshProUGUI InteractionText => interactionText;

    private void Awake()
    {
        ValidateComponents();
        InitializeComponents();
    }

    private void Start()
    {
        ValidateAssignments();
    }

    void Update()
    {
        if (!IsValidForInteraction()) return;

        HandleInteractionDetection();
    }

    private bool IsValidForInteraction()
    {
        return cam != null && Mouse.current != null;
    }

    private void HandleInteractionDetection()
    {
        try
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out hitInfo, MAX_INTERACTION_DISTANCE))
            {
                ProcessRaycastHit();
            }
            else
            {
                DeactivateInteractionUI();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in interaction detection: {e.Message}");
            DeactivateInteractionUI();
        }
    }

    private void ProcessRaycastHit()
    {
        if (hitInfo.collider?.gameObject == null)
        {
            DeactivateInteractionUI();
            return;
        }

        var item = hitInfo.collider.gameObject.GetComponent<ItemPickable>();
        var storage = hitInfo.collider.gameObject.GetComponent<Storage>();
        var interactable = hitInfo.collider.gameObject.GetComponentInParent<Interactable>();

        if (item != null || storage != null || interactable != null)
        {
            ActivateInteractionUI();
        }
        else
        {
            DeactivateInteractionUI();
        }
    }

    private void ActivateInteractionUI()
    {
        if (interactionUI != null)
            interactionUI.SetActive(true);
    }

    private void DeactivateInteractionUI()
    {
        if (interactionUI != null)
            interactionUI.SetActive(false);
    }

    private void ValidateComponents()
    {
        if (cam == null) cam = Camera.main;
        if (playerAudioSource == null) playerAudioSource = GetComponent<AudioSource>();
    }

    private void InitializeComponents()
    {
        if (cam && playerAudioSource == null)
            playerAudioSource = GetComponent<AudioSource>();
    }

    private void ValidateAssignments()
    {
        Assert.IsNotNull(inventoryManager, "InventoryManager is not assigned.");
        Assert.IsNotNull(cam, "Camera is not assigned.");
        Assert.IsNotNull(interactionUI, "Interaction UI is not assigned.");
        Assert.IsNotNull(fillingCircle, "Filling circle is not assigned.");

        if (playerAudioSource == null)
            Debug.LogWarning("Player AudioSource is not assigned - audio effects will not work.");
    }

    public void OnInteract(InputAction.CallbackContext value)
    {
        try
        {
            if (value.started)
            {
                HandleInteractionStart();
            }
            else if (value.canceled)
            {
                HandleInteractionCancel();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during interaction: {e.Message}");
            CleanupInteraction();
        }
    }

    private void HandleInteractionStart()
    {
        if (!IsValidHitInfo()) return;

        var pickableItem = hitInfo.collider.gameObject.GetComponentInParent<ItemPickable>();
        var interactable = hitInfo.collider.GetComponent<Interactable>();
        var storage = hitInfo.collider.GetComponent<Storage>();

        if (pickableItem != null)
        {
            HandlePickableItem(pickableItem);
        }
        else if (interactable != null)
        {
            HandleInteractable(interactable);
        }
        else if (storage != null)
        {
            HandleStorage(storage);
        }
    }

    private bool IsValidHitInfo()
    {
        return hitInfo.collider?.gameObject != null;
    }

    private void HandlePickableItem(ItemPickable pickableItem)
    {
        if (pickableItem.InteractionTime > 0)
        {
            interactionCoroutine = StartCoroutine(InteractWithObject(pickableItem));
        }
        else
        {
            Debug.Log($"Picking up item: {pickableItem.gameObject.name}");
            if (inventoryManager != null)
            {
                inventoryManager.ItemPicked(pickableItem.gameObject);
            }
        }
    }

    private void HandleInteractable(Interactable interactable)
    {
        if (interactable.InteractionTime > 0)
        {
            interactionCoroutine = StartCoroutine(InteractWithObject(interactable));
        }
        else
        {
            interactable.Interact();
        }
    }

    private void HandleStorage(Storage storage)
    {
        if (inventoryManager == null) return;

        if (inventoryManager.IsStorageOpened)
        {
            inventoryManager.CloseStorage(storage);
        }
        else
        {
            inventoryManager.OpenStorage(storage);
        }
    }

    private void HandleInteractionCancel()
    {
        CleanupInteraction();
    }

    private void CleanupInteraction()
    {
        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;

            if (fillingCircle != null)
                fillingCircle.fillAmount = 1f;
        }
    }

    private IEnumerator InteractWithObject(Interactable interactable)
    {
        if (interactable == null || hitInfo.collider?.gameObject == null)
        {
            yield break;
        }

        GameObject interactableObject = hitInfo.collider.gameObject;

        if (fillingCircle != null)
            fillingCircle.fillAmount = 0f;

        if (interactable.InteractionTime > 0)
        {
            float elapsedTime = 0f;
            while (elapsedTime < interactable.InteractionTime)
            {
                if (fillingCircle != null)
                {
                    float fillAmount = elapsedTime / interactable.InteractionTime;
                    fillingCircle.fillAmount = fillAmount;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (fillingCircle != null)
                fillingCircle.fillAmount = 1f;
        }

        // Complete the interaction
        var itemPickable = interactableObject.GetComponent<ItemPickable>();
        if (itemPickable != null && inventoryManager != null)
        {
            inventoryManager.ItemPicked(interactableObject);
        }
        else
        {
            interactable.Interact();
        }

        if (interactionUI != null)
            interactionUI.SetActive(false);

        if (fillingCircle != null)
            fillingCircle.fillAmount = 1f;
    }
}