using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PlayerStartItemController : MonoBehaviour
{


    [System.Serializable]public class ClassItem
    {
        public string className;
        public List<GameObject> ItemList = new List<GameObject>();
        
    }
    [SerializeField] private List<ClassItem> classItemsList;
    [SerializeField] private InventoryManager inventoryManager;

    private void Awake() 
    {
        inventoryManager = this.CheckComponent(inventoryManager, nameof(inventoryManager));
    }




    /// <summary>
    /// Gives the starting items of <paramref name="className"/>. Only where the rules run (offline, the server): a network
    /// client receives the items with its inventory snapshot, so calling it there does nothing.
    /// </summary>
    public void SetPlayerClassItems(string className)
    {
        if (string.IsNullOrEmpty(className) || inventoryManager == null || classItemsList == null)
            return;
        if (!GameNet.NetAuthority.RunsRules(gameObject) || !inventoryManager.IsAuthoritative)
            return;
        foreach (ClassItem classItem in classItemsList)
            if (classItem != null && string.Equals(classItem.className, className, StringComparison.OrdinalIgnoreCase))
                inventoryManager.InstantiateClassItems(classItem.ItemList);

        Debug.Log(className);
    }

}
