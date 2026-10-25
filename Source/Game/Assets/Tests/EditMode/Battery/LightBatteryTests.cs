using System;
using Boki.Battery;
using Boki.Routes;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class LightBatteryTests
    {
        [Test]
        public void DrainIsFractionalClampedAndEmptyPenaltyRecovers()
        {
            var battery = new LightBatteryState();
            battery.Tick(0.1f, true);
            Assert.That(battery.Current, Is.EqualTo(99.85f).Within(0.0001f));
            Assert.That(battery.EnemySpeedMultiplier, Is.EqualTo(1f));
            battery.Tick(100f, true);
            Assert.That(battery.Current, Is.Zero);
            Assert.That(battery.EnemySpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(battery.TryRecharge(), Is.True);
            Assert.That(battery.Current, Is.EqualTo(25f));
            Assert.That(battery.EnemySpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DrainDoesNotDependOnFrameSizeAndLargeFiniteValuesClamp()
        {
            var single = new LightBatteryState();
            var many = new LightBatteryState();
            single.Tick(10f, true);
            for (int i = 0; i < 100; i++) many.Tick(0.1f, true);
            Assert.That(many.Current, Is.EqualTo(single.Current).Within(0.001f));
            single.Tick(float.MaxValue, true);
            Assert.That(single.Current, Is.Zero);
            Assert.That(single.TryRecharge(float.NaN), Is.False);
            Assert.That(single.TryRecharge(float.PositiveInfinity), Is.False);
            Assert.That(single.TryRecharge(-1f), Is.False);
            Assert.That(single.TryRecharge(float.MaxValue), Is.True);
            Assert.That(single.Current, Is.EqualTo(single.Capacity));
        }

        [Test]
        public void PauseAndInvalidTimeDoNotDrainOrRecharge()
        {
            var battery = new LightBatteryState { Current = 50f };
            var space = battery.GetSpace("forest-a", 3);
            space.Occupied = true;
            Assert.That(space.TryCollect(0, battery, true), Is.True);
            battery.Tick(30f, false);
            foreach (float seconds in new[] { -1f, 0f, float.NaN, float.PositiveInfinity }) battery.Tick(seconds, true);
            Assert.That(battery.Current, Is.EqualTo(75f));
            Assert.That(space.Remaining(0), Is.EqualTo(20f));
            Assert.That(space.TryCollect(1, battery, false), Is.False);
        }

        [Test]
        public void FullChargeIsLeftAvailableAndCollectionIsOncePerRecharge()
        {
            var battery = new LightBatteryState();
            var space = battery.GetSpace("boss", 2);
            space.Occupied = true;
            Assert.That(space.TryCollect(0, battery, true), Is.False);
            Assert.That(space.Available(0), Is.True);
            battery.Current = 90f;
            Assert.That(space.TryCollect(0, battery, true), Is.True);
            Assert.That(battery.Current, Is.EqualTo(100f));
            battery.Current = 10f;
            Assert.That(space.TryCollect(0, battery, true), Is.False);
            battery.Tick(19.5f, true);
            Assert.That(space.Available(0), Is.False);
            battery.Tick(0.5f, true);
            Assert.That(space.TryCollect(0, battery, true), Is.True);
        }

        [Test]
        public void LeavingAndReenteringPreservesTimersAndOtherSpacesStayInactive()
        {
            var battery = new LightBatteryState { Current = 10f };
            var a = battery.GetSpace("forest-a", 3);
            var b = battery.GetSpace("forest-b", 3);
            a.Occupied = true;
            a.TryCollect(0, battery, true);
            battery.Tick(7f, true);
            a.Occupied = false;
            battery.Tick(60f, true);
            Assert.That(a.Remaining(0), Is.EqualTo(13f));
            Assert.That(b.TryCollect(0, battery, true), Is.False);
            Assert.That(battery.GetSpace("forest-a", 3), Is.SameAs(a));
            a.Occupied = true;
            battery.Tick(13f, true);
            Assert.That(a.Available(0), Is.True);
        }

        [Test]
        public void CapacityPreservesPercentageAndNewRunResetsEverything()
        {
            var run = new RunState(default, 0);
            run.Battery = 50f;
            run.LightBattery.SetCapacity(140f);
            Assert.That(run.Battery, Is.EqualTo(70f));
            var space = run.LightBattery.GetSpace("forest-a", 3);
            space.Occupied = true;
            space.TryCollect(0, run.LightBattery, true);
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(run.Battery, Is.EqualTo(95f));
            Assert.That(run.LightBattery.Capacity, Is.EqualTo(140f));
            run.NewRun(default, 1);
            Assert.That(run.Battery, Is.EqualTo(100f));
            Assert.That(run.LightBattery.Capacity, Is.EqualTo(100f));
            Assert.That(run.LightBattery.GetSpace("forest-a", 3), Is.Not.SameAs(space));
        }

        [Test]
        public void InvalidCapacityAndConflictingSpaceLayoutAreRejected()
        {
            var battery = new LightBatteryState();
            Assert.Throws<ArgumentOutOfRangeException>(() => battery.SetCapacity(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => battery.Current = float.NaN);
            battery.GetSpace("arena", 3);
            Assert.Throws<InvalidOperationException>(() => battery.GetSpace("arena", 2));
        }
    }
}
