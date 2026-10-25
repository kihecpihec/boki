using System;
using Boki.Player;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class BunnyHopTests
    {
        private static ScooterMotion Make(float headroom = 0.1f) => new ScooterMotion(new ScooterMotionTuning(
            6f, 10.5f, 0.35f, 1f, 1.2f, 25f, 60f, 35f, 25f, 6f, 0.75f, 360f,
            0.1f, 0.1f, 0.1f, 18f, 3f, 1.5f, headroom));

        private static void StartRide(ScooterMotion motion) => motion.Tick(0f, 1f, true, false, false, 0.2f);

        [Test]
        public void FasterStrafingBuildsMoreSpeedThanPreviousTuning()
        {
            var current = Make();
            var previous = new ScooterMotion(new ScooterMotionTuning(
                6f, 10.5f, 0.35f, 1f, 1.2f, 25f, 60f, 20f, 25f, 6f, 2f, 240f,
                0.1f, 0.1f, 0.1f, 6f, 2f));
            foreach (var motion in new[] { current, previous })
            {
                StartRide(motion);
                for (int i = 0; i < 240; i++)
                {
                    float angle = (float)Math.Atan2(motion.HorizontalZ, motion.HorizontalX) + 1f;
                    motion.Tick((float)Math.Cos(angle), (float)Math.Sin(angle), false, false, false, 1f / 240f);
                }
            }
            Assert.That(current.Speed, Is.GreaterThan(previous.Speed + 0.5f));
            Assert.That(current.Speed, Is.LessThanOrEqualTo(18.101f));
        }

        [Test]
        public void FirstJumpDoesNotGrantLandingHopBonus()
        {
            var motion = Make();
            StartRide(motion);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TimedOrBufferedLandingHopAddsSpeed(bool buffered)
        {
            var motion = Make();
            StartRide(motion);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            motion.Tick(0f, 1f, false, buffered, false, 0.05f);
            motion.Tick(0f, 1f, true, !buffered, false, 0.01f);
            Assert.That(motion.Speed, Is.GreaterThan(7.4f).And.LessThanOrEqualTo(7.5f));
        }

        [Test]
        public void RepeatedHopsBuildSpeedWithoutExceedingLimit()
        {
            var motion = Make();
            StartRide(motion);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            for (int i = 0; i < 30; i++)
            {
                motion.Tick(0f, 1f, false, false, false, 0.5f);
                motion.Tick(0f, 1f, true, true, false, 0.01f);
                Assert.That(motion.Speed, Is.LessThanOrEqualTo(18.101f));
            }
            Assert.That(motion.Speed, Is.GreaterThan(18f));
        }

        [TestCase(0f, 18f)]
        [TestCase(0.1f, 18.1f)]
        public void AirStrafingRespectsConfiguredSpeedHeadroom(float headroom, float limit)
        {
            var motion = Make(headroom);
            StartRide(motion);
            for (int i = 0; i < 7200; i++)
            {
                float angle = (float)Math.Atan2(motion.HorizontalZ, motion.HorizontalX) + 1.35f;
                motion.Tick((float)Math.Cos(angle), (float)Math.Sin(angle), false, false, false, 1f / 240f);
                Assert.That(motion.Speed, Is.LessThanOrEqualTo(limit + 0.001f));
            }
            Assert.That(motion.Speed, Is.EqualTo(limit).Within(0.01f));
        }

        [Test]
        public void LateJumpDoesNotReceiveHopBonus()
        {
            var motion = Make();
            StartRide(motion);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            motion.Tick(0f, 1f, false, false, false, 0.5f);
            motion.Tick(0f, 1f, true, false, false, 0.15f);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void HoldingJumpThroughLandingDoesNotAutomaticallyHop()
        {
            var motion = Make();
            StartRide(motion);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            motion.Tick(0f, 1f, false, false, false, 0.5f);
            motion.Tick(0f, 1f, true, false, false, 0.01f);
            Assert.That(motion.VerticalSpeed, Is.LessThanOrEqualTo(0f));
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void StationaryOrReverseLandingJumpDoesNotGainSpeed()
        {
            var motion = Make();
            motion.Tick(0f, 0f, false, false, false, 0.2f);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.LessThan(1f));
            motion.Reset();
            StartRide(motion);
            motion.Tick(0f, 1f, false, false, false, 0.2f);
            motion.Tick(0f, -1f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.LessThanOrEqualTo(6f));
        }

        [Test]
        public void FastDownhillHopIsNotClampedToGainLimit()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 2f, slopeAccelerationZ: 10f);
            motion.Tick(0f, 1f, false, false, false, 0.1f);
            float speed = motion.Speed;
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.GreaterThan(speed - 0.1f).And.LessThanOrEqualTo(speed));
        }
    }
}
