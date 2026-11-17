using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>Healing herbs (gardens, greenhouses) and food (kitchens): heal the player who uses them, once.</summary>
    public class DungeonHerb : DungeonInteractable
    {
        [Tooltip("Share of max health restored.")]
        [Range(0f, 1f)] public float heal = 0.35f;
        [Tooltip("Shown when used.")]
        public string message = "The herbs soothe your wounds.";
        [Tooltip("What the prompt calls using it (\"Gather\", \"Eat\").")]
        public string verb = "Gather";

        public override string PromptVerb => string.IsNullOrWhiteSpace(verb) ? "Gather" : verb;
        protected override string UsedUpReason => "Nothing left";

        protected override bool CanUse(CombatEntity player, out string reason)
        {
            bool hurt = player.HealthRatio < 0.999f;
            reason = hurt ? "" : "You are at full health";
            return hurt;
        }

        protected override bool Use(CombatEntity player)
        {
            if (player.HealthRatio >= 0.999f)
                return false;   // keep it for when it's needed
            player.ApplyHeal(player.MaxHealth * heal);
            DungeonMessages.Show(this, message);
            return true;
        }

        protected override void OnUsedUp()
        {
            transform.localScale *= 0.4f;
            base.OnUsedUp();
        }
    }
}
