using System;
using Boki.Player;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class MomentumGainTests
    {
        private static ScooterMotion Make() => new ScooterMotion(new ScooterMotionTuning(
            6f, 10.5f, 0.35f, 1f, 1.2f, 25f, 60f, 20f, 25f, 6f, 2f, 240f, 0.1f, 0.1f, 0.1f, 6f, 2f));

        [Test]
        public void DownhillBuildsSpeedAndJumpCarriesItWithoutFurtherSlopeAcceleration()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 1f, slopeAccelerationZ: 3f);
            Assert.That(motion.Speed, Is.GreaterThan(8f));
            float speed = motion.Speed;
            motion.Tick(0f, 1f, true, true, false, 0.01f, slopeAccelerationZ: 3f);
            Assert.That(motion.Speed, Is.LessThanOrEqualTo(speed));
            Assert.That(motion.Speed, Is.GreaterThan(6f));
        }

        [Test]
        public void UphillSlowsRidingAndBrakingCanStopOnSlope()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 0.3f, slopeAccelerationZ: -3f);
            Assert.That(motion.Speed, Is.LessThan(6f));
            motion.Tick(0f, 0f, true, false, false, 1f, slopeAccelerationZ: 3f);
            Assert.That(motion.Speed, Is.Zero.Within(0.001f));
        }

        [Test]
        public void FlatGroundDecaysDownhillOverspeed()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 1f, slopeAccelerationZ: 3f);
            motion.Tick(0f, 1f, true, false, false, 2f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void ForwardInputAndJumpAloneDoNotBuildExtraAirSpeed()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 0.2f);
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            motion.Tick(0f, 1f, false, false, false, 0.5f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void SteeringAcrossVelocityBuildsAirSpeedWithinTunableLimit()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 0.2f);
            for (int i = 0; i < 7200; i++)
            {
                float angle = (float)Math.Atan2(motion.HorizontalZ, motion.HorizontalX) + 1.15f;
                motion.Tick((float)Math.Cos(angle), (float)Math.Sin(angle), false, false, false, 1f / 240f);
            }
            Assert.That(motion.Speed, Is.GreaterThan(11.9f).And.LessThanOrEqualTo(12.001f));
        }

        [Test]
        public void AirStrafeLimitDoesNotClampFasterDownhillMomentum()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 2f, slopeAccelerationZ: 10f);
            float speed = motion.Speed;
            Assert.That(speed, Is.GreaterThan(12f));
            motion.Tick(1f, 0f, false, false, false, 0.01f);
            Assert.That(motion.Speed, Is.GreaterThan(speed - 0.1f).And.LessThanOrEqualTo(speed));
        }

        [Test]
        public void GainDistanceAgreesAt30And144Fps()
        {
            float[] distances = new float[2];
            foreach (int fps in new[] { 30, 144 })
            {
                var motion = Make();
                float distance = 0f;
                for (int i = 0; i < fps; i++)
                {
                    motion.Tick(0f, 1f, true, false, false, 1f / fps, slopeAccelerationZ: 3f);
                    distance += motion.DisplacementZ;
                }
                distances[fps == 30 ? 0 : 1] = distance;
            }
            Assert.That(distances[0], Is.EqualTo(distances[1]).Within(0.02f));
        }
    }
}
