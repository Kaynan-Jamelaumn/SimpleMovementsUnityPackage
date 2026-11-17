using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A gambling altar. A blood altar takes a share of the player's health (never their last hit point); a cursed
    /// altar lays a curse on them (a timed weakness through <see cref="TimedStatModifiers"/>). Either way it then reveals
    /// its reward: the hidden loot beside it (the placements whose <see cref="DungeonSpawned.link"/> is the altar's).
    /// Once.
    /// </summary>
    public class DungeonGamblingAltar : DungeonInteractable
    {
        public enum Price { Blood, Curse }

        [Serializable]
        public class Curse
        {
            public string name = "Frailty";
            [Tooltip("Shown to the player.")]
            public string description = "-15 Defense and Magic Resistance";
            public List<CombatStatModifier> stats = new List<CombatStatModifier>();
        }

        [Tooltip("Blood: pay with health. Curse: take a curse.")]
        public Price price = Price.Blood;
        [Tooltip("Blood: share of max health taken.")]
        [Range(0.05f, 0.9f)] public float bloodCost = 0.3f;
        [Tooltip("Curse: how long it lasts (seconds).")]
        [Min(1f)] public float curseDuration = 240f;
        [Tooltip("Curse: one is picked at random. Empty = Frailty, Sluggishness, Clumsiness, Dimwit.")]
        public List<Curse> curses = new List<Curse>();

        public static event Action<DungeonGamblingAltar, CombatEntity> AnyPaid;

        /// <summary>The price is in the prompt, and the Interact input is held a moment: nobody pays by a slip of the finger.</summary>
        public override string PromptVerb => price == Price.Blood ? $"Offer blood ({Mathf.RoundToInt(bloodCost * 100f)}% health)" : "Accept a curse";
        public override float PromptHold => Mathf.Max(interactHold, 0.8f);
        protected override string UsedUpReason => "The altar is spent";

        private float rewardCheckedAt = -10f;
        private bool rewardWaiting;

        protected override bool CanUse(CombatEntity player, out string reason)
        {
            reason = "";
            // (Looked up a few times a second, not every frame the prompt asks.)
            if (Time.time - rewardCheckedAt > 0.5f)
            {
                rewardCheckedAt = Time.time;
                rewardWaiting = HasReward();
            }
            if (!rewardWaiting)
            {
                reason = "It has nothing to offer";
                return false;
            }
            if (price == Price.Blood && (player.Health == null || player.Health.CurrentValue <= player.MaxHealth * bloodCost + 1f))
            {
                reason = "Not enough health";
                return false;
            }
            return true;
        }

        protected override bool Use(CombatEntity player)
        {
            // An altar with nothing to give takes nothing.
            if (!HasReward())
            {
                DungeonMessages.Show(this, "The altar is silent. It has nothing to offer.");
                return false;
            }
            if (price == Price.Blood)
            {
                float cost = player.MaxHealth * bloodCost;
                if (player.Health == null || player.Health.CurrentValue <= cost + 1f)
                {
                    DungeonMessages.Show(this, "The altar thirsts for more blood than you have to give.");
                    return false;
                }
                player.ApplyDamage(new DamageInfo { amount = cost, target = player, point = transform.position, direction = Vector3.down, type = DamageType.True });
                DungeonMessages.Show(this, "The altar drinks your blood... and offers a gift.", true);
            }
            else
            {
                if (curses == null || curses.Count == 0)
                    curses = Defaults();
                Curse c = curses[UnityEngine.Random.Range(0, curses.Count)];
                TimedStatModifiers.For(player).Apply(this, "Curse of " + c.name, null, false, ElementType.Dark, curseDuration, 1f, c.stats, null, null);
                DungeonMessages.Show(this, $"Curse of {c.name}: {c.description} for {curseDuration / 60f:0.#} minutes. The altar offers a gift.", true);
            }
            RevealReward();
            AnyPaid?.Invoke(this, player);
            return true;
        }

        /// <summary>
        /// Is its hidden reward still waiting beside it (the loot with the altar's link)? An altar that was placed by hand
        /// (not generated: no <see cref="DungeonSpawned"/>) always counts - whoever placed it listens to
        /// <see cref="AnyPaid"/> - and so does a client's copy (the server knows).
        /// </summary>
        public bool HasReward()
        {
            var tag = GetComponent<DungeonSpawned>();
            if (tag == null || DungeonNet.IsReplica(this))
                return true;
            DungeonInstance d = Dungeon;
            // Link 0 = generation gave it no reward (0 is also the link of a room's ordinary hidden loot: not the altar's).
            if (d == null || tag.link == 0)
                return false;
            foreach (DungeonSpawned s in d.SpawnedIn(tag.floor, tag.area))
                if (s != tag && s.dormant && s.link == tag.link && s.kind == PlacementKind.Loot)
                    return true;
            return false;
        }

        private void RevealReward()
        {
            var tag = GetComponent<DungeonSpawned>();
            DungeonInstance d = Dungeon;
            if (tag == null || d == null || tag.link == 0)
                return;
            foreach (DungeonSpawned s in d.SpawnedIn(tag.floor, tag.area))
                if (s != tag && s.dormant && s.link == tag.link && s.kind == PlacementKind.Loot)
                    s.Reveal();
        }

        protected override void OnUsedUp()
        {
            base.OnUsedUp();
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial != null && r.sharedMaterial.IsKeywordEnabled("_EMISSION"))
                    r.enabled = false;   // its glow goes out
        }

        private static List<Curse> Defaults()
        {
            Curse Make(string name, string description, params (CombatStatType stat, float value)[] mods)
            {
                var c = new Curse { name = name, description = description };
                foreach (var m in mods)
                    c.stats.Add(new CombatStatModifier { stat = m.stat, value = m.value });
                return c;
            }
            return new List<Curse>
            {
                Make("Frailty", "-15 Defense and Magic Resistance", (CombatStatType.Defense, -15f), (CombatStatType.MagicResistance, -15f)),
                Make("Sluggishness", "-15% attack and casting speed", (CombatStatType.AttackSpeed, -15f), (CombatStatType.CastingSpeed, -15f)),
                Make("Clumsiness", "-10% critical chance, -3 Strength", (CombatStatType.CriticalChance, -10f), (CombatStatType.Strength, -3f)),
                Make("Dimwit", "-4 Intelligence, -10% weapon damage", (CombatStatType.Intelligence, -4f), (CombatStatType.WeaponDamage, -10f)),
            };
        }
    }
}
