using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The sounds of the player's movement: footsteps (walk, run, crouch), jump, landing, dash, roll, and water - a splash
/// when entering, swim strokes, a gasp when surfacing after a long dive, a loop while underwater and, for this
/// machine's own player, the world muffled underwater (a low-pass filter on the listener, like Subnautica).
/// <para>Put it on the player (Tools ▸ SimpleMovements ▸ Player ▸ Add Movement Audio does it) and drag your clips in;
/// empty lists are simply silent. Footsteps follow the OnFootstep animation events when your walk/run clips have them,
/// otherwise a timer that follows the speed.</para>
/// <para>Network sessions: every machine plays the sounds of every player it shows (3D, from that player), from the
/// movement that runs on every copy; replayed steps play nothing. The underwater muffle and loop are for this
/// machine's own player only. A dedicated server plays nothing.</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerMovementAudio : MonoBehaviour
{
    [Header("Output")]
    [Tooltip("Mixer group the sounds go to (usually SFX). Empty = no mixer.")]
    [SerializeField] private AudioMixerGroup output;
    [Tooltip("Volume of every movement sound.")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [Tooltip("Random pitch change of each sound (0.05 = ±5%).")]
    [Range(0f, 0.3f)] [SerializeField] private float pitchVariation = 0.06f;
    [Tooltip("Sounds of other players are heard up to this distance (metres).")]
    [Min(1f)] [SerializeField] private float maxDistance = 30f;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footsteps = new AudioClip[0];
    [Tooltip("Used while running (empty = the footsteps, a little louder).")]
    [SerializeField] private AudioClip[] runFootsteps = new AudioClip[0];
    [Tooltip("Seconds between steps while walking (when no OnFootstep animation events arrive).")]
    [Min(0.1f)] [SerializeField] private float walkStepInterval = 0.5f;
    [Min(0.1f)] [SerializeField] private float runStepInterval = 0.32f;
    [Min(0.1f)] [SerializeField] private float crouchStepInterval = 0.7f;
    [Tooltip("Crouched steps are this loud (sneaking).")]
    [Range(0f, 1f)] [SerializeField] private float crouchVolume = 0.35f;

    [Header("Jump, landing, dash, roll")]
    [SerializeField] private AudioClip[] jump = new AudioClip[0];
    [SerializeField] private AudioClip[] land = new AudioClip[0];
    [Tooltip("Seconds in the air before a landing makes a sound.")]
    [Min(0f)] [SerializeField] private float minAirTimeForLanding = 0.25f;
    [SerializeField] private AudioClip[] dash = new AudioClip[0];
    [SerializeField] private AudioClip[] roll = new AudioClip[0];

    [Header("Water")]
    [Tooltip("Entering deep water (louder the faster the fall).")]
    [SerializeField] private AudioClip[] splash = new AudioClip[0];
    [Tooltip("Climbing / walking out of deep water.")]
    [SerializeField] private AudioClip[] waterExit = new AudioClip[0];
    [Tooltip("Swim strokes while moving in the water.")]
    [SerializeField] private AudioClip[] swimStrokes = new AudioClip[0];
    [Min(0.2f)] [SerializeField] private float strokeInterval = 0.9f;
    [Tooltip("Head out of the water after this many seconds under it: a gasp for air.")]
    [SerializeField] private AudioClip[] surfaceGasp = new AudioClip[0];
    [Min(0f)] [SerializeField] private float gaspAfterSeconds = 4f;
    [Tooltip("Looping sound while the local player's head is underwater (bubbles, pressure).")]
    [SerializeField] private AudioClip underwaterLoop;
    [Range(0f, 1f)] [SerializeField] private float underwaterLoopVolume = 0.6f;
    [Tooltip("Muffle the whole world while the local player's head is underwater (low-pass filter on the Audio Listener).")]
    [SerializeField] private bool muffleUnderwater = true;
    [Tooltip("Cut-off frequency of the muffle (Hz): lower = more muffled.")]
    [Range(200f, 5000f)] [SerializeField] private float underwaterCutoff = 900f;

    private MovementStateMachine machine;
    private PlayerMovementModel model;
    private PlayerMovementController controller;
    private PlayerAnimationController animationController;
    private AudioSource oneShots;
    private AudioSource loop;
    private AudioLowPassFilter muffle;
    private bool muffleAdded;
    private float stepTimer, strokeTimer, airTime, underTime;
    private bool wasAirborne, wasUnder, animationSteps;

    private bool Silent => GameNet.NetAuthority.IsHeadless;
    private bool IsLocal => GameNet.NetAuthority.IsLocallyControlled(gameObject);

    private void Awake()
    {
        machine = GetComponentInChildren<MovementStateMachine>(true);
        model = GetComponentInChildren<PlayerMovementModel>(true);
        controller = GetComponentInChildren<PlayerMovementController>(true);
        animationController = GetComponentInChildren<PlayerAnimationController>(true);
        oneShots = MakeSource("Movement Audio", false);
        loop = MakeSource("Underwater Audio", true);
        loop.spatialBlend = 0f; // heard by the local player only, from everywhere
    }

    private void OnEnable()
    {
        if (machine != null) machine.StateChanged += OnStateChanged;
        if (animationController != null) animationController.Footstep += OnAnimationFootstep;
    }

    private void OnDisable()
    {
        if (machine != null) machine.StateChanged -= OnStateChanged;
        if (animationController != null) animationController.Footstep -= OnAnimationFootstep;
        SetUnderwater(false);
    }

    private AudioSource MakeSource(string label, bool looping)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up;
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = looping;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 2f;
        s.maxDistance = maxDistance;
        s.outputAudioMixerGroup = output;
        return s;
    }

    private void Update()
    {
        if (Silent || machine == null || model == null)
            return;
        float dt = Time.deltaTime;
        MovementStateMachine.EMovementState state = machine.CurrentState != null ? machine.CurrentState.StateKey : MovementStateMachine.EMovementState.Idle;
        bool swimming = state == MovementStateMachine.EMovementState.Swimming;
        bool grounded = swimming || controller == null || controller.IsGrounded();

        // Landing.
        if (!grounded)
            airTime += dt;
        else
        {
            if (wasAirborne && airTime >= minAirTimeForLanding && !swimming)
                Play(land, Mathf.Clamp01(0.5f + airTime * 0.5f));
            airTime = 0f;
        }
        wasAirborne = !grounded;

        // Footsteps on a timer (unless the walk/run clips send OnFootstep events).
        bool walking = grounded && !swimming && model.Movement2D.sqrMagnitude > 0.01f &&
                       (state == MovementStateMachine.EMovementState.Walking || state == MovementStateMachine.EMovementState.Running || state == MovementStateMachine.EMovementState.Crouching);
        if (walking && !animationSteps)
        {
            float interval = state == MovementStateMachine.EMovementState.Running ? runStepInterval
                : state == MovementStateMachine.EMovementState.Crouching ? crouchStepInterval : walkStepInterval;
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                stepTimer = interval;
                Footstep(state);
            }
        }
        else if (!walking)
            stepTimer = 0f;

        // Swim strokes.
        if (swimming && machine.SwimVelocity.sqrMagnitude > 0.25f)
        {
            strokeTimer -= dt;
            if (strokeTimer <= 0f)
            {
                strokeTimer = strokeInterval;
                Play(swimStrokes, 0.7f);
            }
        }
        else
            strokeTimer = 0f;

        // Head under the water: the local player's muffle and loop, and a gasp when coming up after a long dive.
        bool under = model.HeadUnderwater;
        if (under)
            underTime += dt;
        else
        {
            if (wasUnder && underTime >= gaspAfterSeconds)
                Play(surfaceGasp, 1f);
            underTime = 0f;
        }
        if (under != wasUnder)
            SetUnderwater(under && IsLocal);
        wasUnder = under;
    }

    private void OnStateChanged(MovementStateMachine.EMovementState from, MovementStateMachine.EMovementState to)
    {
        if (Silent)
            return;
        switch (to)
        {
            case MovementStateMachine.EMovementState.Jumping:
                if (from != MovementStateMachine.EMovementState.Swimming)
                    Play(jump, 1f);
                else
                    Play(waterExit, 0.8f);
                break;
            case MovementStateMachine.EMovementState.Dashing:
                Play(dash, 1f);
                break;
            case MovementStateMachine.EMovementState.Rolling:
                Play(roll, 1f);
                break;
            case MovementStateMachine.EMovementState.Swimming:
                float fall = model != null ? Mathf.Abs(Mathf.Min(0f, model.VerticalVelocity)) : 0f;
                Play(splash, Mathf.Clamp01(0.4f + fall * 0.15f));
                airTime = 0f; // no landing sound into water
                break;
        }
        if (from == MovementStateMachine.EMovementState.Swimming && to != MovementStateMachine.EMovementState.Jumping)
            Play(waterExit, 0.6f);
    }

    private void OnAnimationFootstep()
    {
        animationSteps = true; // the clips have events: the timer stops
        if (Silent || machine == null || machine.CurrentState == null)
            return;
        Footstep(machine.CurrentState.StateKey);
    }

    private void Footstep(MovementStateMachine.EMovementState state)
    {
        if (state == MovementStateMachine.EMovementState.Running)
            Play(runFootsteps.Length > 0 ? runFootsteps : footsteps, runFootsteps.Length > 0 ? 1f : 1.1f);
        else if (state == MovementStateMachine.EMovementState.Crouching)
            Play(footsteps, crouchVolume);
        else
            Play(footsteps, 0.9f);
    }

    private void Play(AudioClip[] clips, float loudness)
    {
        if (clips == null || clips.Length == 0 || oneShots == null)
            return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;
        oneShots.spatialBlend = IsLocal ? 0.5f : 1f; // your own steps are not panned from behind the camera
        oneShots.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        oneShots.PlayOneShot(clip, Mathf.Clamp01(volume * loudness));
    }

    private void SetUnderwater(bool on)
    {
        if (loop != null)
        {
            if (on && underwaterLoop != null)
            {
                loop.clip = underwaterLoop;
                loop.volume = volume * underwaterLoopVolume;
                if (!loop.isPlaying) loop.Play();
            }
            else if (loop.isPlaying)
                loop.Stop();
        }
        if (!muffleUnderwater)
            return;
        if (on)
        {
            AudioListener listener = FindListener();
            if (listener == null)
                return;
            muffle = listener.GetComponent<AudioLowPassFilter>();
            if (muffle == null)
            {
                muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
                muffleAdded = true;
            }
            muffle.cutoffFrequency = underwaterCutoff;
            muffle.enabled = true;
        }
        else if (muffle != null)
        {
            if (muffleAdded)
                Destroy(muffle);
            else
                muffle.enabled = false;
            muffle = null;
            muffleAdded = false;
        }
    }

    private AudioListener FindListener()
    {
        AudioListener l = GetComponentInChildren<AudioListener>();
        if (l != null && l.isActiveAndEnabled)
            return l;
#if UNITY_6000_4_OR_NEWER
        AudioListener[] all = FindObjectsByType<AudioListener>();
#else
        AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
#endif
        foreach (AudioListener a in all)
            if (a != null && a.isActiveAndEnabled)
                return a;
        return null;
    }
}
