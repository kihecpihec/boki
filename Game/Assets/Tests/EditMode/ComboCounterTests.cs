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
        public void EachKillRaisesCountAndNotifies()
        {
            var combo = new ComboCounter();
            int changes = 0;
            combo.Changed += () => changes++;
            combo.RegisterKill();
            combo.RegisterKill();
            combo.RegisterKill();
            Assert.That(combo.Count, Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void ResetClearsStreakAndNotifies()
        {
            var combo = new ComboCounter();
            combo.RegisterKill();
            combo.RegisterKill();
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
            combo.RegisterKill();
            combo.RegisterKill();
            combo.Reset();
            combo.RegisterKill();
            Assert.That(combo.Count, Is.EqualTo(1));
        }
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(20)]
        public void MilestoneCanBeReachedAgainAfterBreakingStreak(int kills)
        {
            var combo = new ComboCounter();
            for (int i = 0; i < kills; i++) combo.RegisterKill();
            Assert.That(combo.Count, Is.EqualTo(kills));
            combo.Reset();
            for (int i = 0; i < kills; i++) combo.RegisterKill();
            Assert.That(combo.Count, Is.EqualTo(kills));
        }

    }
}
