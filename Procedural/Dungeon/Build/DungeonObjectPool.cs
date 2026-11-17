using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Reuses prefab instances (props, tile-kit pieces) between dungeons instead of destroying and
    /// re-instantiating them - the same idea as the world's PlacementInstantiator pool. Components implementing
    /// IPooledTerrainObject get their reset calls. Objects with dungeon mechanics (chests, levers, doors, traps, keys...)
    /// are not reused: an opened chest or a pulled lever would come back opened or pulled. Main thread only.
    /// </summary>
    public sealed class DungeonObjectPool
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> sourceOf = new Dictionary<GameObject, GameObject>();
        private static readonly List<IPooledTerrainObject> Listeners = new List<IPooledTerrainObject>();
        private static readonly List<MonoBehaviour> Behaviours = new List<MonoBehaviour>();
        private Transform holder;
        private int count;

        public int MaxPooled = 3000;
        public int Pooled => count;

        public GameObject Get(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            if (prefab == null)
                return null;
            GameObject go = null;
            if (pool.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                while (stack.Count > 0 && go == null)
                    go = stack.Pop();
                if (go != null)
                    count--;
            }
            if (go != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPosition;
                go.transform.localRotation = localRotation;
                go.SetActive(true);
                go.GetComponents(Listeners);
                foreach (IPooledTerrainObject l in Listeners)
                    l.OnTakenFromPool();
            }
            else
            {
                go = Object.Instantiate(prefab, parent, false);
                go.transform.localPosition = localPosition;
                go.transform.localRotation = localRotation;
            }
            sourceOf[go] = prefab;
            return go;
        }

        /// <summary>Returns an instance made by <see cref="Get"/> (others are destroyed).</summary>
        public void Release(GameObject go)
        {
            if (go == null)
                return;
            if (!sourceOf.TryGetValue(go, out GameObject prefab) || prefab == null || count >= MaxPooled || !Application.isPlaying || KeepsState(go))
            {
                sourceOf.Remove(go);
                DungeonMaterials.Destroy(go);
                return;
            }
            sourceOf.Remove(go);
            // The builder scales what it places: the next use starts from the prefab's own size again.
            go.transform.localScale = prefab.transform.localScale;
            go.GetComponents(Listeners);
            foreach (IPooledTerrainObject l in Listeners)
                l.OnReturnedToPool();
            if (holder == null)
            {
                var h = new GameObject("Dungeon Pool");
                h.SetActive(false);
                Object.DontDestroyOnLoad(h);
                holder = h.transform;
            }
            go.transform.SetParent(holder, false);
            if (!pool.TryGetValue(prefab, out Stack<GameObject> stack))
                pool[prefab] = stack = new Stack<GameObject>();
            stack.Push(go);
            count++;
        }

        public bool IsPooledInstance(GameObject go) => go != null && sourceOf.ContainsKey(go);

        /// <summary>
        /// Does the object carry dungeon mechanics - state that a reused copy would wrongly start with (an opened chest, a
        /// pulled lever, a broken barrel, a sunk gate, a found key, a trap put on another rhythm)?
        /// </summary>
        private static bool KeepsState(GameObject go)
        {
            go.GetComponentsInChildren(true, Behaviours);
            bool keeps = false;
            foreach (MonoBehaviour m in Behaviours)
                keeps |= m is IDungeonNetState || m is DungeonPressurePlate || m is DungeonKey || m is DungeonTeleporter ||
                         m is DungeonMovingPlatform || m is DungeonTripwire || m is DungeonHazard || m is DungeonBladeTrap ||
                         m is DungeonDartTrap;
            Behaviours.Clear();
            return keeps;
        }

        public void Clear()
        {
            foreach (Stack<GameObject> s in pool.Values)
                foreach (GameObject go in s)
                    DungeonMaterials.Destroy(go);
            pool.Clear();
            sourceOf.Clear();
            count = 0;
            if (holder != null)
                DungeonMaterials.Destroy(holder.gameObject);
            holder = null;
        }
    }
}
