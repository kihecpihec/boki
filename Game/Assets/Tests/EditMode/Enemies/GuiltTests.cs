using Boki.Enemies;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class GuiltTests
    {
        [TestCase(0f, 18f, 4.5f, 10)]
        [TestCase(29.999f, 18f, 4.5f, 10)]
        [TestCase(30f, 15f, 5.0f, 12)]
        [TestCase(60f, 12f, 5.5f, 14)]
        [TestCase(90f, 9f, 6.0f, 16)]
        [TestCase(120f, 6f, 6.5f, 18)]
        [TestCase(600f, 6f, 6.5f, 18)]
        public void TierMatchesGameplayTable(float seconds, float rest, float speed, int damage)
        {
            GuiltTier tier = GuiltPressure.Evaluate(seconds);
            Assert.That(tier.RestSeconds, Is.EqualTo(rest));
            Assert.That(tier.ChaseSpeed, Is.EqualTo(speed));
            Assert.That(tier.ContactDamage, Is.EqualTo(damage));
        }

        [Test]
        public void StaysDistantThroughTwentySecondGrace()
        {
            var guilt = new GuiltState();
            guilt.Tick(19.9f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void WarningThenChaseThenRestFollowGrace()
        {
            var guilt = new GuiltState();
            guilt.Tick(20f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
            guilt.Tick(1.5f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
            guilt.Tick(6f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void ChaseEndPassesThroughRepositionBeforeRest()
        {
            var guilt = new GuiltState();
            var phases = new System.Collections.Generic.List<GuiltPhase>();
            guilt.PhaseChanged += phases.Add;
            guilt.Tick(20f);
            guilt.Tick(1.5f);
            guilt.Tick(6f);
            Assert.That(phases, Is.EqualTo(new[]
            {
                GuiltPhase.Warning, GuiltPhase.Chase, GuiltPhase.Reposition, GuiltPhase.Distant
            }));
        }

        [Test]
        public void RestUsesAccumulatedTimeWhenRestBegins()
        {
            var guilt = new GuiltState();
            guilt.Tick(20f);   // warning; accumulated 20 s, first tier
            guilt.Tick(1.5f);  // chase
            guilt.Tick(6f);    // rest starts at 27.5 s: 18 s rest
            guilt.Tick(17.9f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
            guilt.Tick(0.2f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
        }

        [Test]
        public void ChaseStatsAreSnapshottedWhenWarningBegins()
        {
            var guilt = new GuiltState();
            guilt.Tick(20f);
            float speed = guilt.ChaseSpeed;
            int damage = guilt.ContactDamage;
            // Crosses the 30 s boundary during the chase; the snapshot must hold.
            guilt.Tick(1.5f);
            guilt.Tick(5.9f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.GreaterThan(27f));
            Assert.That(guilt.ChaseSpeed, Is.EqualTo(speed));
            Assert.That(guilt.ContactDamage, Is.EqualTo(damage));
            Assert.That(speed, Is.EqualTo(4.5f));
            Assert.That(damage, Is.EqualTo(10));
        }

        [Test]
        public void ZeroOrNegativeTimeDoesNotAdvancePressure()
        {
            var guilt = new GuiltState();
            guilt.Tick(0f);
            guilt.Tick(-5f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.Zero);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void ResetRestartsGraceAndClearsPressure()
        {
            var guilt = new GuiltState();
            guilt.Tick(25f);
            guilt.Reset();
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
            Assert.That(guilt.AccumulatedCombatSeconds, Is.Zero);
            guilt.Tick(19.9f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void RepositionIsOnlyAcceptedDuringChase()
        {
            var guilt = new GuiltState();
            Assert.That(guilt.RequestReposition(), Is.False);
            guilt.Tick(20f);
            guilt.Tick(1.5f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
            Assert.That(guilt.RequestReposition(), Is.True);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
        }
    }
}
