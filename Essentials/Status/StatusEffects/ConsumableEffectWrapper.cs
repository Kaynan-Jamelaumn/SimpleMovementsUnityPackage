// ConsumableEffect wrapper
public class ConsumableEffectWrapper : IStatusEffect
{
    public ConsumableEffect ConsumableEffect { get; private set; }

    public ConsumableEffectWrapper(ConsumableEffect effect)
    {
        ConsumableEffect = effect;
    }

    public string GetEffectName() => ConsumableEffect.effectName;
    public float GetAmount() => ConsumableEffect.GetRandomizedAmount();
    public float GetDuration() => ConsumableEffect.GetRandomizedDuration();
    public float GetTickCooldown() => ConsumableEffect.GetRandomizedTickCooldown();
    public bool IsProcedural() => ConsumableEffect.isProcedural;
    public bool IsStackable() => ConsumableEffect.isStackable;
    public bool ShouldApply(int playerLevel = 1) => ConsumableEffect.ShouldApplyEffect(playerLevel);
    public string GetEffectDescription() => $"{GetAmount()} {ConsumableEffect.effectType}";
}