using System;
using NUnit.Framework;

namespace Boki.Enemies.Tests
{
    public sealed class EncounterCadenceTests
    {
        [TestCase(4f, 0f, 4f)]
        [TestCase(4f, 30f, 3.5f)]
        [TestCase(4f, 60f, 3f)]
        [TestCase(4f, 120f, 2f)]
        [TestCase(4f, 600f, 2f)]
        [TestCase(3f, 0f, 3f)]
        [TestCase(3f, 60f, 2.25f)]
        [TestCase(3f, 120f, 1.5f)]
        [TestCase(4f, -1f, 4f)]
        public void IntervalDecreasesSmoothlyAndStopsAtHalf(float initial, float seconds, float expected)
        {
            Assert.That(EncounterCadence.Evaluate(initial, seconds), Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(-10f, 1f, 1f)]
        [TestCase(0f, 1f, 1f)]
        [TestCase(60f, 1.25f, 1.15f)]
        [TestCase(120f, 1.5f, 1.3f)]
        [TestCase(600f, 3.5f, 2.5f)]
        public void RegularEnemyStrengthGrowsBeyondTheCadencePlateau(float seconds, float health, float damage)
        {
            Assert.That(EncounterCadence.HealthMultiplier(seconds), Is.EqualTo(health).Within(0.0001f));
            Assert.That(EncounterCadence.DamageMultiplier(seconds), Is.EqualTo(damage).Within(0.0001f));
        }

        [Test]
        public void SharedClockRetainsProgressOnReentryAndFreezesWhileSuspended()
        {
            var clock = new GuiltState();
            clock.SetCombatSpaceOccupied(1, true);
            clock.SetCombatSpaceOccupied(2, true);
            clock.Tick(60f);
            Assert.That(EncounterCadence.Evaluate(4f, clock.AccumulatedCombatSeconds), Is.EqualTo(3f));
            clock.SetCombatSpaceOccupied(1, false);
            clock.SetCombatSpaceOccupied(2, false);
            clock.Tick(60f);
            clock.SetCombatSpaceOccupied(1, true);
            clock.GameplaySuspended = true;
            clock.Tick(60f);
            clock.GameplaySuspended = false;
            clock.IsLoading = true;
            clock.Tick(60f);
            clock.IsLoading = false;
            clock.ScriptedSequenceActive = true;
            clock.Tick(60f);
            Assert.That(EncounterCadence.Evaluate(4f, clock.AccumulatedCombatSeconds), Is.EqualTo(3f));
            clock.ScriptedSequenceActive = false;
            clock.Tick(60f);
            Assert.That(EncounterCadence.Evaluate(4f, clock.AccumulatedCombatSeconds), Is.EqualTo(2f));
            clock.Reset();
            Assert.That(EncounterCadence.Evaluate(4f, clock.AccumulatedCombatSeconds), Is.EqualTo(4f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidInitialIntervalIsRejected(float initial)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EncounterCadence.Evaluate(initial, 0f));
        }
    }
}
