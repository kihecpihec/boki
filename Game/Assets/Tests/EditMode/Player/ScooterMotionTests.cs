using Boki.Player;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class ScooterMotionTests
    {
        private static ScooterMotion Make() => new ScooterMotion(new ScooterMotionTuning(
            6f, 10.5f, 0.35f, 1f, 1.2f, 25f, 60f, 20f, 25f, 6f, 2f, 240f, 0.1f, 0.1f, 0.1f));

        [Test]
        public void AcceleratesWithoutExceedingCapAndNormalizesDiagonalInput()
        {
            var motion = Make();
            motion.Tick(1f, 1f, true, false, false, 0.02f);
            Assert.That(motion.Speed, Is.GreaterThan(0f).And.LessThan(6f));
            motion.Tick(1f, 1f, true, false, false, 1f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void AirborneReleasePreservesNormalSpeedAndJumpCarriesMomentum()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, false, 0.2f);
            motion.Tick(0f, 0f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
            Assert.That(motion.VerticalSpeed, Is.GreaterThan(0f));
            motion.Tick(0f, 0f, false, false, false, 0.2f);
            Assert.That(motion.Speed, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void AirSteeringDoesNotInstantlyReverseVelocity()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, true, true, 0.01f);
            motion.Tick(0f, -1f, false, false, false, 0.02f);
            Assert.That(motion.HorizontalZ, Is.GreaterThan(0f));
        }

        [Test]
        public void BoostEndsWithSoftDecayAndCooldownStartsAfterDuration()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, false, true, 0.35f);
            Assert.That(motion.Speed, Is.EqualTo(10.5f).Within(0.002f));
            motion.Tick(0f, 1f, true, false, true, 0.1f);
            Assert.That(motion.Speed, Is.GreaterThan(6f).And.LessThan(10.5f));
            Assert.That(motion.CooldownRemaining, Is.EqualTo(0.9f).Within(0.002f));
            motion.Tick(0f, 1f, true, false, false, 1f);
            motion.Tick(0f, 1f, true, false, true, 0.01f);
            Assert.That(motion.BoostRemaining, Is.GreaterThan(0f));
        }

        [Test]
        public void BoostCannotStartInAir()
        {
            var motion = Make();
            motion.Tick(0f, 1f, false, false, true, 0.01f);
            Assert.That(motion.BoostRemaining, Is.Zero);
        }

        [Test]
        public void StationaryBoostFollowsPlayerFacing()
        {
            var motion = Make();
            motion.Tick(0f, 0f, true, false, true, 0.01f, 1f, 0f);
            Assert.That(motion.HorizontalX, Is.EqualTo(10.5f).Within(0.001f));
            Assert.That(motion.HorizontalZ, Is.Zero.Within(0.001f));
        }

        [Test]
        public void GroundBrakingResumesAfterLandingGrace()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, true, false, 0.2f);
            motion.Tick(0f, 0f, false, false, false, 0.3f);
            motion.Tick(0f, 0f, true, false, false, 0.25f);
            Assert.That(motion.Speed, Is.LessThan(6f));
            motion.Tick(0f, 0f, true, false, false, 1f);
            Assert.That(motion.Speed, Is.Zero.Within(0.001f));
        }

        [Test]
        public void SuspensionClearsPendingJumpAndMomentum()
        {
            var motion = Make();
            motion.Tick(0f, 1f, false, true, false, 0.01f);
            motion.SuspendInput();
            motion.Tick(0f, 0f, true, false, false, 0.01f);
            Assert.That(motion.VerticalSpeed, Is.LessThanOrEqualTo(0f));
            Assert.That(motion.Speed, Is.Zero);
        }

        [TestCase(0.05f, true)]
        [TestCase(0.12f, false)]
        public void CoyoteJumpHasBoundedWindow(float delay, bool expected)
        {
            var motion = Make();
            motion.Tick(0f, 0f, true, false, false, 0.01f);
            motion.Tick(0f, 0f, false, false, false, delay);
            motion.Tick(0f, 0f, false, true, false, 0.01f);
            Assert.That(motion.VerticalSpeed > 0f, Is.EqualTo(expected));
        }

        [TestCase(0.05f, true)]
        [TestCase(0.12f, false)]
        public void BufferedJumpFiresOnlyWhenLandingBeforeExpiry(float delay, bool expected)
        {
            var motion = Make();
            motion.Tick(0f, 0f, false, true, false, delay);
            motion.Tick(0f, 0f, true, false, false, 0.01f);
            Assert.That(motion.VerticalSpeed > 0f, Is.EqualTo(expected));
        }

        [Test]
        public void LingeringGroundContactCannotCauseDoubleJump()
        {
            var motion = Make();
            motion.Tick(0f, 0f, true, true, false, 0.05f);
            float before = motion.VerticalSpeed;
            motion.Tick(0f, 0f, true, true, false, 0.01f);
            Assert.That(motion.VerticalSpeed, Is.LessThan(before));
        }

        [Test]
        public void LandingGracePreservesSpeedAndBufferedHopRetainsOverspeed()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, true, true, 0.01f);
            motion.Tick(0f, 1f, false, false, false, 0.5f);
            float speed = motion.Speed;
            motion.Tick(0f, 0f, true, false, false, 0.04f);
            Assert.That(motion.Speed, Is.EqualTo(speed).Within(0.001f));
            motion.Tick(0f, 1f, true, true, false, 0.01f);
            Assert.That(motion.Speed, Is.GreaterThan(6f));
            Assert.That(motion.VerticalSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void WallCollisionRemovesInwardSpeedButPreservesTangent()
        {
            var motion = Make();
            motion.Tick(1f, 1f, true, false, false, 0.2f);
            float tangent = motion.HorizontalZ;
            motion.ResolveWall(-1f, 0f);
            Assert.That(motion.HorizontalX, Is.Zero.Within(0.001f));
            Assert.That(motion.HorizontalZ, Is.EqualTo(tangent).Within(0.001f));
            motion.ResolveWall(0f, -1f);
            Assert.That(motion.Speed, Is.Zero.Within(0.001f));
        }

        [Test]
        public void CeilingStopsRiseAndResetClearsStoredInputAndMomentum()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, true, true, 0.01f);
            motion.ResolveVertical(false, true);
            Assert.That(motion.VerticalSpeed, Is.Zero);
            motion.Reset();
            motion.Tick(0f, 0f, true, false, false, 0.01f);
            Assert.That(motion.Speed, Is.Zero);
            Assert.That(motion.VerticalSpeed, Is.LessThanOrEqualTo(0f));
            Assert.That(motion.BoostRemaining, Is.Zero);
        }

        [Test]
        public void ZeroDeltaDoesNotConsumeJumpOrBoost()
        {
            var motion = Make();
            motion.Tick(0f, 1f, true, true, true, 0f);
            Assert.That(motion.Speed, Is.Zero);
            Assert.That(motion.VerticalSpeed, Is.Zero);
            Assert.That(motion.BoostRemaining, Is.Zero);
        }

        [Test]
        public void AccelerationBoostDistanceAndCooldownAgreeAt30And144Fps()
        {
            float[] distances = new float[2];
            foreach (int fps in new[] { 30, 144 })
            {
                var motion = Make();
                float distance = 0f;
                for (int i = 0; i < fps * 2; i++)
                {
                    motion.Tick(0f, 1f, true, false, i == 0, 1f / fps);
                    distance += motion.DisplacementZ;
                }
                distances[fps == 30 ? 0 : 1] = distance;
                Assert.That(motion.CooldownRemaining, Is.Zero);
            }
            Assert.That(distances[0], Is.EqualTo(distances[1]).Within(0.02f));
        }
    }
}
