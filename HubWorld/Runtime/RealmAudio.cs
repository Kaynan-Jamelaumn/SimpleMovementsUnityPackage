using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// The music and ambience of where this machine's player is: the City, the World (optionally per zone ring around
    /// the city - calm in the Safe Zone, tense in the Dangerous Zone) or a dungeon. It cross-fades through the
    /// <see cref="SoundManager"/> (one is created when the scene has none) whenever the player changes realm or zone.
    /// <para>Put one in the City scene (Tools ▸ SimpleMovements ▸ Scene ▸ Create Realm Music); it survives scene changes
    /// with the SoundManager. Each machine plays the music of its own player; a dedicated server plays nothing.</para>
    /// </summary>
    public class RealmAudio : MonoBehaviour
    {
        [Header("City")]
        public AudioClip cityMusic;
        public AudioClip cityAmbience;

        [Header("World")]
        [Tooltip("Music in the open world (used for every ring without its own music below).")]
        public AudioClip worldMusic;
        [Tooltip("Music per zone ring around the city, in ring order (0 = Safe Zone, 1 = Frontier...). Empty entries use World Music.")]
        public AudioClip[] worldZoneMusic = new AudioClip[0];
        public AudioClip worldAmbience;

        [Header("Dungeons")]
        public AudioClip dungeonMusic;
        public AudioClip dungeonAmbience;

        [Header("Fading")]
        [Tooltip("Seconds of cross-fade between two pieces of music.")]
        [Min(0f)] public float fadeTime = 2.5f;
        [Tooltip("Seconds between two checks of the player's realm and zone.")]
        [Min(0.1f)] public float checkInterval = 0.5f;

        private AudioClip currentMusic, currentAmbience;
        private float nextCheck;

        private void Awake()
        {
            if (GameNet.NetAuthority.IsHeadless)
            {
                enabled = false;
                return;
            }
            if (SoundManager.Instance == null && FindAnyObjectByType<SoundManager>() == null)
                new GameObject("Sound Manager").AddComponent<SoundManager>();
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + checkInterval;
            SoundManager sound = SoundManager.Instance;
            if (sound == null)
                return;
            Pick(out AudioClip music, out AudioClip ambience);
            if (music != currentMusic)
            {
                currentMusic = music;
                if (music != null) sound.PlayMusic(music, true, fadeTime);
                else sound.StopMusic(true, fadeTime);
            }
            if (ambience != currentAmbience)
            {
                currentAmbience = ambience;
                if (ambience != null) sound.PlayAmbient(ambience, true, fadeTime);
                else sound.StopAmbient(true, fadeTime);
            }
        }

        private void Pick(out AudioClip music, out AudioClip ambience)
        {
            music = currentMusic;
            ambience = currentAmbience;
            CombatEntity local = PlayerLocator.Local;
            if (local == null)
                return;
            if (ProceduralDungeon.DungeonSession.RunOf(local.gameObject) != null)
            {
                music = dungeonMusic;
                ambience = dungeonAmbience;
                return;
            }
            PlayerRealmState s = PlayerRealmState.Of(local);
            RealmKind realm = s != null ? s.Realm : RealmKind.City;
            switch (realm)
            {
                case RealmKind.World:
                    music = WorldMusicAt(local.transform.position);
                    ambience = worldAmbience;
                    break;
                case RealmKind.Dungeon:
                    music = dungeonMusic;
                    ambience = dungeonAmbience;
                    break;
                case RealmKind.City:
                    music = cityMusic;
                    ambience = cityAmbience;
                    break;
                default:
                    break; // travelling: keep what plays
            }
        }

        private AudioClip WorldMusicAt(Vector3 position)
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            if (director != null && director.Zones != null && worldZoneMusic != null && worldZoneMusic.Length > 0)
            {
                int ring = director.Zones.At(position).index;
                if (ring >= 0)
                {
                    AudioClip c = worldZoneMusic[Mathf.Min(ring, worldZoneMusic.Length - 1)];
                    if (c != null)
                        return c;
                }
            }
            return worldMusic;
        }
    }
}
