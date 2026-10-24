using Boki.Player;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class UpgradeMovementTests
    {
        private static ScooterMotion Motion() => new ScooterMotion(new ScooterMotionTuning(
            6, 10.5f, 0.35f, 1, 1.2f, 25, 60, 35, 25, 6, 0.75f, 360, 0.1f, 0.1f, 0.1f));

        [Test]
        public void TurboRaisesRidingCapWithoutRaisingBoostSpeed()
        {
            var motion = Motion(); motion.SetModifiers(1.4f, false);
            motion.Tick(0, 1, true, false, false, 1);
            Assert.That(motion.Speed, Is.EqualTo(8.4f).Within(0.001f));
            motion.Tick(0, 1, true, false, true, 0.01f);
            Assert.That(motion.Speed, Is.EqualTo(10.5f).Within(0.001f));
        }

        [Test]
        public void PanicBoostSnapshotsItsDurationAndSpeed()
        {
            var motion = Motion();
            motion.SetModifiers(1f, true);
            motion.Tick(0, 1, true, false, true, 0.01f);
            Assert.That(motion.Speed, Is.EqualTo(13.125f).Within(0.001f));
            Assert.That(motion.BoostRemaining, Is.EqualTo(0.515f).Within(0.001f));
            motion.SetModifiers(1f, false);
            motion.Tick(0, 1, true, false, false, 0.35f);
            Assert.That(motion.BoostRemaining, Is.GreaterThan(0.15f));
            Assert.That(motion.Speed, Is.EqualTo(13.125f).Within(0.001f));
        }
    }
}
