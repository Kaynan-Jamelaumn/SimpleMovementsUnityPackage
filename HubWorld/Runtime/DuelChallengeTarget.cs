using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// "Challenge to a duel" on a player, for the other players' <see cref="PlayerInteractor"/>. Added to every player by
    /// the <see cref="DuelSystem"/>.
    /// </summary>
    /// <remarks>
    /// In a network session the economy module's interactor relays the interaction to the server, where
    /// <see cref="Interact"/> runs with the asking player's interactor and this player as the target; on a client the duel
    /// system sends the challenge to the server itself. Both paths may arrive: a repeated identical challenge is ignored.
    /// </remarks>
    [DisallowMultipleComponent]
    public class DuelChallengeTarget : MonoBehaviour, IInteractable
    {
        private CombatEntity owner;

        internal void Bind(CombatEntity player) => owner = player;

        private CombatEntity Owner => owner != null ? owner : (owner = PlayerLocator.FromObject(gameObject));

        public string InteractionName => DuelSystem.DisplayName(Owner);
        public string InteractionVerb => "Challenge to a duel";
        public Transform InteractionPoint => transform;
        public float InteractionRange => HubWorldSettings.Instance.duels.challengeRange;
        public int InteractionPriority => -10; // NPCs and doors come first
        public float HoldDuration => 0f;
        public bool AllowsClick => true;

        public bool CanInteract(PlayerInteractor interactor, out string reason)
        {
            reason = "";
            DuelSystem duels = DuelSystem.Instance;
            CombatEntity me = interactor != null ? PlayerLocator.FromObject(interactor.PlayerObject) : null;
            if (duels == null || me == null)
                return false;
            return duels.CanChallenge(me, Owner, out reason);
        }

        public void Interact(PlayerInteractor interactor)
        {
            DuelSystem duels = DuelSystem.Instance;
            CombatEntity me = interactor != null ? PlayerLocator.FromObject(interactor.PlayerObject) : null;
            if (duels == null || me == null)
                return;
            // Already challenged them (the same request relayed twice over the network): nothing to add.
            Duel open = duels.DuelOf(me);
            if (open != null && open.State == DuelState.Challenged && open.Challenger == PlayerIds.Of(me) && open.Challenged == PlayerIds.Of(Owner))
                return;
            if (!duels.Challenge(me, Owner, out string reason))
                HubWorldMessages.Send(me, reason, HubMessageKind.Warning);
        }

        public void OnFocusChanged(PlayerInteractor interactor, bool focused)
        {
        }
    }
}
