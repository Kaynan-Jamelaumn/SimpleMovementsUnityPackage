using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>The volume sliders: everything, and the four groups of sound under it.</summary>
public enum AudioBus
{
    Master,
    Music,
    SFX,
    Ambient,
    Voice
}

/// <summary>
/// The one place a sound is played from. Every system of the game (abilities, weapons, injuries, mobs, stations, the
/// weather, the UI...) plays through here, so all of it:
/// <list type="bullet">
/// <item>follows the volume sliders - through the Audio Mixer when the <see cref="SoundManager"/> has one with the
/// exposed parameters, and in software when it has none (the sliders then scale the sounds themselves);</item>
/// <item>goes out of the mixer group of its <see cref="AudioCategory"/>, so effects and snapshots of the mixer apply;</item>
/// <item>reuses a small pool of sources instead of creating and destroying an object per sound.</item>
/// </list>
/// Nothing needs to be set up: with no SoundManager and no saved volume, a sound plays exactly as
/// <see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3, float)"/> or <see cref="AudioSource.PlayOneShot(AudioClip, float)"/>
/// did.
/// <para>Three ways in: <see cref="Play(AudioClip, Vector3, float, float, AudioCategory, float, float)"/> for a sound
/// at a place, <see cref="PlayOn"/> for a sound on a character's own source (it follows the character), and
/// <see cref="SetLevel"/> for a source the code keeps and fades itself (a loop, the music).</para>
/// </summary>
public static class GameAudio
{
    public const int BusCount = 5;

    /// <summary>How many one-shot sounds can play at once; the oldest is cut when a new one needs its source.</summary>
    public static int MaxVoices = 32;

    /// <summary>Raised when a slider moved (or the mixer started or stopped handling a bus).</summary>
    public static event Action VolumesChanged;

    private static readonly string[] PrefKeys = { "MasterVolume", "MusicVolume", "SFXVolume", "AmbientVolume", "VoiceVolume" };
    private static readonly float[] volumes = { 1f, 1f, 1f, 1f, 1f };
    private static readonly bool[] mixed = new bool[BusCount];
    private static bool loaded;

    // ------------------------------------------------------------------ one-shot voices
    private sealed class Voice
    {
        public AudioSource source;
        public Transform transform;
        public float startedAt;
    }

    private static readonly List<Voice> voices = new List<Voice>(16);
    private static Transform host;

    // ------------------------------------------------------------------ sources the code keeps (loops, music)
    private struct Tracked
    {
        public float level;
        public AudioCategory category;
    }

    private static readonly Dictionary<AudioSource, Tracked> tracked =
        new Dictionary<AudioSource, Tracked>(ReferenceComparer<AudioSource>.Instance);
    private static readonly List<AudioSource> sweep = new List<AudioSource>(32);

