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




    public void SetPlayerClassItems(string className)
    {
       // ClassItem chosenClass = classItemsList.Find(x => x.className.ToLower() == className.ToLower());
         foreach (ClassItem classItem in classItemsList)
            if (classItem.className.ToLower() == className.ToLower()) inventoryManager.InstantiateClassItems(classItem.ItemList);

         Debug.Log(className);
    }

}
