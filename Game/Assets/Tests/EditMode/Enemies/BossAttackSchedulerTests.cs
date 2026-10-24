using System;
using System.Collections.Generic;
using Boki.Enemies;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class BossAttackSchedulerTests
    {
        private static List<(float time, BossAction action)> Run(BossAttackScheduler boss, float seconds, float health)
        {
            var log = new List<(float, BossAction)>();
            const float dt = 0.05f;
            for (float t = dt; t <= seconds + 0.0001f; t += dt)
            {
                BossAction action = boss.Tick(dt, health);
                if (action != BossAction.None) log.Add((t, action));
            }
            return log;
        }

        [Test]
        public void BarrageFiresThreeSheetsThenSlamAlternates()
        {
            var boss = new BossAttackScheduler(2f, 0.5f, 1.25f, 1f);
            var actions = Run(boss, 12f, 1f).ConvertAll(entry => entry.action);
            Assert.That(actions.GetRange(0, 5), Is.EqualTo(new[]
            {
                BossAction.FireSheet, BossAction.FireSheet, BossAction.FireSheet,
                BossAction.StartSlamTelegraph, BossAction.SlamImpact
            }));
            Assert.That(actions[5], Is.EqualTo(BossAction.FireSheet));
        }

        [Test]
        public void SlamImpactFollowsTelegraphByEightTenthsOfASecond()
        {
            var boss = new BossAttackScheduler(2f, 0.5f, 1.25f, 0f);
            var log = Run(boss, 12f, 1f);
            float start = log.Find(e => e.action == BossAction.StartSlamTelegraph).time;
            float impact = log.Find(e => e.action == BossAction.SlamImpact).time;
            Assert.That(impact - start, Is.EqualTo(BossAttackScheduler.SlamTelegraphSeconds).Within(0.06f));
        }

        [Test]
        public void NothingHappensDuringOpeningDelayOrWithoutTime()
        {
            var boss = new BossAttackScheduler(2f, 0.5f, 1.25f, 1f);
            Assert.That(boss.Tick(0f, 1f), Is.EqualTo(BossAction.None));
            Assert.That(boss.Tick(0.9f, 1f), Is.EqualTo(BossAction.None));
            Assert.That(boss.Tick(0.2f, 1f), Is.EqualTo(BossAction.FireSheet));
        }

        [Test]
        public void EnragesAtHalfHealthAndStaysEnraged()
        {
            var boss = new BossAttackScheduler(2f, 0.5f, 1.25f, 0f);
            boss.Tick(0.01f, 0.51f);
            Assert.That(boss.IsEnraged, Is.False);
            boss.Tick(0.01f, 0.5f);
            Assert.That(boss.IsEnraged, Is.True);
            boss.Tick(0.01f, 1f);
            Assert.That(boss.IsEnraged, Is.True);
        }

        [Test]
        public void EnragedBossCompletesMoreAttacksInTheSameTime()
        {
            int Count(float health)
            {
                var boss = new BossAttackScheduler(2f, 0.5f, 1.25f, 0f);
                return Run(boss, 30f, health).Count;
            }
            Assert.That(Count(0.4f), Is.GreaterThan(Count(1f)));
        }

        [Test]
        public void EnragedSlamTelegraphIsNotShortened()
        {
            var boss = new BossAttackScheduler(2f, 0.5f, 2f, 0f);
            var log = Run(boss, 30f, 0.1f);
            float start = log.Find(e => e.action == BossAction.StartSlamTelegraph).time;
            float impact = log.Find(e => e.action == BossAction.SlamImpact).time;
            Assert.That(impact - start, Is.EqualTo(BossAttackScheduler.SlamTelegraphSeconds).Within(0.06f));
        }

        [Test]
        public void InvalidTuningIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossAttackScheduler(0f, 0.5f, 1.25f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossAttackScheduler(2f, 0f, 1.25f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossAttackScheduler(2f, 0.5f, 0.9f, 0f));
        }
    }
}