    // ------------------------------------------------------------------ the listener (everything heard)
    private static readonly Dictionary<object, float> listenerFactors = new Dictionary<object, float>();
    private static bool listenerOwned;
    private static float listenerBase = 1f;
    private static AudioListener listener;
    private static float nextListenerSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        for (int i = 0; i < BusCount; i++)
        {
            volumes[i] = 1f;
            mixed[i] = false;
        }
        loaded = false;
        voices.Clear();
        host = null;
        tracked.Clear();
        listenerFactors.Clear();
        listenerOwned = false;
        listenerBase = 1f;
        listener = null;
        nextListenerSearch = 0f;
        VolumesChanged = null;
        MaxVoices = 32;
    }

    /// <summary>False on a headless dedicated server: nobody hears it, so nothing is played or created.</summary>
    public static bool Enabled => !GameNet.NetAuthority.IsHeadless;

    // ================================================================== volumes

    /// <summary>The slider of a bus (0-1). Before anything set it: what was saved, else 1.</summary>
    public static float GetVolume(AudioBus bus)
    {
        EnsureLoaded();
        return volumes[(int)bus];
    }

    /// <summary>
    /// Called by whoever owns the sliders (the SoundManager's volumes, or the settings menu on its own):
    /// <paramref name="linear"/> is the slider (0-1), <paramref name="handledByMixer"/> says the Audio Mixer applies it
    /// (its exposed parameter exists and was set) - when it does not, the sounds are scaled here instead.
    /// </summary>
    public static void SetVolume(AudioBus bus, float linear, bool handledByMixer)
    {
        EnsureLoaded();
        int i = (int)bus;
        linear = Mathf.Clamp01(linear);
        if (Mathf.Approximately(volumes[i], linear) && mixed[i] == handledByMixer)
            return;
        volumes[i] = linear;
        mixed[i] = handledByMixer;
        Reapply();
    }

    /// <summary>Does the Audio Mixer apply this slider (true), or is it applied to the sounds themselves (false)?</summary>
    public static bool HandledByMixer(AudioBus bus) => mixed[(int)bus];

    /// <summary>The slider a kind of sound follows.</summary>
    public static AudioBus BusOf(AudioCategory category)
    {
        switch (category)
        {
            case AudioCategory.Music: return AudioBus.Music;
            case AudioCategory.Ambient: return AudioBus.Ambient;
            case AudioCategory.Voice:
            case AudioCategory.Dialogue: return AudioBus.Voice;
            default: return AudioBus.SFX;
        }
    }

    /// <summary>The mixer group a kind of sound goes out of (null without a SoundManager, or when it has none assigned).</summary>
    public static AudioMixerGroup GroupFor(AudioCategory category)
    {
        SoundManager manager = SoundManager.Instance;
        if (manager == null)
            return null;
        AudioMixerGroup group;
        switch (BusOf(category))
        {
            case AudioBus.Music: group = manager.musicMixerGroup; break;
            case AudioBus.Ambient: group = manager.ambientMixerGroup; break;
            case AudioBus.Voice: group = manager.voiceMixerGroup; break;
            default: group = manager.sfxMixerGroup; break;
        }
        return group != null ? group : null;
    }

    /// <summary>
    /// What a sound of this kind must be multiplied by so that it follows the sliders: 1 when the mixer does it for
    /// this source, else the slider of its bus (and of the master, when the mixer handles the master but this source
    /// does not go through the mixer).
    /// </summary>
    public static float Gain(AudioCategory category, AudioSource source = null)
    {
        EnsureLoaded();
        bool routed = source != null && source.outputAudioMixerGroup != null;
        int bus = (int)BusOf(category);
        float gain = 1f;
        if (!(routed && mixed[bus]))
            gain *= volumes[bus];
        // Not handled by the mixer: the master is on the listener, which every source goes through.
        if (mixed[0] && !routed)
            gain *= volumes[0];
        return gain;
    }

    /// <summary>Gives a source the mixer group of its kind of sound, unless it has one already.</summary>
    public static void Route(AudioSource source, AudioCategory category)
    {
        if (source == null || source.outputAudioMixerGroup != null)
            return;
        AudioMixerGroup group = GroupFor(category);
        if (group != null)
            source.outputAudioMixerGroup = group;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;
        loaded = true;
        // Sliders saved by an earlier session apply from the first sound, before any menu or manager loads them.
        for (int i = 0; i < BusCount; i++)
        {
            if (PlayerPrefs.HasKey(PrefKeys[i]))
                volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeys[i], 1f));
        }
        ApplyListener();
    }

    private static void Reapply()
    {
        ApplyListener();
        sweep.Clear();
        foreach (KeyValuePair<AudioSource, Tracked> kv in tracked)
            sweep.Add(kv.Key);
        for (int i = 0; i < sweep.Count; i++)
        {
            AudioSource source = sweep[i];
            if (source == null)
            {
                tracked.Remove(source);
                continue;
            }
            Tracked t = tracked[source];
            source.volume = Mathf.Clamp01(t.level * Gain(t.category, source));
        }
        sweep.Clear();
        VolumesChanged?.Invoke();
    }

    // ================================================================== sources the code keeps

    /// <summary>
    /// Sets the volume of a source the code keeps (a loop, the music, a fading ambience): <paramref name="level"/> is
    /// what the code wants (0-1), the sliders are applied on top and follow when they move. Use it instead of
    /// <c>source.volume = level</c>, and <see cref="GetLevel"/> instead of reading <c>source.volume</c>.
    /// </summary>
    public static void SetLevel(AudioSource source, float level, AudioCategory category = AudioCategory.SFX)
    {
        if (source == null)
            return;
        if (!tracked.ContainsKey(source))
        {
            if (tracked.Count >= 256)
                DropDestroyed();
            Route(source, category);
        }
        tracked[source] = new Tracked { level = level, category = category };
        source.volume = Mathf.Clamp01(level * Gain(category, source));
    }

    /// <summary>The level last given to <see cref="SetLevel"/> (the source's own volume when it never was).</summary>
    public static float GetLevel(AudioSource source)
    {
        if (source == null)
            return 0f;
        return tracked.TryGetValue(source, out Tracked t) ? t.level : source.volume;
    }

    /// <summary>Stops following a source (it keeps its current volume).</summary>
    public static void Forget(AudioSource source)
    {
        if (!ReferenceEquals(source, null))
            tracked.Remove(source);
    }

    private static void DropDestroyed()
    {
        sweep.Clear();
        foreach (KeyValuePair<AudioSource, Tracked> kv in tracked)
        {
            if (kv.Key == null)
                sweep.Add(kv.Key);
        }
        for (int i = 0; i < sweep.Count; i++)
            tracked.Remove(sweep[i]);
        sweep.Clear();
    }

    // ================================================================== playing

    /// <summary>
    /// Plays a sound at a place in the world. With the defaults it sounds like
    /// <see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3, float)"/> (fully 3D, heard up to 500 m, fading with
    /// the distance), from a pooled source. <paramref name="maxDistance"/> above 0 makes it fade out evenly and be
    /// silent beyond that distance. Returns the source playing it (null when nothing was played).
    /// </summary>
    public static AudioSource Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f,
        AudioCategory category = AudioCategory.SFX, float spatialBlend = 1f, float maxDistance = 0f)
    {
        if (clip == null || !Enabled)
            return null;
        if (!Application.isPlaying)
        {
            AudioSource.PlayClipAtPoint(clip, position, volume);
            return null;
        }
        Voice voice = Acquire();
        if (voice == null)
            return null;
        AudioSource source = voice.source;
        voice.transform.position = position;
        voice.startedAt = Time.unscaledTime;
        source.outputAudioMixerGroup = GroupFor(category);
        source.clip = clip;
        source.pitch = Mathf.Approximately(pitch, 0f) ? 1f : pitch;
        source.loop = false;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.minDistance = 1f;
        if (maxDistance > 0f)
        {
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = Mathf.Max(maxDistance, 1.01f);
        }
        else
        {
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.maxDistance = 500f;
        }
        SetLevel(source, Mathf.Clamp01(volume), category);
        source.Play();
        return source;
    }

    /// <summary>Plays a sound that is not in the world (a menu click, a notification): same in both ears, any distance.</summary>
    public static AudioSource Play2D(AudioClip clip, float volume = 1f, float pitch = 1f, AudioCategory category = AudioCategory.UI) =>
        Play(clip, Vector3.zero, volume, pitch, category, 0f);

    /// <summary>
    /// Plays a sound on a source of the game (a character's, a station's): it stays on that object and keeps the
    /// source's own 3D settings, exactly like <see cref="AudioSource.PlayOneShot(AudioClip, float)"/>, and follows the
    /// sliders. A null source plays nothing.
    /// </summary>
    public static void PlayOn(AudioSource source, AudioClip clip, float volume = 1f, AudioCategory category = AudioCategory.SFX)
    {
        if (source == null || clip == null)
            return;
        Route(source, category);
        // A source whose volume already follows the sliders (SetLevel) must not be scaled twice.
        float gain = tracked.ContainsKey(source) ? 1f : Gain(category, source);
        source.PlayOneShot(clip, volume * gain);
    }

    /// <summary>
    /// Plays on the source when there is one, else at the position: for code that prefers a character's own source
    /// and falls back to the place.
    /// </summary>
    public static void PlayOnOrAt(AudioSource source, AudioClip clip, Vector3 position, float volume = 1f, AudioCategory category = AudioCategory.SFX)
    {
        if (clip == null)
            return;
        if (source != null && source.isActiveAndEnabled)
            PlayOn(source, clip, volume, category);
        else
            Play(clip, position, volume, 1f, category);
    }

    /// <summary>Stops every one-shot sound playing from the pool.</summary>
    public static void StopAll()
    {
        for (int i = 0; i < voices.Count; i++)
        {
            if (voices[i].source != null)
                voices[i].source.Stop();
        }
    }

    /// <summary>One-shot sounds playing right now (for a debug display).</summary>
    public static int ActiveVoices
    {
        get
        {
            int n = 0;
            for (int i = 0; i < voices.Count; i++)
            {
                if (voices[i].source != null && voices[i].source.isPlaying)
                    n++;
            }
            return n;
        }
    }

    private static Voice Acquire()
    {
        if (host == null)
        {
            voices.Clear();
            var go = new GameObject("[GameAudio]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.transform;
        }
        Voice oldest = null;
        for (int i = voices.Count - 1; i >= 0; i--)
        {
            Voice v = voices[i];
            if (v.source == null)
            {
                voices.RemoveAt(i);
                continue;
            }
            if (!v.source.isPlaying)
                return v;
            if (oldest == null || v.startedAt < oldest.startedAt)
                oldest = v;
        }
        if (voices.Count < Mathf.Max(1, MaxVoices))
        {
            var go = new GameObject("Voice " + voices.Count);
            go.transform.SetParent(host, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var created = new Voice { source = source, transform = go.transform };
            voices.Add(created);
            return created;
        }
        if (oldest != null)
            oldest.source.Stop();
        return oldest;
    }

    // ================================================================== the listener

    /// <summary>Where the player's ears are (the scene's Audio Listener). False when there is none.</summary>
    public static bool TryGetListenerPosition(out Vector3 position)
    {
        if (listener == null || !listener.isActiveAndEnabled)
        {
            // Looked for again at most twice a second: scenes change, the camera can be replaced.
            if (Time.unscaledTime >= nextListenerSearch)
            {
                nextListenerSearch = Time.unscaledTime + 0.5f;
                listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
            }
            if (listener == null)
            {
                position = default;
                return false;
            }
        }
        position = listener.transform.position;
        return true;
    }

    /// <summary>
    /// Can a sound heard up to <paramref name="maxDistance"/> metres be heard from <paramref name="position"/>? True
    /// when it has no limit (0) or when nothing tells where the listener is - only a sure "no" skips a sound.
    /// </summary>
    public static bool Audible(Vector3 position, float maxDistance)
    {
        if (maxDistance <= 0f || !TryGetListenerPosition(out Vector3 ears))
            return true;
        return (ears - position).sqrMagnitude <= maxDistance * maxDistance;
    }

    /// <summary>
    /// Scales everything the player hears (0-1) for one reason - a hearing impairment, a stun, being underwater -
    /// without fighting the master slider or another reason: all of them multiply. <see cref="ClearListenerFactor"/>
    /// removes it.
    /// </summary>
    public static void SetListenerFactor(object key, float factor)
    {
        if (key == null)
            return;
        factor = Mathf.Clamp01(factor);
        if (listenerFactors.TryGetValue(key, out float old) && Mathf.Approximately(old, factor))
            return;
        listenerFactors[key] = factor;
        ApplyListener();
    }

    public static void ClearListenerFactor(object key)
    {
        if (key != null && listenerFactors.Remove(key))
            ApplyListener();
    }

    private static void ApplyListener()
    {
        if (!Enabled || !Application.isPlaying)
            return;
        // The master is on the listener when the mixer does not carry it.
        float gain = mixed[0] ? 1f : volumes[0];
        foreach (KeyValuePair<object, float> kv in listenerFactors)
            gain *= kv.Value;
        gain = Mathf.Clamp01(gain);
        bool full = gain >= 0.999f;
        if (!listenerOwned)
        {
            if (full)
                return;
            listenerBase = AudioListener.volume;
            listenerOwned = true;
        }
        if (full)
        {
            AudioListener.volume = listenerBase;
            listenerOwned = false;
            return;
        }
        AudioListener.volume = listenerBase * gain;
    }
}
