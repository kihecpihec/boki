using System;
using Boki.Combat;
using Boki.Progression;
using Boki.Routes;
using NUnit.Framework;

public sealed class RunProgressionTests
{
    [Test]
    public void ThresholdRequiresExactlyOneChoiceAndRejectsDuplicate()
    {
        var p = new RunProgression();
        p.CollectXP(); p.CollectXP();
        Assert.That(p.NeedsSelection, Is.False);
        p.CollectXP();
        Assert.That(p.NeedsSelection, Is.True);
        Assert.That(p.Choose(UpgradeKind.RaveOverdrive, false, false), Is.True);
        Assert.That(p.Choose(UpgradeKind.TurboSkiro, false, false), Is.False);
        Assert.That(p.DamageMultiplier, Is.EqualTo(1.3f).Within(0.0001f));
    }

    [Test]
    public void QueuedThresholdsResolveWithoutAdditionalXPAndOffersStayDistinct()
    {
        var p = new RunProgression { XP = 33 };
        var random = new Random(42);
        for (int i = 0; i < 6; i++)
        {
            var offer = p.Offer(random, false, false);
            Assert.That(offer.Length, Is.InRange(1, 3));
            Assert.That(offer, Is.Unique);
            Assert.That(p.Choose(offer[0], false, false), Is.True);
        }
        Assert.That(p.MaximumLevel, Is.True);
        Assert.That(p.CollectXP(), Is.False);
        Assert.That(p.XP, Is.EqualTo(33));
    }

    [Test]
    public void LateOfferShowsOnlyEligibleChoicesWhenHealthAndBatteryAreFull()
    {
        var p = new RunProgression { XP = 33 };
        foreach (var kind in new[] { UpgradeKind.DoubleTrouble, UpgradeKind.PanicMode, UpgradeKind.TurboSkiro,
            UpgradeKind.TurboSkiro, UpgradeKind.RaveOverdrive }) Assert.That(p.Choose(kind, false, false), Is.True);
        var offer = p.Offer(new Random(1), false, false);
        Assert.That(offer, Is.EquivalentTo(new[] { UpgradeKind.RaveOverdrive, UpgradeKind.BiggerBattery }));
    }

    [Test]
    public void CapsRecoveryEligibilityAndAdditiveSpeedAreEnforced()
    {
        var p = new RunProgression { XP = 33 };
        Assert.That(p.Choose(UpgradeKind.EmergencySnack, false, false), Is.False);
        Assert.That(p.Choose(UpgradeKind.BatteryCharge, false, false), Is.False);
        p.Choose(UpgradeKind.TurboSkiro, false, false);
        p.Choose(UpgradeKind.TurboSkiro, false, false);
        Assert.That(p.RidingBonus, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(p.Eligible(UpgradeKind.TurboSkiro, true, true), Is.False);
        p.Reset();
        Assert.That(p.XP, Is.Zero);
        Assert.That(p.RidingBonus, Is.Zero);
        Assert.That(p.Selections, Is.Zero);
    }

    [Test]
    public void CapacityUpgradeKeepsChargePercentageAndNewRunClearsProgress()
    {
        var run = new RunState(default, 0);
        run.Battery = 50f;
        run.XP = 3;
        Assert.That(run.ChooseUpgrade(UpgradeKind.BiggerBattery), Is.True);
        Assert.That(run.LightBattery.Capacity, Is.EqualTo(140f));
        Assert.That(run.Battery, Is.EqualTo(70f));
        run.NewRun(default, 1);
        Assert.That(run.LightBattery.Capacity, Is.EqualTo(100f));
        Assert.That(run.Progression.Selections, Is.Zero);
        Assert.That(run.Upgrades, Is.Empty);
    }

    [Test]
    public void HealingClampsAndCannotRevive()
    {
        var health = new HealthState(100);
        health.TakeDamage(10); health.Heal(30);
        Assert.That(health.Current, Is.EqualTo(100));
        health.TakeDamage(100); health.Heal(30);
        Assert.That(health.Current, Is.Zero);
    }

    [Test]
    public void RouteInteractionsRequireTheCorrectSelectedRoute()
    {
        var run = new RunState(new UnlockSnapshot { CityC = true, FeriC = true }, 0);
        Assert.That(run.CompleteCityEscape(), Is.False);
        Assert.That(run.SwitchPowerOff(), Is.False);
        run.TrySelect(RouteLevel.Forest, RouteChoice.B); run.TryAdvance(RouteLevel.Forest);
        run.TrySelect(RouteLevel.City, RouteChoice.C);
        Assert.That(run.CompleteCityEscape(), Is.False);
        run.BeginCityRetreat();
        Assert.That(run.CompleteCityEscape(), Is.True);
        run.TryAdvance(RouteLevel.City); run.TrySelect(RouteLevel.Feri, RouteChoice.C);
        Assert.That(run.SwitchPowerOff(), Is.True);
        run.NewRun(default, 1);
        Assert.That(run.CitySideEscapeCompleted || run.PowerOff, Is.False);
    }
}
