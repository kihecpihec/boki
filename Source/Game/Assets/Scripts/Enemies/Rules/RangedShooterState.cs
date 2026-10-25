using System;

namespace Boki.Enemies
{
    // Gameplay §6 RangedShooter: fires slow projectiles on a cooldown while Boki is in range and visible.
    public sealed class RangedShooterState
    {
        private readonly CooldownTimer cooldown;
        public float FireRange { get; }

        public RangedShooterState(float fireRange, float cooldownSeconds, float firstShotDelay)
        {
            if (fireRange <= 0f) throw new ArgumentOutOfRangeException(nameof(fireRange));
            FireRange = fireRange;
            cooldown = new CooldownTimer(cooldownSeconds, firstShotDelay);
        }

        // The cooldown keeps running while the target is out of range or hidden, but a shot is never banked.
        public bool Tick(float deltaTime, float distanceToTarget, bool hasLineOfSight)
        {
            cooldown.Tick(deltaTime);
            if (distanceToTarget > FireRange || !hasLineOfSight) return false;
            return cooldown.TryTrigger();
        }
    }
}
