#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    // Items made at runtime (a sword forged at a crafting station, a half-worked piece) exist on the server first. Their
    // ids travel like any other item id (snapshots, what a player holds and wears, world items, trades); a client that
    // meets one it does not know asks the server for the item's definition through its own player's inventory, builds
    // the item from it (the same numbers, name, icon and model) and shows again whatever named it. The server pushes a
    // new definition when an item changes (a crafted item that was tried) to the clients it sent the old one to.
    public partial class NetInventory
    {
        /// <summary>Seconds before a client asks again for a definition the server did not send.</summary>
        public const float DefinitionRetry = 5f;

        // Owner client
        private static NetInventory localOwner;
        private readonly Dictionary<string, float> asked = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly NetPayloadAssembler definitionParts = new NetPayloadAssembler();
        private InventorySnapshot lastSnapshot;
        private bool clientDefinitionsHooked;

        // Server
        private readonly HashSet<string> definedForOwner = new HashSet<string>(StringComparer.Ordinal);
        private WeaponNetMath.RateLimiter definitionLimiter = new WeaponNetMath.RateLimiter(40f, 80f);
        private int definitionTransfer;
        private bool serverDefinitionsHooked;

        // ------------------------------------------------------------------ hooks
        private void HookClientDefinitions(bool on)
        {
            if (on == clientDefinitionsHooked)
                return;
            clientDefinitionsHooked = on;
            if (on)
            {
                ItemDatabase.RuntimeItemDefined += OnRuntimeItemDefined;
                if (IsOwner)
                {
                    localOwner = this;
                    ItemDatabase.RuntimeItemMissing -= OnRuntimeItemMissing;
                    ItemDatabase.RuntimeItemMissing += OnRuntimeItemMissing;
                }
                return;
            }
            ItemDatabase.RuntimeItemDefined -= OnRuntimeItemDefined;
            if (localOwner == this)
            {
                localOwner = null;
                ItemDatabase.RuntimeItemMissing -= OnRuntimeItemMissing;
            }
        }

        private void HookServerDefinitions(bool on)
        {
            if (on == serverDefinitionsHooked)
                return;
            serverDefinitionsHooked = on;
            if (on)
                ItemDatabase.RuntimeItemChanged += OnRuntimeItemChanged;
            else
            {
                ItemDatabase.RuntimeItemChanged -= OnRuntimeItemChanged;
                definedForOwner.Clear();
            }
        }

        // ------------------------------------------------------------------ client: asking
        /// <summary>An id this client does not know was looked up (by any inventory, world item or window): ask the server.</summary>
        private static void OnRuntimeItemMissing(string id)
        {
            NetInventory owner = localOwner;
            if (owner != null && owner.IsClientInitialized && owner.IsOwner && !owner.IsServerInitialized)
                owner.Ask(id);
        }

        private void Ask(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > InventoryNetRules.MaxItemIdLength)
                return;
            float now = Time.unscaledTime;
            if (asked.TryGetValue(id, out float at) && now - at < DefinitionRetry)
                return;
            asked[id] = now;
            CmdItemDefinition(id);
        }

        /// <summary>A definition arrived: whatever showed that id shows the item now.</summary>
        private void OnRuntimeItemDefined(string id)
        {
            if (IsServerInitialized || inventory == null)
                return;
            if (!IsOwner)
            {
                remoteDirty = true; // what this player holds and wears is shown again in Update
                return;
            }
            asked.Remove(id);
            if (lastSnapshot != null && InventoryNetRules.ShouldApply(lastSnapshot.ack, sentSeq) && Names(lastSnapshot, id))
                inventory.ApplySnapshot(lastSnapshot);
        }

        private static bool Names(InventorySnapshot s, string id)
        {
            if (s.stacks != null)
                foreach (InventoryStackData d in s.stacks)
                    if (d.itemId == id) return true;
            if (s.quickSlots != null)
                foreach (string q in s.quickSlots)
                    if (q == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ server: answering
        [ServerRpc]
        private void CmdItemDefinition(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > InventoryNetRules.MaxItemIdLength || !definitionLimiter.TryTake(Time.unscaledTime))
                return;
            SendDefinition(id);
        }

        private void SendDefinition(string id)
        {
            NetworkConnection owner = Owner;
            if (!IsServerInitialized || owner == null || !owner.IsValid || owner.IsLocalClient)
                return;
            string definition = ItemDatabase.ExportRuntime(id);
            if (string.IsNullOrEmpty(definition))
                return; // unknown here, or every machine makes it itself
            definedForOwner.Add(id);
            List<byte[]> parts = NetPayload.Split(NetPayload.Pack(definition));
            int transfer = ++definitionTransfer;
            for (int i = 0; i < parts.Count; i++)
                TargetItemDefinition(owner, id, transfer, (short)i, (short)parts.Count, parts[i]);
        }

        /// <summary>An item this owner was sent changed on the server (a crafted item was tried): it gets the new definition.</summary>
        private void OnRuntimeItemChanged(string id)
        {
            if (definedForOwner.Contains(id))
                SendDefinition(id);
        }

        [TargetRpc]
        private void TargetItemDefinition(NetworkConnection conn, string id, int transfer, short index, short count, byte[] part)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(id) || id.Length > InventoryNetRules.MaxItemIdLength)
                return;
            if (!definitionParts.Add(transfer, index, count, part, out byte[] whole))
                return;
            if (NetPayload.TryUnpack(whole, out string definition) && ItemDatabase.ImportRuntime(id, definition) == null)
                Debug.LogWarning($"[Inventory] The server's definition of '{id}' could not be used by this build.", this);
        }
    }
}
#endif
