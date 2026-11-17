using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Plays an animation clip on a character <b>without the Animator Controller needing a state for it</b>: the clip is
/// laid over the controller's pose, blended in, played, and blended back out to whatever the controller is doing.
/// <para><b>Optional, and off unless you add it.</b> Without this component nothing changes: a clip plays only when
/// the controller has a state named after it, as it always did. Add it to the player (next to the Player Animation
/// Controller) when you want the clips of items and weapons to play as they are, without building a state for each
/// one in every controller:</para>
/// <list type="bullet">
/// <item><b>Play Action Clips</b>: an item's Use Animation, a weapon's equip, unequip, reload and charge clips.</item>
/// <item><b>Play Attack Clips</b>: an attack whose clip has no state plays that clip (stretched to the attack's
/// duration, as usual) instead of the generic attack trigger. The weapon's hit detection follows the hand through
/// the animation that actually plays, so each weapon then swings along its own clip.</item>
/// </list>
/// A clip that does have a state of its name in the controller is always left to the controller. The whole body
/// plays the clip (it is not limited to the arms); the controller keeps running underneath and shows again when the
/// clip ends. Try it on one weapon first: it changes the motion the hits are measured from.
/// </summary>
[DisallowMultipleComponent]
public class AnimationClipOverlay : MonoBehaviour
{
    [Tooltip("The character's Animator (found in the children when empty).")]
    [SerializeField] private Animator animator;

    [Tooltip("An item's Use Animation, a weapon's equip / unequip / reload / charge clip: played as it is when the controller has no state of its name.")]
    [SerializeField] private bool playActionClips = true;

    [Tooltip("An attack whose clip has no state of its name in the controller plays that clip instead of the generic attack trigger. " +
             "The blade's hits then follow that clip's motion.")]
    [SerializeField] private bool playAttackClips = false;

    [Tooltip("Seconds to blend from the controller's pose into the clip.")]
    [SerializeField, Min(0f)] private float blendIn = 0.1f;

    [Tooltip("Seconds to blend back to the controller's pose when the clip ends.")]
    [SerializeField, Min(0f)] private float blendOut = 0.15f;

    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable playable;
    private AnimationClip clip;
    private float weight;
    private float fadeIn;
    private float remaining;

    /// <summary>One-off clips (item use, equip, reload) may play through the overlay.</summary>
    public bool PlayActionClips => isActiveAndEnabled && playActionClips;

    /// <summary>Attack clips with no state of their name may play through the overlay.</summary>
    public bool PlayAttackClips => isActiveAndEnabled && playAttackClips;

    /// <summary>A clip is playing (not counting the blend back out).</summary>
    public bool IsPlaying => clip != null;

    /// <summary>The clip playing (null = none).</summary>
    public AnimationClip Current => clip;

    /// <summary>
    /// Plays <paramref name="newClip"/> over the controller's pose. <paramref name="speed"/>: its playback speed.
    /// <paramref name="duration"/>: seconds it stays up (0 = its own length at that speed, or until <see cref="Stop"/>
    /// for a looping clip); a clip shorter than that holds its last frame, or repeats when it is a looping clip. <paramref name="fade"/>: the blend-in, in seconds
    /// (negative = the component's). False when it could not play (no Animator, switched off).
    /// </summary>
    public bool Play(AnimationClip newClip, float speed = 1f, float duration = 0f, float fade = -1f)
    {
        if (newClip == null || !isActiveAndEnabled || !Ensure())
            return false;
        if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
            speed = 1f;
        if (playable.IsValid())
            playable.Destroy();
        playable = AnimationClipPlayable.Create(graph, newClip);
        playable.SetSpeed(speed);
        output.SetSourcePlayable(playable);
        clip = newClip;
        // Its own length when none is given; a looping clip (a held charge) stays until it is stopped.
        remaining = duration > 0f ? duration : newClip.isLooping ? float.PositiveInfinity : newClip.length / speed;
        fadeIn = fade >= 0f ? fade : blendIn;
        if (fadeIn <= 0f)
        {
            weight = 1f;
            output.SetWeight(1f);
        }
        if (!graph.IsPlaying())
            graph.Play();
        return true;
    }

    /// <summary>
    /// Ends the clip and blends back to the controller (<paramref name="immediate"/>: at once). With
    /// <paramref name="only"/>, nothing happens unless that is the clip playing.
    /// </summary>
    public void Stop(AnimationClip only = null, bool immediate = false)
    {
        if (only != null && only != clip)
            return;
        clip = null;
        if (immediate)
            Release();
    }

    private bool Ensure()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            return false;
        if (graph.IsValid())
            return true;
        graph = PlayableGraph.Create(name + " (clip overlay)");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, "Clip", animator);
        output.SetWeight(0f);
        weight = 0f;
        return true;
    }

    private void Update()
    {
        if (!graph.IsValid())
            return;
        float dt = Time.deltaTime;
        if (clip != null)
        {
            remaining -= dt;
            weight = fadeIn > 0f ? Mathf.MoveTowards(weight, 1f, dt / fadeIn) : 1f;
            output.SetWeight(weight);
            if (remaining <= 0f)
                clip = null; // the blend back out starts, the last frame held meanwhile
            return;
        }
        if (weight <= 0f)
            return;
        weight = blendOut > 0f ? Mathf.MoveTowards(weight, 0f, dt / blendOut) : 0f;
        output.SetWeight(weight);
        if (weight <= 0f)
            Release();
    }

    /// <summary>Nothing left to show: the controller alone drives the character again.</summary>
    private void Release()
    {
        weight = 0f;
        if (!graph.IsValid())
            return;
        output.SetWeight(0f);
        if (graph.IsPlaying())
            graph.Stop();
        if (playable.IsValid())
            playable.Destroy();
    }

    private void OnDisable()
    {
        clip = null;
        Release();
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
