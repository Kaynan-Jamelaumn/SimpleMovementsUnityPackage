// Interface for components that can handle special mechanics
using System.Collections.Generic;

public interface ISpecialMechanicHandler
{
    bool CanHandleMechanic(string mechanicId);
    void ApplyMechanic(SpecialMechanic mechanic, bool enable);
    void UpdateMechanic(SpecialMechanic mechanic, Dictionary<string, float> parameters);
    List<string> GetSupportedMechanics();
}
