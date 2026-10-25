using System;
using System.Collections.Generic;
using Boki.Upgrades;
using Boki.Core;
using Boki.Combat;
using Boki.Battery;
using Boki.Enemies;

namespace Boki.Routes
{
    public enum RouteLevel { Forest, City, Feri }
    public enum RouteChoice { None, A, B, C }
    public enum GateState { Available, PersistentLock, RouteLock, Selected }

    // Value semantics prevent an earned unlock from changing the current run.
    public struct UnlockSnapshot
    {
        public bool ForestC;
        public bool CityC;
        public bool FeriC;
        public bool AllowsC(RouteLevel level) => level == RouteLevel.Forest ? ForestC
            : level == RouteLevel.City ? CityC : level == RouteLevel.Feri && FeriC;
    }

    public sealed class RunState
    {
        private readonly bool[] phoneStoryConsumed = new bool[3];
        private readonly RouteChoice[] choices = new RouteChoice[3];
        public GuiltState Guilt { get; } = new GuiltState();
        public HealthState Health { get; private set; }
        public LightBatteryState LightBattery { get; } = new LightBatteryState();
        public float Battery { get => LightBattery.Current; set => LightBattery.Current = value; }
        public PauseState Pause { get; } = new PauseState();
        public UpgradeState Progression { get; private set; }
        public int XP => Progression.XP;
        public IReadOnlyList<UpgradeId> Upgrades => Progression.Accepted;
        public bool CitySideEscapeCompleted { get; private set; }
        public bool CityRetreatStarted { get; private set; }
        public bool PowerOff { get; private set; }
        public bool BossShortcutTaken { get; private set; }
        public int KillCount { get; set; }
        public double StartedAt { get; private set; }
        public UnlockSnapshot AvailableUnlocks { get; private set; }
        public RouteLevel ActiveLevel { get; private set; }
        public event Action Changed;

        public RunState(UnlockSnapshot unlocks, double startedAt) => NewRun(unlocks, startedAt);

        public void NewRun(UnlockSnapshot unlocks, double startedAt)
        {
            Guilt.Reset();
            Health = new HealthState(100);
            LightBattery.Reset();
            if (Progression != null) Progression.Changed -= NotifyChanged;
            Progression = new UpgradeState(Health, LightBattery);
            Progression.Changed += NotifyChanged;
            KillCount = 0;
            Pause.Reset();
            CityRetreatStarted = CitySideEscapeCompleted = PowerOff = BossShortcutTaken = false;
            Array.Clear(choices, 0, choices.Length);
            Array.Clear(phoneStoryConsumed, 0, phoneStoryConsumed.Length);
            ActiveLevel = RouteLevel.Forest;
            StartedAt = startedAt;
            AvailableUnlocks = unlocks;
            Changed?.Invoke();
        }

        private void NotifyChanged() => Changed?.Invoke();

        public RouteChoice Choice(RouteLevel level) => ValidLevel(level) ? choices[(int)level] : RouteChoice.None;
        public static bool ValidLevel(RouteLevel level) => level >= RouteLevel.Forest && level <= RouteLevel.Feri;
        public static bool ValidChoice(RouteChoice route) => route >= RouteChoice.A && route <= RouteChoice.C;

        // Story §3: a missed safe window is consumed too, never queued into combat.
        public bool TryConsumePhoneStory(RouteLevel level)
        {
            if (!ValidLevel(level) || level != ActiveLevel || !Health.IsAlive
                || Pause.IsPaused || Guilt.GameplaySuspended || Guilt.IsLoading
                || BossShortcutTaken || Guilt.BossEntered || phoneStoryConsumed[(int)level]) return false;
            phoneStoryConsumed[(int)level] = true;
            return true;
        }

