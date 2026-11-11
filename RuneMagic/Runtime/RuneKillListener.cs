using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Rune drops and magical energy from kills (design spec 3.1, 5.1, directive 4), where the rules run (offline, the
    /// server): when a player (or their summon) defeats a monster, its drop profile rolls runes, which drop as rune items
    /// anyone can pick up, and the killer - if a rune user - gains magical energy to give to a rune of their choice (decision
    /// D5). Listens to <see cref="CombatEvents.Killed"/> like item drops and essence absorption, and changes neither: loot,
    /// experience and essences are rolled as before. Summons and players carry no runes.
    /// </summary>
    public static class RuneKillListener
    {
        /// <summary>A kill was rolled: victim, the player responsible, the runes dropped, the energy gained.</summary>
        public static event Action<CombatEntity, CombatEntity, IReadOnlyList<RuneDrop>, float> Rolled;

        /// <summary>The random generator of the drops (tests may set a seeded one).</summary>
        public static System.Random Random { get; set; } = new System.Random();

        /// <summary>Log every roll to the Console.</summary>
        public static bool LogRolls { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Rolled = null;
            Random = new System.Random();
            CombatEvents.Killed -= OnKilled;
            CombatEvents.Killed += OnKilled;
        }

        private static void OnKilled(CombatEntity victim, CombatEntity killer)
        {
            // Network sessions: rolled once, by the server.
            if (GameNet.NetAuthority.IsClientOnly || victim == null)
                return;
            RuneCatalog c = RuneMagicDatabase.Instance.Catalog;
            if (!c.Settings.enabled || victim.Kind == CombatEntity.EntityKind.Player || victim.Summoner != null)
                return;
            CombatEntity player = AbilityAbsorption.PlayerSide(killer);
            if (player == null)
                return; // only player kills give runes and energy
            RuneSource source = RuneSource.Of(victim);
            if (source != null && source.NoRunes)
                return;
            RuneDropContext ctx = ContextOf(victim, player, source);
            bool runeUser = RuneMagicGate.IsRuneUser(player);

            float energy = 0f;
            if (runeUser)
            {
                RuneSoul soul = RuneSoul.For(player);
                energy = DropRules.EnergyReward(c, ctx);
                if (soul != null && energy > 0f)
                    energy = soul.AddPendingEnergy(energy, NameOf(victim));
            }

            var drops = new List<RuneDrop>();
            RuneAcquisitionBalance a = c.Balance.acquisition;
            if (runeUser || (a != null && a.dropKillers == RuneDropKillers.AnyPlayer))
            {
                drops.AddRange(DropRules.Roll(c, ctx, Random ?? new System.Random()));
                if (source != null)
                    foreach (RuneDropEntry g in source.Guaranteed)
                    {
                        RuneDef def = g != null ? c.Rune(g.runeId) : null;
                        if (def == null) continue;
                        RuneDropProfile p = DropRules.SelectProfile(c, ctx);
                        var rng = Random ?? new System.Random();
                        int rank = DropRules.RollRank(a, p, g, def, ctx, rng);
                        float quality = DropRules.RollQuality(a, p, ctx, rng);
                        drops.Add(new RuneDrop { runeId = def.id, rank = rank, quality = quality, energy = 0f });
                    }
                Vector3 at = victim.Position;
                for (int i = 0; i < drops.Count; i++)
                {
                    float angle = i * 2.39996f; // golden angle: drops do not land on each other
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.5f + 0.2f * i) + Vector3.up * 0.6f;
                    RuneItems.Spawn(drops[i], at + offset, victim.gameObject);
                }
            }
            if (LogRolls)
                Debug.Log($"[Rune Magic] {NameOf(victim)} (tier {ctx.tier}, {ctx.rarity}) killed by {player.name}: " +
                          $"{(drops.Count == 0 ? "no rune" : string.Join(", ", drops))}; energy {energy:0}.", victim);
            Rolled?.Invoke(victim, player, drops, energy);
        }

        /// <summary>What a defeated monster tells about its runes: species, name, faction, Rune Source data, tier, rarity, zone.</summary>
        public static RuneDropContext ContextOf(CombatEntity victim, CombatEntity player, RuneSource source = null)
        {
            Mob mob = victim != null ? victim.GetComponentInParent<Mob>() : null;
            if (mob == null && victim != null) mob = victim.GetComponentInChildren<Mob>();
            ItemDropOnDeath.Describe(victim, out int tier, out bool elite, out bool boss);
            RuneDropContext ctx = RuneDropContext.For(mob != null ? mob.type : "", tier);
            ctx.displayName = mob != null ? mob.DisplayName : victim != null ? victim.name : "";
            ctx.faction = mob != null && mob.Faction != null ? mob.Faction.name : "";
            ctx.rarity = boss ? MonsterRarity.Boss : elite ? MonsterRarity.Elite : MonsterRarity.Normal;
            if (source != null)
            {
                ctx.profileId = source.Profile ?? "";
                ctx.alignments = source.Alignments;
                ctx.tags = source.Tags;
                ctx.monsterRank = source.MonsterRank;
                if (source.Rarity != MonsterRarity.Auto) ctx.rarity = source.Rarity;
                if (source.Tier >= 0) ctx.tier = source.Tier;
                ctx.chanceMultiplier = source.ChanceMultiplier;
                ctx.energyMultiplier = source.EnergyMultiplier;
            }
            HubWorld.PlayerRealmState realm = HubWorld.PlayerRealmState.Of(player);
            ctx.zoneRewardMultiplier = realm != null && realm.Zone.rewardMultiplier > 0f ? realm.Zone.rewardMultiplier : 1f;
            return ctx;
        }

        private static string NameOf(CombatEntity e)
        {
            if (e == null) return "";
            Mob mob = e.GetComponentInParent<Mob>();
            return mob != null ? mob.DisplayName : e.name.Replace("(Clone)", "").Trim();
        }
    }
}
