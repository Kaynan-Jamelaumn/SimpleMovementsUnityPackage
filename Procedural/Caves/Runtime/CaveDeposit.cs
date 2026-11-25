using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Sits next to the <see cref="OreDeposit"/> of every deposit a cave builds and ties it to its cave: what it is (its
    /// resource, depth and tier), the cave's memory of it (a mined deposit stays mined - and goes on growing back - between
    /// visits) and the network: on a server it publishes the seam's state to the run's clients (through
    /// <see cref="DungeonNet"/>, by the deposit's element id) and gathers loose stones by hand for a client's request (once
    /// the client held Interact on it for the seam's gather time, timed by the server); on a
    /// client's copy the seam only shows what the server says (<see cref="OreDeposit.ShowOnly"/>) and asks the server when
    /// its player gathers. Pickaxe hits need nothing here: the server's weapon hits reach the server's seam.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CaveDeposit : MonoBehaviour, IDungeonNetState, IDungeonNetInteract, IDungeonNetHold
    {
        private bool replica;

        /// <summary>The seam (its mining rules).</summary>
        public OreDeposit Deposit { get; private set; }
        /// <summary>The cave that built it.</summary>
        public CaveInstance Cave { get; private set; }
        /// <summary>Its placement's id in the cave's layout (the same on the server and on clients).</summary>
        public int PlacementId { get; private set; } = -1;
        /// <summary>What kind of deposit it is.</summary>
        public CaveResourceEntry Resource { get; private set; }
        /// <summary>How deep it lies (its chamber's depth score).</summary>
        public float Depth { get; private set; }
        /// <summary>Its tier (0 common .. 4 legendary).</summary>
        public int Tier => Resource != null ? Resource.Tier : 0;

        internal void Setup(CaveInstance cave, OreDeposit deposit, int placementId, CaveResourceEntry resource, float depth, bool isReplica)
        {
            Release();
            Cave = cave;
            Deposit = deposit;
            PlacementId = placementId;
            Resource = resource;
            Depth = depth;
            replica = isReplica;
            if (deposit == null)
                return;
            deposit.ShowOnly = isReplica;
            deposit.RemoteGather = isReplica ? RelayGather : null;
            deposit.LumpNumber = isReplica ? null : NumberLump;
            deposit.Changed += OnChanged;
            deposit.LumpTaken += OnLumpTaken;
        }

        /// <summary>Lets go of the seam (the deposit object goes back to the pool).</summary>
        internal void Release()
        {
            if (Deposit != null)
            {
                Deposit.Changed -= OnChanged;
                Deposit.LumpTaken -= OnLumpTaken;
                Deposit.RemoteGather = null;
                Deposit.LumpNumber = null;
                Deposit.ShowOnly = false;
            }
            Cave = null;
            Deposit = null;
            Resource = null;
            PlacementId = -1;
        }

        private void OnDestroy() => Release();

        /// <summary>Server (offline: nothing to send): tells the run's clients how the seam is now.</summary>
        internal void PublishState()
        {
            if (!replica && Deposit != null)
                DungeonNet.Publish(this, Deposit.ReplicatedState);
        }

        private void OnChanged(OreDeposit d)
        {
            if (replica || d == null)
                return;
            DungeonNet.Publish(this, d.ReplicatedState);
            if (Cave != null)
                Cave.KeepDeposit(PlacementId, d.CaptureState());
        }

        private void OnLumpTaken(OreDeposit d, CombatEntity player, CraftStockItemSO item, int lump)
        {
            if (Cave != null)
                Cave.NoteMined(this, player, item);
        }

        /// <summary>The cave numbers the seam's lumps (one count for every visit of the cave going at once).</summary>
        private int NumberLump(OreDeposit d) => Cave != null ? Cave.NextLump(PlacementId, d) : d.LumpsIssued;

        /// <summary>A client's copy: its player gathers by hand - the server's copy does it (by this deposit's element id).</summary>
        private bool RelayGather(OreDeposit d, CombatEntity player) =>
            DungeonNet.TryRelayInteract(this, player != null ? PlayerLocator.MovableRoot(player) : null);

        // ------------------------------------------------------------------ the network (see DungeonNet)
        public void ApplyNetState(int state, byte[] payload)
        {
            if (Deposit != null)
                Deposit.ApplyReplicatedState(state);
        }

        public float NetInteractRange => Deposit != null ? Deposit.InteractionRange + 0.5f : 3f;

        public bool NetInteract(CombatEntity player) => Deposit != null && Deposit.GatherFor(player);

        /// <summary>A loose stone is gathered by holding Interact (the seam's gather time): the server checks the hold.</summary>
        public float NetHoldDuration => Deposit != null ? Deposit.HoldDuration : 0f;
    }
}
