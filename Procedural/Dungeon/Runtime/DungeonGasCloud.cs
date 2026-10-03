using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A gas chamber's poison (one per room, created by the builder): it floods the whole room for a while, poisoning
    /// every player inside, then clears for a while - a hiss warns before it comes back. Run through in the clear spells,
    /// or pull the room's valve lever (<see cref="Shut"/>) to stop it for good.
    /// </summary>
    public class DungeonGasCloud : MonoBehaviour
    {
        [Tooltip("Seconds the gas fills the room.")]
        [Min(0.5f)] public float onTime = 7f;
        [Tooltip("Seconds the room is clear between floods.")]
        [Min(0.5f)] public float offTime = 6f;
        [Tooltip("Damage per tick to each player inside while it floods.")]
        [Min(0f)] public float damage = 5f;
        [Tooltip("Seconds between damage ticks.")]
        [Min(0.1f)] public float tick = 0.8f;
        [Tooltip("Colour of the gas.")]
        public Color color = new Color(0.45f, 0.8f, 0.2f);

        public bool IsFlooding { get; private set; }
        public bool IsShut { get; private set; }

        public event Action<DungeonGasCloud> ShutDown;

        private DungeonInstance dungeon;
        private int floor, area;
        private float switchAt, nextTick;
        private bool warned;
        private readonly List<GameObject> clouds = new List<GameObject>();

        /// <summary>Builds the cloud's look over the room's cells (puffs every few cells, at chest height).</summary>
        public void Setup(DungeonInstance owner, int floorIndex, int areaId, Material material)
        {
            dungeon = owner;
            floor = floorIndex;
            area = areaId;
            FloorLayout f = owner.Layout.Floors[floorIndex];
            if (areaId < 0 || areaId >= f.Areas.Count)
                return;
            TileGrid g = f.Grid;
            float cs = owner.CellSize;
            foreach (int c in f.Areas[areaId].Cells)
            {
                int x = g.X(c), y = g.Y(c);
                if ((x + y) % 2 != 0 || x % 2 != 0)
                    continue;
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(puff.GetComponent<Collider>());   // (built in the editor's preview too: no Destroy there)
                puff.name = "Gas";
                puff.transform.SetParent(transform, true);
                puff.transform.position = owner.CellToWorld(floorIndex, new Vector2(x + 0.5f, y + 0.5f), g.FloorHeight[c] + 1.1f);
                puff.transform.localScale = new Vector3(cs * 2.2f, 1.6f, cs * 2.2f);
                var r = puff.GetComponent<Renderer>();
                if (material != null)
                    r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                puff.SetActive(false);
                clouds.Add(puff);
            }
        }

        private void Start() => switchAt = Time.time + offTime;

        private void Update()
        {
            if (IsShut || dungeon == null)
                return;
            if (!IsFlooding && !warned && Time.time >= switchAt - 2f && DungeonPlayers.CountInArea(dungeon, floor, area) > 0)
            {
                warned = true;
                DungeonMessages.Show("The vents hiss...");
            }
            if (Time.time >= switchAt)
                Set(!IsFlooding);
            if (!IsFlooding || Time.time < nextTick)
                return;
            nextTick = Time.time + tick;
            TileGrid g = dungeon.Layout.Floors[floor].Grid;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive || !dungeon.WorldToCell(p.transform.position, out int f, out Vector2Int cell) || f != floor)
                    continue;
                if (g.Area[g.Index(cell)] != area)
                    continue;
                p.ApplyDamage(new DamageInfo { amount = damage, target = p, point = p.transform.position, direction = Vector3.up, type = DamageType.Magical, element = ElementType.Poison });
            }
        }

        private void Set(bool flooding)
        {
            IsFlooding = flooding;
            warned = false;
            switchAt = Time.time + (flooding ? onTime : offTime);
            foreach (GameObject c in clouds)
                if (c != null)
                    c.SetActive(flooding);
        }

        /// <summary>Stops the gas for good (the valve).</summary>
        public void Shut()
        {
            if (IsShut)
                return;
            Set(false);
            IsShut = true;
            ShutDown?.Invoke(this);
        }
    }
}
