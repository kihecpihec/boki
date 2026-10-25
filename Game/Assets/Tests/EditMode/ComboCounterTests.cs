using Boki.Combat;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class ComboCounterTests
    {
        [Test]
        public void StartsAtZero()
        {
            Assert.That(new ComboCounter().Count, Is.EqualTo(0));
        }

        [Test]
        public void EachHitRaisesCountAndNotifies()
        {
            var combo = new ComboCounter();
            int changes = 0;
            combo.Changed += () => changes++;
            combo.RegisterHit();
            combo.RegisterHit();
            combo.RegisterHit();
            Assert.That(combo.Count, Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void ResetClearsStreakAndNotifies()
        {
            var combo = new ComboCounter();
            combo.RegisterHit();
            combo.RegisterHit();
            int changes = 0;
            combo.Changed += () => changes++;
            combo.Reset();
            Assert.That(combo.Count, Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ResetWithoutStreakDoesNotNotify()
        {
            var combo = new ComboCounter();
            int changes = 0;
            combo.Changed += () => changes++;
            combo.Reset();
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void StreakRestartsFromOneAfterReset()
        {
            var combo = new ComboCounter();
            combo.RegisterHit();
            combo.RegisterHit();
            combo.Reset();
            combo.RegisterHit();
            Assert.That(combo.Count, Is.EqualTo(1));
        }
    }
}
