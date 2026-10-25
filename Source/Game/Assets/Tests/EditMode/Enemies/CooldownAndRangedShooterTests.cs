using System;
using Boki.Enemies;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class CooldownAndRangedShooterTests
    {
        [Test]
        public void CooldownTriggersOnlyWhenReadyAndThenRestarts()
        {
            var cooldown = new CooldownTimer(1f);
            Assert.That(cooldown.TryTrigger(), Is.True);
            Assert.That(cooldown.TryTrigger(), Is.False);
            cooldown.Tick(0.99f);
            Assert.That(cooldown.TryTrigger(), Is.False);
            cooldown.Tick(0.02f);
            Assert.That(cooldown.TryTrigger(), Is.True);
        }

        [Test]
        public void InitialDelayBlocksFirstTrigger()
        {
            var cooldown = new CooldownTimer(1f, 0.5f);
            Assert.That(cooldown.TryTrigger(), Is.False);
            cooldown.Tick(0.5f);
            Assert.That(cooldown.TryTrigger(), Is.True);
        }

        [Test]
        public void NonPositiveIntervalIsRejected() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new CooldownTimer(0f));

        [Test]
        public void ShooterFiresInRangeWithLineOfSightThenWaitsForCooldown()
        {
            var shooter = new RangedShooterState(15f, 2f, 0f);
            Assert.That(shooter.Tick(0.1f, 10f, true), Is.True);
            Assert.That(shooter.Tick(1.9f, 10f, true), Is.False);
            Assert.That(shooter.Tick(0.2f, 10f, true), Is.True);
        }

        [TestCase(15.01f, true)]
        [TestCase(10f, false)]
        public void ShooterHoldsFireOutOfRangeOrWithoutLineOfSight(float distance, bool lineOfSight)
        {
            var shooter = new RangedShooterState(15f, 2f, 0f);
            Assert.That(shooter.Tick(0.1f, distance, lineOfSight), Is.False);
        }

        [Test]
        public void ShooterFiresOnceWhenTargetReturnsAfterLongBlock()
        {
            var shooter = new RangedShooterState(15f, 2f, 0f);
            for (int i = 0; i < 10; i++) shooter.Tick(1f, 30f, true);
            Assert.That(shooter.Tick(0.1f, 10f, true), Is.True);
            Assert.That(shooter.Tick(0.1f, 10f, true), Is.False);
        }
    }
}
