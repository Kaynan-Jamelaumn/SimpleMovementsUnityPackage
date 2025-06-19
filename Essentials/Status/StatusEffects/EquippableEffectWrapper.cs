// EquippableEffect wrapper
public class EquippableEffectWrapper : IStatusEffect
{
    public EquippableEffect EquippableEffect { get; private set; }

    public EquippableEffectWrapper(EquippableEffect effect)
    {
        EquippableEffect = effect;
    }

    public string GetEffectName() => EquippableEffect.GetFormattedDescription();
    public float GetAmount() => EquippableEffect.amount;
    public float GetDuration() => EquippableEffect.duration;
    public float GetTickCooldown() => 0.5f;
    public bool IsProcedural() => false;
    public bool IsStackable() => EquippableEffect.canStack;
    public bool ShouldApply(int playerLevel = 1) => EquippableEffect.ShouldApply(playerLevel);
    public string GetEffectDescription() => EquippableEffect.GetFormattedDescription();
}