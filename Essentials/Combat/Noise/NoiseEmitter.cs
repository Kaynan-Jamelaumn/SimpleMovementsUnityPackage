using UnityEngine;

/// <summary>
/// A thing that makes noise mobs and players hear: a bell, an alarm, a creaking door, a lure, a bottle thrown to
/// distract a guard. Call <see cref="Emit()"/> (from a UnityEvent, an animation event, your own script), let it repeat,
/// or let it sound when it hits something hard enough (a thrown object with a Rigidbody).
/// <para>Mobs within the radius - times their hearing, less far through walls - come and look. With a
/// <see cref="blame"/> (who rang it, who threw it) the mobs that are its enemies grow suspicious of that character
/// instead, as for any noise it makes itself.</para>
/// </summary>
[AddComponentMenu("SimpleMovements/Combat/Noise Emitter")]
public class NoiseEmitter : MonoBehaviour
{
    [Tooltip("How far it is heard in the open by a listener with normal hearing (metres).")]
    [Min(0f)] public float radius = 15f;
    [Tooltip("How alarming it is: mobs walk to a quiet noise and run to a loud one, and a blamed character is noticed faster.")]
    [Range(0f, 1f)] public float intensity = 0.7f;
    [Tooltip("What kind of noise (the players' hearing may treat kinds differently).")]
    public NoiseKind kind = NoiseKind.Object;
    [Tooltip("Who made it (optional): mobs that are this character's enemies become aware of it. Empty = nobody: they come and look.")]
    public CombatEntity blame;

    [Header("When")]
    [Tooltip("Sound once when the object is switched on.")]
    public bool emitOnEnable;
    [Tooltip("Sound again every this many seconds while switched on (0 = only when asked).")]
    [Min(0f)] public float repeatEvery;
    [Tooltip("Sound when it hits something at this speed or faster (m/s; needs a Rigidbody and a collider). 0 = never. " +
             "A thrown bottle: 3.")]
    [Min(0f)] public float impactSpeed;

    private float nextRepeat;
    private float lastImpact = -10f;

    private void OnEnable()
    {
        nextRepeat = Time.time + repeatEvery;
        if (emitOnEnable)
            Emit();
    }

    private void Update()
    {
        if (repeatEvery <= 0f || Time.time < nextRepeat)
            return;
        nextRepeat = Time.time + repeatEvery;
        Emit();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactSpeed <= 0f || collision.relativeVelocity.magnitude < impactSpeed || Time.time - lastImpact < 0.2f)
            return;
        lastImpact = Time.time;
        Emit();
    }

    /// <summary>Makes the noise now, where the object is.</summary>
    public void Emit() => CombatEvents.EmitNoise(transform.position, radius, blame, intensity, kind);

    /// <summary>Makes the noise now, blamed on <paramref name="who"/> (the one who rang the bell, threw the bottle).</summary>
    public void Emit(CombatEntity who)
    {
        blame = who;
        Emit();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
