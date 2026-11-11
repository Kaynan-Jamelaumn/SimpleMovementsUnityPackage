using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Specialized abilities and traits that improve the soul (design spec 15.1, 4.1): while this component is enabled on
    /// the player (or on an object under it: a passive skill, an equipped item's model, a buff object) it changes the
    /// rune user's soul recovery, Soul Rune Capacity and maximum Soul Integrity. Disable or remove it and the change goes.
    /// <para>Code can do the same without a component: <see cref="RuneSoul.SetRecoveryModifier"/>,
    /// <see cref="RuneSoul.SetCapacityModifier"/>, <see cref="RuneSoul.SetMaxIntegrityModifier"/> (while it lasts) or
    /// <see cref="RuneSoul.SetCapacityBonus"/> / <see cref="RuneSoul.SetMaxIntegrityBonus"/> (lasting, saved).</para>
    /// </summary>
    [AddComponentMenu("SimpleMovements/Rune Magic/Soul Attunement")]
    public class SoulAttunement : MonoBehaviour
    {
        [Tooltip("Shown in tools.")]
        [SerializeField] private string label = "Soul Attunement";
        [Tooltip("Every soul recovery is multiplied by this (1 = no change, 1.5 = 50% faster).")]
        [SerializeField, Min(0f)] private float recoveryMultiplier = 1.5f;
        [Tooltip("Soul Integrity recovered per minute in addition (even without resting or meditating).")]
        [SerializeField, Min(0f)] private float recoveryPerMinute = 0f;
        [Tooltip("Extra soul rune capacity (slots).")]
        [SerializeField] private int capacitySlots = 0;
        [Tooltip("Extra maximum Soul Integrity.")]
        [SerializeField] private float maxIntegrity = 0f;

        private RuneSoul soul;
        private float nextLook;

        public string Label => label;

        private void OnEnable()
        {
            nextLook = 0f;
            Apply();
        }

        private void OnDisable() => Remove();

        private void OnValidate()
        {
            if (isActiveAndEnabled && soul != null) Apply();
        }

        // The soul may come later than this component (added when the game starts): looked for once a second.
        private void Update()
        {
            if (soul != null || Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + 1f;
            Apply();
        }

        /// <summary>Changes the values while enabled.</summary>
        public void Set(float multiplier, float perMinute, int slots, float max)
        {
            recoveryMultiplier = Mathf.Max(0f, multiplier);
            recoveryPerMinute = Mathf.Max(0f, perMinute);
            capacitySlots = slots;
            maxIntegrity = max;
            if (isActiveAndEnabled) Apply();
        }

        private void Apply()
        {
            if (soul == null) soul = RuneSoul.For(this);
            if (soul == null) return;
            soul.SetRecoveryModifier(this, recoveryMultiplier, recoveryPerMinute);
            soul.SetCapacityModifier(this, capacitySlots);
            soul.SetMaxIntegrityModifier(this, maxIntegrity);
        }

        private void Remove()
        {
            if (soul == null) return;
            soul.ClearRecoveryModifier(this);
            soul.ClearCapacityModifier(this);
            soul.ClearMaxIntegrityModifier(this);
        }
    }
}
