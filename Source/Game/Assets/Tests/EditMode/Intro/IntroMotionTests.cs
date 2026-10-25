using System;
using Boki.Intro;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class IntroMotionTests
    {
        [Test]
        public void PopIsZeroBeforeTheTriggerAndExactlyOneWhenDone()
        {
            Assert.That(IntroMotion.Pop(-1f, 0.5f), Is.Zero);
            Assert.That(IntroMotion.Pop(0f, 0.5f), Is.Zero);
            Assert.That(IntroMotion.Pop(0.5f, 0.5f), Is.EqualTo(1f));
            Assert.That(IntroMotion.Pop(30f, 0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void PopOvershootsBeforeSettling()
        {
            float peak = 0f;
            for (float t = 0f; t <= 0.5f; t += 0.005f) peak = Math.Max(peak, IntroMotion.Pop(t, 0.5f));
            Assert.That(peak, Is.GreaterThan(1.1f));
            Assert.That(peak, Is.LessThan(1.5f));
        }

        [Test]
        public void PopStaysFiniteAndContinuousIntoTheSettle()
        {
            float previous = IntroMotion.Pop(0f, 0.4f);
            for (float t = 0.001f; t <= 0.4f; t += 0.001f)
            {
                float value = IntroMotion.Pop(t, 0.4f);
                Assert.That(float.IsFinite(value), Is.True);
                Assert.That(Math.Abs(value - previous), Is.LessThan(0.05f));
                previous = value;
            }
        }

        [Test]
        public void ImpactStartsAtOneAndOnlyFalls()
        {
            Assert.That(IntroMotion.Impact(-0.1f, 6f), Is.Zero);
            Assert.That(IntroMotion.Impact(0f, 6f), Is.EqualTo(1f));
            Assert.That(IntroMotion.Impact(0.2f, 6f), Is.LessThan(IntroMotion.Impact(0.1f, 6f)));
            Assert.That(IntroMotion.Impact(5f, 6f), Is.LessThan(0.001f));
        }

        [Test]
        public void TremorIsQuietBeforeTheTriggerAndBoundedByItsEnvelope()
        {
            Assert.That(IntroMotion.Tremor(-1f, 20f, 4f), Is.Zero);
            for (float t = 0.001f; t < 2f; t += 0.003f)
                Assert.That(Math.Abs(IntroMotion.Tremor(t, 20f, 4f)), Is.LessThanOrEqualTo(IntroMotion.Impact(t, 4f) + 1e-6f));
        }

        [Test]
        public void InvalidCurveParametersAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IntroMotion.Pop(0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => IntroMotion.Pop(0.1f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => IntroMotion.Impact(0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => IntroMotion.Tremor(0.1f, 0f, 4f));
        }
    }
}
