using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A rest fountain: heals every living player within Radius, brings fallen party members back, and becomes the
    /// party's checkpoint (<see cref="DungeonSession.Checkpoint"/> - your respawn code can send players there).
    /// </summary>
    public class DungeonRestPoint : DungeonInteractable
    {
        [Tooltip("Players within this distance are healed (meters).")]
        [Min(0f)] public float radius = 8f;
        [Tooltip("Share of max health restored (1 = full).")]
        [Range(0f, 1f)] public float heal = 1f;
        [Tooltip("Revive dead players of the dungeon's party.")]
        public bool reviveFallen = true;

        public static event Action<DungeonRestPoint> AnyUsed;

        private void Reset()
        {
            uses = 0;
            cooldown = 60f;
        }

        protected override bool Use(CombatEntity player)
        {
            int revived = 0;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null)
                    continue;
                if (p.IsDead)
                {
                    if (reviveFallen && DungeonSession.IsParticipant(PlayerLocator.MovableRoot(p)))
                    {
                        p.Revive();
                        revived++;
                    }
                    continue;
                }
                if ((p.transform.position - transform.position).sqrMagnitude > radius * radius)
                    continue;
                p.ApplyHeal(p.MaxHealth * heal);
            }
            DungeonSession.SetCheckpoint(new Pose(transform.position + transform.forward * 1.5f, transform.rotation));
            DungeonMessages.Show(revived > 0 ? "You rest. Wounds close, and the fallen rise again." : "You rest at the fountain. Health restored - checkpoint set.", true);
            AnyUsed?.Invoke(this);
            return true;   // resting always sets the checkpoint, even at full health
        }
    }
}
