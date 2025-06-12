using System.Collections.Generic;

// Interface for components that can handle special mechanics
public interface ISpecialMechanicHandler
{
    string MechanicId { get; }
    bool CanHandleMechanic(string mechanicId);
    void ApplyMechanic(SpecialMechanic mechanic, bool enable);
    void UpdateMechanic(SpecialMechanic mechanic, Dictionary<string, float> parameters);
    List<string> GetSupportedMechanics();
}

// Interface for effect applicators
public interface IEffectApplicator
{
    EquippableEffectType EffectType { get; }
    void ApplyEffect(float amount, PlayerStatusController statusController);
    void RemoveEffect(float amount, PlayerStatusController statusController);
}

// Interface for trait effect handlers
public interface ITraitEffectHandler
{
    string TargetStat { get; }
    void ApplyEffect(TraitEffect effect, PlayerStatusController statusController);
    void RemoveEffect(TraitEffect effect, PlayerStatusController statusController);
}