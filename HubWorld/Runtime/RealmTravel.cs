using ProceduralDungeon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HubWorld
{
    /// <summary>
    /// The mechanics of moving a player between scenes: freezing them, holding them while the destination loads, moving
    /// their whole object into another scene and placing them. Decisions (who may go where) are the
    /// <see cref="HubWorldDirector"/>'s; this only carries them out.
    /// </summary>
    /// <remarks>
    /// The player's top-level object moves with everything under it (camera, UI...) - keep what belongs to the player
    /// under one root, or in DontDestroyOnLoad. A scene that is unloaded destroys what is left in it, so players are
    /// always moved out first: while their destination loads they wait in the DontDestroyOnLoad scene.
    /// </remarks>
    public static class RealmTravel
    {
        /// <summary>The object that moves a player (the one with the CharacterController).</summary>
        public static GameObject Mover(CombatEntity player) => PlayerLocator.MovableRoot(player);

        /// <summary>Switches the player's movement off (or back on).</summary>
        public static void SetControls(CombatEntity player, bool on)
        {
            GameObject mover = Mover(player);
            if (mover != null)
                DungeonSession.SetControls(mover, on);
        }

        /// <summary>Holds a player outside every realm scene (so no unload can destroy them), frozen.</summary>
        public static void Hold(CombatEntity player)
        {
            GameObject mover = Mover(player);
            if (mover == null)
                return;
            DungeonSession.SetControls(mover, false);
            GameObject root = mover.transform.root.gameObject;
            // Networked players stay in a scene the network knows; the server only unloads a scene once its players left.
            if (root.scene.name != "DontDestroyOnLoad" && GameNet.NetScenes.CanPark(root))
                Object.DontDestroyOnLoad(root);
        }

        /// <summary>Moves the player's top-level object into <paramref name="scene"/> (no-op when already there).</summary>
        public static void MoveToScene(CombatEntity player, Scene scene)
        {
            GameObject mover = Mover(player);
            if (mover != null)
                DungeonSession.MoveToScene(mover, scene);
        }

        /// <summary>Puts the player at a pose at once.</summary>
        public static void Place(CombatEntity player, Pose pose)
        {
            GameObject mover = Mover(player);
            if (mover == null)
                return;
            DungeonSession.Teleport(mover, pose.position, pose.rotation);
            if (player.PlayerMovement != null)
                player.PlayerMovement.VerticalVelocity = 0f;
            // A server tells the movement explicitly (short moves, like the arrival spread, are below its detection
            // distance): the teleport then travels in the reconcile and every machine snaps the player there.
            if (GameNet.NetAuthority.IsNetworked && GameNet.NetAuthority.IsServer)
            {
                MovementStateMachine movement = mover.GetComponentInChildren<MovementStateMachine>(true);
                if (movement != null)
                    movement.NotifyTeleported();
            }
        }

        /// <summary>
        /// Moves a player into <paramref name="scene"/>, places them and gives them their controls back (unless
        /// <paramref name="controls"/> is false: a server keeps a remote player frozen until their client has arrived too).
        /// With <paramref name="local"/> the scene also becomes the active one (its lighting is used and runtime objects
        /// are created in it) - single player only: a server's active scene stays the City.
        /// </summary>
        public static void Arrive(CombatEntity player, Scene scene, Pose pose, bool local, bool controls = true)
        {
            MoveToScene(player, scene);
            Place(player, pose);
            SetControls(player, controls);
            if (local && scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() != scene)
                SceneManager.SetActiveScene(scene);
        }

        /// <summary>
        /// The ground under (x, z), searched from high above: true with its height once a collider exists there (terrain
        /// chunks are generated in the background after the world scene loads, colliders last).
        /// </summary>
        public static bool TryFindGround(Vector3 near, out float height)
        {
            height = near.y;
            var from = new Vector3(near.x, near.y + 800f, near.z);
            int mask = CombatSettings.Instance.groundLayers;
            if (!ScenePhysics.Raycast(from, Vector3.down, out RaycastHit hit, 2000f, mask, QueryTriggerInteraction.Ignore))
                return false;
            height = hit.point.y;
            return true;
        }

        /// <summary>
        /// Like <see cref="TryFindGround"/>, but only ground belonging to <paramref name="scene"/> counts (a client that still
        /// has the scene it left loaded must not take that scene's floor for the destination's).
        /// </summary>
        public static bool TryFindGroundIn(Vector3 near, Scene scene, out float height)
        {
            height = near.y;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;
            var from = new Vector3(near.x, near.y + 800f, near.z);
            int mask = CombatSettings.Instance.groundLayers;
            // Every hit along the ray: the scene left behind may still stand at the same place (the City and the World's
            // copy of it overlap), its floor must not hide the destination's.
            int count = scene.GetPhysicsScene().Raycast(from, Vector3.down, groundHits, 2000f, mask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = groundHits[i].collider;
                if (c == null || c.gameObject.scene != scene || (found && groundHits[i].point.y <= height))
                    continue;
                height = groundHits[i].point.y;
                found = true;
            }
            return found;
        }

        private static readonly RaycastHit[] groundHits = new RaycastHit[16];

        /// <summary>The best guess of the ground height at (x, z) without a collider: the terrain's data, else <paramref name="near"/>.y.</summary>
        public static float GroundGuess(Vector3 near) => LoadedTerrain.TryGetHeight(near.x, near.z, out float h) ? h : near.y;
    }
}
