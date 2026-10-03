using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralDungeon
{
    /// <summary>
    /// The map of the floor the player is on, drawn over the screen - only for floors whose map was revealed (a map
    /// room's table). The Map key toggles it: the action named "Map" in the input actions - created there when missing,
    /// on M (keyboard) and Select / View (gamepad), and rebindable in the Key Bindings menu like any other action. Rooms are pale, corridors darker, special rooms tinted, the
    /// chasm dark blue; the dots are the players.
    /// </summary>
    public class DungeonMapOverlay : MonoBehaviour
    {
        [Tooltip("Share of the screen height the map may fill.")]
        [Range(0.2f, 1f)] public float size = 0.7f;
        [Tooltip("Opacity of the map.")]
        [Range(0.2f, 1f)] public float opacity = 0.85f;
        [Tooltip("Actions that toggle the map (the first one found is used; the first name is created when none exists).")]
        public string[] mapActionNames = { "Map" };
        [Tooltip("Default keyboard control of a created Map action (rebind it in Key Bindings).")]
        public string mapKeyControl = "<Keyboard>/m";
        [Tooltip("Default gamepad control of a created Map action.")]
        public string mapGamepadControl = "<Gamepad>/select";

        public bool Visible { get; private set; }

        /// <summary>The key that shows the map, for messages.</summary>
        public string KeyName => input != null && input.ReadAction != null && input.ReadAction.bindings.Count > 0
            ? input.ReadAction.GetBindingDisplayString(0) : "M";

        private DungeonInstance dungeon;
        private GlobalInputBinding input;
        private readonly Dictionary<int, Texture2D> maps = new Dictionary<int, Texture2D>();
        private Texture2D dot;
        private float hideAt = -1f;
        private int shownFloor = -1;

        /// <summary>The dungeon's overlay (added when it has none).</summary>
        public static DungeonMapOverlay For(DungeonInstance d)
        {
            var o = d.GetComponent<DungeonMapOverlay>();
            if (o == null)
                o = d.gameObject.AddComponent<DungeonMapOverlay>();
            return o;
        }

        private void Awake()
        {
            dungeon = GetComponent<DungeonInstance>();
            // Found by name in the input actions, else created there (rebindable, saved with the other key bindings).
            input = GlobalInputBinding.Create(mapActionNames, new[] { mapKeyControl, mapGamepadControl }, "Map", true, true, this);
        }

        /// <summary>Shows a floor's map for a few seconds (0 = until toggled off).</summary>
        public void Show(int floor, float seconds)
        {
            shownFloor = floor;
            Visible = true;
            hideAt = seconds > 0f ? Time.time + seconds : -1f;
        }

        private void Update()
        {
            if (dungeon == null)
                return;
            if (input != null && input.Pressed)
            {
                int floor = dungeon.CurrentFloor;
                if (Visible)
                    Visible = false;
                else if (dungeon.IsMapped(floor))
                    Show(floor, 0f);
                else
                    DungeonMessages.Show("You have no map of this floor. Find its map room.");
            }
            if (Visible && hideAt > 0f && Time.time >= hideAt)
                Visible = false;
            if (Visible && hideAt < 0f && dungeon.IsMapped(dungeon.CurrentFloor))
                shownFloor = dungeon.CurrentFloor;
        }

        private void OnGUI()
        {
            if (!Visible || dungeon == null || dungeon.Layout == null || shownFloor < 0 || shownFloor >= dungeon.FloorCount)
                return;
            Texture2D map = MapOf(shownFloor);
            float h = Screen.height * size, w = h * map.width / map.height;
            if (w > Screen.width * 0.9f)
            {
                w = Screen.width * 0.9f;
                h = w * map.height / map.width;
            }
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Color before = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, opacity);
            GUI.DrawTexture(rect, map, ScaleMode.StretchToFill);
            GUI.color = new Color(1f, 0.25f, 0.2f, 1f);
            if (dot == null)
            {
                dot = new Texture2D(1, 1);
                dot.SetPixel(0, 0, Color.white);
                dot.Apply();
            }
            TileGrid g = dungeon.Layout.Floors[shownFloor].Grid;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive || !dungeon.WorldToCell(p.transform.position, out int f, out Vector2Int cell) || f != shownFloor)
                    continue;
                float px = rect.x + (cell.x + 0.5f) / g.Width * rect.width, py = rect.yMax - (cell.y + 0.5f) / g.Height * rect.height;
                GUI.DrawTexture(new Rect(px - 4f, py - 4f, 8f, 8f), dot);
            }
            GUI.color = before;
            GUI.Label(new Rect(rect.x, rect.y - 22f, rect.width, 20f), $"Floor {shownFloor + 1}");
        }

        private Texture2D MapOf(int floor)
        {
            if (maps.TryGetValue(floor, out Texture2D t) && t != null)
                return t;
            FloorLayout f = dungeon.Layout.Floors[floor];
            TileGrid g = f.Grid;
            t = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[g.Width * g.Height];
            for (int i = 0; i < g.Count; i++)
                pixels[i] = ColorOf(f, g, i);
            t.SetPixels32(pixels);
            t.Apply();
            maps[floor] = t;
            return t;
        }

        private static Color32 ColorOf(FloorLayout f, TileGrid g, int i)
        {
            if (g.IsChasm(i) && !g.IsBridge(i))
                return new Color32(14, 18, 48, 220);
            if (!g.IsWalkable(i))
                return new Color32(12, 10, 9, 200);
            if (g.Type[i] == CellType.Door)
                return new Color32(200, 150, 90, 255);
            if (g.Type[i] == CellType.Link || g.Has(i, CellFlags.Landing))
                return new Color32(120, 200, 255, 255);
            Area a = f.AreaAt(i);
            if (a == null)
                return new Color32(120, 112, 100, 255);
            switch (a.Role)
            {
                case AreaRole.Boss: return new Color32(210, 70, 60, 255);
                case AreaRole.Vault:
                case AreaRole.Treasure: return new Color32(230, 190, 80, 255);
                case AreaRole.Shrine:
                case AreaRole.Rest: return new Color32(120, 200, 140, 255);
            }
            if (a.AnchorIndex >= 0)
                return new Color32(150, 190, 240, 255);
            return a.Kind == AreaKind.Corridor ? new Color32(140, 130, 118, 255) : new Color32(205, 196, 180, 255);
        }

        private void OnDestroy()
        {
            input?.Dispose();
            foreach (Texture2D t in maps.Values)
                if (t != null)
                    Destroy(t);
            if (dot != null)
                Destroy(dot);
        }
    }
}
