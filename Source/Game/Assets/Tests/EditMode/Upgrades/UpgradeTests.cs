using System;
using System.Linq;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Routes;
using Boki.Upgrades;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class UpgradeTests
    {
        private static void Choose(UpgradeState state, UpgradeId id)
        {
            // An injected deterministic RNG places the requested eligible card first.
            Assert.That(state.CreateOffer(new PickRandom(id, state)), Is.True);
            int index = Enumerable.Range(0, state.OfferCount).First(i => state.Offer(i) == id);
            Assert.That(state.TrySelect(index, state.OfferVersion), Is.True);
        }

        private sealed class PickRandom : Random
        {
            private readonly int first;
            private bool picked;
            public PickRandom(UpgradeId id, UpgradeState state)
            {
                for (UpgradeId candidate = 0; candidate < id; candidate++)
                    if (state.Eligible(candidate)) first++;
            }
            public override int Next(int maxValue)
            {
                if (picked) return 0;
                picked = true;
                return first % maxValue;
            }
        }

        [Test]
        public void ThresholdsAreCumulativeAndChoicesResolveSequentially()
        {
            var state = new UpgradeState(new HealthState(100), new LightBatteryState());
            int[] thresholds = { 8, 18, 30, 45, 65, 90 };
            foreach (int threshold in thresholds)
            {
                state.AddXp(threshold - state.XP - 1);
                Assert.That(state.HasPendingSelection, Is.False);
                state.AddXp();
                Assert.That(state.HasPendingSelection, Is.True);
                state.CreateOffer(new Random(threshold));
                int version = state.OfferVersion;
                Assert.That(state.TrySelect(0, version), Is.True);
                Assert.That(state.TrySelect(0, version), Is.False);
            }
            Assert.That(state.IsMaxLevel, Is.True);
            Assert.That(state.AddXp(), Is.False);
            Assert.That(state.NextThreshold, Is.Zero);
            Assert.That(state.CreateOffer(new Random()), Is.False);
        }

        [Test]
        public void BulkXpQueuesSixSelectionsAndRejectsStaleAndInvalidChoices()
        {
            var state = new UpgradeState(new HealthState(100), new LightBatteryState());
            state.AddXp(int.MaxValue);
            Assert.That(state.XP, Is.EqualTo(90));
            state.CreateOffer(new Random(2));
            int old = state.OfferVersion;
            Assert.That(state.TrySelect(-1, old), Is.False);
            Assert.That(state.TrySelect(3, old), Is.False);
            Assert.That(state.TrySelect(0, old + 1), Is.False);
            Assert.That(state.TrySelect(0, old), Is.True);
            Assert.That(state.HasPendingSelection, Is.True);
            state.CreateOffer(new Random(3));
            Assert.That(state.TrySelect(0, old), Is.False);
            for (int i = 1; i < 6; i++)
            {
                state.CreateOffer(new Random(i));
                Assert.That(state.TrySelect(0, state.OfferVersion), Is.True);
            }
            Assert.That(state.Selections, Is.EqualTo(6));
        }

        [Test]
        public void DamageStacksAdditivelyAndCapDisappearsFromOffers()
        {
            var state = new UpgradeState(new HealthState(100), new LightBatteryState());
            state.AddXp(90);
            for (int i = 1; i <= 3; i++)
            {
                Choose(state, UpgradeId.RaveOverdrive);
                Assert.That(state.DamageMultiplier, Is.EqualTo(1f + .3f * i).Within(.0001));
            }
            Assert.That(state.Eligible(UpgradeId.RaveOverdrive), Is.False);
            state.CreateOffer(new Random(1));
            Assert.That(Enumerable.Range(0, state.OfferCount).Any(i => state.Offer(i) == UpgradeId.RaveOverdrive), Is.False);
        }

        [Test]
        public void CapacityPreservesChargeAndTurboAddsToGuiltWithoutChangingBoostStats()
        {
            var battery = new LightBatteryState { Current = 25f };
            var state = new UpgradeState(new HealthState(100), battery);
            state.AddXp(90);
            Choose(state, UpgradeId.BiggerBattery);
            Assert.That(battery.Capacity, Is.EqualTo(140f).Within(.001));
            Assert.That(battery.Current, Is.EqualTo(35f).Within(.001));
            Choose(state, UpgradeId.BiggerBattery);
            Assert.That(battery.Capacity, Is.EqualTo(180f).Within(.001));
            Assert.That(battery.Current, Is.EqualTo(45f).Within(.001));
            Assert.That(state.Eligible(UpgradeId.BiggerBattery), Is.False);
            Choose(state, UpgradeId.TurboSkiro);
            Assert.That(state.RidingMultiplier(true), Is.EqualTo(1.45f).Within(.001));
            Choose(state, UpgradeId.TurboSkiro);
            Assert.That(state.RidingMultiplier(true), Is.EqualTo(1.65f).Within(.001));
            Assert.That(state.Eligible(UpgradeId.TurboSkiro), Is.False);
        }

        [Test]
        public void RecoveryIsClampedInjuryOnlyAndDoesNotRevive()
        {
            var health = new HealthState(100);
            var battery = new LightBatteryState();
            var state = new UpgradeState(health, battery);
            Assert.That(state.Eligible(UpgradeId.EmergencySnack), Is.False);
            Assert.That(state.Eligible(UpgradeId.BatteryCharge), Is.False);
            health.TakeDamage(10);
            state.AddXp(90);
            Choose(state, UpgradeId.EmergencySnack);
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(state.Selections, Is.EqualTo(1));
            Assert.That(state.Eligible(UpgradeId.EmergencySnack), Is.False);
            health.TakeDamage(100);
            Assert.That(health.TryHeal(30), Is.False);
            Assert.That(state.CreateOffer(new Random()), Is.False);
            Assert.That(state.AddXp(), Is.False);
        }

        [Test]
        public void StaleRecoveryOfferIsRejectedAndRefreshedWithoutConsumingSelection()
        {
            var health = new HealthState(100);
            health.TakeDamage(60);
            var state = new UpgradeState(health, new LightBatteryState());
            state.AddXp(8);
            state.CreateOffer(new PickRandom(UpgradeId.EmergencySnack, state));
            int version = state.OfferVersion;
            health.TryHeal(100);
            Assert.That(state.TrySelect(0, version), Is.False);
            Assert.That(state.Selections, Is.Zero);
            state.CreateOffer(new Random(1));
            Assert.That(state.OfferVersion, Is.GreaterThan(version));
        }

        [Test]
        public void SixthOfferUsesTwoCardsAtFullResourcesAndRecoveryWhenNeeded()
        {
            var health = new HealthState(100);
            var battery = new LightBatteryState();
            var state = new UpgradeState(health, battery);
            state.AddXp(90);
            Choose(state, UpgradeId.RaveOverdrive);
            Choose(state, UpgradeId.RaveOverdrive);
            Choose(state, UpgradeId.RaveOverdrive);
            Choose(state, UpgradeId.DoubleTrouble);
            Choose(state, UpgradeId.PanicMode);
            Assert.That(state.CreateOffer(new Random(1)), Is.True);
            Assert.That(state.OfferCount, Is.EqualTo(2));
            Assert.That(state.TrySelect(2, state.OfferVersion), Is.False);
            Assert.That(new[] { state.Offer(0), state.Offer(1) }, Is.EquivalentTo(new[] { UpgradeId.TurboSkiro, UpgradeId.BiggerBattery }));
        }

        [Test]
        public void BatteryRecoveryOnlyFillsAShortOfferAndConsumesASelection()
        {
            var battery = new LightBatteryState { Current = 90f };
            var state = new UpgradeState(new HealthState(100), battery);
            state.AddXp(90);
            Choose(state, UpgradeId.RaveOverdrive);
            Choose(state, UpgradeId.RaveOverdrive);
            Choose(state, UpgradeId.RaveOverdrive);
            Choose(state, UpgradeId.DoubleTrouble);
            Choose(state, UpgradeId.PanicMode);
            state.CreateOffer(new PickRandom(UpgradeId.BatteryCharge, state));
            Assert.That(state.OfferCount, Is.EqualTo(3));
            Assert.That(state.Offer(0), Is.EqualTo(UpgradeId.BatteryCharge));
            Assert.That(state.TrySelect(0, state.OfferVersion), Is.True);
            Assert.That(battery.Current, Is.EqualTo(100f));
            Assert.That(state.IsMaxLevel, Is.True);
        }

        [Test]
        public void ThousandsOfFullRunsKeepDistinctEligibleOffers()
        {
            for (int seed = 0; seed < 2000; seed++)
            {
                var health = new HealthState(100);
                var battery = new LightBatteryState();
                var state = new UpgradeState(health, battery);
                var random = new Random(seed);
                state.AddXp(90);
                while (!state.IsMaxLevel)
                {
                    if (seed % 2 == 0) health.TakeDamage(1);
                    battery.Current -= seed % 3;
                    Assert.That(state.CreateOffer(random), Is.True);
                    var cards = Enumerable.Range(0, state.OfferCount).Select(state.Offer).ToArray();
                    Assert.That(cards.Length, Is.InRange(2, 3));
                    Assert.That(cards.Distinct().Count(), Is.EqualTo(cards.Length));
                    Assert.That(cards.All(state.Eligible), Is.True);
                    Assert.That(state.TrySelect(random.Next(state.OfferCount), state.OfferVersion), Is.True);
                }
            }
        }

        [Test]
        public void PanicRequiresPurchasedUpgradeActiveChaseAndTenMeterDistance()
        {
            var state = new UpgradeState(new HealthState(100), new LightBatteryState());
            Assert.That(state.PanicEligible(true, 0f), Is.False);
            state.AddXp(90);
            Choose(state, UpgradeId.PanicMode);
            Assert.That(state.PanicEligible(true, 10f), Is.True);
            Assert.That(state.PanicEligible(true, 10.001f), Is.False);
            Assert.That(state.PanicEligible(false, 1f), Is.False);
            Assert.That(state.PanicEligible(true, float.NaN), Is.False);
            Assert.That(state.Eligible(UpgradeId.PanicMode), Is.False);
            Choose(state, UpgradeId.DoubleTrouble);
            Assert.That(state.HasDoubleTrouble, Is.True);
            Assert.That(state.Eligible(UpgradeId.DoubleTrouble), Is.False);
        }

        [Test]
        public void RunCarriesProgressAndResetsItTogetherWithHealthAndBattery()
        {
            var run = new RunState(default, 0);
            run.Progression.AddXp(8);
            Choose(run.Progression, UpgradeId.RaveOverdrive);
            var original = run.Progression;
            run.TrySelect(RouteLevel.Forest, RouteChoice.A);
            run.TryAdvance(RouteLevel.Forest);
            Assert.That(run.Progression, Is.SameAs(original));
            Assert.That(run.Progression.DamageMultiplier, Is.EqualTo(1.3f));
            run.NewRun(default, 1);
            Assert.That(run.Progression.Selections, Is.Zero);
            Assert.That(run.XP, Is.Zero);
            Assert.That(run.Health.Current, Is.EqualTo(100));
            Assert.That(run.LightBattery.Capacity, Is.EqualTo(100f));
        }

        [Test]
        public void ResolvingUpgradePauseCannotClearAnotherPauseReason()
        {
            var pause = new PauseState();
            pause.Set(PauseReason.Menu, true);
            pause.Set(PauseReason.Upgrade, true);
            pause.Set(PauseReason.Upgrade, false);
            Assert.That(pause.IsPaused, Is.True);
            pause.Set(PauseReason.Menu, false);
            Assert.That(pause.IsPaused, Is.False);
        }
    }
}
