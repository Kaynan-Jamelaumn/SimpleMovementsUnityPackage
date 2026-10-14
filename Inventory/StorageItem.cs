using System.Collections.Generic;
using UnityEngine;

/// <summary>One slot of a <see cref="Storage"/>: an item, how many, and what the items had when they were stored.</summary>
[System.Serializable]
public class StorageItem
{
    public int currentStack;
    public ItemSO itemScriptableObject;
    [Tooltip("Uses left of each unit after the first (empty = fresh items).")]
    public List<int> durabilities = new List<int>();
    [Tooltip("Uses left of the unit in use (0 or less = fresh).")]
    public float durability;
    [Tooltip("Rounds loaded in a magazine weapon (-1 = not loaded yet).")]
    public int loadedRounds = -1;

    public StorageItem(int _currentStack, ItemSO _itemScriptableObject)
    {
        currentStack = _currentStack;
        itemScriptableObject = _itemScriptableObject;
    }
}
