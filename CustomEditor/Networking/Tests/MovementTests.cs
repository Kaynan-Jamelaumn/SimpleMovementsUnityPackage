#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the deterministic pieces of the predicted player movement: the input's packed network form
    /// and sanitizing, the snapshot's timer conversions and flags, the movement context's jump buffer, the state
    /// machine's manual stepping/restoring, and the tick-based stamina regeneration.
    /// </summary>
    public class MovementTests
    {
        // ------------------------------------------------------------------ input packing

        [Test]
        public void EveryButtonCombinationSurvivesPacking()
        {
            for (int bits = 0; bits < 128; bits++)
            {
                var input = new MovementInput
                {
                    Sprint = (bits & 1) != 0,
                    Crouch = (bits & 2) != 0,
                    JumpPressed = (bits & 4) != 0,
                    JumpHeld = (bits & 8) != 0,
                    DashPressed = (bits & 16) != 0,
                    RollPressed = (bits & 32) != 0,
                    RotateWithCamera = (bits & 64) != 0,
                    Move = new Vector2(0.25f, -0.5f),
                    CameraYaw = 123.5f,
                };
                byte packed = input.PackButtons();
                Assert.AreEqual(bits, packed, "bit layout is part of the network data and must not change");
                MovementInput back = MovementInput.FromPacked(input.Move, packed, input.CameraYaw);
                Assert.AreEqual(input.Sprint, back.Sprint);
                Assert.AreEqual(input.Crouch, back.Crouch);
                Assert.AreEqual(input.JumpPressed, back.JumpPressed);
                Assert.AreEqual(input.JumpHeld, back.JumpHeld);
                Assert.AreEqual(input.DashPressed, back.DashPressed);
                Assert.AreEqual(input.RollPressed, back.RollPressed);
                Assert.AreEqual(input.RotateWithCamera, back.RotateWithCamera);
                Assert.AreEqual(input.Move, back.Move);
                Assert.AreEqual(input.CameraYaw, back.CameraYaw);
            }
        }

        [Test]
        public void UnknownButtonBitsAreIgnored()
        {
            MovementInput i = MovementInput.FromPacked(Vector2.zero, 0x80, 0f);
            Assert.AreEqual(0, i.PackButtons());
        }

        [Test]
        public void WithoutEdgesKeepsHeldButtonsOnly()
        {
            byte all = (byte)MovementButtons.All;
            var held = (MovementButtons)MovementInput.WithoutEdges(all);
            Assert.AreEqual(MovementButtons.Sprint | MovementButtons.Crouch | MovementButtons.JumpHeld | MovementButtons.RotateWithCamera, held);
            Assert.IsFalse(MovementInput.FromPacked(Vector2.zero, (byte)held, 0f).HasEdges);
        }

        [Test]
        public void SanitizedClampsAndRejectsNonFiniteValues()
        {
            var fast = new MovementInput { Move = new Vector2(3f, 4f), CameraYaw = 725f };
            MovementInput s = fast.Sanitized();
            Assert.AreEqual(1f, s.Move.magnitude, 1e-5f, "a longer stick vector would be a speed hack");
            Assert.AreEqual(0.6f, s.Move.x, 1e-5f);
            Assert.AreEqual(5f, s.CameraYaw, 1e-3f);

            var broken = new MovementInput { Move = new Vector2(float.NaN, 1f), CameraYaw = float.PositiveInfinity };
            MovementInput b = broken.Sanitized();
            Assert.AreEqual(Vector2.zero, b.Move);
            Assert.AreEqual(0f, b.CameraYaw);

            var negative = new MovementInput { Move = new Vector2(0.3f, 0.4f), CameraYaw = -90f };
            MovementInput n = negative.Sanitized();
            Assert.AreEqual(new Vector2(0.3f, 0.4f), n.Move, "a move inside the unit circle is left alone");
            Assert.AreEqual(270f, n.CameraYaw, 1e-3f);
        }

        [Test]
        public void NeutralizedAsksForNothingButKeepsTheCamera()
        {
            var input = new MovementInput
            {
                Move = Vector2.one, Sprint = true, Crouch = true, JumpPressed = true, JumpHeld = true,
                DashPressed = true, RollPressed = true, RotateWithCamera = true, CameraYaw = 42f,
            };
            MovementInput n = input.Neutralized();
            Assert.AreEqual(Vector2.zero, n.Move);
            Assert.AreEqual((byte)MovementButtons.RotateWithCamera, n.PackButtons());
            Assert.AreEqual(42f, n.CameraYaw);
        }

        // ------------------------------------------------------------------ snapshot timers and flags

        [Test]
        public void TimerThatNeverStartedIsNone()
        {
            Assert.AreEqual(MovementSnapshot.None, MovementSnapshot.Remaining(10f, float.NegativeInfinity, 4f));
            Assert.IsTrue(float.IsNegativeInfinity(MovementSnapshot.Since(10f, MovementSnapshot.None, 4f)));
        }

        [Test]
        public void TimerRemainingMatchesElapsedTime()
        {
            Assert.AreEqual(3f, MovementSnapshot.Remaining(11f, 10f, 4f), 1e-5f);
            Assert.AreEqual(0f, MovementSnapshot.Remaining(14f, 10f, 4f), 1e-5f, "the end of the window still counts as inside it");
            Assert.AreEqual(MovementSnapshot.None, MovementSnapshot.Remaining(14.5f, 10f, 4f));
        }

        // A dash is ready when now - lastUse > cooldown, a buffered jump counts while now - press <= window. Converting
        // to "time left" and back (what a reconcile does) must never change either answer.
        [Test]
        public void RestoringATimerKeepsCooldownAndBufferDecisions()
        {
            const float cooldown = 4f, buffer = 0.2f;
            var samples = new List<float> { 0f, 0.01f, 0.1f, 0.19f, 0.2f, 0.21f, 1f, 3.99f, 4f, 4.01f, 10f };
            foreach (float clientNow in new[] { 0f, 5f, 1234.5f })
            {
                foreach (float elapsed in samples)
                {
                    const float serverNow = 100f;
                    float since = serverNow - elapsed;

                    float cdLeft = MovementSnapshot.Remaining(serverNow, since, cooldown);
                    float cdSince = MovementSnapshot.Since(clientNow, cdLeft, cooldown);
                    Assert.AreEqual(serverNow - since > cooldown, clientNow - cdSince > cooldown, $"cooldown, elapsed {elapsed}");

                    float bufLeft = MovementSnapshot.Remaining(serverNow, since, buffer);
                    float bufSince = MovementSnapshot.Since(clientNow, bufLeft, buffer);
                    Assert.AreEqual(serverNow - since <= buffer, clientNow - bufSince <= buffer, $"jump buffer, elapsed {elapsed}");
                }
            }
        }

        [Test]
        public void SnapshotFlagsRoundTrip()
        {
            for (int bits = 0; bits < 32; bits++)
            {
                var s = new MovementSnapshot
                {
                    StaminaConsuming = (bits & 1) != 0,
                    StaminaRegenerating = (bits & 2) != 0,
                    SuspendGravity = (bits & 4) != 0,
                    Frozen = (bits & 8) != 0,
                    Climbing = (bits & 16) != 0,
                };
                var back = new MovementSnapshot();
                back.UnpackFlags(s.PackFlags());
                Assert.AreEqual(s.StaminaConsuming, back.StaminaConsuming);
                Assert.AreEqual(s.StaminaRegenerating, back.StaminaRegenerating);
                Assert.AreEqual(s.SuspendGravity, back.SuspendGravity);
                Assert.AreEqual(s.Frozen, back.Frozen);
                Assert.AreEqual(s.Climbing, back.Climbing);
            }
        }

        // ------------------------------------------------------------------ context

        [Test]
        public void JumpBufferUsesTheMovementClock()
        {
            var ctx = new MovementContext(null, null, null, null, null, null, null, null);
            Assert.IsFalse(ctx.JumpPressBuffered, "no press yet");
            ctx.SimTime = 5f;
            ctx.RecordJumpPress();
            ctx.SimTime = 5f + ctx.JumpBufferTime;
            Assert.IsTrue(ctx.JumpPressBuffered);
            ctx.SimTime = 5f + ctx.JumpBufferTime + 0.01f;
            Assert.IsFalse(ctx.JumpPressBuffered);
            ctx.SimTime = 6f;
            ctx.RecordJumpPress();
            ctx.ConsumeJumpPress();
            Assert.IsFalse(ctx.JumpPressBuffered);
        }

        [Test]
        public void ContextMoveWithoutControllerDoesNothing()
        {
            var ctx = new MovementContext(null, null, null, null, null, null, null, null);
            Assert.DoesNotThrow(() => ctx.Move(Vector3.forward));
            ctx.Frozen = true;
            Assert.DoesNotThrow(() => ctx.Move(Vector3.forward));
        }

        // ------------------------------------------------------------------ swimming

        [Test]
        public void SwimmingIsAppendedAndMatchesTheCombatState()
        {
            // The state travels over the network as a byte and is copied into the combat state by number.
            Assert.AreEqual(6, (int)MovementStateMachine.EMovementState.Rolling, "existing values never move");
            Assert.AreEqual(7, (int)MovementStateMachine.EMovementState.Swimming);
            Assert.AreEqual((int)MovementStateMachine.EMovementState.Swimming, (int)PlayerMovementModel.PlayerState.Swimming);
            byte wire = (byte)MovementStateMachine.EMovementState.Swimming;
            Assert.AreEqual(MovementStateMachine.EMovementState.Swimming, (MovementStateMachine.EMovementState)wire);
        }

        [Test]
        public void CameraPitchTravelsAndIsClamped()
        {
            var input = new MovementInput { Move = Vector2.up, CameraYaw = 10f, CameraPitch = 35f };
            MovementInput back = MovementInput.FromPacked(input.Move, input.PackButtons(), input.CameraYaw, input.CameraPitch);
            Assert.AreEqual(35f, back.CameraPitch);
            Assert.AreEqual(-30f, new MovementInput { CameraPitch = 330f }.Sanitized().CameraPitch, 1e-3f, "Unity's 330 means looking 30 up");
            Assert.AreEqual(89f, new MovementInput { CameraPitch = 120f }.Sanitized().CameraPitch, 1e-3f);
            Assert.AreEqual(0f, new MovementInput { CameraPitch = float.NaN }.Sanitized().CameraPitch);
            Assert.AreEqual(35f, input.Neutralized().CameraPitch, "a frozen player keeps looking where it looked");
        }

        [Test]
        public void SwimmingStartsAndEndsWithHysteresis()
        {
            var go = new GameObject("SwimModel");
            try
            {
                PlayerMovementModel model = go.AddComponent<PlayerMovementModel>();
                SwimSettings swim = model.Swimming;
                swim.minWaterDepth = 1.4f;
                swim.enterDepth = 1.1f;
                swim.exitDepth = 0.75f;
                var ctx = new MovementContext(model, null, null, null, null, null, null, null);

                ctx.InWater = false;
                Assert.IsFalse(ctx.ShouldStartSwimming, "no water");

                ctx.InWater = true;
                ctx.WaterDepth = 1.2f;
                ctx.FeetDepth = 1.2f;
                Assert.IsFalse(ctx.ShouldStartSwimming, "too shallow: waded through");

                ctx.WaterDepth = 5f;
                ctx.FeetDepth = 0.9f;
                Assert.IsFalse(ctx.ShouldStartSwimming, "feet not deep enough yet");
                Assert.IsTrue(ctx.ShouldKeepSwimming, "but a swimmer this deep stays a swimmer");

                ctx.FeetDepth = 1.15f;
                Assert.IsTrue(ctx.ShouldStartSwimming);

                ctx.FeetDepth = 0.7f;
                Assert.IsFalse(ctx.ShouldKeepSwimming, "standing up in the shallows");

                ctx.FeetDepth = 1.3f;
                ctx.WaterDepth = 1.35f;
                Assert.IsTrue(ctx.ShouldKeepSwimming, "a little shallower than to start still swims (no flicker on a slope)");
                ctx.WaterDepth = 1.25f;
                Assert.IsFalse(ctx.ShouldKeepSwimming);

                swim.enabled = false;
                ctx.WaterDepth = 10f;
                ctx.FeetDepth = 3f;
                Assert.IsFalse(ctx.ShouldStartSwimming, "swimming switched off");
                Assert.IsFalse(ctx.ShouldKeepSwimming);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ state machine stepping

        private enum TestState { A, B }

        private sealed class CountingState : BaseState<TestState>
        {
            public int Enters, Exits, Updates;
            public TestState Next;
            public CountingState(TestState key) : base(key) { Next = key; }
            public override void EnterState() => Enters++;
            public override void ExitState() => Exits++;
            public override void UpdateState() => Updates++;
            public override TestState GetNextState() => Next;
            public override void OnTriggerEnter(Collider other) { }
            public override void OnTriggerStay(Collider other) { }
            public override void OnTriggerExit(Collider other) { }
            public override void LateUpdateState() { }
        }

        private sealed class SteppedMachine : StateManager<TestState>
        {
            public float Clock;
            public bool Replaying;
            [System.NonSerialized] public CountingState A = new CountingState(TestState.A), B = new CountingState(TestState.B);
            protected override float CurrentTime => Clock;
            protected override bool SuppressStateEvents => Replaying;
            public void Build()
            {
                States.Add(TestState.A, A);
                States.Add(TestState.B, B);
                CurrentState = A;
            }
            public void Begin() => StartMachine();
            public void Step() => StepMachine();
        }

        [Test]
        public void ManualStepsUseTheMachineClockAndRestoreSkipsEnterExit()
        {
            var go = new GameObject("SteppedMachine");
            try
            {
                SteppedMachine m = go.AddComponent<SteppedMachine>();
                m.Build();
                m.Clock = 10f;
                m.Begin();
                m.Begin(); // entering the initial state happens once
                Assert.AreEqual(1, m.A.Enters);

                m.Clock = 11f;
                m.Step();
                Assert.AreEqual(1, m.A.Updates);
                Assert.AreEqual(1f, m.TimeInState, 1e-5f);

                int events = 0;
                m.StateChanged += (from, to) => events++;
                m.A.Next = TestState.B;
                m.Step();
                Assert.AreEqual(TestState.B, m.CurrentState.StateKey);
                Assert.AreEqual(TestState.A, m.PreviousStateKey);
                Assert.AreEqual(1, m.A.Exits);
                Assert.AreEqual(1, m.B.Enters);
                Assert.AreEqual(0, m.B.Updates, "no UpdateState on the transition step");
                Assert.AreEqual(1, events);

                m.RestoreState(TestState.A, TestState.B, 7f);
                Assert.AreEqual(TestState.A, m.CurrentState.StateKey);
                Assert.AreEqual(TestState.B, m.PreviousStateKey);
                Assert.AreEqual(4f, m.TimeInState, 1e-5f);
                Assert.AreEqual(1, m.A.Enters, "restoring runs no EnterState");
                Assert.AreEqual(0, m.B.Exits, "restoring runs no ExitState");

                m.Replaying = true;
                m.A.Next = TestState.B;
                m.Step();
                Assert.AreEqual(TestState.B, m.CurrentState.StateKey);
                Assert.AreEqual(1, events, "a replayed transition raises no event");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ stamina regeneration

        [Test]
        public void StaminaRegeneratesOnTheOldRhythm()
        {
            var go = new GameObject("Stamina");
            try
            {
                StaminaManager st = go.AddComponent<StaminaManager>();
                float max = st.MaxValue;
                float amount = st.IncrementValue;
                st.SetCurrentValue(max - 10f * amount);

                // The first amount comes at once, the next one only after a whole tick interval.
                st.TickRegen(0.01f);
                Assert.IsTrue(st.IsRegenerating);
                Assert.AreEqual(max - 9f * amount, st.CurrentValue, 1e-4f);
                st.GetRegenState(out bool running, out float toNext);
                Assert.IsTrue(running);
                Assert.Greater(toNext, 0f);
                st.TickRegen(toNext * 0.5f);
                Assert.AreEqual(max - 9f * amount, st.CurrentValue, 1e-4f);
                st.TickRegen(toNext * 0.5f);
                Assert.AreEqual(max - 8f * amount, st.CurrentValue, 1e-4f);

                // Saving and restoring the timer continues exactly where it was.
                st.GetRegenState(out running, out toNext);
                st.SetRegenState(false, 0f);
                st.SetRegenState(running, toNext);
                st.GetRegenState(out bool running2, out float toNext2);
                Assert.AreEqual(running, running2);
                Assert.AreEqual(toNext, toNext2, 1e-6f);

                // Spending over time stops it (after the amount already due, like the coroutine did) and blocks a restart.
                st.IsConsuming = true;
                st.TickRegen(toNext);
                Assert.IsFalse(st.IsRegenerating);
                float stopped = st.CurrentValue;
                st.TickRegen(10f);
                Assert.AreEqual(stopped, st.CurrentValue, 1e-4f);

                // Nothing to regenerate at max.
                st.IsConsuming = false;
                st.SetCurrentValue(max);
                st.TickRegen(0.01f);
                Assert.IsFalse(st.IsRegenerating);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
