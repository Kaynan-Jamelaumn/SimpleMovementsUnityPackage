using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The crowd of a pit fight's stands: its figures (the children named "Spectator") sway idly, jump and cheer while
    /// the arena's fight is on, and celebrate when it is won.
    /// </summary>
    public class DungeonSpectators : MonoBehaviour
    {
        [Tooltip("How high they jump when cheering (meters).")]
        [Min(0f)] public float jump = 0.25f;

        private readonly List<Transform> figures = new List<Transform>();
        private readonly List<Vector3> rest = new List<Vector3>();
        private DungeonRoomEvent fight;
        private bool searched;
        private float celebrateUntil;

        private void Start()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>())
                if (t.name == "Spectator")
                {
                    figures.Add(t);
                    rest.Add(t.localPosition);
                }
        }

        private void Update()
        {
            if (!searched)
                Find();
            bool cheering = fight != null && (fight.Current == DungeonRoomEvent.State.Fighting || Time.time < celebrateUntil);
            for (int i = 0; i < figures.Count; i++)
            {
                if (figures[i] == null)
                    continue;
                float phase = i * 1.7f;
                float y = cheering ? Mathf.Abs(Mathf.Sin(Time.time * 6f + phase)) * jump : Mathf.Sin(Time.time * 1.3f + phase) * 0.02f;
                figures[i].localPosition = rest[i] + Vector3.up * y;
            }
        }

        private void Find()
        {
            var tag = GetComponent<DungeonSpawned>();
            if (tag == null || tag.Dungeon == null)
                return;
            searched = true;
            foreach (DungeonSpawned s in tag.Dungeon.SpawnedIn(tag.floor, tag.area))
            {
                var e = s.GetComponent<DungeonRoomEvent>();
                if (e == null)
                    continue;
                fight = e;
                fight.Completed += _ => celebrateUntil = Time.time + 6f;
                break;
            }
        }
    }
}
