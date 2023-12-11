using UnityEditor.PackageManager;
using UnityEngine;
using static UnityEditor.Progress;

public class Player : MonoBehaviour
{
    [SerializeField] InventoryManager inventoryManager;
    //[SerializeField] PlayerCameraModel camera;
    [SerializeField] Camera cam;
    [SerializeField] TMPro.TextMeshProUGUI interactionText; // Reference to TextMeshPro component

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo, 3.2f))
        {
            itemPickable item = hitInfo.collider.gameObject.GetComponent<itemPickable>();
            Storage storage = hitInfo.collider.gameObject.GetComponent<Storage>();

            if (item != null || hitInfo.collider.gameObject.GetComponentInParent<itemPickable>() || storage)
            {
                // Display TextMeshPro text
                interactionText.gameObject.SetActive(true);
            }
            else
            {
                // Hide TextMeshPro text
                interactionText.gameObject.SetActive(false);
            }
            if (Input.GetKey(KeyCode.E))
            {
                if (item != null || hitInfo.collider.gameObject.GetComponentInParent<itemPickable>())
                {
                    inventoryManager.ItemPicked(hitInfo.collider.gameObject);
                }
                else if (storage != null)
                {
                    if (Input.GetKeyDown(KeyCode.E) && inventoryManager.isStorageOpened)
                    {
                        inventoryManager.CloseStorage(storage);
                    }
                    else if (Input.GetKeyDown(KeyCode.E) && !inventoryManager.isStorageOpened)
                    {
                        inventoryManager.OpenStorage(storage);
                    }
                }
            }
        }



    }
}
