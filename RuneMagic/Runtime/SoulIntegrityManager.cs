using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// Soul Integrity as a status, so the Player HUD shows it like the other statuses: a "Soul" gauge, only for rune users
    /// (for everyone else it is hidden and does nothing). The value is the <see cref="RuneSoul"/>'s; this status keeps no
    /// value of its own and never regenerates by itself.
    /// <list type="bullet">
    /// <item>Gains through the status API (<see cref="AddCurrentValue"/>, from other systems) are ordinary soul recovery;
    /// losses are ordinary soul damage.</item>
    /// <item>Increment Factor (percent) scales every soul recovery, Decrement Factor every soul damage: equipment and
    /// effects that change a status's factors work on the soul too.</item>
    /// <item>Respawning does not restore it: Soul Integrity is a long-term resource (design spec 14.4, 15.2).</item>
    /// </list>
    /// Added automatically with the Rune Soul.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SimpleMovements/Rune Magic/Soul Integrity Manager")]
    public class SoulIntegrityManager : StatusManager
    {
        /// <summary>The colour of the Soul gauge (HUD).</summary>
        public static readonly Color GaugeColor = new Color(0.45f, 0.86f, 0.82f);
        /// <summary>The label of the Soul gauge (HUD).</summary>
        public const string GaugeLabel = "Soul";

        private RuneSoul soul;
        private Image styled;

        private RuneSoul Soul => soul != null ? soul : soul = GetComponent<RuneSoul>();

        /// <summary>Does the HUD show the gauge (a rune user's soul)?</summary>
        public bool Shown => Soul != null && Soul.View.runeUser;

        private void Awake()
        {
            shouldConsume = false;
            isConsuming = false;
            if (maxValue <= 0f) maxValue = 100f;
            currentValue = maxValue;
        }

        private void Update()
        {
            bool show = Shown;
            if (uiImage != null)
            {
                if (uiImage.gameObject.activeSelf != show)
                    uiImage.gameObject.SetActive(show);
                if (show) UpdateBar();
                if (styled != uiImage)
                {
                    styled = uiImage;
                    StyleGauge();
                }
            }
        }

        /// <summary>The soul's values (set by the Rune Soul where the rules run).</summary>
        internal void Mirror(float current, float max)
        {
            maxValue = Mathf.Max(1f, max);
            currentValue = Mathf.Clamp(current, 0f, maxValue);
        }

        /// <summary>Ordinary soul recovery (positive) or ordinary soul damage (negative), through the soul.</summary>
        public override void AddCurrentValue(float amount)
        {
            if (IsReplica || Soul == null || float.IsNaN(amount))
                return;
            if (amount > 0f) Soul.Restore(amount, "status");
            else if (amount < 0f) Soul.Damage(-amount, "", "status");
        }

        /// <summary>Respawning does not restore the soul (a long-term resource).</summary>
        public override void ResetForRespawn(float fraction) { }

        protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
        protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);
        protected override void UpdateStatus() { }

        // The HUD makes a gauge named after this status ("SoulIntegrity"): it is given its label and colour.
        private void StyleGauge()
        {
            if (uiImage == null)
                return;
            // Once per gauge binding (works on every Unity version; prefabs and assets never show this status).
            foreach (HudNeedGauge g in Resources.FindObjectsOfTypeAll<HudNeedGauge>())
            {
                if (g == null || g.Status != this)
                    continue;
                g.color = GaugeColor;
                g.warnAt = 0.4f;
                if (g.label != null) g.label.text = GaugeLabel;
                if (g.ring != null) g.ring.color = GaugeColor;
                break;
            }
        }
    }
}
