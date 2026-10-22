using System.Linq;
using NUnit.Framework;
using Boki.Routes;

namespace Boki.Tests
{
    public sealed class RunStateTests
    {
        private static readonly RouteChoice[] Routes = { RouteChoice.A, RouteChoice.B, RouteChoice.C };
        private static UnlockSnapshot All => new UnlockSnapshot { ForestC = true, CityC = true, FeriC = true };

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
        public void UnlocksChangeOnlyOnNewRunAndAllFieldsReset()
        {
            var source = new UnlockSnapshot();
            var run = new RunState(source, 5);
            source.ForestC = true;
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.C), Is.EqualTo(GateState.PersistentLock));
            run.Health.TakeDamage(27);
            run.Battery = 2; run.XP = 40; run.KillCount = 3; run.Upgrades.Add("test");
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
