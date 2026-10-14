#if FISHNET
using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Interest management for the hub world (Fish-Net observer condition): a client sees an object only when it is in
    /// the object's scene - the City, the World or its own dungeon instance - and, out in the open World, only within
    /// <see cref="worldDistance"/> of one of its objects. A client always sees what it owns. Set as the default
    /// condition of the ObserverManager (the network setup menu creates the asset).
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Network/Realm Observer Condition", fileName = "RealmObserverCondition")]
    public class RealmObserverCondition : ObserverCondition
    {
        [Tooltip("Out in the World, objects farther than this from all of a client's objects are hidden from it (metres).")]
        [Min(10f)] public float worldDistance = 140f;

        [Tooltip("Extra distance before an object already seen is hidden again (avoids flicker at the edge), as a fraction.")]
        [Range(0f, 0.5f)] public float hideMargin = 0.1f;

        [Tooltip("Apply the distance limit in dungeon instances too (they are small: usually off).")]
        public bool distanceInDungeons;

        [Tooltip("Apply the distance limit in the City too (usually off: the city is small and everyone meets there).")]
        public bool distanceInCity;

        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            NetworkObject nob = NetworkObject;
            if (nob == null || connection == null)
                return false;
            if (nob.Owner == connection)
                return true;

            UnityEngine.SceneManagement.Scene scene = nob.gameObject.scene;
            if (!connection.Scenes.Contains(scene))
                return false;
            if (!UsesDistance(scene) || nob.GetComponent<NetDungeonRun>() != null)
                return true;

            float max = worldDistance * (currentlyAdded ? 1f + hideMargin : 1f);
            float sqr = max * max;
            Vector3 position = nob.transform.position;
            foreach (NetworkObject own in connection.Objects)
            {
                if (own == null || own.gameObject.scene != scene)
                    continue;
                if ((own.transform.position - position).sqrMagnitude <= sqr)
                    return true;
            }
            return false;
        }

        private bool UsesDistance(UnityEngine.SceneManagement.Scene scene)
        {
            RealmScene realm = RealmScene.ForScene(scene);
            if (realm != null)
                return realm.realm == RealmKind.World || (realm.realm == RealmKind.City && distanceInCity);
            return distanceInDungeons && ProceduralDungeon.DungeonScenes.Find(scene) != null;
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Timed;
    }
}
#endif
