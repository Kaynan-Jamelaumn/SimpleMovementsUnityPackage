#if FISHNET
using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// An item lying in the world in a network session (a drop, loot, a harvest, a recovered arrow). The server owns it:
    /// it decides who picks it up (<see cref="NetInventory"/>) and despawns it for everyone when it is gone. Every client
    /// is told which item it is, how many and their uses left (<see cref="WorldItemState"/>), so the pickup prompt and the
    /// inventory show the right thing.
    /// <para>
    /// Two ways to use it: on an item's own world prefab (made networked with Tools ▸ SimpleMovements ▸ Network ▸ Make
    /// Selected Prefab Networked ▸ Pickup or Loot Drop: the prefab's model is used), or on the generic CARRIER - a prefab
    /// named "NetWorldItem" in a Resources folder with only a NetworkObject and this component - which builds the model
    /// of whatever item it holds (items whose own prefab is not networked are shown through it). A copy without its own
    /// renderers is a carrier.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NetWorldItem : NetworkBehaviour
    {
        /// <summary>Resources path of the generic carrier prefab.</summary>
        public const string CarrierResourcePath = "NetWorldItem";

        private readonly SyncVar<WorldItemState> state = new SyncVar<WorldItemState>();

        private ItemPickable pickable;
        private bool carrier;
        private bool intendedSpin = true;
        private GameObject model;
        private ItemSO modelFor;
        // Server: what was last published
        private ItemSO lastItem;
        private int lastQuantity = int.MinValue;
        private int lastDurabilityCount = -1;
        private bool lastSpin;

        /// <summary>The item component this networks.</summary>
        public ItemPickable Pickable => pickable;

        /// <summary>True on the generic carrier (it builds the item's model itself).</summary>
        public bool IsCarrier => carrier;

        /// <summary>What every machine knows about the item.</summary>
        public WorldItemState State => state.Value;

        private void Awake()
        {
            // The carrier has no model of its own; an item's world prefab has one.
            carrier = GetComponentInChildren<Renderer>(true) == null;
            ResolvePickable(carrier);
        }

        /// <summary>
        /// The item component (an item's world prefab may get it right after it is created, from the code that drops it).
        /// <paramref name="create"/> adds one when missing.
        /// </summary>
        private ItemPickable ResolvePickable(bool create)
        {
            if (pickable == null)
                pickable = GetComponent<ItemPickable>();
            if (pickable == null)
                pickable = GetComponentInChildren<ItemPickable>(true);
            if (pickable == null && create)
                pickable = gameObject.AddComponent<ItemPickable>();
            return pickable;
        }

        // ------------------------------------------------------------------ server
        public override void OnStartServer()
        {
            base.OnStartServer();
            ResolvePickable(false);
            intendedSpin = pickable != null && pickable.spinInWorld;
            // A synced transform would send the turning every tick: the server's copy keeps still, clients turn their own.
            if (pickable != null && GetComponent<NetworkTransform>() != null)
                pickable.spinInWorld = false;
            Publish();
        }

        private void Update()
        {
            if (!IsServerInitialized || pickable == null)
                return;
            // Partly picked up (the rest stays) or changed by a script: everyone follows.
            int durabilityCount = pickable.DurabilityList != null ? pickable.DurabilityList.Count : 0;
            if (pickable.itemScriptableObject != lastItem || pickable.quantity != lastQuantity || durabilityCount != lastDurabilityCount
                || (pickable.spinInWorld || intendedSpin) != lastSpin)
                Publish();
        }

        private void Publish()
        {
            if (pickable == null)
                return;
            List<int> list = pickable.DurabilityList;
            lastItem = pickable.itemScriptableObject;
            lastQuantity = pickable.quantity;
            lastDurabilityCount = list != null ? list.Count : 0;
            lastSpin = pickable.spinInWorld || intendedSpin;
            int[] durabilities = null;
            if (list != null && list.Count > 0)
            {
                int n = Mathf.Min(list.Count, InventoryNetRules.MaxDurabilities);
                durabilities = new int[n];
                for (int i = 0; i < n; i++)
                    durabilities[i] = list[i];
            }
            string id = lastItem != null ? ItemDatabase.Id(lastItem) : null;
            if (lastItem != null && string.IsNullOrEmpty(id))
                Debug.LogWarning($"[Network] World item '{lastItem.Name}' is not in the Item Database: clients cannot show it. " +
                                 "Run Tools ▸ SimpleMovements ▸ Project Setup ▸ Build Item Database.", this);
            var next = new WorldItemState
            {
                itemId = id ?? "",
                quantity = Mathf.Max(1, lastQuantity),
                durabilities = durabilities,
                spin = lastSpin,
            };
            if (!next.Equals(state.Value))
                state.Value = next;
            if (carrier)
                EnsureModel(lastItem);
        }

        /// <summary>The server filled the item in before spawning it: the carrier builds the model now (it is measured for the ground).</summary>
        internal void PrepareOnServer()
        {
            if (carrier && ResolvePickable(true) != null)
                EnsureModel(pickable.itemScriptableObject);
        }

        // ------------------------------------------------------------------ clients
        public override void OnStartClient()
        {
            base.OnStartClient();
            state.OnChange += OnStateChanged;
            ItemDatabase.RuntimeItemDefined += OnRuntimeItemDefined;
            if (!IsServerInitialized)
            {
                // The server simulates it; this copy follows (a synced transform moves it).
                foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
                    rb.isKinematic = true;
                Apply(state.Value);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            state.OnChange -= OnStateChanged;
            ItemDatabase.RuntimeItemDefined -= OnRuntimeItemDefined;
        }

        private void OnDestroy() => ItemDatabase.RuntimeItemDefined -= OnRuntimeItemDefined;

        /// <summary>The definition of the item this copy holds (made at runtime on the server) arrived: it is shown now.</summary>
        private void OnRuntimeItemDefined(string id)
        {
            if (!IsServerInitialized && id == state.Value.itemId)
                Apply(state.Value);
        }

        private void OnStateChanged(WorldItemState previous, WorldItemState next, bool asServer)
        {
            if (asServer || IsServerInitialized)
                return;
            Apply(next);
        }

        private void Apply(WorldItemState s)
        {
            if (ResolvePickable(!string.IsNullOrEmpty(s.itemId)) == null)
                return;
            ItemSO item = string.IsNullOrEmpty(s.itemId) ? null : ItemDatabase.Item(s.itemId);
            if (item != null)
            {
                pickable.itemScriptableObject = item;
                pickable.InteractionTime = item.PickUpTime;
            }
            else if (!string.IsNullOrEmpty(s.itemId) && !ItemDatabase.IsRuntimeId(s.itemId))
            {
                Debug.LogWarning($"[Network] World item '{s.itemId}' is not in this build's Item Database.", this);
            }
            pickable.quantity = Mathf.Max(1, s.quantity);
            pickable.DurabilityList = s.durabilities != null ? new List<int>(s.durabilities) : new List<int>();
            pickable.spinInWorld = s.spin;
            if (carrier)
                EnsureModel(item);
        }

        // ------------------------------------------------------------------ the carrier's model
        /// <summary>The carrier shows <paramref name="item"/>'s model (its Equipment Visuals model, else its world prefab).</summary>
        private void EnsureModel(ItemSO item)
        {
            if (item == modelFor && (model != null || item == null))
                return;
            if (model != null)
                Destroy(model);
            model = null;
            modelFor = item;
            if (item == null)
                return;
            GameObject source = item.Visuals.model != null ? item.Visuals.model : item.Prefab;
            if (source == null)
                return;

            // Built under an inactive holder: the copy's own scripts never wake up before they are removed.
            var holder = new GameObject("World Item Model (building)");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            GameObject copy = Instantiate(source, holder.transform, false);
            StripForDisplay(copy);
            copy.transform.SetParent(transform, false);
            copy.name = "Model";
            Destroy(holder);
            model = copy;
            if (!model.activeSelf)
                model.SetActive(true);
            MakeInteractable();
        }

        /// <summary>Only the look stays: scripts (network ones included) and physics bodies are removed from the copy.</summary>
        private static void StripForDisplay(GameObject copy)
        {
            var behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = behaviours.Length - 1; i >= 0; i--)
                {
                    if (behaviours[i] == null)
                        continue;
                    try { DestroyImmediate(behaviours[i]); }
                    catch (System.Exception) { /* another component still needs it: the second pass removes it */ }
                }
            }
            foreach (Joint j in copy.GetComponentsInChildren<Joint>(true))
                DestroyImmediate(j);
            foreach (Rigidbody rb in copy.GetComponentsInChildren<Rigidbody>(true))
                DestroyImmediate(rb);
        }

        /// <summary>The pickup prompt's raycast must hit the carrier: a box around the model when it has no collider.</summary>
        private void MakeInteractable()
        {
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer == ignoreRaycast)
                    t.gameObject.layer = 0;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                if (c.enabled)
                    return;
            BoxCollider box = gameObject.GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();
            box.enabled = true;
            bool found = false;
            Bounds b = default;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer)
                    continue;
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!found)
            {
                box.size = Vector3.one * 0.4f;
                return;
            }
            Vector3 scale = transform.lossyScale;
            box.center = transform.InverseTransformPoint(b.center);
            box.size = new Vector3(
                b.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                b.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                b.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }
    }

    /// <summary>
    /// The server's world-item service (<see cref="IWorldItemNet"/>): tells which prefabs are network prefabs, creates the
    /// carrier, tells clients when a scene item without a network identity was picked up, and turns stuck arrows and
    /// thrown weapons into networked pickups (<see cref="WeaponProjectile.RecoveredItemSpawner"/>). Installed while this
    /// machine is a server in a session; removed otherwise.
    /// </summary>
    internal sealed class NetWorldItemService : IWorldItemNet
    {
        private static readonly NetWorldItemService instance = new NetWorldItemService();
        private static readonly RecoveredItemSpawn recovered = SpawnRecovered;
        private GameObject carrierPrefab;
        private bool searched;
        private bool warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            instance.carrierPrefab = null;
            instance.searched = false;
            instance.warned = false;
            NetAuthority.ModeChanged -= OnModeChanged;
            NetAuthority.ModeChanged += OnModeChanged;
            OnModeChanged(NetAuthority.Mode);
        }

        private static void OnModeChanged(NetMode mode)
        {
            if (NetAuthority.IsNetworked && NetAuthority.IsServer)
            {
                WorldItems.Net = instance;
                WeaponProjectile.RecoveredItemSpawner = recovered;
                return;
            }
            if (ReferenceEquals(WorldItems.Net, instance))
                WorldItems.Net = null;
            if (WeaponProjectile.RecoveredItemSpawner == recovered)
                WeaponProjectile.RecoveredItemSpawner = null;
        }

        public bool IsNetworkPrefab(GameObject prefab)
        {
            NetworkObject nob = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            return nob != null && nob.PrefabId != NetworkObject.UNSET_PREFABID_VALUE;
        }

        public bool HasNetworkIdentity(GameObject go)
        {
            NetworkObject nob = go != null ? go.GetComponent<NetworkObject>() : null;
            return nob != null && NetObjects.IsSpawned(nob);
        }

        public void Prepare(GameObject worldItem)
        {
            NetWorldItem w = worldItem != null ? worldItem.GetComponent<NetWorldItem>() : null;
            if (w != null)
                w.PrepareOnServer();
        }

        public GameObject CreateCarrier(Vector3 position, Quaternion rotation)
        {
            GameObject prefab = Carrier;
            return prefab != null ? Object.Instantiate(prefab, position, rotation) : null;
        }

        private GameObject Carrier
        {
            get
            {
                if (!searched)
                {
                    searched = true;
                    GameObject p = SMAssets.Load<GameObject>(SMAssets.Networking, NetWorldItem.CarrierResourcePath);
                    if (p != null && IsNetworkPrefab(p) && p.GetComponent<NetWorldItem>() != null)
                        carrierPrefab = p;
                }
                if (carrierPrefab == null && !warned)
                {
                    warned = true;
                    Debug.LogWarning("[Network] No world item carrier (a prefab 'Resources/" + SMAssets.ResourcePath(SMAssets.Networking, NetWorldItem.CarrierResourcePath) +
                                     "' with a NetworkObject and a NetWorldItem, registered as a network prefab): items whose own prefab is " +
                                     "not networked stay on the server only, and clients cannot see or pick them up.");
                }
                return carrierPrefab;
            }
        }

        public void SceneItemTaken(ItemPickable item, int remaining, GameObject takenBy)
        {
            if (item == null || takenBy == null)
                return;
            string path = SceneObjectPath.Of(item.transform);
            if (string.IsNullOrEmpty(path))
                return;
            NetInventory relay = takenBy.GetComponentInParent<NetInventory>();
            if (relay == null)
            {
                NetworkObject nob = NetObjects.Of(takenBy);
                relay = nob != null ? nob.GetComponentInChildren<NetInventory>(true) : null;
            }
            if (relay != null)
                relay.ServerSceneItemTaken(path, Mathf.Max(0, remaining));
        }

        /// <summary>A stuck arrow or thrown weapon becomes a networked pickup where it is stuck (it does not fall or turn).</summary>
        private static bool SpawnRecovered(ItemSO item, IList<int> durability, Vector3 position, Quaternion rotation, GameObject sceneOf)
        {
            GameObject go = ItemHandler.SpawnWorldItemAt(item, 1, durability, position, rotation, sceneOf, false, false);
            if (go == null)
                return false;
            if (NetAuthority.IsNetworkedObject(go))
                return true;
            Object.Destroy(go); // could not be networked: the projectile stays the (server-side) pickup, as before
            return false;
        }
    }
}
#endif
