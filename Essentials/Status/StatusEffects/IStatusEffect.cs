
// Core interface for all status effects
public interface IStatusEffect
{
    string GetEffectName();
    float GetAmount();
    float GetDuration();
    float GetTickCooldown();
    bool IsProcedural();
    bool IsStackable();
    bool ShouldApply(int playerLevel = 1);
    string GetEffectDescription();
}
