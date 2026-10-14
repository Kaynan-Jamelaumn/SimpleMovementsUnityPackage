using System;
using System.Security.Cryptography;

namespace GameNet
{
    /// <summary>
    /// Per-floor "build seeds" for network sessions: the only randomness the dungeon's presentation needs (the cave wall
    /// jitter and bulges of the floor meshes). The server meshes its own floors with them and sends each one to the
    /// clients together with its floor, so both build the same geometry and colliders.
    /// </summary>
    /// <remarks>
    /// A build seed is HMAC-SHA256(key, dungeon attempt seed, floor) where the key is 32 random bytes the server
    /// process picks at start and never sends. A plain hash of the dungeon seed would not be safe: the seed has only
    /// 2^32 values, so a client could hash every candidate and find the one that matches a received build seed, then
    /// generate the whole dungeon (future floors, loot, mobs). With the secret key in the hash every guess would also
    /// need the 256-bit key, so the build seed tells nothing about the dungeon seed, and it is useless for the
    /// generator anyway (the pipeline never reads it). Single player keeps its old mesh seeds (no network, nothing to hide).
    /// </remarks>
    public static class DungeonBuildSeed
    {
        private static readonly object Gate = new object();
        private static byte[] processKey;

        /// <summary>The server's secret key (created on first use; never leaves the process).</summary>
        private static byte[] ProcessKey
        {
            get
            {
                lock (Gate)
                {
                    if (processKey == null)
                    {
                        processKey = new byte[32];
                        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                            rng.GetBytes(processKey);
                    }
                    return processKey;
                }
            }
        }

        /// <summary>The build seed of <paramref name="floor"/> for a dungeon generated from <paramref name="attemptSeed"/> (thread-safe).</summary>
        public static int ForFloor(int attemptSeed, int floor) => ForFloor(ProcessKey, attemptSeed, floor);

        /// <summary>The build seeds of every floor of a dungeon (thread-safe).</summary>
        public static int[] ForFloors(int attemptSeed, int floors)
        {
            var seeds = new int[Math.Max(0, floors)];
            byte[] key = ProcessKey;
            for (int i = 0; i < seeds.Length; i++)
                seeds[i] = ForFloor(key, attemptSeed, i);
            return seeds;
        }

        /// <summary>The keyed hash itself (exposed for tests): never 0.</summary>
        public static int ForFloor(byte[] key, int attemptSeed, int floor) => Keyed(key, attemptSeed, floor, 0x4D657368); // "Mesh"

        /// <summary>
        /// Network sessions: the seed a dungeon is really generated from, for a request seed clients may be able to work
        /// out themselves (a world portal's seed comes from the world seed, which clients know, and its position). Keyed
        /// with the server's secret, so the same portal still leads to the same dungeon on this server, but no client can
        /// regenerate it. 0 stays 0 (a random dungeon).
        /// </summary>
        public static int SecretDungeonSeed(int requestSeed) => requestSeed == 0 ? 0 : SecretDungeonSeed(ProcessKey, requestSeed);

        /// <summary><see cref="SecretDungeonSeed(int)"/> with an explicit key (exposed for tests): never 0.</summary>
        public static int SecretDungeonSeed(byte[] key, int requestSeed) => Keyed(key, requestSeed, 0, 0x44756E67); // "Dung"

        private static int Keyed(byte[] key, int a, int b, int tag)
        {
            if (key == null || key.Length == 0)
                throw new ArgumentException("A key is required.", nameof(key));
            var message = new byte[12];
            WriteInt(message, 0, a);
            WriteInt(message, 4, b);
            WriteInt(message, 8, tag);   // one tag per purpose: values made for different purposes are unrelated
            byte[] mac;
            using (var hmac = new HMACSHA256(key))
                mac = hmac.ComputeHash(message);
            int seed = mac[0] | (mac[1] << 8) | (mac[2] << 16) | (mac[3] << 24);
            return seed == 0 ? 1 : seed;
        }

        private static void WriteInt(byte[] into, int at, int value)
        {
            into[at] = (byte)value;
            into[at + 1] = (byte)(value >> 8);
            into[at + 2] = (byte)(value >> 16);
            into[at + 3] = (byte)(value >> 24);
        }
    }
}
