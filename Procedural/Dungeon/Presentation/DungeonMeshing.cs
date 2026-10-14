using System.Collections.Generic;

namespace ProceduralDungeon
{
    /// <summary>
    /// Builds the mesh data of a whole dungeon (every floor, plus stair, drop, climb and spiral geometry attached to the floor
    /// below - a spiral stack's top to the top floor)
    /// - pure data, run on the worker thread right after generation, floors in parallel when on a worker pool.
    /// </summary>
    public static class DungeonMeshing
    {
        public static List<FloorMeshData> BuildAll(DungeonLayout layout, CompiledProfile profile)
        {
            var result = new FloorMeshData[layout.Floors.Count];

            void Build(int i) => result[i] = BuildFloor(layout, profile, i);

            TerrainWorkerPool pool = TerrainWorkerPool.Current;
            if (pool != null && result.Length > 1)
                pool.For(result.Length, Build);
            else
                for (int i = 0; i < result.Length; i++)
                    Build(i);
            return new List<FloorMeshData>(result);
        }

        /// <summary>
        /// The mesh data of one floor (with the stairs, drops, climbs and spirals attached to it). Needs that floor and,
        /// for its stairs, the floor above - which is all a network client has when a floor reaches it.
        /// </summary>
        public static FloorMeshData BuildFloor(DungeonLayout layout, CompiledProfile profile, int i)
        {
            bool kit = profile.HasTileKit && profile.Build.useTileKit;
            var kitInfo = profile.TileKit;
            FloorLayout floor = layout.Floors[i];
            var options = new DungeonMesher.Options
            {
                CellSize = layout.CellSize,
                ChunkCells = profile.Build.meshChunkCells,
                Ceilings = profile.Build.buildCeilings,
                DoorFrames = profile.Build.doorFrames,
                TextureScale = profile.TextureScale,
                WallRoughness = profile.Caves.wallRoughness,
                WallBulge = profile.Caves.wallBulge,
                Seed = layout.FloorBuildSeed(i),
                DoorHeight = profile.Ceilings != null ? profile.Ceilings.doorHeight : 2.6f,
                WaterLevel = floor.Spec.Modifier == FloorModifier.Flooded && profile.FloorModifiers != null ? profile.FloorModifiers.waterLevel : float.NaN,
                SkipBuiltFloors = kit && kitInfo.HasFloor,
                SkipBuiltWalls = kit && kitInfo.HasWall,
                SkipBuiltCeilings = kit && kitInfo.HasCeiling,
                SkipBuiltDoorFrames = kit && kitInfo.HasDoorFrame,
            };
            FloorMeshData data = DungeonMesher.Build(floor, options);
            float newel = profile.Tower != null ? profile.Tower.newelRadius : 1.1f;
            foreach (VerticalLink link in layout.Links)
            {
                if (link == null)
                    continue;   // a client's layout only holds the links of the floors it was sent
                if (link.Kind == LinkKind.Spiral)
                {
                    if (link.LowerFloor == i)
                        data.Links.Add(LinkMesher.BuildSpiral(layout, link, profile.TextureScale, newel, options.DoorHeight));
                    if (link.UpperFloor == i && link.Above < 0)
                        data.Links.Add(LinkMesher.BuildSpiralTop(layout, link, profile.TextureScale, newel, options.DoorHeight));
                }
                else if (link.LowerFloor == i)
                {
                    data.Links.Add(LinkMesher.Build(layout, link, profile.TextureScale));
                }
            }
            return data;
        }
    }
}
