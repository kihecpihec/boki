using Boki.Enemies;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class GuiltTests
    {
        private static GuiltState ActiveGuilt()
        {
            var guilt = new GuiltState();
            guilt.SetCombatSpaceOccupied(1, true);
            return guilt;
        }

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
            var guilt = ActiveGuilt();
            guilt.Tick(19.9f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void WarningThenChaseThenRestFollowGrace()
        {
            var guilt = ActiveGuilt();
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
            var guilt = ActiveGuilt();
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
            var guilt = ActiveGuilt();
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
            var guilt = ActiveGuilt();
            guilt.Tick(87.5f); // Fourth warning begins in the 60–89.999 s tier.
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
            guilt.Tick(1.5f);
            guilt.Tick(2f); // Cross 90 s while chasing.
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(91f));
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
            Assert.That(guilt.ChaseSpeed, Is.EqualTo(5.5f));
            Assert.That(guilt.ContactDamage, Is.EqualTo(14));
        }

        [Test]
        public void ZeroOrNegativeTimeDoesNotAdvancePressure()
        {
            var guilt = ActiveGuilt();
            guilt.Tick(0f);
            guilt.Tick(-5f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.Zero);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void ResetRestartsGraceAndClearsPressure()
        {
            var guilt = ActiveGuilt();
            guilt.Tick(25f);
            guilt.Reset();
            guilt.SetCombatSpaceOccupied(1, true);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
            Assert.That(guilt.AccumulatedCombatSeconds, Is.Zero);
            guilt.Tick(19.9f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void RepositionIsOnlyAcceptedDuringChase()
        {
            var guilt = ActiveGuilt();
            Assert.That(guilt.RequestReposition(), Is.False);
            guilt.Tick(20f);
            guilt.Tick(1.5f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
            Assert.That(guilt.RequestReposition(), Is.True);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
        }

        [Test]
        public void NoCombatSpaceMeansNoPressure()
        {
            var guilt = new GuiltState();
            guilt.Tick(60f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.Zero);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void ReentryRetainsWarningAndAccumulatedTime()
        {
            var guilt = ActiveGuilt();
            guilt.Tick(20.5f);
            guilt.SetCombatSpaceOccupied(1, false);
            guilt.Tick(100f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(20.5f));
            guilt.SetCombatSpaceOccupied(2, true);
            guilt.Tick(0.9f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
            guilt.Tick(0.11f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
        }

        [Test]
        public void OverlappingSpacesAndDuplicateEntersDoNotDoubleCountTime()
        {
            var guilt = ActiveGuilt();
            guilt.SetCombatSpaceOccupied(1, true);
            guilt.SetCombatSpaceOccupied(2, true);
            guilt.Tick(10f);
            guilt.SetCombatSpaceOccupied(1, false);
            guilt.SetCombatSpaceOccupied(1, false);
            guilt.Tick(5f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(15f));
            guilt.SetCombatSpaceOccupied(2, false);
            guilt.Tick(5f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(15f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SuspensionFreezesPressureAndChaseThenResumes(int reason)
        {
            var guilt = ActiveGuilt();
            guilt.Tick(22f);
            guilt.GameplaySuspended = reason == 0;
            guilt.ScriptedSequenceActive = reason == 1;
            guilt.IsLoading = reason == 2;
            guilt.Tick(100f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(22f));
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
            Assert.That(guilt.RequestReposition(), Is.False);
            guilt.GameplaySuspended = guilt.ScriptedSequenceActive = guilt.IsLoading = false;
            guilt.Tick(5.5f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
        }

        [Test]
        public void ClearingOneSuspensionDoesNotClearAnother()
        {
            var guilt = ActiveGuilt();
            guilt.GameplaySuspended = guilt.ScriptedSequenceActive = true;
            guilt.GameplaySuspended = false;
            guilt.Tick(50f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.Zero);
        }

        [Test]
        public void BossEntryEndsPursuitUntilReset()
        {
            var guilt = ActiveGuilt();
            guilt.Tick(22f);
            guilt.EndForBoss();
            guilt.EndForBoss();
            guilt.SetCombatSpaceOccupied(2, true);
            guilt.Tick(50f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(22f));
            Assert.That(guilt.IsActive, Is.False);
            guilt.Reset();
            Assert.That(guilt.BossEntered, Is.False);
            Assert.That(guilt.IsActive, Is.False);
            guilt.SetCombatSpaceOccupied(3, true);
            guilt.Tick(20f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
        }

        [Test]
        public void LargeTickMatchesSmallTicksAcrossMultipleCycles()
        {
            var large = ActiveGuilt();
            var small = ActiveGuilt();
            large.Tick(150f);
            for (int i = 0; i < 600; i++) small.Tick(0.25f);
            Assert.That(large.AccumulatedCombatSeconds, Is.EqualTo(small.AccumulatedCombatSeconds));
            Assert.That(large.Phase, Is.EqualTo(small.Phase));
            Assert.That(large.ChaseSpeed, Is.EqualTo(small.ChaseSpeed));
            Assert.That(large.ContactDamage, Is.EqualTo(small.ContactDamage));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotPoisonPressure(float time)
        {
            var guilt = ActiveGuilt();
            guilt.Tick(time);
            guilt.Tick(20f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(20f));
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
        }

        [Test]
        public void MissingPlacementKeepsPressureButPreservesFullWarning()
        {
            var guilt = ActiveGuilt();
            guilt.PhaseChanged += phase =>
            {
                if (phase == GuiltPhase.Warning) guilt.WaitForPlacement();
            };
            guilt.Tick(35f);
            Assert.That(guilt.AccumulatedCombatSeconds, Is.EqualTo(35f));
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
            guilt.ConfirmPlacement();
            Assert.That(guilt.ChaseSpeed, Is.EqualTo(5f));
            Assert.That(guilt.ContactDamage, Is.EqualTo(12));
            guilt.Tick(1.4f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Warning));
            guilt.Tick(0.11f);
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Chase));
        }

        [Test]
        public void BossAndResetClearPendingPlacement()
        {
            var guilt = ActiveGuilt();
            guilt.Tick(20f);
            guilt.WaitForPlacement();
            guilt.EndForBoss();
            Assert.That(guilt.AwaitingPlacement, Is.False);
            guilt.ConfirmPlacement();
            Assert.That(guilt.Phase, Is.EqualTo(GuiltPhase.Distant));
            guilt.Reset();
            Assert.That(guilt.AwaitingPlacement, Is.False);
        }
    }
}
