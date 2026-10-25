using System;
using Boki.Ending;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class EndingMotionTests
    {
        private const float Period = 0.5f;

        [Test]
        public void NothingMovesBeforeTheLickingBegins()
        {
            Assert.That(EndingMotion.Reach(-1f, Period), Is.Zero);
            Assert.That(EndingMotion.Reach(0f, Period), Is.Zero);
            Assert.That(EndingMotion.Along(-1f, Period), Is.Zero);
            Assert.That(EndingMotion.StrokePhase(0f, Period), Is.Zero);
        }

        [Test]
        public void TheTongueIsInBetweenStrokesAndOutThroughTheMiddleOfOne()
        {
            Assert.That(EndingMotion.Reach(Period, Period), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(EndingMotion.Reach(Period * 3f, Period), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(EndingMotion.Reach(Period * 0.5f, Period), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(EndingMotion.Reach(Period * 2.5f, Period), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ReachAndAlongStayInsideZeroAndOne()
        {
            for (float t = -0.2f; t <= 6f; t += 0.007f)
            {
                float reach = EndingMotion.Reach(t, Period);
                float along = EndingMotion.Along(t, Period);
                Assert.That(reach, Is.InRange(0f, 1f));
                Assert.That(along, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void EveryStrokeRepeatsTheFirst()
        {
            for (float t = 0.01f; t < Period; t += 0.01f)
            {
                Assert.That(EndingMotion.Reach(t + Period, Period), Is.EqualTo(EndingMotion.Reach(t, Period)).Within(1e-3f));
                Assert.That(EndingMotion.Along(t + 3f * Period, Period), Is.EqualTo(EndingMotion.Along(t, Period)).Within(1e-3f));
            }
        }

        [Test]
        public void TheTongueSweepsFromWristToElbowThenStartsAgain()
        {
            float previous = EndingMotion.Along(0.001f, Period);
            for (float t = 0.01f; t < Period * 0.99f; t += 0.01f)
            {
                float along = EndingMotion.Along(t, Period);
                Assert.That(along, Is.GreaterThanOrEqualTo(previous));
                previous = along;
            }
            Assert.That(previous, Is.GreaterThan(0.95f));
            Assert.That(EndingMotion.Along(Period + 0.01f, Period), Is.LessThan(0.05f));
        }

        [Test]
        public void InvalidPeriodsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EndingMotion.Reach(1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndingMotion.Along(1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndingMotion.StrokePhase(1f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => EndingMotion.Reach(-1f, float.PositiveInfinity));
        }
    }
}
