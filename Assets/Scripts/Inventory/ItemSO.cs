using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class ItemSO : ScriptableObject
{
    public string name;
    public Sprite icon;
    public GameObject prefab;
    public int stackMax;

    [Header("Item Hand Postion")]
    [Header("Position")]
    public Vector3 position;

    [Header("Rotation")]
    public Quaternion rotation;

    [Header("Scale")]
    public Vector3 scale;

}
