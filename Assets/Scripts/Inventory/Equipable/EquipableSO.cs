using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Equipable", menuName = "Scriptable Objects/Equipable")]
public class EquipableSO : ItemSO
{
    public int armor;
    public int requiredLevel;
}