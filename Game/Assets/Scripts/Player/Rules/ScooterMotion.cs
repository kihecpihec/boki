using System;
using System.Numerics;

namespace Boki.Player
{
    public sealed class ScooterMotion
    {
        private readonly ScooterMotionTuning tuning;
        private Vector2 horizontal;
        private float ridingMultiplier = 1f;
        private bool panicReady;
        private float activeBoostSpeed;
        public float ActiveBoostDuration { get; private set; }
        public float ActiveBoostSpeed => activeBoostSpeed;
        public void SetModifiers(float ordinaryRidingMultiplier, bool eligiblePanicBoost)
        {
            if (ordinaryRidingMultiplier < 1f || float.IsNaN(ordinaryRidingMultiplier)
                || float.IsInfinity(ordinaryRidingMultiplier)) throw new ArgumentOutOfRangeException(nameof(ordinaryRidingMultiplier));
            ridingMultiplier = ordinaryRidingMultiplier;
            panicReady = eligiblePanicBoost;
        }
        private float RidingSpeed => tuning.Speed * ridingMultiplier;
        private float coyoteRemaining, bufferRemaining, graceRemaining;
        private bool wasGrounded, ignoreGroundUntilAirborne;
        private bool hasBeenAirborne, hopEligible;

        public float HorizontalX => horizontal.X;
        public float HorizontalZ => horizontal.Y;
        public float Speed => horizontal.Length();
        public float VerticalSpeed { get; private set; }
        public float BoostRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float DisplacementX { get; private set; }
        public float DisplacementY { get; private set; }
        public float DisplacementZ { get; private set; }

        public ScooterMotion(ScooterMotionTuning tuning) => this.tuning = tuning;

        public void Reset()
        {
            horizontal = Vector2.Zero;
            ActiveBoostDuration = tuning.BoostDuration;
            activeBoostSpeed = tuning.BoostSpeed;
            VerticalSpeed = BoostRemaining = CooldownRemaining = 0f;
            coyoteRemaining = bufferRemaining = graceRemaining = 0f;
            DisplacementX = DisplacementY = DisplacementZ = 0f;
            wasGrounded = ignoreGroundUntilAirborne = false;
            hasBeenAirborne = hopEligible = false;
        }

        public void SuspendInput()
        {
            horizontal = Vector2.Zero;
            bufferRemaining = BoostRemaining = 0f;
            hopEligible = false;
        }

        public void Tick(float directionX, float directionZ, bool grounded,
            bool jumpPressed, bool boostPressed, float deltaSeconds, float facingX = 0f, float facingZ = 1f,
            float slopeAccelerationX = 0f, float slopeAccelerationZ = 0f)
        {
            DisplacementX = DisplacementY = DisplacementZ = 0f;
            if (deltaSeconds <= 0f) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            var input = new Vector2(directionX, directionZ);
            var slopeAcceleration = new Vector2(slopeAccelerationX, slopeAccelerationZ);
            if (input.LengthSquared() > 1f) input = Vector2.Normalize(input);
            if (!grounded)
            {
                ignoreGroundUntilAirborne = false;
                hasBeenAirborne = true;
            }
            grounded &= !ignoreGroundUntilAirborne;
            if (grounded && !wasGrounded)
            {
                graceRemaining = tuning.LandingGrace;
                hopEligible = hasBeenAirborne;
                hasBeenAirborne = false;
            }
            if (jumpPressed) bufferRemaining = tuning.JumpBuffer;
            if (boostPressed && grounded && BoostRemaining <= 0f && CooldownRemaining <= 0f)
            {
                var facing = new Vector2(facingX, facingZ);
                if (facing.LengthSquared() < 0.000001f) facing = Vector2.UnitY;
                Vector2 heading = input.LengthSquared() > 0f ? Vector2.Normalize(input)
                    : Speed > 0f ? Vector2.Normalize(horizontal) : Vector2.Normalize(facing);
                activeBoostSpeed = tuning.BoostSpeed * (panicReady ? 1.25f : 1f);
                ActiveBoostDuration = tuning.BoostDuration * (panicReady ? 1.5f : 1f);
                horizontal = heading * Math.Max(Speed, activeBoostSpeed);
                BoostRemaining = ActiveBoostDuration;
            }

            // Small bounded integration steps keep acceleration and timer boundaries stable across frame rates.
            float remaining = deltaSeconds;
            while (remaining > 0f)
            {
                float dt = Math.Min(remaining, 1f / 240f);
                if (BoostRemaining > 0f) dt = Math.Min(dt, BoostRemaining);
                if (grounded) coyoteRemaining = tuning.CoyoteTime;
                bool jump = bufferRemaining > 0f && (grounded || coyoteRemaining > 0f);
                if (jump)
                {
                    if (grounded && hopEligible && graceRemaining > 0f && BoostRemaining <= 0f
                        && Speed >= tuning.Speed * 0.8f && Speed > 0.001f && Vector2.Dot(horizontal, input) > 0f)
                    {
                        float limit = tuning.Speed * tuning.AirStrafeMaxMultiplier;
                        if (Speed < limit) horizontal *= Math.Min(limit, Speed + tuning.BunnyHopGain) / Speed;
                    }
                    hopEligible = false;
                    VerticalSpeed = (float)Math.Sqrt(2f * tuning.Gravity * tuning.JumpHeight);
                    bufferRemaining = coyoteRemaining = 0f;
                    ignoreGroundUntilAirborne = true;
                    grounded = false;
                }
                else if (grounded && VerticalSpeed < 0f) VerticalSpeed = -2f;

                Vector2 before = horizontal;
                bool hasInput = input.LengthSquared() > 0.000001f;
                if (grounded && !hasInput) horizontal += slopeAcceleration * dt;
                bool downhill = grounded && Vector2.Dot(horizontal, slopeAcceleration) > 0f;
                IntegrateHorizontal(input, grounded, downhill, dt);
                if (grounded && hasInput) horizontal += slopeAcceleration * dt;
                else if (BoostRemaining <= 0f) AirStrafe(input, dt);
                Vector2 distance = (before + horizontal) * (0.5f * dt);
                DisplacementX += distance.X;
                DisplacementZ += distance.Y;
                DisplacementY += VerticalSpeed * dt - 0.5f * tuning.Gravity * dt * dt;
                VerticalSpeed -= tuning.Gravity * dt;

                coyoteRemaining = Math.Max(0f, coyoteRemaining - dt);
                bufferRemaining = Math.Max(0f, bufferRemaining - dt);
                graceRemaining = Math.Max(0f, graceRemaining - dt);
                if (BoostRemaining > 0f)
                {
                    BoostRemaining = Math.Max(0f, BoostRemaining - dt);
                    if (BoostRemaining == 0f) CooldownRemaining = tuning.BoostCooldown;
                }
                else CooldownRemaining = Math.Max(0f, CooldownRemaining - dt);
                remaining = Math.Max(0f, remaining - dt);
            }
            wasGrounded = grounded;
        }