        public GateState Gate(RouteLevel level, RouteChoice route)
        {
            if (!ValidLevel(level) || !ValidChoice(route)) return GateState.RouteLock;
            var selected = Choice(level);
            if (selected != RouteChoice.None)
                return selected == route ? GateState.Selected : GateState.RouteLock;
            if (BossShortcutTaken || level != ActiveLevel || !Linked(level, route)) return GateState.RouteLock;
            return route == RouteChoice.C && !AvailableUnlocks.AllowsC(level)
                ? GateState.PersistentLock : GateState.Available;
        }

        private bool Linked(RouteLevel level, RouteChoice route)
        {
            if (level == RouteLevel.Forest) return true;
            var previous = Choice((RouteLevel)((int)level - 1));
            if (level == RouteLevel.City)
            {
                switch (previous)
                {
                    case RouteChoice.A: return route == RouteChoice.A || route == RouteChoice.B;
                    case RouteChoice.B: return route == RouteChoice.A || route == RouteChoice.C;
                    case RouteChoice.C: return route == RouteChoice.B || route == RouteChoice.C;
                    default: return false;
                }
            }
            switch (previous)
            {
                case RouteChoice.A: return route == RouteChoice.A || route == RouteChoice.B;
                case RouteChoice.B: return route == RouteChoice.B || route == RouteChoice.C;
                case RouteChoice.C: return route == RouteChoice.A || route == RouteChoice.C;
                default: return false;
            }
        }

        public bool TrySelect(RouteLevel level, RouteChoice route)
        {
            if (!Health.IsAlive || level != ActiveLevel || Gate(level, route) != GateState.Available) return false;
            choices[(int)level] = route;
            Changed?.Invoke();
            return true;
        }

        public bool CanAdvance(RouteLevel level) => ValidLevel(level) && level == ActiveLevel
            && level != RouteLevel.Feri && Health.IsAlive && Choice(level) != RouteChoice.None;

        // Gameplay §1.1: V1 portal speed shortcut; the adapter supplies horizontal speed and tuning.
        public bool CanSkipToBoss(RouteLevel from, float horizontalSpeed, float minimumSpeed) =>
            !BossShortcutTaken && CanAdvance(from) && !Pause.IsPaused && !Guilt.GameplaySuspended
            && !Guilt.ScriptedSequenceActive && !Guilt.IsLoading
            && (from != RouteLevel.City || Choice(from) != RouteChoice.C || CitySideEscapeCompleted)
            && minimumSpeed > 0f && !float.IsInfinity(minimumSpeed)
            && horizontalSpeed >= minimumSpeed && !float.IsInfinity(horizontalSpeed);

        public bool TrySkipToBoss(RouteLevel from, float horizontalSpeed, float minimumSpeed)
        {
            if (!CanSkipToBoss(from, horizontalSpeed, minimumSpeed)) return false;
            BossShortcutTaken = true;
            ActiveLevel = RouteLevel.Feri;
            Guilt.Reset();
            Changed?.Invoke();
            return true;
        }

        public bool CompleteCityEscape()
        {
            if (ActiveLevel != RouteLevel.City || Choice(RouteLevel.City) != RouteChoice.C || !Health.IsAlive || !CityRetreatStarted) return false;
            CitySideEscapeCompleted = true;
            Guilt.ScriptedSequenceActive = false;
            Changed?.Invoke();
            return true;
        }

        public bool BeginCityRetreat()
        {
            if (CityRetreatStarted || ActiveLevel != RouteLevel.City || Choice(RouteLevel.City) != RouteChoice.C || !Health.IsAlive) return false;
            CityRetreatStarted = true;
            Guilt.ScriptedSequenceActive = true;
            return true;
        }

        public bool SwitchPowerOff()
        {
            if (ActiveLevel != RouteLevel.Feri || Choice(RouteLevel.Feri) != RouteChoice.C || !Health.IsAlive) return false;
            PowerOff = true;
            Changed?.Invoke();
            return true;
        }

        public bool TryAdvance(RouteLevel level)
        {
            if (!CanAdvance(level)) return false;
            Guilt.Reset();
            ActiveLevel = (RouteLevel)((int)level + 1);
            Changed?.Invoke();
            return true;
        }
    }
}
