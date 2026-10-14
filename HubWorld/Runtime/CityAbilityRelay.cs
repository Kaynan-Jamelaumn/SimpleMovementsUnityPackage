using System;
using System.Collections.Generic;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// The server side of abilities in the city. Where abilities are only shown, the <see cref="AbilityCaster"/> has
    /// already checked that the player owns the ability and that it is ready, put it on cooldown and played its visuals;
    /// nothing was hit. This finds the players close enough to see it (same realm, within <see cref="relayRadius"/>) and
    /// raises <see cref="Relayed"/>.
    /// </summary>
    /// <remarks>
    /// In a network session the cast runs on the server (the abilities module validates it there) and the abilities
    /// module's own cast messages already show it on the observers' clients, so this relay never plays anything itself:
    /// it only runs where the rules run (the server, or single player) and <see cref="Relayed"/> is a server-side
    /// notification. Subscribers must not replay the visuals while <c>GameNet.NetAuthority.IsNetworked</c>, or observers
    /// would see the cast twice.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CityAbilityRelay : MonoBehaviour
    {
        [Tooltip("Players within this distance of the caster are told about a visual-only cast (metres).")]
        [Min(1f)] public float relayRadius = 60f;

        /// <summary>A visual-only cast and the players who should see it (the caster included).</summary>
        public static event Action<AbilityCastInstance, IReadOnlyList<CombatEntity>> Relayed;

        private readonly List<CombatEntity> observers = new List<CombatEntity>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Relayed = null;

        private void OnEnable() => CombatRules.VisualCast += OnVisualCast;

        private void OnDisable() => CombatRules.VisualCast -= OnVisualCast;

        private void OnVisualCast(AbilityCastInstance cast)
        {
            CombatEntity caster = cast != null ? cast.CasterEntity : null;
            if (caster == null)
                return;
            // A client's copy of a cast (its prediction or a mirror of the server's) is never relayed again.
            if (!GameNet.NetAuthority.RunsRules(caster.gameObject))
                return;
            PlayerRealmState from = PlayerRealmState.Of(CombatRules.ControllingPlayer(caster));
            observers.Clear();
            float sqr = relayRadius * relayRadius;
            IReadOnlyList<CombatEntity> all = CombatEntity.Players;
            for (int i = 0; i < all.Count; i++)
            {
                CombatEntity p = all[i];
                if (p == null)
                    continue;
                PlayerRealmState s = PlayerRealmState.Of(p);
                if (from != null && s != null && s.Realm != from.Realm)
                    continue;
                if (CombatRules.AreSeparated(caster, p))
                    continue;
                if ((p.Position - caster.Position).sqrMagnitude <= sqr)
                    observers.Add(p);
            }
            Relayed?.Invoke(cast, observers);
        }
    }
}
