using System;
using Boki.Intro;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class OpeningCrawlRulesTests
    {
        [Test]
        public void CrawlLastsUntilTheLastLineHasLeftThePlane()
        {
            Assert.That(OpeningCrawlRules.CrawlSeconds(1200f, 1800f, 100f), Is.EqualTo(30f).Within(1e-4f));
        }

        [Test]
        public void ALongerTextOrASlowerSpeedTakesLonger()
        {
            float baseline = OpeningCrawlRules.CrawlSeconds(1000f, 1500f, 80f);
            Assert.That(OpeningCrawlRules.CrawlSeconds(1400f, 1500f, 80f), Is.GreaterThan(baseline));
            Assert.That(OpeningCrawlRules.CrawlSeconds(1000f, 1500f, 60f), Is.GreaterThan(baseline));
        }

        [TestCase(0f, 1500f, 80f)]
        [TestCase(-5f, 1500f, 80f)]
        [TestCase(1000f, 0f, 80f)]
        [TestCase(1000f, 1500f, 0f)]
        [TestCase(float.NaN, 1500f, 80f)]
        [TestCase(1000f, 1500f, float.PositiveInfinity)]
        public void CrawlSecondsRejectsNonsenseSizes(float height, float travel, float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OpeningCrawlRules.CrawlSeconds(height, travel, speed));
        }

        [Test]
        public void ScrollStartsAtRestAndRisesAtTheGivenSpeed()
        {
            Assert.That(OpeningCrawlRules.ScrollOffset(-1f, 90f), Is.Zero);
            Assert.That(OpeningCrawlRules.ScrollOffset(0f, 90f), Is.Zero);
            Assert.That(OpeningCrawlRules.ScrollOffset(2f, 90f), Is.EqualTo(180f).Within(1e-4f));
        }

        [Test]
        public void ScrollAtTheEndOfTheCrawlMatchesTheCrawlDistance()
        {
            float seconds = OpeningCrawlRules.CrawlSeconds(1200f, 1800f, 75f);
            Assert.That(OpeningCrawlRules.ScrollOffset(seconds, 75f), Is.EqualTo(3000f).Within(1e-2f));
        }

        [Test]
        public void RampIsClampedAndSmooth()
        {
            Assert.That(OpeningCrawlRules.Ramp(-1f, 0f, 2f), Is.Zero);
            Assert.That(OpeningCrawlRules.Ramp(1f, 0f, 2f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(OpeningCrawlRules.Ramp(5f, 0f, 2f), Is.EqualTo(1f));
            Assert.Throws<ArgumentException>(() => OpeningCrawlRules.Ramp(1f, 2f, 2f));
        }

        [Test]
        public void EnvelopeFadesInHoldsThenFadesOut()
        {
            Assert.That(OpeningCrawlRules.Envelope(0f, 6f, 1f, 1f), Is.Zero);
            Assert.That(OpeningCrawlRules.Envelope(0.5f, 6f, 1f, 1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(OpeningCrawlRules.Envelope(3f, 6f, 1f, 1f), Is.EqualTo(1f));
            Assert.That(OpeningCrawlRules.Envelope(5.5f, 6f, 1f, 1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(OpeningCrawlRules.Envelope(6f, 6f, 1f, 1f), Is.Zero);
        }

        [Test]
        public void EnvelopeWithOverlongFadesNeverExceedsOneAndPeaksMidSpan()
        {
            for (float t = 0f; t <= 2f; t += 0.1f)
                Assert.That(OpeningCrawlRules.Envelope(t, 2f, 3f, 3f), Is.InRange(0f, 1f));
            Assert.That(OpeningCrawlRules.Envelope(1f, 2f, 3f, 3f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void EnvelopeWithoutFadesIsFullInsideTheSpanAndEmptyOutside()
        {
            Assert.That(OpeningCrawlRules.Envelope(0f, 4f, 0f, 0f), Is.EqualTo(1f));
            Assert.That(OpeningCrawlRules.Envelope(4f, 4f, 0f, 0f), Is.EqualTo(1f));
            Assert.That(OpeningCrawlRules.Envelope(-0.1f, 4f, 0f, 0f), Is.Zero);
            Assert.That(OpeningCrawlRules.Envelope(4.1f, 4f, 0f, 0f), Is.Zero);
        }

        [Test]
        public void EnvelopeRejectsAnEmptySpan()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OpeningCrawlRules.Envelope(0f, 0f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => OpeningCrawlRules.Envelope(0f, 4f, -1f, 1f));
        }
    }
}
