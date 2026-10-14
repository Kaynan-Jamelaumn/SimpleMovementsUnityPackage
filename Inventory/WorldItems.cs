using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What the network layer provides for items lying in the world (installed on the server during a session; null
/// offline). Gameplay code never needs it directly: <see cref="WorldItems"/> uses it.
/// </summary>
public interface IWorldItemNet
{
    /// <summary>Can instances of <paramref name="prefab"/> be network-spawned (a registered network prefab)?</summary>
    bool IsNetworkPrefab(GameObject prefab);

    /// <summary>
    /// A new networked world item that shows whatever item its <see cref="ItemPickable"/> holds (used for items whose own
    /// prefab is not networked), or null when the project has none.
    /// </summary>
    GameObject CreateCarrier(Vector3 position, Quaternion rotation);

    /// <summary>Does <paramref name="go"/> have a network identity of its own (a spawned NetworkObject on it)?</summary>
    bool HasNetworkIdentity(GameObject go);

    /// <summary>
    /// A new world item's <see cref="ItemPickable"/> was filled in: the carrier builds the item's model now (before it is
    /// put on the ground, which measures the model).
    /// </summary>
    void Prepare(GameObject worldItem);

    /// <summary>
    /// A world item without a network identity (placed in a scene file, so every machine has its own copy) was picked up
    /// on the server: <paramref name="remaining"/> are left (0 = all taken). Clients remove or update their copy.
    /// </summary>
    void SceneItemTaken(ItemPickable item, int remaining, GameObject takenBy);
}

/// <summary>
/// Creates and removes items lying in the world (drops, loot, harvest) so that the same code works offline and in a
/// network session. Offline nothing changes: prefabs are instantiated and destroyed as before. On a server the item is
/// moved into the scene of what it came from (interest and physics are per scene) and network-spawned, so every
/// player in that scene sees it; an item whose prefab is not networked is shown through the networked carrier
/// (<see cref="IWorldItemNet.CreateCarrier"/>). Clients never create world items: they ask the server.
/// </summary>
public static class WorldItems
{
    /// <summary>The network layer's part (installed on the server during a session).</summary>
    public static IWorldItemNet Net { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Net = null;

    private static bool ServerSession => GameNet.NetAuthority.IsNetworked && GameNet.NetAuthority.IsServer && Net != null;

    /// <summary>
    /// A new world object for <paramref name="item"/>: its prefab, or (on a server, when that prefab is not networked) the
    /// networked carrier. The caller fills its <see cref="ItemPickable"/> and then calls <see cref="Publish"/>.
    /// </summary>
    public static GameObject Instantiate(ItemSO item, Vector3 position, Quaternion rotation)
    {
        if (item == null || item.Prefab == null)
            return null;
        GameObject go = null;
        if (ServerSession && !Net.IsNetworkPrefab(item.Prefab))
            go = Net.CreateCarrier(position, rotation);
        if (go == null)
            go = Object.Instantiate(item.Prefab, position, rotation);
        go.name = item.Prefab.name;
        return go;
    }

    /// <summary>
    /// A new instance of a pickable prefab (loot tables hold these): the prefab itself, or (on a server, when it is not
    /// networked) the networked carrier holding the same item. <paramref name="carrier"/> tells which.
    /// </summary>
    public static GameObject InstantiatePickable(GameObject prefab, Vector3 position, Quaternion rotation, out bool carrier)
    {
        carrier = false;
        if (prefab == null)
            return null;
        if (ServerSession && !Net.IsNetworkPrefab(prefab))
        {
            ItemPickable source = prefab.GetComponent<ItemPickable>();
            if (source == null)
                source = prefab.GetComponentInChildren<ItemPickable>(true);
            if (source != null && source.itemScriptableObject != null)
            {
                GameObject go = Net.CreateCarrier(position, rotation);
                if (go != null)
                {
                    ItemPickable p = go.GetComponent<ItemPickable>();
                    if (p == null)
                        p = go.AddComponent<ItemPickable>();
                    p.itemScriptableObject = source.itemScriptableObject;
                    p.quantity = Mathf.Max(1, source.quantity);
                    p.DurabilityList = source.DurabilityList != null ? new List<int>(source.DurabilityList) : new List<int>();
                    p.InteractionTime = source.itemScriptableObject.PickUpTime;
                    p.spinInWorld = source.spinInWorld;
                    go.name = prefab.name;
                    carrier = true;
                    return go;
                }
            }
        }
        return Object.Instantiate(prefab, position, rotation);
    }

    /// <summary>A new world item's <see cref="ItemPickable"/> is filled in (see <see cref="IWorldItemNet.Prepare"/>). Offline nothing happens.</summary>
    public static void Prepare(GameObject go)
    {
        if (go != null && ServerSession)
            Net.Prepare(go);
    }

    /// <summary>
    /// Makes a world item created on the server visible to the players: moved into the scene of
    /// <paramref name="source"/> (a root object only), then network-spawned. Offline it does nothing. Returns true when
    /// the item was network-spawned.
    /// </summary>
    public static bool Publish(GameObject go, GameObject source)
    {
        if (go == null || !GameNet.NetAuthority.IsNetworked || !GameNet.NetAuthority.IsServer)
            return false;
        Scene scene = source != null ? source.scene : default;
        if (go.transform.parent == null && scene.IsValid() && scene.isLoaded && go.scene != scene)
            SceneManager.MoveGameObjectToScene(go, scene);
        return GameNet.NetSpawn.Spawn(go);
    }

    /// <summary>
    /// A world item was taken (picked up whole): network-despawned for everyone, or destroyed. A scene item without a
    /// network identity is removed from every client's copy of the scene too.
    /// </summary>
    public static void Remove(GameObject go, GameObject takenBy = null)
    {
        if (go == null)
            return;
        if (ServerSession && !Net.HasNetworkIdentity(go))
        {
            ItemPickable p = go.GetComponent<ItemPickable>();
            if (p != null)
                Net.SceneItemTaken(p, 0, takenBy);
        }
        GameNet.NetSpawn.Destroy(go);
    }

    /// <summary>
    /// Part of a world item was taken (what is left stays on the ground). Networked items follow by themselves; a scene
    /// item without a network identity is updated on every client.
    /// </summary>
    public static void QuantityChanged(ItemPickable item, GameObject takenBy = null)
    {
        if (item == null)
            return;
        if (ServerSession && !Net.HasNetworkIdentity(item.gameObject))
            Net.SceneItemTaken(item, item.quantity, takenBy);
    }
}
