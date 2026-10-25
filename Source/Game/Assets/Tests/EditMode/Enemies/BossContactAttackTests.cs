using Boki.Enemies;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class BossContactAttackTests
    {
        [Test]
        public void ContactHitsImmediatelyThenWaitsEvenAfterSeparation()
        {
            var attack = new BossContactAttack(1f);
            Assert.That(attack.Tick(.01f, false), Is.False);
            Assert.That(attack.Tick(.01f, true), Is.True);
            Assert.That(attack.Tick(.2f, false), Is.False);
            Assert.That(attack.Tick(.2f, true), Is.False);
            Assert.That(attack.Tick(.61f, true), Is.True);
        }

        [Test]
        public void PauseFreezesCooldownAndCannotDealDamage()
        {
            var attack = new BossContactAttack(1f);
            Assert.That(attack.Tick(0f, true), Is.False);
            Assert.That(attack.Tick(.01f, true), Is.True);
            for (int i = 0; i < 100; i++) Assert.That(attack.Tick(0f, true), Is.False);
            Assert.That(attack.Tick(.5f, true), Is.False);
            Assert.That(attack.Tick(.51f, true), Is.True);
        }

        [Test]
        public void LongFrameProducesOnlyOneHitAndNoCatchUpBurst()
        {
            var attack = new BossContactAttack(1f);
            Assert.That(attack.Tick(10f, true), Is.True);
            Assert.That(attack.Tick(.01f, true), Is.False);
        }
    }
}
