using System.Linq;
using NUnit.Framework;
using Boki.Routes;

namespace Boki.Tests
{
    public sealed class RunStateTests
    {
        private sealed class FirstOfferRandom : System.Random
        {
            public override int Next(int maxValue) => 0;
        }

        private static readonly RouteChoice[] Routes = { RouteChoice.A, RouteChoice.B, RouteChoice.C };
        private static UnlockSnapshot All => new UnlockSnapshot { ForestC = true, CityC = true, FeriC = true };

        [TestCase(0f, false)]
        [TestCase(10.5f, false)]
        [TestCase(14f, false)]
        [TestCase(17.999f, false)]
        [TestCase(18f, true)]
        [TestCase(20f, true)]
        [TestCase(float.NaN, false)]
        [TestCase(float.PositiveInfinity, false)]
        [TestCase(float.NegativeInfinity, false)]
        public void BossShortcutUsesHorizontalSpeedThreshold(float speed, bool accepted)
        {
            var run = new RunState(default, 0);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, speed, 18f), Is.EqualTo(accepted));
            Assert.That(run.BossShortcutTaken, Is.EqualTo(accepted));
            Assert.That(run.ActiveLevel, Is.EqualTo(accepted ? RouteLevel.Feri : RouteLevel.Forest));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidShortcutTuningCannotSkip(float minimum)
        {
            var run = new RunState(default, 0);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, minimum), Is.False);
        }

        [TestCase(RouteLevel.Forest)]
        [TestCase(RouteLevel.City)]
        public void BossShortcutPreservesResourcesAndActualChoicesWithoutOpeningFeriRoutes(RouteLevel source)
        {
            var run = new RunState(default, 5);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            if (source == RouteLevel.City)
            {
                run.TryAdvance(RouteLevel.Forest);
                run.TrySelect(RouteLevel.City, RouteChoice.B);
            }
            run.Health.TakeDamage(27);
            run.Battery = 23;
            int carriedXp = run.Progression.NextThreshold;
            run.Progression.AddXp(carriedXp);
            run.Progression.CreateOffer(new FirstOfferRandom());
            Assert.That(run.Progression.TrySelect(0, run.Progression.OfferVersion), Is.True);
            var progression = run.Progression;
            var battery = run.LightBattery;
            run.KillCount = 2;
            run.Guilt.SetCombatSpaceOccupied(1, true);
            run.Guilt.Tick(22f);
            int notifications = 0;
            run.Changed += () => notifications++;

            Assert.That(run.TrySkipToBoss(source, 18f, 18f), Is.True);
            Assert.That(run.Health.Current, Is.EqualTo(73));
            Assert.That(run.Battery, Is.EqualTo(23));
            Assert.That(run.Progression, Is.SameAs(progression));
            Assert.That(run.LightBattery, Is.SameAs(battery));
            Assert.That(run.Progression.Selections, Is.EqualTo(1));
            Assert.That(run.XP, Is.EqualTo(carriedXp));
            Assert.That(run.KillCount, Is.EqualTo(2));
            Assert.That(run.StartedAt, Is.EqualTo(5));
            Assert.That(run.Choice(RouteLevel.Forest), Is.EqualTo(RouteChoice.A));
            Assert.That(run.Choice(RouteLevel.City), Is.EqualTo(source == RouteLevel.City ? RouteChoice.B : RouteChoice.None));
            Assert.That(run.Choice(RouteLevel.Feri), Is.EqualTo(RouteChoice.None));
            Assert.That(run.Guilt.AccumulatedCombatSeconds, Is.Zero);
            Assert.That(run.AvailableUnlocks.ForestC || run.AvailableUnlocks.CityC || run.AvailableUnlocks.FeriC, Is.False);
            foreach (var route in Routes) Assert.That(run.Gate(RouteLevel.Feri, route), Is.EqualTo(GateState.RouteLock));
            Assert.That(run.TrySkipToBoss(source, 18f, 18f), Is.False);
            Assert.That(notifications, Is.EqualTo(1));

            run.NewRun(All, 20);
            Assert.That(run.BossShortcutTaken, Is.False);
            Assert.That(run.ActiveLevel, Is.EqualTo(RouteLevel.Forest));
            Assert.That(run.Health.Current, Is.EqualTo(100));
            Assert.That(run.Battery, Is.EqualTo(100));
            Assert.That(run.XP, Is.Zero);
            Assert.That(run.Upgrades, Is.Empty);
        }

        [Test]
        public void BossShortcutRequiresSelectionLifeAndActiveUnsuspendedLevel()
        {
            var run = new RunState(All, 0);
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.False);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            Assert.That(run.TrySkipToBoss(RouteLevel.City, 18f, 18f), Is.False);
            Assert.That(run.TrySkipToBoss((RouteLevel)99, 18f, 18f), Is.False);
            run.Pause.Set(Boki.Core.PauseReason.Menu, true);
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.False);
            run.Pause.Reset();
            run.Guilt.GameplaySuspended = true;
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.False);
            run.Guilt.GameplaySuspended = false;
            run.Guilt.ScriptedSequenceActive = true;
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.False);
            run.Guilt.ScriptedSequenceActive = false;
            run.Guilt.IsLoading = true;
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.False);
            run.Guilt.IsLoading = false;
            run.Health.TakeDamage(100);
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.False);
            Assert.That(run.ActiveLevel, Is.EqualTo(RouteLevel.Forest));
        }

        [Test]
        public void BossShortcutCannotBypassCityCSideEscape()
        {
            var run = new RunState(All, 0);
            run.TrySelect(RouteLevel.Forest, RouteChoice.B);
            run.TryAdvance(RouteLevel.Forest);
            run.TrySelect(RouteLevel.City, RouteChoice.C);
            Assert.That(run.TrySkipToBoss(RouteLevel.City, 18f, 18f), Is.False);
            run.BeginCityRetreat();
            Assert.That(run.TrySkipToBoss(RouteLevel.City, 18f, 18f), Is.False);
            run.CompleteCityEscape();
            Assert.That(run.TrySkipToBoss(RouteLevel.City, 18f, 18f), Is.True);
        }

        [Test]
        public void NextLevelResetsGuiltButRejectedAdvanceDoesNot()
        {
            var run = new RunState(All, 0);
            run.Guilt.SetCombatSpaceOccupied(1, true);
            run.Guilt.Tick(22f);
            Assert.That(run.TryAdvance(RouteLevel.Forest), Is.False);
            Assert.That(run.Guilt.AccumulatedCombatSeconds, Is.EqualTo(22f));
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            Assert.That(run.TryAdvance(RouteLevel.Forest), Is.True);
            Assert.That(run.Guilt.AccumulatedCombatSeconds, Is.Zero);
            Assert.That(run.Guilt.IsActive, Is.False);
        }

        [Test]
        public void NewRunClearsGuiltBossAndSuspensionState()
        {
            var run = new RunState(All, 0);
            run.Guilt.SetCombatSpaceOccupied(1, true);
            run.Guilt.Tick(22f);
            run.Guilt.GameplaySuspended = true;
            run.Guilt.ScriptedSequenceActive = true;
            run.Guilt.IsLoading = true;
            run.Guilt.EndForBoss();
            run.NewRun(All, 1);
            Assert.That(run.Guilt.AccumulatedCombatSeconds, Is.Zero);
            Assert.That(run.Guilt.BossEntered, Is.False);
            Assert.That(run.Guilt.GameplaySuspended, Is.False);
            Assert.That(run.Guilt.ScriptedSequenceActive, Is.False);
            Assert.That(run.Guilt.IsLoading, Is.False);
            Assert.That(run.Guilt.IsActive, Is.False);
        }

        [TestCase(RouteLevel.City, RouteChoice.A, "AB")]
        [TestCase(RouteLevel.City, RouteChoice.B, "AC")]
        [TestCase(RouteLevel.City, RouteChoice.C, "BC")]
        [TestCase(RouteLevel.Feri, RouteChoice.A, "AB")]
        [TestCase(RouteLevel.Feri, RouteChoice.B, "BC")]
        [TestCase(RouteLevel.Feri, RouteChoice.C, "AC")]
        public void MatrixMatchesDesign(RouteLevel level, RouteChoice previous, string allowed)
        {
            var run = new RunState(All, 0);
            Assert.That(run.TrySelect(RouteLevel.Forest, level == RouteLevel.City ? previous : RouteChoice.C));
            Assert.That(run.TryAdvance(RouteLevel.Forest));
            if (level == RouteLevel.Feri)
            {
                // Forest C cannot reach City A, so use Forest A for that case.
                if (previous == RouteChoice.A)
                {
                    run.NewRun(All, 0);
                    run.TrySelect(RouteLevel.Forest, RouteChoice.A);
                    run.TryAdvance(RouteLevel.Forest);
                }
                Assert.That(run.TrySelect(RouteLevel.City, previous));
                Assert.That(run.TryAdvance(RouteLevel.City));
            }
            foreach (var route in Routes)
                Assert.That(run.Gate(level, route), Is.EqualTo(allowed.Contains(route.ToString())
                    ? GateState.Available : GateState.RouteLock));
        }

        [Test]
        public void EveryReachableHistoryHasAContinuationForEverySnapshot()
        {
            for (int flags = 0; flags < 8; flags++)
            {
                var unlocks = new UnlockSnapshot { ForestC = (flags & 1) != 0,
                    CityC = (flags & 2) != 0, FeriC = (flags & 4) != 0 };
                foreach (var forest in Routes)
                foreach (var city in Routes)
                {
                    var run = new RunState(unlocks, 0);
                    if (!run.TrySelect(RouteLevel.Forest, forest)) continue;
                    Assert.That(run.TryAdvance(RouteLevel.Forest));
                    Assert.That(Routes.Any(r => run.Gate(RouteLevel.City, r) == GateState.Available));
                    if (!run.TrySelect(RouteLevel.City, city)) continue;
                    Assert.That(run.TryAdvance(RouteLevel.City));
                    Assert.That(Routes.Any(r => run.Gate(RouteLevel.Feri, r) == GateState.Available));
                }
            }
        }

        [Test]
        public void RouteLockWinsOverPermanentLockAndFirstRunCanForceAChoice()
        {
            var run = new RunState(default, 0);
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.C), Is.EqualTo(GateState.PersistentLock));
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(run.Gate(RouteLevel.City, RouteChoice.C), Is.EqualTo(GateState.RouteLock));
            run.NewRun(default, 1);
            run.TrySelect(RouteLevel.Forest, RouteChoice.B);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(run.Gate(RouteLevel.City, RouteChoice.A), Is.EqualTo(GateState.Available));
            Assert.That(run.Gate(RouteLevel.City, RouteChoice.B), Is.EqualTo(GateState.RouteLock));
            Assert.That(run.Gate(RouteLevel.City, RouteChoice.C), Is.EqualTo(GateState.PersistentLock));
        }

        [Test]
        public void SelectionIsImmediateAndNotifiedOnce()
        {
            var run = new RunState(All, 0);
            int notifications = 0;
            run.Changed += () => { notifications++; Assert.That(run.Choice(RouteLevel.Forest), Is.EqualTo(RouteChoice.A)); };
            Assert.That(run.TrySelect(RouteLevel.Forest, RouteChoice.A));
            Assert.That(run.TrySelect(RouteLevel.Forest, RouteChoice.A), Is.False);
            Assert.That(run.TrySelect(RouteLevel.Forest, RouteChoice.B), Is.False);
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.A), Is.EqualTo(GateState.Selected));
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.C), Is.EqualTo(GateState.RouteLock));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void InvalidOutOfOrderAndDeadRequestsDoNotChangeState()
        {
            var run = new RunState(All, 0);
            Assert.That(run.TryAdvance(RouteLevel.Forest), Is.False);
            Assert.That(run.TrySelect(RouteLevel.City, RouteChoice.A), Is.False);
            Assert.That(run.TrySelect((RouteLevel)99, RouteChoice.A), Is.False);
            Assert.That(run.TrySelect(RouteLevel.Forest, RouteChoice.None), Is.False);
            Assert.That(run.TrySelect(RouteLevel.Forest, (RouteChoice)99), Is.False);
            run.Health.TakeDamage(100);
            Assert.That(run.TrySelect(RouteLevel.Forest, RouteChoice.A), Is.False);
            Assert.That(run.Choice(RouteLevel.Forest), Is.EqualTo(RouteChoice.None));
        }

        [Test]
        public void CityAdvanceRequiresSelectionAndLifeAndPreservesTheRunThroughFeri()
        {
            var run = new RunState(default, 5);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(run.TryAdvance(RouteLevel.City), Is.False);
            run.TrySelect(RouteLevel.City, RouteChoice.B);
            run.Health.TakeDamage(27);
            run.Battery = 23; run.Progression.AddXp(20); run.KillCount = 2; run.Progression.CreateOffer(new FirstOfferRandom());
            for (int i = 0; i < 3; i++)
                if (run.Progression.Offer(i) == Boki.Upgrades.UpgradeId.RaveOverdrive)
                { run.Progression.TrySelect(i, run.Progression.OfferVersion); break; }
            Assert.That(run.TryAdvance(RouteLevel.City));
            Assert.That(run.TryAdvance(RouteLevel.City), Is.False);
            Assert.That(run.ActiveLevel, Is.EqualTo(RouteLevel.Feri));
            Assert.That(run.Health.Current, Is.EqualTo(73));
            Assert.That(run.Battery, Is.EqualTo(23));
            Assert.That(run.XP, Is.EqualTo(20));
            Assert.That(run.KillCount, Is.EqualTo(2));
            Assert.That(run.Upgrades.Count, Is.EqualTo(1));
            Assert.That(run.StartedAt, Is.EqualTo(5));
            Assert.That(run.Choice(RouteLevel.Forest), Is.EqualTo(RouteChoice.A));
            Assert.That(run.Choice(RouteLevel.City), Is.EqualTo(RouteChoice.B));
            Assert.That(run.TrySelect(RouteLevel.Feri, RouteChoice.B));
            Assert.That(run.TryAdvance(RouteLevel.Feri), Is.False);
            run.NewRun(default, 10);
            Assert.That(run.ActiveLevel, Is.EqualTo(RouteLevel.Forest));
            Assert.That(run.Choice(RouteLevel.Feri), Is.EqualTo(RouteChoice.None));
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            run.TryAdvance(RouteLevel.Forest);
            run.TrySelect(RouteLevel.City, RouteChoice.A);
            run.Health.TakeDamage(100);
            Assert.That(run.TryAdvance(RouteLevel.City), Is.False);
            Assert.That(run.ActiveLevel, Is.EqualTo(RouteLevel.City));
        }

        [Test]
        public void UnlocksChangeOnlyOnNewRunAndAllFieldsReset()
        {
            var source = new UnlockSnapshot();
            var run = new RunState(source, 5);
            source.ForestC = true;
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.C), Is.EqualTo(GateState.PersistentLock));
            run.Health.TakeDamage(27);
            run.Battery = 2; run.Progression.AddXp(33); run.KillCount = 3; run.Progression.CreateOffer(new FirstOfferRandom());
            for (int i = 0; i < 3; i++)
                if (run.Progression.Offer(i) == Boki.Upgrades.UpgradeId.RaveOverdrive)
                { run.Progression.TrySelect(i, run.Progression.OfferVersion); break; }
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            Assert.That(run.TryAdvance(RouteLevel.Forest));
            Assert.That(run.TryAdvance(RouteLevel.Forest), Is.False);
            Assert.That(run.Health.Current, Is.EqualTo(73));
            Assert.That(run.Choice(RouteLevel.Forest), Is.EqualTo(RouteChoice.A));
            run.NewRun(source, 10);
            Assert.That(run.Health.Current, Is.EqualTo(100));
            Assert.That(run.Battery, Is.EqualTo(100));
            Assert.That(run.XP + run.KillCount, Is.Zero);
            Assert.That(run.Upgrades, Is.Empty);
            Assert.That(run.StartedAt, Is.EqualTo(10));
            foreach (RouteLevel level in new[] { RouteLevel.Forest, RouteLevel.City, RouteLevel.Feri })
                Assert.That(run.Choice(level), Is.EqualTo(RouteChoice.None));
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.C), Is.EqualTo(GateState.Available));
        }
    }
}
