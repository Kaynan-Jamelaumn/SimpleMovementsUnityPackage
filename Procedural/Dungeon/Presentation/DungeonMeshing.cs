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
            bool kit = profile.HasTileKit && profile.Build.useTileKit;
            var kitInfo = profile.TileKit;

            void BuildFloor(int i)
            {
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
                    Seed = layout.AttemptSeed * 31 + i,
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
                result[i] = data;
            }

            TerrainWorkerPool pool = TerrainWorkerPool.Current;
            if (pool != null && result.Length > 1)
                pool.For(result.Length, BuildFloor);
            else
                for (int i = 0; i < result.Length; i++)
                    BuildFloor(i);
            return new List<FloorMeshData>(result);
        }
    }
}
