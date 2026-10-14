#if FISHNET
using FishNet.Component.Transforming.Beta;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Server-authoritative player movement with client-side prediction (Fish-Net prediction v2). Put it on the player
    /// prefab's root, next to the NetworkObject; it drives the player's <see cref="MovementStateMachine"/> on the
    /// network tick instead of every frame.
    /// </summary>
    /// <remarks>
    /// <para>Every tick the owning client reads its devices into a <see cref="MoveData"/> (move stick, packed buttons,
    /// camera yaw) and runs <see cref="MovementStateMachine.Simulate"/> with it at once (prediction); Fish-Net sends the
    /// data to the server, which runs the same step with it - the server's result is the truth. After the tick the
    /// server captures the movement into a <see cref="MoveReconcile"/> (position, facing, state, timers, stamina...);
    /// when it reaches the owner, the owner restores it and replays the inputs it sent since, so a misprediction is
    /// corrected without the player noticing. Other clients (spectators) receive the forwarded inputs and states and
    /// run the same step, so remote players walk, jump and animate exactly like the server says, without a
    /// NetworkTransform.</para>
    /// <para>The client sends only intentions. The server sanitizes them (non-finite values, a move longer than 1,
    /// unknown button bits) and everything else - speeds, stamina, cooldowns, stun, position - comes from its own
    /// state. Replays run with <see cref="MovementStateMachine.IsReplaying"/> set, so no event fires twice.</para>
    /// <para>Teleports made by the server (realm travel, dungeon portals, respawns) travel in the reconcile; the
    /// teleport count in it makes every machine snap the smoothed model instead of sliding it across the map.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetPlayerMovement : TickNetworkBehaviour
    {
        /// <summary>What the owner asks for in one tick (sent to the server, forwarded to spectators).</summary>
        public struct MoveData : IReplicateData
        {
            /// <summary>Move stick / keys (x = right, y = forward).</summary>
            public Vector2 Move;
            /// <summary>Buttons packed with <see cref="MovementInput.PackButtons"/>.</summary>
            public byte Buttons;
            /// <summary>Yaw of the owner's camera (degrees).</summary>
            public float CameraYaw;
            /// <summary>Pitch of the owner's camera (degrees; swimming goes where the camera looks).</summary>
            public float CameraPitch;

            private uint _tick;

            public MoveData(in MovementInput input)
            {
                Move = input.Move;
                Buttons = input.PackButtons();
                CameraYaw = input.CameraYaw;
                CameraPitch = input.CameraPitch;
                _tick = 0;
            }

            /// <summary>The input this data stands for (not yet sanitized: Simulate does it).</summary>
            public MovementInput ToInput() => MovementInput.FromPacked(Move, Buttons, CameraYaw, CameraPitch);

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>The movement's full state after a tick, as the server computed it (see <see cref="MovementSnapshot"/>).</summary>
        public struct MoveReconcile : IReconcileData
        {
            public Vector3 Position;
            public float RootYaw;
            public float BodyYaw;
            public float VerticalVelocity;
            public byte State;
            public byte PreviousState;
            public float StateTime;
            public float SimTime;
            public float CurrentSpeed;
            public float LastRotation;
            public float JumpBufferLeft;
            public float DashCooldownLeft;
            public float RollCooldownLeft;
            public float ActionElapsed;
            public float Stamina;
            public float StaminaDrainTimer;
            public float StaminaRegenTimer;
            /// <summary>The snapshot's bools (<see cref="MovementSnapshot.PackFlags"/>).</summary>
            public byte Flags;
            public byte AirJumpsUsed;
            public float ClimbTime;
            public byte Teleports;
            public Vector3 SwimVelocity;

            private uint _tick;

            public MoveReconcile(in MovementSnapshot s)
            {
                Position = s.Position;
                RootYaw = s.RootYaw;
                BodyYaw = s.BodyYaw;
                VerticalVelocity = s.VerticalVelocity;
                State = (byte)s.State;
                PreviousState = (byte)s.PreviousState;
                StateTime = s.StateTime;
                SimTime = s.SimTime;
                CurrentSpeed = s.CurrentSpeed;
                LastRotation = s.LastRotation;
                JumpBufferLeft = s.JumpBufferLeft;
                DashCooldownLeft = s.DashCooldownLeft;
                RollCooldownLeft = s.RollCooldownLeft;
                ActionElapsed = s.ActionElapsed;
                Stamina = s.Stamina;
                StaminaDrainTimer = s.StaminaDrainTimer;
                StaminaRegenTimer = s.StaminaRegenTimer;
                Flags = s.PackFlags();
                AirJumpsUsed = s.AirJumpsUsed;
                ClimbTime = s.ClimbTime;
                Teleports = s.Teleports;
                SwimVelocity = s.SwimVelocity;
                _tick = 0;
            }

            /// <summary>The snapshot this data carries.</summary>
            public MovementSnapshot ToSnapshot()
            {
                var s = new MovementSnapshot
                {
                    Position = Position,
                    RootYaw = RootYaw,
                    BodyYaw = BodyYaw,
                    VerticalVelocity = VerticalVelocity,
                    State = (MovementStateMachine.EMovementState)State,
                    PreviousState = (MovementStateMachine.EMovementState)PreviousState,
                    StateTime = StateTime,
                    SimTime = SimTime,
                    CurrentSpeed = CurrentSpeed,
                    LastRotation = LastRotation,
                    JumpBufferLeft = JumpBufferLeft,
                    DashCooldownLeft = DashCooldownLeft,
                    RollCooldownLeft = RollCooldownLeft,
                    ActionElapsed = ActionElapsed,
                    Stamina = Stamina,
                    StaminaDrainTimer = StaminaDrainTimer,
                    StaminaRegenTimer = StaminaRegenTimer,
                    AirJumpsUsed = AirJumpsUsed,
                    ClimbTime = ClimbTime,
                    Teleports = Teleports,
                    SwimVelocity = SwimVelocity,
                };
                s.UnpackFlags(Flags);
                return s;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [Tooltip("The server sends its movement state to the clients every this many ticks (1 = every tick, the most " +
                 "accurate; higher saves bandwidth, corrections arrive later). Clients still record their own every tick.")]
        [SerializeField, Range(1, 30)] private int serverReconcileInterval = 1;

        [Tooltip("Spectators (other players seen from a client) keep using a player's last input for at most this many " +
                 "ticks past the newest one received before assuming it let go of the controls.")]
        [SerializeField, Range(0, 5)] private int spectatorFutureTicks = 1;

        [Tooltip("On the machine that controls this player, cameras that follow the body (which moves on network ticks) " +
                 "follow a copy smoothed every frame instead, so walking does not look jerky. Turn off only if your " +
                 "cameras already follow the NetworkObject's Graphical Object.")]
        [SerializeField] private bool smoothCameraTargets = true;

        private CameraTargetSmoother cameraSmoother;

        [Tooltip("When the model's meshes sit directly under the body the movement turns (PlayerMovementModel ▸ Player " +
                 "Transform, e.g. PlayerShellObject) and nothing else smooths it, show that body smoothed between network " +
                 "ticks (its real pose is put back before every tick). Without it the model moves in steps of one tick.")]
        [SerializeField] private bool smoothBody = true;

        // Body smoothing: the body's real pose after the last two ticks, interpolated for display in between.
        private Transform body;
        private Vector3 bodyRestLocal;
        private Vector3 bodyPrevPos, bodyCurrPos;
        private Quaternion bodyPrevRot, bodyCurrRot;
        private bool bodySmoothing;
        private bool bodyShowsVisual;
        private BodyVisualDriver bodyDriver;
        // Animators whose root motion was switched off for the session (put back when it ends).
        private readonly System.Collections.Generic.List<Animator> rootMotionOff = new System.Collections.Generic.List<Animator>();

        private MovementStateMachine machine;

        // One-tick presses (Jump/Dash/Roll) seen in frames since the last tick, so a press between two ticks is never lost.
        private MovementButtons latchedEdges;
        // The frame whose presses the last built tick already included (a second tick in the same frame must not repeat them).
        private int edgesTakenFrame = -1;
        // Spectators: the last input that really arrived (used to guess a tick or two ahead).
        private MoveData lastTickedData;
        // The teleport count the smoothed model was last snapped for.
        private byte seenTeleports;

        /// <summary>The movement this component drives (null when the prefab has none).</summary>
        public MovementStateMachine Machine => machine;

        private void Awake()
        {
            machine = GetComponentInChildren<MovementStateMachine>(true);
            if (machine == null)
                Debug.LogError($"[NetPlayerMovement] '{name}' has no MovementStateMachine under it: the player cannot move in a network session.", this);
            SetTickCallbacks(TickCallback.PreTick | TickCallback.Tick | TickCallback.PostTick);
        }

        // ------------------------------------------------------------------ network lifecycle

        public override void OnStartNetwork()
        {
            if (machine == null)
                return;
            // From now on the movement steps on the tick only (here), never on its own in Update.
            machine.ExternallyDriven = true;
            seenTeleports = machine.TeleportCount;
            DisableRootMotion();
            SetupBodySmoothing();
            RefreshDeviceInput();
            CheckPrefabSetup();
        }

        private static bool warnedSetup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            warnedSetup = false;
            warnedRootMotion = false;
        }

        // The simulation turns PlayerMovementModel.PlayerTransform and moves the CharacterController; Fish-Net's smoother
        // writes the graphical object every frame. If they are the same transform they fight (the body would never
        // turn and the dash direction would follow the smoothed value), so say how to fix the prefab.
        private void CheckPrefabSetup()
        {
            if (warnedSetup)
                return;
            PlayerMovementModel model = GetComponentInChildren<PlayerMovementModel>(true);
            NetworkTickSmoother tickSmoother = GetComponentInChildren<NetworkTickSmoother>(true);
            Transform legacyGfx = NetworkObject != null ? NetworkObject.GetGraphicalObject() : null;
            Transform gfx = SmoothedModel();
            if (model == null)
                return;
            string problem = null;
            if (model.Controller != null && model.Controller.transform != transform)
                problem = $"its CharacterController is on '{model.Controller.name}', not on the NetworkObject root - the predicted (reconciled) object must be the one the controller moves";
            else if (!bodySmoothing && gfx != null && model.PlayerTransform != null && (model.PlayerTransform == gfx || model.PlayerTransform.IsChildOf(gfx)))
                problem = $"PlayerMovementModel ▸ Player Transform ('{model.PlayerTransform.name}') is inside the NetworkObject's Graphical Object ('{gfx.name}') - set Player Transform to the root, or the Graphical Object to a child that only holds the visuals";
            else if (gfx == null)
                problem = "nothing smooths its model between network ticks (no NetworkTickSmoother on the model), so walking looks jerky - " +
                          "run Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked ▸ Player again on the prefab";
            else if (gfx == transform)
                problem = "the smoothed object is the root itself - it must be the child holding the model (run Make Selected Prefab Networked ▸ Player again)";
            else if (gfx.GetComponentsInChildren<Renderer>(true).Length == 0)
                problem = $"the smoothed object '{gfx.name}' contains no renderers, so the visible model is not smoothed and walking looks jerky - " +
                          "the NetworkTickSmoother must be on the object that holds the model's meshes (run Make Selected Prefab Networked ▸ Player again, or move it by hand)";
            else if (tickSmoother != null && legacyGfx != null)
                problem = "both the NetworkObject's (obsolete) Graphical Object and a NetworkTickSmoother smooth the model, and they fight (jitter) - " +
                          "clear the NetworkObject's Graphical Object (Make Selected Prefab Networked ▸ Player does it)";
            else if (tickSmoother == null && !bodySmoothing)
                problem = "its model is smoothed by the NetworkObject's Graphical Object, which Fish-Net made obsolete and which jitters for the host - " +
                          "run Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked ▸ Player again: it moves the model to a NetworkTickSmoother";
            else
            {
                foreach (MonoBehaviour b in GetComponentsInChildren<MonoBehaviour>(true))
                    if (b != null && b.GetType().FullName == "FishNet.Component.Transforming.NetworkTransform")
                    {
                        problem = $"it has a NetworkTransform on '{b.name}', which moves the player as well as the predicted movement (the player stutters or cannot move) - remove it";
                        break;
                    }
            }
            if (problem == null)
                return;
            warnedSetup = true;
            Debug.LogWarning($"[NetPlayerMovement] Player prefab '{name}': {problem}. See Documentation/Networking/02-Movement-Prediction.md.", this);
        }

        public override void OnStopNetwork()
        {
            if (machine == null)
                return;
            machine.IsReplaying = false;
            machine.ExternallyDriven = false;
            machine.SetDeviceInputEnabled(true);
            latchedEdges = MovementButtons.None;
            cameraSmoother?.End();
            StopBodySmoothing();
            foreach (Animator a in rootMotionOff)
                if (a != null)
                    a.applyRootMotion = true;
            rootMotionOff.Clear();
        }

        // Root motion moves the player's root from the animation every frame, while the predicted movement moves it on
        // ticks: in a network session they fight (the player shakes and the smoothing jitters). The movement is driven by
        // code, so root motion is switched off for the session (single player keeps the prefab's setting).
        private void DisableRootMotion()
        {
            foreach (Animator a in GetComponentsInChildren<Animator>(true))
            {
                if (a == null || !a.applyRootMotion)
                    continue;
                a.applyRootMotion = false;
                rootMotionOff.Add(a);
            }
            if (rootMotionOff.Count > 0 && !warnedRootMotion)
            {
                warnedRootMotion = true;
                Debug.Log($"[NetPlayerMovement] Apply Root Motion was switched off on {rootMotionOff.Count} Animator(s) of '{name}' for the network session " +
                          "(it fights the predicted movement). Turn it off on the prefab to silence this.", this);
            }
        }

        private static bool warnedRootMotion;

        private void OnDestroy() => cameraSmoother?.End(destroying: true);

        public override void OnStartServer() => RefreshDeviceInput();
        public override void OnStartClient() => RefreshDeviceInput();
        public override void OnOwnershipServer(NetworkConnection prevOwner) => RefreshDeviceInput();
        public override void OnOwnershipClient(NetworkConnection prevOwner) => RefreshDeviceInput();

        // Only the copy controlled from this machine reads the keyboard / gamepad. NetPlayer already switches off the
        // camera rig and Unity's PlayerInput component on the other copies; this stops the movement's own reads.
        private void RefreshDeviceInput()
        {
            if (machine == null)
                return;
            bool local = IsOwner && NetAuthority.HasLocalPlayer;
            machine.SetDeviceInputEnabled(local);
            if (!local)
                latchedEdges = MovementButtons.None;
            RefreshCameraSmoothing(local);
        }

        private void RefreshCameraSmoothing(bool local)
        {
            if (local && smoothCameraTargets && IsClientStarted)
            {
                cameraSmoother ??= new CameraTargetSmoother(transform, 5f)
                {
                    TickPercent = () => TimeManager != null ? (float)TimeManager.GetTickPercentAsDouble() : 1f,
                };
                if (!cameraSmoother.Active)
                    cameraSmoother.Begin(SmoothedModel());
            }
            else
            {
                cameraSmoother?.End();
            }
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            // ExternallyDriven first: it is only set once the object is a live network object (IsOwner is valid then).
            if (machine == null || !machine.ExternallyDriven || !IsOwner)
                return;
            if (Time.frameCount == edgesTakenFrame)
                return; // this frame's presses already went into a tick
            MovementInput frame = machine.ReadDeviceInput();
            latchedEdges |= (MovementButtons)frame.PackButtons() & MovementButtons.Edges;
        }

        // The owner's input for this tick: held buttons, move and camera now; presses seen since the last tick.
        private MoveData BuildMoveData()
        {
            if (machine == null || !IsOwner)
                return default;
            MovementInput input = machine.ReadDeviceInput();
            if (Time.frameCount == edgesTakenFrame)
            {
                // A second tick in the same frame: this frame's presses were sent with the first one.
                input.JumpPressed = false;
                input.DashPressed = false;
                input.RollPressed = false;
            }
            input.JumpPressed |= (latchedEdges & MovementButtons.JumpPressed) != 0;
            input.DashPressed |= (latchedEdges & MovementButtons.DashPressed) != 0;
            input.RollPressed |= (latchedEdges & MovementButtons.RollPressed) != 0;
            latchedEdges = MovementButtons.None;
            edgesTakenFrame = Time.frameCount;
            return new MoveData(input);
        }

        // ------------------------------------------------------------------ tick

        // Before the tick (and the reconcile that comes with it on clients): the body back at its real pose.
        protected override void TimeManager_OnPreTick() => RestoreBody();

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
        }

        protected override void TimeManager_OnPostTick()
        {
            SnapAfterTeleport();
            RecordBody();
            if (cameraSmoother != null && cameraSmoother.Active)
                cameraSmoother.RecordTick();
            CreateReconcile();
        }

        public override void CreateReconcile()
        {
            if (machine == null)
                return;
            // Clients build theirs every tick (the fallback when a server state is lost); the server may send less often.
            if (IsServerStarted && serverReconcileInterval > 1 && TimeManager.LocalTick % (uint)serverReconcileInterval != 0)
                return;
            PerformReconcile(new MoveReconcile(machine.Capture()));
        }

        [Replicate]
        private void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (machine == null)
                return;

            // Spectators run ahead of the inputs they received: guess with the last real input for a tick or two
            // (never repeating its one-tick presses), then assume the player let go.
            if (!IsServerStarted && !IsOwner)
            {
                if (state.ContainsTicked())
                {
                    lastTickedData.Dispose();
                    lastTickedData = md;
                }
                else if (state.IsFuture())
                {
                    if (md.GetTick() - lastTickedData.GetTick() > (uint)spectatorFutureTicks)
                    {
                        md.Dispose();
                        md = new MoveData { CameraYaw = lastTickedData.CameraYaw, CameraPitch = lastTickedData.CameraPitch };
                    }
                    else
                    {
                        md.Dispose();
                        md = lastTickedData;
                        md.Buttons = MovementInput.WithoutEdges(md.Buttons);
                    }
                }
            }

            // The server never trusts the numbers: Simulate sanitizes again, but clamp here too so nothing the client
            // sent can reach the step unclamped.
            MovementInput input = md.ToInput();
            if (IsServerStarted)
                input = input.Sanitized();

            machine.IsReplaying = state.ContainsReplayed();
            try
            {
                machine.Simulate(input, (float)TimeManager.TickDelta);
            }
            finally
            {
                machine.IsReplaying = false;
            }
        }

        [Reconcile]
        private void PerformReconcile(MoveReconcile rd, Channel channel = Channel.Unreliable)
        {
            if (machine == null)
                return;
            machine.Restore(rd.ToSnapshot());
        }

        // ------------------------------------------------------------------ teleports

        /// <summary>
        /// Server: the player was just moved somewhere else in one go (call it after moving the player's root, e.g.
        /// right after a teleport). Every machine snaps the smoothed model to the new place instead of sliding it.
        /// Jumps longer than the movement's teleport distance are detected without this call.
        /// </summary>
        public void NotifyTeleported()
        {
            if (machine != null)
                machine.NotifyTeleported();
        }

        // The teleport count changed (this server detected one, or a reconcile brought the server's): snap the
        // graphical object to the predicted root instead of interpolating across the jump.
        private void SnapAfterTeleport()
        {
            if (machine == null || machine.TeleportCount == seenTeleports)
                return;
            seenTeleports = machine.TeleportCount;
            if (bodySmoothing && body != null)
            {
                RestoreBody();
                bodyPrevPos = bodyCurrPos = body.position;
                bodyPrevRot = bodyCurrRot = body.rotation;
            }
            NetworkTickSmoother tickSmoother = GetComponentInChildren<NetworkTickSmoother>(true);
            if (tickSmoother != null && tickSmoother.SmootherController != null && tickSmoother.SmootherController.UniversalSmoother != null)
            {
                tickSmoother.SmootherController.UniversalSmoother.Teleport();
                return;
            }
#pragma warning disable CS0618 // the NetworkObject's own smoother (prefabs not yet moved to a NetworkTickSmoother)
            NetworkObject nob = NetworkObject;
            if (nob != null && nob.GetGraphicalObject() != null && nob.PredictionSmoother != null)
                nob.PredictionSmoother.Teleport();
#pragma warning restore CS0618
        }

        // ------------------------------------------------------------------ body smoothing

        private void SetupBodySmoothing()
        {
            bodySmoothing = false;
            body = null;
            if (!smoothBody || machine == null || NetAuthority.IsHeadless)
                return;
            if (GetComponentInChildren<NetworkTickSmoother>(true) != null || (NetworkObject != null && NetworkObject.GetGraphicalObject() != null))
                return; // Fish-Net smooths a visual child already
            PlayerMovementModel model = GetComponentInChildren<PlayerMovementModel>(true);
            Transform t = model != null ? model.PlayerTransform : null;
            if (t == null || t == transform || !t.IsChildOf(transform))
                return;
            body = t;
            bodyRestLocal = body.localPosition;
            bodyPrevPos = bodyCurrPos = body.position;
            bodyPrevRot = bodyCurrRot = body.rotation;
            bodyShowsVisual = false;
            bodySmoothing = true;
            if (bodyDriver == null)
                bodyDriver = gameObject.AddComponent<BodyVisualDriver>();
            bodyDriver.Step = ShowBodySmoothed;
        }

        private void StopBodySmoothing()
        {
            RestoreBody();
            bodySmoothing = false;
            if (bodyDriver != null)
            {
                bodyDriver.Step = null;
                Destroy(bodyDriver);
                bodyDriver = null;
            }
        }

        // The body's real pose (what the simulation, the reconcile and the server use).
        private void RestoreBody()
        {
            if (!bodyShowsVisual || body == null)
                return;
            body.localPosition = bodyRestLocal;
            body.rotation = bodyCurrRot;
            bodyShowsVisual = false;
        }

        // After a tick: remember where the simulation put the body.
        private void RecordBody()
        {
            if (!bodySmoothing || body == null)
                return;
            RestoreBody();
            bodyPrevPos = bodyCurrPos;
            bodyPrevRot = bodyCurrRot;
            bodyCurrPos = body.position;
            bodyCurrRot = body.rotation;
            if ((bodyCurrPos - bodyPrevPos).sqrMagnitude > 25f)
            {
                bodyPrevPos = bodyCurrPos; // a teleport: no sliding across the map
                bodyPrevRot = bodyCurrRot;
            }
        }

        // Every frame before the cameras: the body between the last two ticks.
        private void ShowBodySmoothed()
        {
            if (!bodySmoothing || body == null || TimeManager == null)
                return;
            float t = Mathf.Clamp01((float)TimeManager.GetTickPercentAsDouble());
            body.SetPositionAndRotation(Vector3.Lerp(bodyPrevPos, bodyCurrPos, t), Quaternion.Slerp(bodyPrevRot, bodyCurrRot, t));
            bodyShowsVisual = true;
        }

        /// <summary>The transform that shows the player smoothed between ticks: the NetworkTickSmoother's, or the NetworkObject's Graphical Object.</summary>
        private Transform SmoothedModel()
        {
            if (bodySmoothing && body != null)
                return body;
            NetworkTickSmoother tickSmoother = GetComponentInChildren<NetworkTickSmoother>(true);
            if (tickSmoother != null && tickSmoother.transform != transform)
                return tickSmoother.transform;
            return NetworkObject != null ? NetworkObject.GetGraphicalObject() : null;
        }
    }

    /// <summary>Shows the smoothed player body every LateUpdate, before the camera targets and Cinemachine.</summary>
    [DefaultExecutionOrder(-1100)]
    [AddComponentMenu("")]
    internal sealed class BodyVisualDriver : MonoBehaviour
    {
        public System.Action Step;

        private void LateUpdate() => Step?.Invoke();
    }
}
#endif
