using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Changes the ambient light and fog when the players move to a floor with a modifier (flooded, molten, overgrown,
    /// dark, frozen) and back to the theme's when they leave it. Added to the dungeon root by the builder.
    /// </summary>
    [RequireComponent(typeof(DungeonInstance))]
    public class DungeonFloorAtmosphere : MonoBehaviour
    {
        private DungeonInstance dungeon;
        private int applied = -1;

        private void Awake() => dungeon = GetComponent<DungeonInstance>();

        private void Update()
        {
            if (dungeon == null || dungeon.Layout == null || dungeon.Profile == null || !DungeonSession.InDungeon || DungeonSession.Current != dungeon)
                return;
            if (dungeon.Profile.FloorModifiers != null && !dungeon.Profile.FloorModifiers.atmosphere)
                return;
            int floor = dungeon.CurrentFloor;
            if (floor == applied)
                return;
            applied = floor;
            DungeonAtmosphere.ApplyModifier(dungeon.Profile.Theme, dungeon.ModifierOf(floor));
        }
    }
}