        private void IntegrateHorizontal(Vector2 input, bool grounded, bool downhill, float dt)
        {
            bool hasInput = input.LengthSquared() > 0.000001f;
            float speed = Speed;
            float cap = BoostRemaining > 0f ? activeBoostSpeed : RidingSpeed;
            if (!hasInput)
            {
                if (grounded && graceRemaining <= 0f)
                    horizontal = MoveTowards(horizontal, Vector2.Zero, tuning.Braking * dt);
                else if (!grounded && speed > cap)
                    horizontal *= Math.Max(cap, speed - tuning.AirDecay * dt) / speed;
                return;
            }
            if (speed > cap)
            {
                Vector2 target = Vector2.Normalize(input);
                if (grounded && Vector2.Dot(horizontal, target) < 0f)
                {
                    horizontal = MoveTowards(horizontal, target * cap, tuning.Braking * dt);
                    return;
                }
                float angle = (float)Math.Atan2(horizontal.X * target.Y - horizontal.Y * target.X,
                    Vector2.Dot(horizontal, target));
                float maxAngle = tuning.TurnRate * ((float)Math.PI / 180f) * dt;
                angle = Math.Max(-maxAngle, Math.Min(maxAngle, angle));
                float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                float decay = grounded ? graceRemaining > 0f || downhill ? 0f : tuning.GroundDecay : tuning.AirDecay;
                float nextSpeed = Math.Max(cap, speed - decay * dt);
                horizontal = new Vector2(horizontal.X * cos - horizontal.Y * sin,
                    horizontal.X * sin + horizontal.Y * cos) * (nextSpeed / speed);
                return;
            }
            Vector2 previous = horizontal;
            horizontal = MoveTowards(horizontal, input * cap,
                (grounded ? tuning.GroundAcceleration : tuning.AirAcceleration) * dt);
            // Steering across the trajectory must not erase the momentum that air strafing builds.
            if (!grounded && tuning.AirStrafeAcceleration > 0f && Vector2.Dot(previous, input) >= 0f
                && Speed > 0.001f && Speed < speed)
                horizontal *= speed / Speed;
        }

        private void AirStrafe(Vector2 input, float dt)
        {
            float strength = input.Length();
            if (strength < 0.001f || Speed < 0.001f || tuning.AirStrafeAcceleration <= 0f) return;
            Vector2 wish = input / strength;
            float projection = Vector2.Dot(horizontal, wish);
            // Accelerate along the wish direction, not by adding an unconditional jump-speed bonus.
            if (projection < 0f || projection >= tuning.Speed) return;
            float limit = Math.Max(Speed, tuning.Speed * tuning.AirStrafeMaxMultiplier);
            horizontal += wish * Math.Min(tuning.Speed - projection, tuning.AirStrafeAcceleration * strength * dt);
            if (Speed > limit) horizontal *= limit / Speed;
        }

        private static Vector2 MoveTowards(Vector2 current, Vector2 target, float distance)
        {
            Vector2 delta = target - current;
            float length = delta.Length();
            return length <= distance || length == 0f ? target : current + delta * (distance / length);
        }

        public void ResolveWall(float normalX, float normalZ)
        {
            var normal = new Vector2(normalX, normalZ);
            if (normal.LengthSquared() < 0.000001f) return;
            normal = Vector2.Normalize(normal);
            float intoWall = Vector2.Dot(horizontal, normal);
            if (intoWall < 0f) horizontal -= normal * intoWall;
        }

        public void ResolveVertical(bool below, bool above)
        {
            if (above && VerticalSpeed > 0f) VerticalSpeed = 0f;
            if (below && VerticalSpeed < 0f) VerticalSpeed = -2f;
        }
    }
}
