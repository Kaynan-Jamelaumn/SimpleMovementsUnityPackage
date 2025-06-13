using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class SpecialMechanicRegistry
{
    [SerializeField] private Dictionary<string, System.Type> mechanicTypes = new Dictionary<string, System.Type>();
    [SerializeField] private Dictionary<string, Component> mechanicHandlers = new Dictionary<string, Component>();

    public void RegisterMechanic(string mechanicId, System.Type handlerType)
    {
        mechanicTypes[mechanicId] = handlerType;
    }

    public void RegisterMechanicHandler(string mechanicId, Component handler)
    {
        mechanicHandlers[mechanicId] = handler;
    }

    public Component GetMechanicHandler(string mechanicId)
    {
        return mechanicHandlers.TryGetValue(mechanicId, out Component handler) ? handler : null;
    }

    public bool HasHandler(string mechanicId)
    {
        return mechanicHandlers.ContainsKey(mechanicId);
    }
}
