using Boki.Combat;
using Boki.Routes;
using NUnit.Framework;

public sealed class RunProgressionTests
{
    [Test]
    public void HealingClampsAndCannotRevive()
    {
        var health = new HealthState(100);
        health.TakeDamage(10); health.TryHeal(30);
        Assert.That(health.Current, Is.EqualTo(100));
        health.TakeDamage(100); health.TryHeal(30);
        Assert.That(health.Current, Is.Zero);
    }

    [Test]
    public void RouteInteractionsRequireTheCorrectSelectedRouteAndResetOnNewRun()
    {
        var run = new RunState(new UnlockSnapshot { CityC = true, FeriC = true }, 0);
        Assert.That(run.CompleteCityEscape(), Is.False);
        Assert.That(run.SwitchPowerOff(), Is.False);
        run.TrySelect(RouteLevel.Forest, RouteChoice.B); run.TryAdvance(RouteLevel.Forest);
        run.TrySelect(RouteLevel.City, RouteChoice.C);
        Assert.That(run.CompleteCityEscape(), Is.False);
        Assert.That(run.BeginCityRetreat(), Is.True);
        Assert.That(run.Guilt.ScriptedSequenceActive, Is.True);
        Assert.That(run.CompleteCityEscape(), Is.True);
        Assert.That(run.Guilt.ScriptedSequenceActive, Is.False);
        run.TryAdvance(RouteLevel.City); run.TrySelect(RouteLevel.Feri, RouteChoice.C);
        Assert.That(run.SwitchPowerOff(), Is.True);
        run.NewRun(default, 1);
        Assert.That(run.CityRetreatStarted || run.CitySideEscapeCompleted || run.PowerOff, Is.False);
        Assert.That(run.Progression.Selections, Is.Zero);
    }
}
