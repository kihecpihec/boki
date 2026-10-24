using System;

namespace Boki.Enemies
{
    public enum BossAction { None, FireSheet, StartSlamTelegraph, SlamImpact }

    // Story §3 Final Boss: alternate Exam Barrage (3 slow sheets) and Deadline Slam (0.8 s red warning, then damage).
    // At about 50% HP attacks become slightly faster; the telegraph stays 0.8 s so it remains readable.
    public sealed class BossAttackScheduler
    {
        public const int SheetsPerBarrage = 3;
        public const float SlamTelegraphSeconds = 0.8f;

        private enum Step { Waiting, Barrage, Telegraph }

        private readonly float attackGap;
        private readonly float sheetInterval;
        private readonly float enragedSpeedUp;
        private Step step = Step.Waiting;
        private float remaining;
        private int sheetsLeft;
        private bool slamNext;

        public bool IsEnraged { get; private set; }

        public BossAttackScheduler(float attackGap, float sheetInterval, float enragedSpeedUp, float openingDelay)
        {
            if (attackGap <= 0f) throw new ArgumentOutOfRangeException(nameof(attackGap));
            if (sheetInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(sheetInterval));
            if (enragedSpeedUp < 1f) throw new ArgumentOutOfRangeException(nameof(enragedSpeedUp));
            this.attackGap = attackGap;
            this.sheetInterval = sheetInterval;
            this.enragedSpeedUp = enragedSpeedUp;
            remaining = Math.Max(0f, openingDelay);
        }

        // One action per call; the adapter runs every frame, so long frames only delay an action slightly.
        public BossAction Tick(float deltaTime, float healthFraction)
        {
            if (deltaTime <= 0f) return BossAction.None;
            if (healthFraction <= 0.5f) IsEnraged = true;
            remaining -= deltaTime;
            if (remaining > 0f) return BossAction.None;

            switch (step)
            {
                case Step.Waiting:
                    return BeginAttack();
                case Step.Barrage:
                    remaining = Scaled(sheetInterval);
                    if (--sheetsLeft <= 0) EndAttack();
                    return BossAction.FireSheet;
                default:
                    EndAttack();
                    return BossAction.SlamImpact;
            }
        }

        private BossAction BeginAttack()
        {
            if (slamNext)
            {
                step = Step.Telegraph;
                remaining = SlamTelegraphSeconds;
                slamNext = false;
                return BossAction.StartSlamTelegraph;
            }
            step = Step.Barrage;
            sheetsLeft = SheetsPerBarrage - 1;
            remaining = Scaled(sheetInterval);
            slamNext = true;
            if (sheetsLeft == 0) EndAttack();
            return BossAction.FireSheet;
        }

        private void EndAttack()
        {
            // The gap after an attack starts once its last action has fired.
            step = Step.Waiting;
            remaining = Scaled(attackGap);
        }

        private float Scaled(float seconds) => IsEnraged ? seconds / enragedSpeedUp : seconds;
    }
}
