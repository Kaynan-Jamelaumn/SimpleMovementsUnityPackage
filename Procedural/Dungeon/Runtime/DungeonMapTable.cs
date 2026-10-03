using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A map room's carved table: studying it (Interact, or standing at it a moment) reveals the floor's map. The map
    /// is shown at once and from then on with the Map key (<see cref="DungeonMapOverlay"/>, added to the dungeon if it
    /// has none).
    /// </summary>
    public class DungeonMapTable : DungeonInteractable
    {
        protected override bool Use(CombatEntity player)
        {
            var tag = GetComponent<DungeonSpawned>();
            DungeonInstance d = Dungeon;
            if (tag == null || d == null)
                return false;
            if (d.IsMapped(tag.floor))
            {
                DungeonMapOverlay.For(d).Show(tag.floor, 6f);
                return false;
            }
            d.RevealMap(tag.floor);
            DungeonMapOverlay overlay = DungeonMapOverlay.For(d);
            overlay.Show(tag.floor, 8f);
            DungeonMessages.Show($"You study the carved map of this floor. ({overlay.KeyName} shows it again.)", true);
            return true;
        }

        protected override void OnUsedUp() { }
    }
}
