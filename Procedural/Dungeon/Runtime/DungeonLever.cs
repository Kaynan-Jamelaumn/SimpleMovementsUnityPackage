using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A lever on a wall. Pulling it either opens a secret door (the wine cellar's hidden room: the door in cell
    /// <see cref="DungeonSpawned.link"/> of its floor, which can't be found by searching) or shuts a gas chamber's
    /// valve (the <see cref="DungeonGasCloud"/> of area <see cref="DungeonSpawned.link"/>). Once.
    /// </summary>
    public class DungeonLever : DungeonInteractable
    {
        public enum LeverAction { OpenSecretDoor, CloseGasValve }

        [Tooltip("What pulling it does (set by the builder).")]
        public LeverAction action = LeverAction.OpenSecretDoor;
        [Tooltip("The handle that tilts when pulled (child named \"Handle\" by default).")]
        public Transform handle;

        public static event Action<DungeonLever> AnyPulled;

        private float pulled = -1f;

        private void Start()
        {
            if (handle == null)
                handle = transform.Find("Handle");
        }

        protected override void Update()
        {
            base.Update();
            if (pulled >= 0f && handle != null)
                handle.localRotation = Quaternion.Slerp(handle.localRotation, Quaternion.Euler(-40f, 0f, 0f), Time.deltaTime * 8f);
        }

        protected override bool Use(CombatEntity player)
        {
            var tag = GetComponent<DungeonSpawned>();
            DungeonInstance d = Dungeon;
            if (tag == null || d == null)
                return false;
            pulled = Time.time;
            if (action == LeverAction.OpenSecretDoor)
            {
                DungeonSecretDoor door = d.SecretDoorAt(tag.floor, tag.link);
                if (door != null && !door.IsOpen)
                {
                    door.Open();
                    DungeonMessages.Show("Somewhere nearby, stone grinds against stone...", true);
                }
            }
            else
            {
                foreach (DungeonSpawned s in d.SpawnedIn(tag.floor, tag.link))
                {
                    var gas = s.GetComponent<DungeonGasCloud>();
                    if (gas != null)
                        gas.Shut();
                }
                DungeonMessages.Show("The valve groans shut. The gas stops.", true);
            }
            AnyPulled?.Invoke(this);
            return true;
        }

        protected override void OnUsedUp() { }
    }
}
