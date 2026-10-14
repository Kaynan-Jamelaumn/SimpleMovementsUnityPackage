#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Makes an interactable object (a door, a merchant NPC, a gate, a lever, a chest) part of a network session. Put it
    /// on the object's root next to a NetworkObject (Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked ▸
    /// Interactable).
    /// <list type="bullet">
    /// <item>The NetworkObject lets every machine name the object (its interactables are found below it by path), whether
    /// it is placed in a scene or spawned. Interactions themselves are relayed by the players' <see cref="NetInteractor"/>.</item>
    /// <item>The server's state of every <see cref="IReplicatedInteractable"/> on the object (door open or closed, how many
    /// players an NPC is talking to) is sent to every client watching, late joiners included.</item>
    /// </list>
    /// Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetInteractable : NetworkBehaviour
    {
        private readonly SyncList<int> states = new SyncList<int>();

        private IReplicatedInteractable[] parts = Array.Empty<IReplicatedInteractable>();

        private void Awake() => Collect();

        /// <summary>Finds the replicated interactables of this object (not those of networked objects nested in it).</summary>
        private void Collect()
        {
            var found = new List<IReplicatedInteractable>();
            GetComponentsInChildren(true, found);
            NetworkObject own = GetComponentInParent<NetworkObject>();
            found.RemoveAll(p => !(p is Component c) || c == null || c.GetComponentInParent<NetworkObject>() != own);
            parts = found.ToArray();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Collect();
            Push();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            states.OnChange += OnStatesChanged;
            if (!IsServerInitialized)
                ApplyAll();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            states.OnChange -= OnStatesChanged;
        }

        private void OnDestroy() => states.OnChange -= OnStatesChanged;

        private void Update()
        {
            if (IsServerInitialized)
                Push();
        }

        /// <summary>Server: writes every state that changed.</summary>
        private void Push()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                int value = Read(parts[i]);
                if (i < states.Count)
                {
                    if (states[i] != value)
                        states[i] = value;
                }
                else
                {
                    states.Add(value);
                }
            }
        }

        private static int Read(IReplicatedInteractable part)
        {
            if (part == null || (part is UnityEngine.Object o && o == null))
                return 0;
            try { return part.ReplicatedState; }
            catch (Exception e)
            {
                Debug.LogException(e);
                return 0;
            }
        }

        private void OnStatesChanged(SyncListOperation op, int index, int previous, int next, bool asServer)
        {
            if (asServer || IsServerInitialized)
                return;
            if ((op == SyncListOperation.Add || op == SyncListOperation.Set || op == SyncListOperation.Insert) && index >= 0)
                Apply(index, next);
            else if (op == SyncListOperation.Complete)
                ApplyAll();
        }

        private void ApplyAll()
        {
            for (int i = 0; i < states.Count; i++)
                Apply(i, states[i]);
        }

        private void Apply(int index, int value)
        {
            if (index >= parts.Length || parts[index] == null || (parts[index] is UnityEngine.Object o && o == null))
                return;
            try { parts[index].ApplyReplicatedState(value); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
#endif
