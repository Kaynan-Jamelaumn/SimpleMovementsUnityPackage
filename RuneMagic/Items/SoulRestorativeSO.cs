using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A Soul Healing Item (design spec 15.1): a consumable that restores a rune user's Soul Integrity, at once or over
    /// time, and can heal deep damage (specialized healing: what rest and meditation cannot reach). It is an ordinary
    /// consumable otherwise: it stacks, sits in quick slots, can be traded, crafted or brewed (craft it from rare materials:
    /// any item can be a recipe ingredient), and its Consumable Effects still apply to anyone.
    /// <para>Only rune users' souls are affected; others gain only the ordinary effects. Used where the rules run (offline,
    /// the server): the inventory sends the use there.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Soul Restorative", menuName = "SimpleMovements/Rune Magic/Soul Restorative", order = 20)]
    public class SoulRestorativeSO : ConsumableSO
    {
        [Header("Soul Integrity")]
        [Tooltip("Soul Integrity restored.")]
        [Min(0f)] public float restoreAmount = 15f;
        [Tooltip("Soul Integrity restored, as a percent of the maximum (added to Restore Amount).")]
        [Range(0f, 100f)] public float restorePercent = 0f;
        [Tooltip("Seconds over which it restores (0 = at once).")]
        [Min(0f)] public float overSeconds = 0f;
        [Tooltip("Deep damage removed (0 = an ordinary restorative that cannot reach deep damage).")]
        [Min(0f)] public float deepHealAmount = 0f;

        private void OnEnable()
        {
            // An asset made from code has no effects list yet: the consumable code expects one.
            if (Effects == null) SetEffects(null);
        }

        public override void UseItem(GameObject playerObject, PlayerStatusController statusController = null)
        {
            if (Effects == null) SetEffects(null);
            if (statusController != null)
                base.UseItem(playerObject, statusController); // ordinary consumable effects, sound and animation
            RuneSoul soul = playerObject != null ? RuneSoul.For(playerObject) : statusController != null ? RuneSoul.For(statusController) : null;
            if (soul != null)
                soul.UseRestorative(this);
        }

        public override void AppendTooltip(List<string> lines)
        {
            base.AppendTooltip(lines);
            if (lines == null) return;
            string over = overSeconds > 0f ? $" over {overSeconds:0.#} s" : "";
            if (restoreAmount > 0f || restorePercent > 0f)
            {
                string amount = restoreAmount > 0f && restorePercent > 0f ? $"{restoreAmount:0.#} + {restorePercent:0.#}%"
                    : restoreAmount > 0f ? $"{restoreAmount:0.#}" : $"{restorePercent:0.#}%";
                lines.Add($"Soul Integrity +{amount}{over} (rune users)");
            }
            if (deepHealAmount > 0f)
                lines.Add($"Heals deep soul damage {deepHealAmount:0.#}{over}");
        }

        public override void ValidateItem(List<string> errors, List<string> warnings)
        {
            base.ValidateItem(errors, warnings);
            if (restoreAmount <= 0f && restorePercent <= 0f && deepHealAmount <= 0f)
                warnings?.Add("The soul restorative restores nothing (Restore Amount, Restore Percent and Deep Heal Amount are 0).");
        }
    }
}
