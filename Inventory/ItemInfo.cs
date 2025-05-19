using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using Unity.VisualScripting.Antlr3.Runtime.Misc;

public class ItemInfo : MonoBehaviour
{
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private InventoryItem clickedItem;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI itemPrice;
    [SerializeField] private TextMeshProUGUI itemWeight;
    [SerializeField] private TextMeshProUGUI itemType;
    [SerializeField] private TextMeshProUGUI itemDescription;
    [SerializeField] private TextMeshProUGUI itemQuantity;
    // Start is called before the first frame update

    private void SetItemInfoText(string itemNameText, string itemPriceText, string itemWeightText, string itemQuantityText, string itemTypeText, string itemDescriptionText)
    {
        itemName.text = itemNameText;
        itemPrice.text = itemPriceText;
        itemWeight.text = itemWeightText;
        itemQuantity.text = itemQuantityText;
        itemType.text = itemTypeText;
        itemDescription.text = itemDescriptionText;
    }
    private void PositionRelativeToMouse(Vector2 mousePosition)
    {
        RectTransform itemInfoRectTransform = GetComponent<RectTransform>();

        // Retrieve canvas and itemInfo dimensions
        RectTransform canvasRectTransform = itemInfoRectTransform.root.GetComponent<RectTransform>();
        float canvasHeight = canvasRectTransform.rect.height;
        float canvasWidth = canvasRectTransform.rect.width;
        float imageHeight = itemInfoRectTransform.rect.height;
        float imageWidth = itemInfoRectTransform.rect.width;

        // Calculate new position based on mouse position
        Vector2 newPosition;

        // Check if the lower part of the itemInfo goes beyond the canvas
        if (mousePosition.y - imageHeight * 0.5f < 0)
        {
            newPosition = new Vector2(mousePosition.x + imageWidth, imageHeight * 0.5f);
        }
        else
        {
            newPosition = new Vector2(
                Mathf.Min(mousePosition.x + imageWidth, canvasWidth - imageWidth * 0.5f),
                Mathf.Max(mousePosition.y - imageHeight * 0.5f, imageHeight * 0.5f));
        }
        // Set the itemInfo position
        itemInfoRectTransform.position = newPosition;
    }

    public void SplitItem()
    {
        //inventoryManager.SplitItemIntoNewStack(clickedItem);
        Debug.Log("split");
        SplitItemHandler.SplitItemIntoNewStack(inventoryManager, clickedItem, inventoryManager.Slots, inventoryManager.Player);
    }

    public void ShowItemInfo(InventoryItem itemToShow)
    {
        transform.gameObject.SetActive(true);
        clickedItem = itemToShow;
        SetItemInfoText(
            $"{clickedItem.itemScriptableObject.Name}",
            $"Price: {clickedItem.itemScriptableObject.Price}",
            $"Weight: {clickedItem.totalWeight} Kg",
            $"Quantity: {clickedItem.stackCurrent}/{clickedItem.stackMax}",
            $"{clickedItem.itemScriptableObject.ItemType}",
            $"{clickedItem.itemScriptableObject.Description}");

        // Get mouse position relative to the screen
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // Position itemInfo relative to the mouse cursor
        PositionRelativeToMouse(mousePosition);
    }

}

