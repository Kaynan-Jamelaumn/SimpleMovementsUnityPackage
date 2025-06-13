using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpecialMechanic
{
    [Header("Basic Info")]
    public string mechanicId;
    public string mechanicName = "Special Mechanic";
    [TextArea(2, 3)]
    public string mechanicDescription;

    [Header("Handler Configuration")]
    public SpecialMechanicHandlerBase handlerPrefab;
    public List<SpecialMechanicParameter> parameters = new List<SpecialMechanicParameter>();

    [Header("Activation Settings")]
    public bool activateOnEquip = true;
    public bool deactivateOnUnequip = true;
    public float cooldownDuration = 0f;
}

[System.Serializable]
public class SpecialMechanicParameter
{
    public string parameterName;
    public float value;
    public string description;
}
