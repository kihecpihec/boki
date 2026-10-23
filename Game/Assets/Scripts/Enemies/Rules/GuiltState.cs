using System;

namespace Boki.Enemies
{
    public enum GuiltPhase { Distant, Warning, Chase, Reposition }

    // Gameplay §5: Distant -> Warning -> Chase -> Reposition. The adapter calls Tick only for active
    // combat-space time (pause, upgrade selection and scripted sequences pass no time), so the clock,
    // grace period and rest timers freeze together.
    public sealed class GuiltState
    {
        public const float GraceSeconds = 20f;
        public const float WarningSeconds = 1.5f;
        public const float ChaseSeconds = 6f;

        private float phaseRemaining = GraceSeconds;

        public GuiltPhase Phase { get; private set; } = GuiltPhase.Distant;
        public float AccumulatedCombatSeconds { get; private set; }
        // Snapshotted when the warning begins and unchanged through that chase.
        public float ChaseSpeed { get; private set; }
        public int ContactDamage { get; private set; }
        public event Action<GuiltPhase> PhaseChanged;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            AccumulatedCombatSeconds += deltaTime;
            phaseRemaining -= deltaTime;
            if (phaseRemaining > 0f) return;

            switch (Phase)
            {
                case GuiltPhase.Distant: BeginWarning(); break;
                case GuiltPhase.Warning: Enter(GuiltPhase.Chase, ChaseSeconds); break;
                case GuiltPhase.Chase: Enter(GuiltPhase.Reposition, 0f); break;
            }
            // Reposition is a single hand-off tick: the adapter moves Guilt out of sight, then rest begins.
            if (Phase == GuiltPhase.Reposition)
                Enter(GuiltPhase.Distant, GuiltPressure.Evaluate(AccumulatedCombatSeconds).RestSeconds);
        }

        // Stuck or too far away: Guilt relocates invisibly and the ambient cycle continues unchanged.
        public bool RequestReposition()
        {
            if (Phase != GuiltPhase.Chase) return false;
            PhaseChanged?.Invoke(GuiltPhase.Reposition);
            PhaseChanged?.Invoke(GuiltPhase.Chase);
            return true;
        }

        // Next level or New Run: pressure and phase progress start over (Gameplay §5).
        public void Reset()
        {
            Phase = GuiltPhase.Distant;
            AccumulatedCombatSeconds = 0f;
            phaseRemaining = GraceSeconds;
            ChaseSpeed = 0f;
            ContactDamage = 0;
        }

        private void BeginWarning()
        {
            GuiltTier tier = GuiltPressure.Evaluate(AccumulatedCombatSeconds);
            ChaseSpeed = tier.ChaseSpeed;
            ContactDamage = tier.ContactDamage;
            Enter(GuiltPhase.Warning, WarningSeconds);
        }

        private void Enter(GuiltPhase next, float duration)
        {
            Phase = next;
            phaseRemaining = duration;
            PhaseChanged?.Invoke(next);
        }
    }
}
