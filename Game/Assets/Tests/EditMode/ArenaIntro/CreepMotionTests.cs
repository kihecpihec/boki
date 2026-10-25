using System;
using Boki.ArenaIntro;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class CreepMotionTests
    {
        private const float Duration = 8f;
        private const int Lurches = 8;
        private static CreepMotion Creep() => new CreepMotion(Duration, Lurches, 0.5f);

        [Test]
        public void ItStartsAtRestAndEndsHavingCoveredTheWholeApproach()
        {
            CreepMotion creep = Creep();
            Assert.That(creep.Progress(-1f), Is.Zero);
            Assert.That(creep.Progress(0f), Is.Zero);
            Assert.That(creep.Progress(Duration), Is.EqualTo(1f));
            Assert.That(creep.Progress(Duration + 5f), Is.EqualTo(1f));
        }

        [Test]
        public void ItNeverGoesBackwardsOrOutsideTheApproach()
        {
            CreepMotion creep = Creep();
            float previous = 0f;
            for (float t = 0f; t <= Duration + 0.5f; t += 0.003f)
            {
                float progress = creep.Progress(t);
                Assert.That(progress, Is.InRange(0f, 1f));
                Assert.That(progress, Is.GreaterThanOrEqualTo(previous));
                previous = progress;
            }
        }

        [Test]
        public void ItHoldsStillBetweenLurchesAndMovesDuringThem()
        {
            CreepMotion creep = Creep();
            float cycle = Duration / Lurches;
            // Third cycle: the first half moves, the second half is still.
            float movingAt = 2f * cycle + 0.2f * cycle;
            float stillFrom = 2f * cycle + 0.6f * cycle;
            float stillTo = 2f * cycle + 0.95f * cycle;
            Assert.That(creep.IsMoving(movingAt), Is.True);
            Assert.That(creep.IsMoving(stillFrom), Is.False);
            Assert.That(creep.Progress(stillTo), Is.EqualTo(creep.Progress(stillFrom)).Within(1e-6f));
            Assert.That(creep.Progress(movingAt + 0.05f), Is.GreaterThan(creep.Progress(movingAt)));
        }

        [Test]
        public void EveryLurchEndsOnAWholeShareOfTheApproach()
        {
            CreepMotion creep = Creep();
            float cycle = Duration / Lurches;
            for (int i = 0; i < Lurches - 1; i++)
                Assert.That(creep.Progress((i + 0.9f) * cycle), Is.EqualTo((i + 1f) / Lurches).Within(1e-5f));
        }

        [Test]
        public void ItIsStillBeforeItBeginsAndAfterItArrives()
        {
            CreepMotion creep = Creep();
            Assert.That(creep.IsMoving(0f), Is.False);
            Assert.That(creep.IsMoving(-2f), Is.False);
            Assert.That(creep.IsMoving(Duration), Is.False);
            Assert.That(creep.IsMoving(Duration + 3f), Is.False);
        }

        [Test]
        public void FootstepsCountTwoPerLurchAndNeverFall()
        {
            CreepMotion creep = Creep();
            Assert.That(creep.Footfalls(-1f), Is.Zero);
            Assert.That(creep.Footfalls(0f), Is.Zero);
            Assert.That(creep.Footfalls(Duration), Is.EqualTo(CreepMotion.FootfallsPerLurch * Lurches));
            int previous = 0;
            for (float t = 0f; t <= Duration; t += 0.01f)
            {
                int count = creep.Footfalls(t);
                Assert.That(count, Is.GreaterThanOrEqualTo(previous));
                Assert.That(count - previous, Is.LessThanOrEqualTo(1));
                previous = count;
            }
        }

        [Test]
        public void FootstepsFallInsideTheLurchesNotTheStillness()
        {
            CreepMotion creep = Creep();
            float cycle = Duration / Lurches;
            // Stepping happens at 25% and 75% of the moving half, so none lands in the second half of a cycle.
            for (int i = 0; i < Lurches; i++)
                Assert.That(creep.Footfalls((i + 0.55f) * cycle), Is.EqualTo(CreepMotion.FootfallsPerLurch * (i + 1)));
        }

        [Test]
        public void InvalidShapesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreepMotion(0f, 4, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreepMotion(float.PositiveInfinity, 4, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreepMotion(float.NaN, 4, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreepMotion(4f, 0, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreepMotion(4f, 4, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CreepMotion(4f, 4, 1f));
        }
    }
}
