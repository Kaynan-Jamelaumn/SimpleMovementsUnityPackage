// AttackEffect wrapper
public class AttackEffectWrapper : IStatusEffect
{
    public AttackEffect AttackEffect { get; private set; }

    public AttackEffectWrapper(AttackEffect effect)
    {
        AttackEffect = effect;
    }

    public string GetEffectName() => AttackEffect.effectName;
    public float GetAmount() => AttackEffect.randomAmount ? UnityEngine.Random.Range(AttackEffect.minAmount, AttackEffect.maxAmount) : AttackEffect.amount;
    public float GetDuration() => AttackEffect.randomTimeBuffEffect ? UnityEngine.Random.Range(AttackEffect.minTimeBuffEffect, AttackEffect.maxTimeBuffEffect) : AttackEffect.timeBuffEffect;
    public float GetTickCooldown() => AttackEffect.randomTickCooldown ? UnityEngine.Random.Range(AttackEffect.minTickCooldown, AttackEffect.maxTickCooldown) : AttackEffect.tickCooldown;
    public bool IsProcedural() => AttackEffect.isProcedural;
    public bool IsStackable() => AttackEffect.isStackable;
    public bool ShouldApply(int playerLevel = 1) => UnityEngine.Random.value <= AttackEffect.probabilityToApply;
    public string GetEffectDescription() => $"{GetAmount()} {AttackEffect.effectType}";
}
