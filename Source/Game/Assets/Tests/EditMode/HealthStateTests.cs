using System;
using Boki.Combat;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class HealthStateTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        public void MaximumMustBePositive(int maximum)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthState(maximum));
        }

        [Test]
        public void StartsAtFullHealth()
        {
            var health = new HealthState(100);
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.Maximum, Is.EqualTo(100));
            Assert.That(health.IsAlive, Is.True);
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void NonPositiveDamageDoesNotChangeHealthOrNotify(int damage)
        {
            var health = new HealthState(100);
            int changes = 0;
            health.Changed += () => changes++;
            health.TakeDamage(damage);
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void NonlethalDamageNotifiesWithUpdatedHealth()
        {
            var health = new HealthState(100);
            int observed = -1;
            int deaths = 0;
            health.Changed += () => observed = health.Current;
            health.Died += () => deaths++;
            health.TakeDamage(25);
            Assert.That(observed, Is.EqualTo(75));
            Assert.That(health.IsAlive, Is.True);
            Assert.That(deaths, Is.Zero);
        }

        [TestCase(100)]
        [TestCase(int.MaxValue)]
        public void LethalDamageClampsAndNotifiesDeathOnlyOnce(int damage)
        {
            var health = new HealthState(100);
            int deaths = 0;
            int changes = 0;
            health.Died += () => deaths++;
            health.Changed += () => changes++;
            health.TakeDamage(damage);
            health.TakeDamage(damage);
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void DamageFromChangeListenerCannotRepeatDeath()
        {
            var health = new HealthState(100);
            int deaths = 0;
            health.Changed += () => health.TakeDamage(100);
            health.Died += () => deaths++;
            health.TakeDamage(100);
            Assert.That(deaths, Is.EqualTo(1));
        }
    }
}
