#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using NUnit.Framework;

namespace MobSystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window ▸ General ▸ Test Runner) of the mobs' locomotion rules (<see cref="MobGait"/>,
    /// <see cref="MobLocomotion"/>): hops keep the mob's pace and always land, a slime squashes and stretches, zigzags turn at
    /// their own random moments, a slither comes back to its line, darts average their pace, swimmers keep their back
    /// under the water and flyers their altitude over water, and each habitat goes only where it may.
    /// </summary>
    public class MobLocomotionTests
    {
        private const float Dt = 0.01f;

        /// <summary>Distance a land gait covers in <paramref name="seconds"/> at <paramref name="speed"/>.</summary>
        private static float Distance(MobLandGait gait, MobGaitShape shape, float speed, float seconds, int mob = 1)
        {
            MobGaitState s = MobGaitState.For(mob);
            float d = 0f;
            for (float t = 0f; t < seconds; t += Dt)
                d += speed * MobGait.Land(gait, shape, ref s, speed, Dt).speedScale * Dt;
            return d;
        }

        [Test]
        public void HopsBouncesAndLeaps_KeepTheMobsPace_OnAverage()
        {
            foreach (MobLandGait g in new[] { MobLandGait.Hop, MobLandGait.Bounce, MobLandGait.Leap })
            {
                MobGaitShape shape = MobGait.DefaultShape(g, 1f);
                float d = Distance(g, shape, 2f, 20f);
                Assert.That(d, Is.EqualTo(40f).Within(40f * 0.05f), $"{g}: 20 s at 2 m/s covers 40 m (moving only in the air, faster then)");
            }
        }

        [Test]
        public void AHop_RisesAndLands_AndIsNeverBelowTheGround()
        {
            MobGaitShape shape = new MobGaitShape { height = 0.5f, length = 1f, airShare = 0.7f };
            MobGaitState s = MobGaitState.For(3);
            float peak = 0f;
            bool sawGround = false, sawAir = false;
            for (float t = 0f; t < 3f; t += Dt)
            {
                MobGaitStep step = MobGait.Land(MobLandGait.Hop, shape, ref s, 1.5f, Dt);
                Assert.That(step.lift, Is.GreaterThanOrEqualTo(0f));
                peak = Math.Max(peak, step.lift);
                if (step.airborne) sawAir = true;
                else
                {
                    sawGround = true;
                    Assert.That(step.lift, Is.EqualTo(0f), "on the ground between hops");
                    Assert.That(step.speedScale, Is.EqualTo(0f), "it pauses on the ground");
                }
            }
            Assert.That(sawAir && sawGround);
            Assert.That(peak, Is.EqualTo(0.5f).Within(0.02f), "the arc peaks at the hop's height");
        }

        [Test]
        public void AMobThatStopsMidHop_LandsInsteadOfHangingInTheAir()
        {
            MobGaitShape shape = new MobGaitShape { height = 0.6f, length = 1f, airShare = 0.8f };
            MobGaitState s = MobGaitState.For(4);
            MobGaitStep step = default;
            for (float t = 0f; t < 0.2f; t += Dt)
                step = MobGait.Land(MobLandGait.Hop, shape, ref s, 2f, Dt);
            Assert.That(step.airborne && step.lift > 0.1f, "mid-hop");
            for (float t = 0f; t < 2f; t += Dt)
            {
                step = MobGait.Land(MobLandGait.Hop, shape, ref s, 0f, Dt);
                Assert.That(step.speedScale, Is.EqualTo(0f), "stopped: it does not move on");
            }
            Assert.That(step.lift, Is.EqualTo(0f), "back on the ground");
        }

        [Test]
        public void ASlime_IsStretchedInTheAir_AndSquashedOnTheGround()
        {
            MobGaitShape shape = MobGait.DefaultShape(MobLandGait.Bounce, 1f);
            MobGaitState s = MobGaitState.For(5);
            float most = 1f, least = 1f;
            for (float t = 0f; t < 4f; t += Dt)
            {
                MobGaitStep step = MobGait.Land(MobLandGait.Bounce, shape, ref s, 1.5f, Dt);
                if (step.airborne) most = Math.Max(most, step.squash);
                else least = Math.Min(least, step.squash);
            }
            Assert.That(most, Is.GreaterThan(1.15f), "stretched mid-bounce");
            Assert.That(least, Is.LessThan(0.75f), "squashed between bounces");
        }

        [Test]
        public void AZigzag_ChangesSideAtItsOwnRandomMoments_AndNeverOutpacesItsForwardSpeed()
        {
            MobGaitShape shape = new MobGaitShape { width = 1f, interval = 0.5f };
            int Switches(int mob, out float shortest, out float longest, out float maxLateral, out float first)
            {
                MobGaitState s = MobGaitState.For(mob);
                float last = 0f, side = 0f;
                int n = 0;
                shortest = float.MaxValue; longest = 0f; maxLateral = 0f; first = -1f;
                for (float t = 0f; t < 30f; t += Dt)
                {
                    MobGaitStep step = MobGait.Land(MobLandGait.Zigzag, shape, ref s, 3f, Dt);
                    maxLateral = Math.Max(maxLateral, Math.Abs(step.lateral));
                    float now = Math.Sign(step.lateral);
                    if (side != 0f && now != side)
                    {
                        n++;
                        if (first < 0f) first = t;
                        if (n > 1) { shortest = Math.Min(shortest, t - last); longest = Math.Max(longest, t - last); }
                        last = t;
                    }
                    side = now;
                }
                return n;
            }
            int a = Switches(1, out float shortA, out float longA, out float lateralA, out float firstA);
            Switches(2, out _, out _, out _, out float firstB);
            Assert.That(a, Is.GreaterThan(30), "about one switch every half second");
            Assert.That(shortA, Is.GreaterThanOrEqualTo(0.3f - 0.02f), "at least 0.6 of the interval between switches");
            Assert.That(longA, Is.LessThanOrEqualTo(0.7f + 0.02f), "at most 1.4 of the interval");
            Assert.That(Math.Abs(firstA - firstB), Is.GreaterThan(0.015f), "two mobs do not zigzag in step");
            Assert.That(lateralA, Is.LessThanOrEqualTo(3f * 0.9f + 1e-4f), "never faster sideways than forwards");

            MobGaitState still = MobGaitState.For(1);
            Assert.That(MobGait.Land(MobLandGait.Zigzag, shape, ref still, 0f, Dt).lateral, Is.EqualTo(0f), "standing still, it does not drift");
        }

        [Test]
        public void ASlither_SwingsToEitherSide_AndComesBackToItsLine()
        {
            MobGaitShape shape = new MobGaitShape { width = 0.3f, length = 2f };
            MobGaitState s = MobGaitState.For(6);
            float x = 0f, most = 0f, least = 0f;
            // Two full waves: 4 m at 1 m/s.
            for (int i = 0; i < 400; i++)
            {
                x += MobGait.Land(MobLandGait.Slither, shape, ref s, 1f, Dt).lateral * Dt;
                most = Math.Max(most, x);
                least = Math.Min(least, x);
            }
            Assert.That(x, Is.EqualTo(0f).Within(0.02f), "back on its line after whole waves");
            Assert.That(most, Is.EqualTo(0.3f).Within(0.03f), "out to its width on one side");
            Assert.That(least, Is.EqualTo(-0.3f).Within(0.03f), "and out to its width on the other");
        }

        [Test]
        public void Darts_AverageTheirPace_WithABurstAndAGlide()
        {
            float sum = 0f;
            const int n = 1000;
            for (int i = 0; i < n; i++)
                sum += MobGait.DartScale((i + 0.5f) / n);
            Assert.That(sum / n, Is.EqualTo(1f).Within(0.005f));
            Assert.That(MobGait.DartScale(0.1f), Is.EqualTo(2f), "the burst");
            Assert.That(MobGait.DartScale(0.99f), Is.LessThan(0.4f), "the end of the glide");
        }

        [Test]
        public void ASwimmer_KeepsItsBackUnderTheSurface_AndPaddlersKeepTheirHeadOut()
        {
            // A 1 m tall shark (half height 0.5) in 6 m of water (bed at 0, surface at 6), asked to swim 1.5 m down.
            float feet = MobGait.SwimOffset(6f, 0f, 1.5f, 0.5f, false);
            Assert.That(feet + 0.5f, Is.EqualTo(6f - 1.5f).Within(1e-4f), "the middle of its body at its depth");
            // Asked to swim 0.1 m down: its back (feet + 1 m) still stays under.
            float shallow = MobGait.SwimOffset(6f, 0f, 0.1f, 0.5f, false);
            Assert.That(shallow + 1f, Is.LessThan(6f), "its back under the surface");
            // Water shallower than it: it lies on the bottom.
            Assert.That(MobGait.SwimOffset(0.5f, 0f, 1.5f, 0.5f, false), Is.EqualTo(0f));
            // A paddling deer (half height 0.6) in 4 m of water: three quarters under, head out.
            float deer = MobGait.SwimOffset(4f, 0f, 0f, 0.6f, true);
            Assert.That(deer + 1.2f, Is.GreaterThan(4f), "its head is out");
            Assert.That(deer + 0.3f, Is.LessThan(4f), "most of it is under");
            // No water over the ground: 0.
            Assert.That(MobGait.SwimOffset(2f, 3f, 1f, 0.5f, false), Is.EqualTo(0f));
        }

        [Test]
        public void AFlyer_HoldsItsAltitudeOverTheWater_NotOverTheLakeBed()
        {
            Assert.That(MobGait.FlyOffset(10f, float.NaN, 6f), Is.EqualTo(6f), "over land: its altitude");
            Assert.That(MobGait.FlyOffset(10f, 14f, 6f), Is.EqualTo(10f), "over 4 m of water: 6 m above the surface");
            Assert.That(MobGait.Approach(0f, 6f, 3f, 6f, 1f), Is.EqualTo(3f), "climbs at its climb rate");
            Assert.That(MobGait.Approach(6f, 0f, 3f, 6f, 0.5f), Is.EqualTo(3f), "dives at its dive rate");
            Assert.That(MobGait.Approach(5.9f, 6f, 3f, 6f, 1f), Is.EqualTo(6f), "never overshoots");
        }

        [Test]
        public void AGlider_SinksSlowly_ThenClimbsBack_AroundItsAltitude()
        {
            MobGaitShape shape = new MobGaitShape { height = 2f, interval = 5f };
            MobGaitState s = MobGaitState.For(7);
            float lo = 0f, hi = 0f;
            int sinking = 0, climbing = 0;
            float last = MobGait.Air(MobAirGait.Glide, shape, ref s, 4f, Dt).lift;
            for (float t = 0f; t < 20f; t += Dt)
            {
                float lift = MobGait.Air(MobAirGait.Glide, shape, ref s, 4f, Dt).lift;
                lo = Math.Min(lo, lift);
                hi = Math.Max(hi, lift);
                if (lift < last) sinking++; else if (lift > last) climbing++;
                last = lift;
            }
            Assert.That(lo, Is.EqualTo(-1f).Within(0.05f));
            Assert.That(hi, Is.EqualTo(1f).Within(0.05f));
            Assert.That(sinking, Is.GreaterThan(climbing * 3), "most of the time it glides down; it climbs back quickly");
        }

        [Test]
        public void EachHabitat_GoesOnlyWhereItMay()
        {
            var fish = MobLocomotion.Create(MobLocomotion.Preset.Fish);
            Assert.That(fish.AllowsDepth(0f, 0.4f), Is.False, "a fish never onto dry land");
            Assert.That(fish.AllowsDepth(0.3f, 0.4f), Is.False, "nor into water shallower than its Min Swim Depth");
            Assert.That(fish.AllowsDepth(2f, 0.4f), Is.True);

            var wolf = new MobLocomotion();
            Assert.That(wolf.WadeDepth(1f), Is.EqualTo(0.8f).Within(1e-5f), "wades up to 80% of its height");
            Assert.That(wolf.AllowsDepth(0.5f, 1f), Is.True, "through the shallows");
            Assert.That(wolf.AllowsDepth(3f, 1f), Is.False, "never along a lake bed");

            Assert.That(MobLocomotion.Create(MobLocomotion.Preset.Crocodile).AllowsDepth(3f, 0.8f), Is.True, "an amphibian: water...");
            Assert.That(MobLocomotion.Create(MobLocomotion.Preset.Crocodile).AllowsDepth(0f, 0.8f), Is.True, "...and land");
            Assert.That(MobLocomotion.Create(MobLocomotion.Preset.BirdOfPrey).AllowsDepth(30f, 0.8f), Is.True, "a flyer over any water");
        }

        [Test]
        public void EveryPreset_IsValid_AndSaysWhatItDoes()
        {
            foreach (MobLocomotion.Preset p in Enum.GetValues(typeof(MobLocomotion.Preset)))
            {
                MobLocomotion l = MobLocomotion.Create(p);
                var errors = new System.Collections.Generic.List<string>();
                var warnings = new System.Collections.Generic.List<string>();
                l.Validate(p.ToString(), errors, warnings);
                Assert.That(errors, Is.Empty, p.ToString());
                Assert.That(l.Describe(), Is.Not.Empty);
            }
            Assert.That(MobLocomotion.Create(MobLocomotion.Preset.Slime).landGait, Is.EqualTo(MobLandGait.Bounce));
            Assert.That(MobLocomotion.Create(MobLocomotion.Preset.Shark).habitat, Is.EqualTo(MobHabitat.Aquatic));
            Assert.That(MobLocomotion.Create(MobLocomotion.Preset.Bat).landsToRest, Is.False);
        }
    }
}
#endif
