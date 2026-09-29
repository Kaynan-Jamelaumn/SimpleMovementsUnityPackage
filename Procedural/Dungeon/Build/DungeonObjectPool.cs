using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Reuses prefab instances (props, loot, tile-kit pieces) between dungeons instead of destroying and
    /// re-instantiating them - the same idea as the world's PlacementInstantiator pool. Components implementing
    /// IPooledTerrainObject get their reset calls. Main thread only.
    /// </summary>
    public sealed class DungeonObjectPool
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> sourceOf = new Dictionary<GameObject, GameObject>();
        private static readonly List<IPooledTerrainObject> Listeners = new List<IPooledTerrainObject>();
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
            if (!sourceOf.TryGetValue(go, out GameObject prefab) || prefab == null || count >= MaxPooled || !Application.isPlaying)
            {
                sourceOf.Remove(go);
                DungeonMaterials.Destroy(go);
                return;
            }
            sourceOf.Remove(go);
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
