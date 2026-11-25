using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The generated layouts and mesh data of recently built dungeons (see <see cref="BuildSettings.keepGenerated"/>):
    /// entering one of them again - the same cave entrance, the same portal - skips the worker-thread generation and
    /// meshing, and only the scene objects are built again. A layout is never changed after generation (runtime code only
    /// reads it), so a kept one is as good as a new one. Main thread only.
    /// <para>The key is everything a generation depends on: the profile (and its edit revision), the request (seed, size,
    /// difficulty, floor count, style, depth) and whether the floors were meshed with a network server's keyed build seeds.
    /// Kept generations of a profile beyond its limit are dropped oldest first.</para>
    /// </summary>
    public static class DungeonGenerationCache
    {
        private sealed class Entry
        {
            public string Key;
            public int ProfileId;
            public DungeonLayout Layout;
            public List<FloorMeshData> Meshes;
            public long LastUsed;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static long clock;

        /// <summary>Generations currently kept (all profiles).</summary>
        public static int Count => entries.Count;

        /// <summary>Times a kept generation was reused since the game started (diagnostics).</summary>
        public static int Hits { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
            clock = 0;
            Hits = 0;
        }

        /// <summary>The key of a generation of <paramref name="profile"/> for <paramref name="request"/>.</summary>
        public static string KeyFor(DungeonProfile profile, DungeonRequest request, bool networkSeeds)
        {
            if (request == null)
                return null;
            int id = profile != null ? profile.GetInstanceID() : 0;
            return string.Join("|", id.ToString(), DungeonProfile.Revision.ToString(), request.seed.ToString(), ((int)request.size).ToString(),
                Mathf.RoundToInt(request.difficulty * 1000f).ToString(), request.floorCount.ToString(), request.overrideStyle ? ((int)request.style).ToString() : "-",
                request.depth.ToString(), networkSeeds ? "net" : "local");
        }

        /// <summary>A kept generation for <paramref name="key"/> (its layout and mesh data, to build again).</summary>
        public static bool TryGet(string key, out DungeonLayout layout, out List<FloorMeshData> meshes)
        {
            layout = null;
            meshes = null;
            if (string.IsNullOrEmpty(key))
                return false;
            foreach (Entry e in entries)
            {
                if (e.Key != key)
                    continue;
                e.LastUsed = ++clock;
                layout = e.Layout;
                meshes = e.Meshes;
                Hits++;
                return true;
            }
            return false;
        }

        /// <summary>Keeps a generation (at most <paramref name="perProfile"/> of the same profile; 0 = keeps nothing).</summary>
        public static void Put(string key, DungeonProfile profile, DungeonLayout layout, List<FloorMeshData> meshes, int perProfile)
        {
            if (string.IsNullOrEmpty(key) || layout == null || meshes == null || perProfile <= 0)
                return;
            int id = profile != null ? profile.GetInstanceID() : 0;
            entries.RemoveAll(e => e.Key == key);
            entries.Add(new Entry { Key = key, ProfileId = id, Layout = layout, Meshes = meshes, LastUsed = ++clock });
            int count = 0;
            foreach (Entry e in entries)
                if (e.ProfileId == id)
                    count++;
            while (count > perProfile)
            {
                Entry oldest = null;
                foreach (Entry e in entries)
                    if (e.ProfileId == id && (oldest == null || e.LastUsed < oldest.LastUsed))
                        oldest = e;
                if (oldest == null)
                    break;
                entries.Remove(oldest);
                count--;
            }
        }

        /// <summary>Forgets every kept generation (of one profile, or all with null).</summary>
        public static void Clear(DungeonProfile profile = null)
        {
            if (profile == null)
            {
                entries.Clear();
                return;
            }
            int id = profile.GetInstanceID();
            entries.RemoveAll(e => e.ProfileId == id);
        }
    }
}
