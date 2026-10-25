using System;
using System.Collections.Generic;

namespace Boki.Enemies
{
    public enum GuiltPhase { Distant, Warning, Chase, Reposition }

    // Gameplay §5: combat-space occupancy and suspension gate both pressure and ambient timers.
    public sealed class GuiltState
    {
        public const float GraceSeconds = 20f;
        public const float WarningSeconds = 1.5f;
        public const float ChaseSeconds = 6f;

        private float phaseRemaining = GraceSeconds;
        private readonly HashSet<ulong> combatSpaces = new HashSet<ulong>();

        public bool GameplaySuspended { get; set; }
        public bool ScriptedSequenceActive { get; set; }
        public bool IsLoading { get; set; }
        public bool BossEntered { get; private set; }
        public bool AwaitingPlacement { get; private set; }

        public void WaitForPlacement()
        {
            if (Phase == GuiltPhase.Warning) AwaitingPlacement = true;
        }

        public void ConfirmPlacement()
        {
            if (!AwaitingPlacement) return;
            AwaitingPlacement = false;
            SnapshotChase();
            phaseRemaining = WarningSeconds;
        }

        public bool IsActive => combatSpaces.Count > 0 && !GameplaySuspended
            && !ScriptedSequenceActive && !IsLoading && !BossEntered;

        public void SetCombatSpaceOccupied(ulong spaceId, bool occupied)
        {
            if (occupied) combatSpaces.Add(spaceId);
            else combatSpaces.Remove(spaceId);
        }

        public void EndForBoss()
        {
            if (BossEntered) return;
            BossEntered = true;
            AwaitingPlacement = false;
            Enter(GuiltPhase.Distant, GraceSeconds);
        }

        public GuiltPhase Phase { get; private set; } = GuiltPhase.Distant;
        public float AccumulatedCombatSeconds { get; private set; }
        // Snapshotted when the warning begins and unchanged through that chase.
        public float ChaseSpeed { get; private set; }
        public int ContactDamage { get; private set; }
        public event Action<GuiltPhase> PhaseChanged;

        public void Tick(float deltaTime)
        {
            if (!IsActive || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            // Consume each boundary at its actual time, so frame size cannot change tier snapshots.
            while (deltaTime > 0f)
            {
                // Missing safe geometry must not shorten the visible warning or stop lingering pressure.
                if (AwaitingPlacement)
                {
                    AccumulatedCombatSeconds += deltaTime;
                    break;
                }
                float step = Math.Min(deltaTime, phaseRemaining);
                AccumulatedCombatSeconds += step;
                phaseRemaining -= step;
                deltaTime -= step;
                if (phaseRemaining > 0f) break;

                switch (Phase)
                {
                    case GuiltPhase.Distant: BeginWarning(); break;
                    case GuiltPhase.Warning: Enter(GuiltPhase.Chase, ChaseSeconds); break;
                    case GuiltPhase.Chase:
                        Enter(GuiltPhase.Reposition, 0f);
                        Enter(GuiltPhase.Distant, GuiltPressure.Evaluate(AccumulatedCombatSeconds).RestSeconds);
                        break;
                }
                if (!IsActive) break;
            }
        }

        // Stuck or too far away: Guilt relocates invisibly and the ambient cycle continues unchanged.
        public bool RequestReposition()
        {
            if (!IsActive || Phase != GuiltPhase.Chase) return false;
            PhaseChanged?.Invoke(GuiltPhase.Reposition);
            PhaseChanged?.Invoke(GuiltPhase.Chase);
            return true;
        }

        // Next level or New Run: pressure and phase progress start over (Gameplay §5).
        public void Reset()
        {
            combatSpaces.Clear();
            GameplaySuspended = ScriptedSequenceActive = IsLoading = BossEntered = AwaitingPlacement = false;
            AccumulatedCombatSeconds = 0f;
            phaseRemaining = GraceSeconds;
            ChaseSpeed = 0f;
            ContactDamage = 0;
            Enter(GuiltPhase.Distant, GraceSeconds);
        }

        private void BeginWarning()
        {
            SnapshotChase();
            Enter(GuiltPhase.Warning, WarningSeconds);
        }

        private void SnapshotChase()
        {
            GuiltTier tier = GuiltPressure.Evaluate(AccumulatedCombatSeconds);
            ChaseSpeed = tier.ChaseSpeed;
            ContactDamage = tier.ContactDamage;
        }

        private void Enter(GuiltPhase next, float duration)
        {
            Phase = next;
            phaseRemaining = duration;
            PhaseChanged?.Invoke(next);
        }
    }
}
