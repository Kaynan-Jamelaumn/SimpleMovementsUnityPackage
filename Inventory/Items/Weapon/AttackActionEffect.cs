/// <summary>
/// A classic status effect of a weapon attack (Hp, Speed, Stamina...). Same as <see cref="AttackEffect"/>; its drawer
/// hides the Attack Cast list, which weapon attacks do not use (they have their own hit detection).
/// </summary>
[System.Serializable]
public class AttackActionEffect : AttackEffect
{
}
