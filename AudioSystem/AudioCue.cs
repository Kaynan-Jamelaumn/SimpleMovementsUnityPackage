using UnityEngine;

/// <summary>
/// A sound of the game as one reusable asset: a few clips to vary between, how loud and at what pitch, which volume
/// slider it follows, how far it carries - and optionally the effect and the Animator trigger that go with it. A
/// component names the cue ("hurt", "level up") and plays it; how it sounds is tuned here, once, for everything that
/// uses it. Played through <see cref="GameAudio"/>, so it is pooled and follows the sliders and the mixer.
/// <para>Create ▸ SimpleMovements ▸ Audio ▸ Audio Cue (or select clips and use Audio Cue From Selected Clips).</para>
/// </summary>
[CreateAssetMenu(menuName = "SimpleMovements/Audio/Audio Cue", fileName = "AudioCue")]
public class AudioCue : ScriptableObject
{
    [Header("Sound")]
    [Tooltip("One is picked at random each time (never the same twice in a row when there are several).")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Random loudness taken off each time (0.1 = between 90% and 100% of the volume).")]
    [Range(0f, 0.5f)]
    public float volumeVariation = 0f;

    [Tooltip("Lowest and highest pitch: one between them is picked each time (1 and 1 = always the clip's own pitch).")]
    public Vector2 pitch = new Vector2(1f, 1f);

    [Tooltip("The volume slider and mixer group it follows.")]
    public AudioCategory category = AudioCategory.SFX;

    [Tooltip("1 = a sound in the world (heard from where it plays), 0 = not placed (menus, notifications).")]
    [Range(0f, 1f)]
    public float spatialBlend = 1f;

    [Tooltip("Silent beyond this distance, and not played at all for a listener further away. 0 = fades with the distance like any sound.")]
    [Min(0f)]
    public float maxDistance = 0f;

    [Tooltip("Seconds before this cue can play again (stops a burst of the same sound). 0 = no limit.")]
    [Min(0f)]
    public float cooldown = 0f;

    [Header("Along with the sound (optional)")]
    [Tooltip("An effect shown where the cue plays.")]
    public GameObject effect;

    [Tooltip("Seconds the effect stays (0 = its particles' own length).")]
    [Min(0f)]
    public float effectLifetime = 0f;

    [Tooltip("An Animator trigger set on the character the cue plays on (ignored when its Animator has no such trigger).")]
    public string animatorTrigger;

    [System.NonSerialized] private float lastPlayedAt = float.NegativeInfinity;
    [System.NonSerialized] private int lastIndex = -1;

    /// <summary>True when it has at least one clip.</summary>
    public bool HasSound
    {
        get
        {
            if (clips == null)
                return false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Plays the cue at a place. False when it did not play (cooling down, out of earshot, no clip).</summary>
    public bool Play(Vector3 position, float volumeScale = 1f)
    {
        if (!Ready(position))
            return false;
        AudioClip clip = Pick();
        if (clip != null)
            GameAudio.Play(clip, position, Loudness(volumeScale), Pitch(), category, spatialBlend, maxDistance);
        if (effect != null)
            AbilityPool.PlayVfx(effect, position, Quaternion.identity, effectLifetime);
        return true;
    }

    /// <summary>
    /// Plays the cue on a character or an object: at its position (the middle of a character), with the effect
    /// attached to it and the trigger set on its Animator.
    /// </summary>
    public bool Play(GameObject on, float volumeScale = 1f)
    {
        if (on == null)
            return false;
        CombatEntity entity = CombatEntity.Resolve(on);
        Vector3 position = entity != null ? entity.Center : on.transform.position;
        if (!Ready(position))
            return false;
        AudioClip clip = Pick();
        if (clip != null)
            GameAudio.Play(clip, position, Loudness(volumeScale), Pitch(), category, spatialBlend, maxDistance);
        if (effect != null)
            AbilityPool.PlayVfx(effect, position, on.transform.rotation, effectLifetime, 1f, on.transform);
        if (!string.IsNullOrEmpty(animatorTrigger))
        {
            Animator animator = on.GetComponentInChildren<Animator>();
            if (animator != null)
                AnimatorParameterCache.SetTrigger(animator, animatorTrigger);
        }
        return true;
    }

    /// <summary>Plays the cue's sound on a source of the game (it follows that object and keeps its 3D settings).</summary>
    public bool PlayOn(AudioSource source, float volumeScale = 1f)
    {
        if (source == null || !Ready(source.transform.position))
            return false;
        AudioClip clip = Pick();
        if (clip == null)
            return false;
        source.pitch = Pitch();
        GameAudio.PlayOn(source, clip, Loudness(volumeScale), category);
        return true;
    }

    /// <summary>Plays a cue that may be missing (an empty slot plays nothing).</summary>
    public static bool Play(AudioCue cue, Vector3 position, float volumeScale = 1f) => cue != null && cue.Play(position, volumeScale);

    /// <summary>Plays a cue that may be missing, on a character or an object.</summary>
    public static bool Play(AudioCue cue, GameObject on, float volumeScale = 1f) => cue != null && cue.Play(on, volumeScale);

    private bool Ready(Vector3 position)
    {
        if (!GameAudio.Enabled)
            return false;
        float now = Time.unscaledTime;
        // A time from an earlier play session (the asset outlives it in the editor) is not a cooldown.
        if (lastPlayedAt > now)
            lastPlayedAt = float.NegativeInfinity;
        if (cooldown > 0f && now - lastPlayedAt < cooldown)
            return false;
        if (spatialBlend > 0.99f && !GameAudio.Audible(position, maxDistance))
            return false;
        lastPlayedAt = now;
        return true;
    }

    private AudioClip Pick()
    {
        if (clips == null || clips.Length == 0)
            return null;
        if (clips.Length == 1)
            return clips[0];
        int index = Random.Range(0, clips.Length);
        if (index == lastIndex)
            index = (index + 1) % clips.Length;
        lastIndex = index;
        return clips[index];
    }

    private float Loudness(float scale) =>
        Mathf.Clamp01(volume * scale * (1f - Random.Range(0f, volumeVariation)));

    private float Pitch()
    {
        float low = Mathf.Min(pitch.x, pitch.y);
        float high = Mathf.Max(pitch.x, pitch.y);
        if (low <= 0f && high <= 0f)
            return 1f;
        return Mathf.Approximately(low, high) ? low : Random.Range(low, high);
    }
}
