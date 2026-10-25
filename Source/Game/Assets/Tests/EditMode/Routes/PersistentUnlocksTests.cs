using Boki.Routes;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class PersistentUnlocksTests
    {
        private sealed class Store : IUnlockStore
        {
            public readonly bool[] Flags = new bool[3];
            public int Saves;
            public bool Read(RouteLevel level) => Flags[(int)level];
            public void Write(RouteLevel level) => Flags[(int)level] = true;
            public void Save() => Saves++;
        }

        [Test]
        public void BossShortcutAwardsOnlyTheExitActuallyReachedAndKeepsUnlocksForNextRun()
        {
            var store = new Store();
            var progress = new PersistentUnlocks(store);
            var run = new RunState(default, 0);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            Assert.That(progress.Earn(run, RouteLevel.Forest), Is.True);
            Assert.That(run.TrySkipToBoss(RouteLevel.Forest, 18f, 18f), Is.True);
            Assert.That(progress.Earn(run, RouteLevel.City), Is.False);
            Assert.That(progress.Earn(run, RouteLevel.Feri), Is.False);
            Assert.That(run.AvailableUnlocks.ForestC, Is.False);
            run.NewRun(progress.Read(), 10);
            Assert.That(run.AvailableUnlocks.ForestC, Is.True);
            Assert.That(run.AvailableUnlocks.CityC || run.AvailableUnlocks.FeriC, Is.False);
            Assert.That(store.Saves, Is.EqualTo(1));
        }

        [Test]
        public void MilestonesSaveOnceAndApplyOnlyOnNewRun()
        {
            var store = new Store();
            var progress = new PersistentUnlocks(store);
            var run = new RunState(progress.Read(), 0);
            foreach (var level in new[] { RouteLevel.Forest, RouteLevel.City, RouteLevel.Feri })
            {
                Assert.That(progress.Earn(run, level), Is.False, "Unchosen milestone");
                Assert.That(run.TrySelect(level, RouteChoice.A), Is.True);
                Assert.That(progress.Earn(run, level), Is.True);
                Assert.That(progress.Earn(run, level), Is.False, "Duplicate milestone");
                Assert.That(run.AvailableUnlocks.AllowsC(level), Is.False);
                if (level != RouteLevel.Feri) Assert.That(run.TryAdvance(level), Is.True);
            }
            Assert.That(store.Saves, Is.EqualTo(3));
            run.Health.TakeDamage(100);
            run.NewRun(new PersistentUnlocks(store).Read(), 5);
            Assert.That(run.Health.Current, Is.EqualTo(100));
            foreach (var level in new[] { RouteLevel.Forest, RouteLevel.City, RouteLevel.Feri })
            {
                Assert.That(run.AvailableUnlocks.AllowsC(level), Is.True);
                Assert.That(run.Choice(level), Is.EqualTo(RouteChoice.None));
            }
            Assert.That(run.Gate(RouteLevel.Forest, RouteChoice.C), Is.EqualTo(GateState.Available));
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(run.Gate(RouteLevel.City, RouteChoice.C), Is.EqualTo(GateState.RouteLock));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void ReloadCopiesEveryCombinationWithoutWriting(int bits)
        {
            var store = new Store();
            for (int i = 0; i < 3; i++) store.Flags[i] = (bits & (1 << i)) != 0;
            var run = new RunState(new PersistentUnlocks(store).Read(), 0);
            for (int i = 0; i < 3; i++)
                Assert.That(run.AvailableUnlocks.AllowsC((RouteLevel)i), Is.EqualTo(store.Flags[i]));
            store.Flags[0] = !store.Flags[0];
            Assert.That(run.AvailableUnlocks.ForestC, Is.EqualTo((bits & 1) != 0));
            Assert.That(store.Saves, Is.Zero);
        }

        [Test]
        public void InvalidDeadAndOutOfOrderMilestonesNeverSave()
        {
            var store = new Store();
            var progress = new PersistentUnlocks(store);
            var run = new RunState(progress.Read(), 0);
            Assert.That(progress.Earn(null, RouteLevel.Forest), Is.False);
            Assert.That(progress.Earn(run, (RouteLevel)99), Is.False);
            Assert.That(progress.Earn(run, RouteLevel.City), Is.False);
            run.TrySelect(RouteLevel.Forest, RouteChoice.B);
            run.Health.TakeDamage(100);
            Assert.That(progress.Earn(run, RouteLevel.Forest), Is.False);
            Assert.That(store.Saves, Is.Zero);
        }

        [Test]
        public void PartialProgressSurvivesDeathAndOnlyEarnedFlagChanges()
        {
            var store = new Store();
            var progress = new PersistentUnlocks(store);
            var run = new RunState(progress.Read(), 0);
            run.TrySelect(RouteLevel.Forest, RouteChoice.B);
            progress.Earn(run, RouteLevel.Forest);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(progress.Earn(run, RouteLevel.Forest), Is.False, "Stale exit");
            run.Health.TakeDamage(100);
            run.NewRun(new PersistentUnlocks(store).Read(), 1);
            Assert.That(run.AvailableUnlocks.ForestC, Is.True);
            Assert.That(run.AvailableUnlocks.CityC, Is.False);
            Assert.That(run.AvailableUnlocks.FeriC, Is.False);
            Assert.That(store.Saves, Is.EqualTo(1));
        }
    }
}
