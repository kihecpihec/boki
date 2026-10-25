using NUnit.Framework;
using Boki.Routes;

namespace Boki.Tests
{
    public sealed class PortalProximityTests
    {
        [TestCase(0f, 1f)]
        [TestCase(9f, 1f)]
        [TestCase(32f, 0f)]
        [TestCase(80f, 0f)]
        [TestCase(20.5f, 0.5f)]
        public void IntensityFollowsDistanceBand(float distance, float expected)
        {
            Assert.That(PortalProximity.Intensity(distance, 9f, 32f), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void IntensityNeverGrowsWithDistance()
        {
            float previous = 1f;
            for (float distance = 0f; distance <= 40f; distance += 0.5f)
            {
                float intensity = PortalProximity.Intensity(distance, 9f, 32f);
                Assert.That(intensity, Is.InRange(0f, 1f));
                Assert.That(intensity, Is.LessThanOrEqualTo(previous));
                previous = intensity;
            }
        }

        [Test]
        public void MissingDistanceShowsNothing()
        {
            Assert.That(PortalProximity.Intensity(float.NaN, 9f, 32f), Is.EqualTo(0f));
        }

        [Test]
        public void InvertedBandStaysABoundedStep()
        {
            Assert.That(PortalProximity.Intensity(5f, 9f, 9f), Is.EqualTo(1f));
            Assert.That(PortalProximity.Intensity(12f, 9f, 9f), Is.EqualTo(0f));
            Assert.That(PortalProximity.Intensity(12f, 9f, 4f), Is.EqualTo(0f));
        }
    }
}
