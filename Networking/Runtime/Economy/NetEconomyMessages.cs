#if FISHNET
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Names an interactable to another machine: the NetworkObject it belongs to (or none, for an object of a scene that
    /// has no network identity) plus a path below it (<see cref="InteractionPath"/>; for scene objects the
    /// <see cref="SceneObjectPath"/> in the player's scene), and which interactable component on that object
    /// (<see cref="kind"/> and <see cref="index"/>).
    /// </summary>
    public struct InteractTargetRef
    {
        /// <summary>The networked object the target is part of (unused for a scene object).</summary>
        public NetworkObject root;
        /// <summary>
        /// The target is an object of a scene without a network identity, named by <see cref="path"/> in the player's
        /// scene. False: it is part of <see cref="root"/> (a root that does not exist on the receiving machine arrives
        /// as null, and the target is then not found).
        /// </summary>
        public bool inScene;
        /// <summary>Path below <see cref="root"/> ("" = the root itself), or the scene path when there is no root.</summary>
        public string path;
        /// <summary><see cref="InteractTargets.KindInteractable"/> or <see cref="InteractTargets.KindLegacy"/>.</summary>
        public byte kind;
        /// <summary>Which of the object's interactables of that kind (in component order).</summary>
        public byte index;
    }

    /// <summary>A client's purchase or sale at a merchant (the server repeats it with its own prices and stock).</summary>
    public struct MerchantTradeRequest
    {
        /// <summary>The merchant's NPC.</summary>
        public InteractTargetRef npc;
        /// <summary>Index of the Merchant in the NPC's behaviours.</summary>
        public int behaviour;
        /// <summary><see cref="MerchantTransactionType"/> as a byte.</summary>
        public byte type;
        /// <summary>Buy: the shop line (<see cref="MerchantStockSlot.Id"/>).</summary>
        public int stockSlotId;
        /// <summary>Sell: the player's stack (bag or hotbar).</summary>
        public InventorySlotRef slot;
        /// <summary>The item (<see cref="ItemDatabase"/> id) the player means.</summary>
        public string itemId;
        public int quantity;
        /// <summary>The total the player was shown (0 = do not check). Only used to refuse a changed price, never as the price.</summary>
        public int expectedTotal;
        /// <summary>Numbers the request; the answer carries it back.</summary>
        public uint seq;
    }

    /// <summary>Building and resolving <see cref="InteractTargetRef"/>s (the same on the server and the clients).</summary>
    public static class InteractTargets
    {
        /// <summary>An <see cref="IInteractable"/> component.</summary>
        public const byte KindInteractable = 0;
        /// <summary>An older Interactable or Storage component (used through <see cref="LegacyInteractableTarget"/>).</summary>
        public const byte KindLegacy = 1;
        /// <summary>Longest path accepted.</summary>
        public const int MaxPathLength = 512;

        private static readonly List<IInteractable> interactables = new List<IInteractable>();
        private static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();

        /// <summary>The component that stands for <paramref name="target"/> on this machine (null for targets that are not components).</summary>
        public static Component AnchorOf(IInteractable target)
        {
            if (target is LegacyInteractableTarget legacy)
                return legacy.Owner;
            return target as Component;
        }

        /// <summary>Names <paramref name="anchor"/> (an IInteractable component, or an older Interactable / Storage). False when it cannot be named.</summary>
        public static bool TryMake(Component anchor, out InteractTargetRef target)
        {
            target = default;
            if (anchor == null)
                return false;
            byte kind;
            int index;
            if (anchor is IInteractable)
            {
                kind = KindInteractable;
                index = IndexOf(anchor, KindInteractable);
            }
            else if (LegacyInteractableTarget.IsOwnerComponent(anchor))
            {
                kind = KindLegacy;
                index = IndexOf(anchor, KindLegacy);
            }
            else
            {
                return false;
            }
            if (index < 0 || index > byte.MaxValue)
                return false;

            NetworkObject nob = anchor.GetComponentInParent<NetworkObject>();
            string path;
            if (nob != null)
            {
                if (!NetObjects.IsSpawned(nob))
                    return false;
                path = InteractionPath.Of(nob.transform, anchor.transform);
            }
            else
            {
                if (!anchor.gameObject.scene.IsValid())
                    return false;
                path = SceneObjectPath.Of(anchor.transform);
            }
            if (path == null || path.Length > MaxPathLength)
                return false;
            target = new InteractTargetRef { root = nob, inScene = nob == null, path = path, kind = kind, index = (byte)index };
            return true;
        }

        /// <summary>
        /// The component <paramref name="target"/> names on this machine: under its NetworkObject, or (scene objects) in
        /// <paramref name="scene"/> - a path never reaches into a networked object. Null when it does not exist here.
        /// </summary>
        public static Component Resolve(in InteractTargetRef target, Scene scene)
        {
            if (target.path == null || target.path.Length > MaxPathLength || target.kind > KindLegacy)
                return null;
            Transform t;
            if (!target.inScene)
            {
                if (target.root == null || !NetObjects.IsSpawned(target.root))
                    return null;
                t = InteractionPath.Find(target.root.transform, target.path);
                // The path stays inside the object: a nested networked object is named by its own reference.
                if (t != null && t.GetComponentInParent<NetworkObject>() != target.root)
                    return null;
            }
            else
            {
                if (target.path.Length == 0)
                    return null;
                t = SceneObjectPath.Find(scene, target.path);
                if (t != null && t.GetComponentInParent<NetworkObject>() != null)
                    return null; // networked objects are sent by reference
            }
            return t != null ? At(t.gameObject, target.kind, target.index) : null;
        }

        private static int IndexOf(Component anchor, byte kind)
        {
            if (kind == KindInteractable)
            {
                anchor.GetComponents(interactables);
                for (int i = 0; i < interactables.Count; i++)
                    if (ReferenceEquals(interactables[i], anchor))
                        return i;
                return -1;
            }
            anchor.GetComponents(behaviours);
            int n = 0;
            for (int i = 0; i < behaviours.Count; i++)
            {
                if (!LegacyInteractableTarget.IsOwnerComponent(behaviours[i]))
                    continue;
                if (ReferenceEquals(behaviours[i], anchor))
                    return n;
                n++;
            }
            return -1;
        }

        private static Component At(GameObject go, byte kind, int index)
        {
            if (kind == KindInteractable)
            {
                go.GetComponents(interactables);
                return index < interactables.Count ? interactables[index] as Component : null;
            }
            go.GetComponents(behaviours);
            int n = 0;
            for (int i = 0; i < behaviours.Count; i++)
            {
                if (!LegacyInteractableTarget.IsOwnerComponent(behaviours[i]))
                    continue;
                if (n++ == index)
                    return behaviours[i];
            }
            return null;
        }
    }
}
#endif
