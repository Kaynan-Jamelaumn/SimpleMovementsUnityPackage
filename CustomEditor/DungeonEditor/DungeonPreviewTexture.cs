using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    /// <summary>What the preview colours by.</summary>
    public enum PreviewMode
    {
        Roles,
        Styles,
        Heights,
        Distance,
        WallDistance,
        Zones,
        /// <summary>Floor-to-ceiling height: dark = low, bright = tall (vaults show as gradients).</summary>
        Ceilings,
    }

    /// <summary>Draws a floor of a <see cref="DungeonLayout"/> into a texture (editor preview).</summary>
    public static class DungeonPreviewTexture
    {
        public static Texture2D Draw(DungeonLayout layout, int floorIndex, PreviewMode mode, int pixelsPerCell,
            bool mainPath, bool placements, bool doors, Texture2D reuse = null)
        {
            FloorLayout floor = layout.Floors[floorIndex];
            TileGrid g = floor.Grid;
            int s = Mathf.Max(1, pixelsPerCell);
            int w = g.Width * s, h = g.Height * s;
            Texture2D tex = reuse != null && reuse.width == w && reuse.height == h
                ? reuse
                : new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];

            int maxDist = 1;
            if (floor.DistanceFromArrival != null)
                foreach (int d in floor.DistanceFromArrival)
                    maxDist = Mathf.Max(maxDist, d);

            for (int i = 0; i < g.Count; i++)
            {
                Color32 c = CellColor(layout, floor, i, mode, maxDist);
                if (mainPath && g.Has(i, CellFlags.MainPath) && g.IsWalkable(i))
                    c = Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.35f);
                int x0 = (i % g.Width) * s, y0 = (i / g.Width) * s;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        px[(y0 + y) * w + x0 + x] = c;
            }

            if (doors)
            {
                for (int i = 0; i < g.Count; i++)
                {
                    if (g.Type[i] != CellType.Door)
                        continue;
                    Color32 dc = g.Has(i, CellFlags.Secret) ? new Color32(255, 40, 40, 255) : new Color32(235, 175, 60, 255);
                    Dot(px, w, h, (i % g.Width) * s + s / 2, (i / g.Width) * s + s / 2, Mathf.Max(1, s / 3), dc);
                }
            }

            if (placements)
            {
                foreach (Placement p in layout.Placements)
                {
                    if (p.Floor != floorIndex)
                        continue;
                    Dot(px, w, h, Mathf.RoundToInt(p.Cell.x * s), Mathf.RoundToInt(p.Cell.y * s), Mathf.Max(1, s / 3), PlacementColor(p.Kind));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        private static void Dot(Color32[] px, int w, int h, int cx, int cy, int r, Color32 c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 0 && y >= 0 && x < w && y < h)
                        px[y * w + x] = c;
        }

        public static Color32 PlacementColor(PlacementKind k)
        {
            switch (k)
            {
                case PlacementKind.Mob: return new Color32(220, 30, 30, 255);
                case PlacementKind.Boss: return new Color32(110, 0, 0, 255);
                case PlacementKind.Loot: return new Color32(255, 215, 0, 255);
                case PlacementKind.Light: return new Color32(255, 250, 190, 255);
                case PlacementKind.PlayerSpawn: return new Color32(0, 255, 0, 255);
                case PlacementKind.EntrancePortal: return new Color32(0, 180, 255, 255);
                case PlacementKind.ExitPortal: return new Color32(0, 255, 160, 255);
                case PlacementKind.Hazard: return new Color32(255, 100, 0, 255);
                case PlacementKind.PointOfInterest: return new Color32(190, 100, 255, 255);
                case PlacementKind.Interactable: return new Color32(120, 200, 255, 255);
                case PlacementKind.Key: return new Color32(255, 255, 0, 255);
                case PlacementKind.LockedDoor: return new Color32(255, 255, 255, 255);
                case PlacementKind.Gate: return new Color32(0, 255, 255, 255);
                case PlacementKind.Switch: return new Color32(160, 80, 255, 255);
                case PlacementKind.Shortcut: return new Color32(0, 255, 90, 255);
                case PlacementKind.Teleporter: return new Color32(150, 110, 255, 255);
                case PlacementKind.MovingPlatform: return new Color32(200, 200, 255, 255);
                case PlacementKind.Tripwire:
                case PlacementKind.ArrowLauncher: return new Color32(255, 150, 60, 255);
                case PlacementKind.ShiftingWall: return new Color32(170, 170, 170, 255);
                case PlacementKind.Lever: return new Color32(255, 120, 255, 255);
                case PlacementKind.Nest: return new Color32(140, 230, 60, 255);
                case PlacementKind.AreaEffect: return new Color32(120, 200, 40, 255);
                default: return new Color32(90, 90, 90, 255);
            }
        }

        public static Color32 RoleColor(Area a)
        {
            switch (a.Role)
            {
                case AreaRole.Entrance: return new Color32(40, 200, 90, 255);
                case AreaRole.Exit: return new Color32(40, 220, 200, 255);
                case AreaRole.StairsUp: return new Color32(90, 120, 255, 255);
                case AreaRole.StairsDown: return new Color32(160, 90, 255, 255);
                case AreaRole.DropSource: return new Color32(255, 120, 200, 255);
                case AreaRole.DropLanding: return new Color32(255, 160, 220, 255);
                case AreaRole.Boss: return new Color32(200, 40, 40, 255);
                case AreaRole.Treasure: return new Color32(230, 200, 40, 255);
                case AreaRole.Rest: return new Color32(120, 220, 120, 255);
                case AreaRole.Arena: return new Color32(230, 120, 40, 255);
                case AreaRole.Shrine: return new Color32(200, 200, 255, 255);
                case AreaRole.Secret: return new Color32(255, 60, 160, 255);
                case AreaRole.Custom: return new Color32(100, 200, 200, 255);
                case AreaRole.MiniBoss: return new Color32(170, 60, 90, 255);
                case AreaRole.Vault: return new Color32(255, 235, 120, 255);
                case AreaRole.TrapRoom: return new Color32(255, 90, 40, 255);
                case AreaRole.Puzzle: return new Color32(120, 140, 255, 255);
                case AreaRole.Ambush: return new Color32(150, 20, 60, 255);
                case AreaRole.Library: return new Color32(150, 110, 70, 255);
                case AreaRole.Armory: return new Color32(140, 150, 170, 255);
                case AreaRole.Prison: return new Color32(90, 90, 100, 255);
                case AreaRole.Crypt: return new Color32(110, 120, 140, 255);
                case AreaRole.Laboratory: return new Color32(120, 220, 160, 255);
                case AreaRole.Garden: return new Color32(70, 170, 60, 255);
                case AreaRole.Throne: return new Color32(200, 150, 40, 255);
                case AreaRole.Nest: return new Color32(120, 160, 50, 255);
                case AreaRole.Gambling: return new Color32(140, 30, 50, 255);
                case AreaRole.Kitchen: return new Color32(210, 150, 90, 255);
                case AreaRole.Gallery: return new Color32(190, 160, 200, 255);
                case AreaRole.Barracks: return new Color32(120, 110, 80, 255);
                case AreaRole.Colosseum: return new Color32(220, 170, 110, 255);
                case AreaRole.Greenhouse: return new Color32(110, 200, 110, 255);
                case AreaRole.WineCellar: return new Color32(110, 40, 60, 255);
                case AreaRole.MapRoom: return new Color32(140, 190, 230, 255);
                case AreaRole.GasChamber: return new Color32(150, 190, 40, 255);
                default: return StyleColor(a.Style);
            }
        }

        public static Color32 StyleColor(ZoneStyle s)
        {
            switch (s)
            {
                case ZoneStyle.Cavern: return new Color32(150, 115, 80, 255);
                case ZoneStyle.Ruins: return new Color32(140, 150, 120, 255);
                default: return new Color32(175, 170, 160, 255);
            }
        }

        private static Color32 CellColor(DungeonLayout layout, FloorLayout floor, int i, PreviewMode mode, int maxDist)
        {
            TileGrid g = floor.Grid;
            switch (g.Type[i])
            {
                case CellType.Solid:
                    if (g.IsChasm(i))
                        return new Color32(12, 16, 40, 255);   // the chasm (moving platforms cross it)
                    return g.Has(i, CellFlags.Reserved) ? new Color32(22, 22, 26, 255) : new Color32(42, 40, 44, 255);
                case CellType.Link:
                    return new Color32(255, 0, 255, 255);
            }

            if (g.IsBridge(i))
                return new Color32(150, 105, 60, 255);
            Area a = floor.AreaAt(i);
            switch (mode)
            {
                case PreviewMode.Styles:
                    return g.Has(i, CellFlags.Organic) ? StyleColor(ZoneStyle.Cavern) : (a != null ? StyleColor(a.Style) : new Color32(150, 150, 150, 255));
                case PreviewMode.Heights:
                {
                    float amp = Mathf.Max(0.01f, layout.FloorSpacing * 0.15f);
                    float t = Mathf.InverseLerp(-amp, amp, g.FloorHeight[i]);
                    return Color32.Lerp(new Color32(30, 60, 160, 255), new Color32(240, 220, 120, 255), t);
                }
                case PreviewMode.Ceilings:
                {
                    float head = g.CeilingHeight[i] - g.FloorHeight[i];
                    return Color32.Lerp(new Color32(40, 30, 60, 255), new Color32(255, 245, 210, 255), Mathf.Clamp01((head - 2.5f) / 12f));
                }
                case PreviewMode.Distance:
                {
                    int d = floor.DistanceFromArrival != null ? floor.DistanceFromArrival[i] : -1;
                    if (d < 0)
                        return new Color32(255, 0, 0, 255);
                    return Color32.Lerp(new Color32(40, 200, 90, 255), new Color32(120, 20, 160, 255), d / (float)maxDist);
                }
                case PreviewMode.WallDistance:
                {
                    float wd = floor.WallDistance != null ? floor.WallDistance[i] : 1f;
                    return Color32.Lerp(new Color32(60, 30, 20, 255), new Color32(255, 240, 200, 255), Mathf.Clamp01((wd - 1f) / 5f));
                }
                case PreviewMode.Zones:
                    if (floor.Zone != null && floor.ZoneStyles != null)
                        return StyleColor(floor.ZoneStyles[floor.Zone[i]]);
                    return a != null ? StyleColor(a.Style) : new Color32(150, 150, 150, 255);
                default:
                    if (a == null)
                        return g.Has(i, CellFlags.Organic) ? new Color32(120, 95, 70, 255) : new Color32(150, 150, 150, 255);
                    return RoleColor(a);
            }
        }
    }
}
