using System;

namespace Boki.Enemies
{
    public enum BossAction { None, FireSheet, StartSlamTelegraph, SlamImpact, FirePaperRing }

    // Story §3 Final Boss: aimed barrage, double slam, radial barrage, triple slam.
    // At about 50% HP attacks become slightly faster; the telegraph stays 0.8 s so it remains readable.
    public sealed class BossAttackScheduler
    {
        public const int SheetsPerBarrage = 3;
        public const float SlamTelegraphSeconds = 0.8f;

        private enum Step { Waiting, Barrage, Telegraph, SlamRecovery }

        private readonly float attackGap;
        private readonly float sheetInterval;
        private readonly float enragedSpeedUp;
        private Step step = Step.Waiting;
        private float remaining;
        private int sheetsLeft;
        private int combo;
        private int slamsLeft;
        public int PaperRingIndex { get; private set; }

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
                    BossAction action = combo == 2 ? BossAction.FirePaperRing : BossAction.FireSheet;
                    if (combo == 2) PaperRingIndex++;
                    if (--sheetsLeft <= 0) EndAttack();
                    return action;
                case Step.SlamRecovery:
                    step = Step.Telegraph;
                    remaining = SlamTelegraphSeconds;
                    return BossAction.StartSlamTelegraph;
                default:
                    if (--slamsLeft > 0)
                    {
                        step = Step.SlamRecovery;
                        remaining = 0.35f;
                    }
                    else EndAttack();
                    return BossAction.SlamImpact;
            }
        }

        private BossAction BeginAttack()
        {
            if (combo == 1 || combo == 3)
            {
                step = Step.Telegraph;
                slamsLeft = combo == 1 ? 2 : 3;
                remaining = SlamTelegraphSeconds;
                return BossAction.StartSlamTelegraph;
            }
            step = Step.Barrage;
            PaperRingIndex = 0;
            sheetsLeft = combo == 2 ? 1 : SheetsPerBarrage - 1;
            remaining = Scaled(sheetInterval);
            return combo == 2 ? BossAction.FirePaperRing : BossAction.FireSheet;
        }

        private void EndAttack()
        {
            // The gap after an attack starts once its last action has fired.
            step = Step.Waiting;
            combo = (combo + 1) % 4;
            remaining = Scaled(attackGap);
        }

        private float Scaled(float seconds) => IsEnraged ? seconds / enragedSpeedUp : seconds;
    }
}
