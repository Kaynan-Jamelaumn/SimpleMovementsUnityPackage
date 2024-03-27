
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Assertions;
using UnityEngine.UI;
using System.Collections;

public class Player : MonoBehaviour
{
    [SerializeField] InventoryManager inventoryManager;
    //[SerializeField] PlayerCameraModel camera;
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
    public AudioSource PlayerAudioSource { get => playerAudioSource; set => playerAudioSource = value; }
    public string PlayerName { get => playerName; set => playerName = value; }
    public Camera Cam
    {
        get => cam;
    }

    public InventoryManager InventoryManager
    {
        get => inventoryManager;
        set => inventoryManager = value;
    }

    public TextMeshProUGUI InteractionText
    {
        get => interactionText;
        set => interactionText = value;
    }
    private void Awake()
    {
        if (Cam) playerAudioSource = transform.GetComponent<AudioSource>();
    }
    private void Start()
    {
        ValidateAsignments();
    }
    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        float maxDistance = 3.2f;

        if (Physics.Raycast(ray, out hitInfo, maxDistance))
        {
            ItemPickable item = hitInfo.collider.gameObject.GetComponent<ItemPickable>();
            Storage storage = hitInfo.collider.gameObject.GetComponent<Storage>();
            Interactable interactable = hitInfo.collider.gameObject.GetComponentInParent<Interactable>();

            if (item != null || storage != null || interactable != null)
            {
                interactionUI.SetActive(true);
            }
            else
            {
                interactionUI.SetActive(false);
            }
        }
        else
        {
            interactionUI.SetActive(false); // No objects hit, deactivate UI
        }
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(inventoryManager, "InventoryManager is not assigned in inventoryManager.");
        Assert.IsNotNull(playerAudioSource, "AudioSource is not assigned in playerAudioSource.");
    }
    public void OnInteract(InputAction.CallbackContext value)
    {
        // Se o botão foi pressionado
        if (value.started)
        {
            if (item != null || hitInfo.collider != null && hitInfo.collider.gameObject != null && hitInfo.collider.GetComponent<Interactable>())
            {
                ItemPickable pickableItem = hitInfo.collider.gameObject.GetComponentInParent<ItemPickable>();
                if (pickableItem != null)
                {
                    if (pickableItem.InteractionTime > 0)
                        interactionCoroutine = StartCoroutine(InteractWithObject(pickableItem));
                    
                    else
                        inventoryManager.ItemPicked(hitInfo.collider.gameObject);
                    
                }
                else
                {
                    Interactable interactable = hitInfo.collider.GetComponent<Interactable>();
                    if (interactable != null)
                    {
                        if (interactable.InteractionTime > 0)
                            interactionCoroutine = StartCoroutine(InteractWithObject(interactable));
                        
                        else
                            interactable.Interact();
                        
                    }

                }
            }
            else if (storage != null)
            {
                if (inventoryManager.isStorageOpened) inventoryManager.CloseStorage(storage);
                else if (!inventoryManager.isStorageOpened) inventoryManager.OpenStorage(storage);
            }
        }
        // Se o botão foi solto
        else if (value.canceled)
        {
            // Se a ação foi cancelada, interrompa a coroutine
            if (interactionCoroutine != null)
            {
                StopCoroutine(interactionCoroutine);
                interactionCoroutine = null; // Limpa a referência à coroutine
                                             // Limpar preenchimento do círculo
                fillingCircle.fillAmount = 1f;
            }
        }
    }


    private IEnumerator InteractWithObject(Interactable interactable)
    {
        GameObject interactableObject = hitInfo.collider.gameObject;
        fillingCircle.fillAmount = 0f;
        if (interactable.InteractionTime > 0)
        {
            float elapsedTime = 0f;
            while (elapsedTime < interactable.InteractionTime)
            {
                float fillAmount = elapsedTime / interactable.InteractionTime;
                fillingCircle.fillAmount = fillAmount;
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            fillingCircle.fillAmount = 1f; // Garante que a imagem esteja preenchida completamente
        }

        if (interactableObject.GetComponent<ItemPickable>() != null)
        {
            inventoryManager.ItemPicked(interactableObject);
        }
        else
        {
            interactable.Interact();

        }
        interactionUI.gameObject.SetActive(false);
        fillingCircle.fillAmount = 1f;
    }

}
