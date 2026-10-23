using System;

namespace Boki.Player
{
    public readonly struct ScooterMotionTuning
    {
        public readonly float Speed, BoostSpeed, BoostDuration, BoostCooldown;
        public readonly float JumpHeight, Gravity, GroundAcceleration, AirAcceleration;
        public readonly float Braking, GroundDecay, AirDecay, TurnRate;
        public readonly float CoyoteTime, JumpBuffer, LandingGrace;
        public readonly float AirStrafeAcceleration, AirStrafeMaxMultiplier;

        public ScooterMotionTuning(float speed, float boostSpeed, float boostDuration, float boostCooldown,
            float jumpHeight, float gravity, float groundAcceleration, float airAcceleration,
            float braking, float groundDecay, float airDecay, float turnRate,
            float coyoteTime, float jumpBuffer, float landingGrace,
            float airStrafeAcceleration = 0f, float airStrafeMaxMultiplier = 2f)
        {
            foreach (float value in new[] { speed, boostSpeed, boostDuration, boostCooldown, jumpHeight,
                gravity, groundAcceleration, airAcceleration, braking, groundDecay, airDecay,
                turnRate, coyoteTime, jumpBuffer, landingGrace, airStrafeAcceleration, airStrafeMaxMultiplier })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new ArgumentOutOfRangeException(nameof(value));
            if (gravity == 0f) throw new ArgumentOutOfRangeException(nameof(gravity));
            Speed = speed; BoostSpeed = boostSpeed; BoostDuration = boostDuration; BoostCooldown = boostCooldown;
            JumpHeight = jumpHeight; Gravity = gravity; GroundAcceleration = groundAcceleration;
            AirAcceleration = airAcceleration; Braking = braking; GroundDecay = groundDecay;
            AirDecay = airDecay; TurnRate = turnRate; CoyoteTime = coyoteTime;
            JumpBuffer = jumpBuffer; LandingGrace = landingGrace;
            AirStrafeAcceleration = airStrafeAcceleration;
            AirStrafeMaxMultiplier = Math.Max(1f, airStrafeMaxMultiplier);
        }
    }
}
