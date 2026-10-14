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
        // A client's copy applied the atmosphere itself (no run releases it there).
        private bool appliedHere;

        private void Awake() => dungeon = GetComponent<DungeonInstance>();

        private void Update()
        {
            if (dungeon == null || dungeon.Layout == null || dungeon.Profile == null)
                return;
            // The lighting is the local player's: only their dungeon changes it (never on a dedicated server).
            if (!LocalPlayerInside())
            {
                if (appliedHere)
                    ReleaseHere();
                return;
            }
            if (dungeon.Profile.FloorModifiers != null && !dungeon.Profile.FloorModifiers.atmosphere)
            {
                // Still the theme's own atmosphere for a client's copy (the server's run applies it on arrival offline).
                if (dungeon.IsReplica && !appliedHere)
                {
                    DungeonAtmosphere.Apply(dungeon.Profile.Theme);
                    appliedHere = true;
                }
                return;
            }
            int floor = dungeon.CurrentFloor;
            if (floor == applied)
                return;
            applied = floor;
            DungeonAtmosphere.ApplyModifier(dungeon.Profile.Theme, dungeon.ModifierOf(floor));
            appliedHere |= dungeon.IsReplica;
        }

        /// <summary>Is this machine's player inside this dungeon (single player: is the run on)?</summary>
        private bool LocalPlayerInside()
        {
            CombatEntity local = PlayerLocator.Local;
            if (dungeon.IsReplica)
                return local != null && local.gameObject.scene == dungeon.gameObject.scene;
            DungeonRun run = DungeonSession.RunOf(dungeon);
            if (run == null || !run.IsInside)
                return false;
            if (local != null)
                return run.HasLocalPlayer;
            return !GameNet.NetAuthority.IsNetworked;
        }

        private void ReleaseHere()
        {
            appliedHere = false;
            applied = -1;
            DungeonAtmosphere.Release(false);
        }

        private void OnDestroy()
        {
            if (appliedHere)
                ReleaseHere();
        }
    }
}
