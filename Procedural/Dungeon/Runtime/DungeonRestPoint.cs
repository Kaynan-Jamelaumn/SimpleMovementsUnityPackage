using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A rest fountain: heals every living player within Radius, brings fallen party members back, and becomes the
    /// party's checkpoint (<see cref="DungeonRun.Checkpoint"/> of the dungeon's run - your respawn code can send players there).
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
            DungeonRun run = DungeonSession.RunAt(this) ?? DungeonSession.RunOf(player != null ? player.gameObject : null);
            int revived = 0;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null)
                    continue;
                if (p.IsDead)
                {
                    if (reviveFallen && (run != null ? run.IsParticipant(PlayerLocator.MovableRoot(p)) : DungeonSession.IsParticipant(PlayerLocator.MovableRoot(p))))
                    {
                        // In a network session with health: Revive() alone keeps the 0 the player died with, and the
                        // server's health is what clients get replicated. Single player keeps its old behaviour.
                        if (GameNet.NetAuthority.IsNetworked)
                            p.Revive(Mathf.Max(0.1f, heal));
                        else
                            p.Revive();
                        revived++;
                    }
                    continue;
                }
                if ((p.transform.position - transform.position).sqrMagnitude > radius * radius)
                    continue;
                p.ApplyHeal(p.MaxHealth * heal);
            }
            var checkpoint = new Pose(transform.position + transform.forward * 1.5f, transform.rotation);
            if (run != null)
                run.SetCheckpoint(checkpoint);
            else
                DungeonSession.SetCheckpoint(checkpoint);
            DungeonMessages.Show(this, revived > 0 ? "You rest. Wounds close, and the fallen rise again." : "You rest at the fountain. Health restored - checkpoint set.", true);
            AnyUsed?.Invoke(this);
            return true;   // resting always sets the checkpoint, even at full health
        }
    }
}
