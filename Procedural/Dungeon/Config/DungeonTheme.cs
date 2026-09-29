using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// How a dungeon looks. Everything is optional: with an empty theme the builder generates meshes with flat-colour
    /// materials and primitive props. Give it materials to texture the generated meshes; give it a tile kit (floor, wall,
    /// ceiling prefabs) to build constructed areas from your own modular pieces; give it portal / door prefabs to replace
    /// the primitive stand-ins.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Dungeon/Dungeon Theme", fileName = "DungeonTheme")]
    public class DungeonTheme : ScriptableObject
    {
        [Header("Materials (empty = generated flat colours)")]
        [Tooltip("Material of generated floors in built areas (rooms, corridors). Tiling uses Texture Scale. Empty = Built Floor Color.")]
        public Material builtFloor;
        [Tooltip("Material of generated walls in built areas. Empty = Built Wall Color.")]
        public Material builtWall;
        [Tooltip("Material of generated ceilings in built areas. Empty = Built Ceiling Color.")]
        public Material builtCeiling;
        [Tooltip("Material of cave floors. Empty = Cave Floor Color.")]
        public Material caveFloor;
        [Tooltip("Material of cave walls. Empty = Cave Wall Color.")]
        public Material caveWall;
        [Tooltip("Material of cave ceilings. Empty = Cave Ceiling Color.")]
        public Material caveCeiling;
        [Tooltip("Material of stairs and stair wells. Empty = Stairs Color.")]
        public Material stairs;
        [Tooltip("Material of door frames and other trim. Empty = Trim Color.")]
        public Material trim;
        [Tooltip("Meters per texture repeat on generated meshes (world-aligned UVs, so textures line up across chunks).")]
        [Min(0.1f)] public float textureScale = 3f;

        [Header("Fallback colours (used where a material above is empty)")]
        [Tooltip("Colour of built floors without a material.")]
        public Color builtFloorColor = new Color(0.36f, 0.34f, 0.31f);
        [Tooltip("Colour of built walls without a material.")]
        public Color builtWallColor = new Color(0.52f, 0.49f, 0.44f);
        [Tooltip("Colour of built ceilings without a material.")]
        public Color builtCeilingColor = new Color(0.28f, 0.27f, 0.25f);
        [Tooltip("Colour of cave floors without a material.")]
        public Color caveFloorColor = new Color(0.30f, 0.26f, 0.22f);
        [Tooltip("Colour of cave walls without a material.")]
        public Color caveWallColor = new Color(0.38f, 0.33f, 0.28f);
        [Tooltip("Colour of cave ceilings without a material.")]
        public Color caveCeilingColor = new Color(0.24f, 0.21f, 0.18f);
        [Tooltip("Colour of stairs without a material.")]
        public Color stairsColor = new Color(0.45f, 0.42f, 0.38f);
        [Tooltip("Colour of trim (door frames) without a material.")]
        public Color trimColor = new Color(0.25f, 0.18f, 0.12f);

        [Header("Tile kit (optional; replaces generated geometry in built areas)")]
        [Tooltip("REQUIRED for the tile kit (with Wall Segment). Floor tile one module square, pivot at its centre, top surface at y = 0. It needs its own collider (the NavMesh is baked from colliders).")]
        public GameObject floorTile;
        [Tooltip("REQUIRED for the tile kit (with Floor Tile). Wall segment one module wide, pivot at the bottom centre of its face, facing +z (into the room). Needs a collider.")]
        public GameObject wallSegment;
        [Tooltip("Optional. Ceiling tile one module square, pivot at its centre, bottom surface at y = 0. Empty = generated ceilings.")]
        public GameObject ceilingTile;
        [Tooltip("Optional. Door frame placed at door cells, pivot at the bottom centre, opening along z. Empty = generated frames.")]
        public GameObject doorFrame;
        [Tooltip("Optional. Pillar for pillar cells (pillared halls, templates), pivot at the bottom centre.")]
        public GameObject pillar;
        [Tooltip("Size of one tile-kit module (meters) - the width of Wall Segment. Pieces are scaled to the profile's Cell Size (set Cell Size equal to it to avoid scaling).")]
        [Min(0.1f)] public float moduleSize = 2f;
        [Tooltip("Height of the Wall Segment prefab (meters): used to stretch walls to the ceiling.")]
        [Min(0.1f)] public float wallPrefabHeight = 4f;
        [Tooltip("Stretch wall segments vertically to the room's ceiling height. Off = walls keep their own height.")]
        public bool scaleWallsToCeiling = true;

        [Header("Special objects (empty = primitive stand-ins)")]
        [Tooltip("Optional. The portal the player arrives through (placed against the entrance room's back wall). It gets a DungeonPortal (leave) and a trigger if it has none.")]
        public GameObject entrancePortal;
        [Tooltip("Optional. The exit portal on the last floor (completes the dungeon). It gets a DungeonPortal and a trigger if it has none.")]
        public GameObject exitPortal;
        [Tooltip("Optional. A door leaf placed in door frames.")]
        public GameObject door;
        [Tooltip("Optional. The wall piece hiding a secret passage (it opens when the player comes close). Empty = a primitive wall block.")]
        public GameObject secretDoor;

        [Header("Lights")]
        [Tooltip("Colour of torch and brazier lights.")]
        public Color torchColor = new Color(1f, 0.62f, 0.3f);
        [Tooltip("Intensity of torch lights.")]
        [Min(0f)] public float torchIntensity = 2.2f;
        [Tooltip("Range of torch lights (meters). Many long-range lights cost performance.")]
        [Min(0f)] public float torchRange = 9f;
        [Tooltip("Colour of cave crystals' glow.")]
        public Color crystalColor = new Color(0.35f, 0.75f, 1f);
        [Tooltip("Colour of the entrance portal's glow.")]
        public Color portalColor = new Color(0.45f, 0.35f, 1f);
        [Tooltip("Colour of the exit portal's glow.")]
        public Color exitPortalColor = new Color(0.3f, 1f, 0.55f);

        [Header("Atmosphere (applied while the player is in the dungeon)")]
        [Tooltip("Replace the scene's ambient light and fog (and switch off directional lights - the sun) while inside; restored on leaving.")]
        public bool applyAtmosphere = true;
        [Tooltip("Ambient light inside (dark keeps torches meaningful).")]
        public Color ambientLight = new Color(0.12f, 0.11f, 0.13f);
        [Tooltip("Use fog inside.")]
        public bool fog = true;
        [Tooltip("Fog colour inside.")]
        public Color fogColor = new Color(0.05f, 0.045f, 0.06f);
        [Tooltip("Fog density (exponential squared). 0.02-0.05 hides far corridors.")]
        [Min(0f)] public float fogDensity = 0.035f;

        public bool HasTileKit => floorTile != null && wallSegment != null;
    }
}
