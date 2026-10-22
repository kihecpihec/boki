using System;
using System.Collections.Generic;
using Boki.Combat;

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
        private readonly RouteChoice[] choices = new RouteChoice[3];
        public HealthState Health { get; private set; }
        public int Battery { get; set; }
        public int XP { get; set; }
        public List<string> Upgrades { get; } = new List<string>();
        public int KillCount { get; set; }
        public double StartedAt { get; private set; }
        public UnlockSnapshot AvailableUnlocks { get; private set; }
        public RouteLevel ActiveLevel { get; private set; }
        public event Action Changed;

        public RunState(UnlockSnapshot unlocks, double startedAt) => NewRun(unlocks, startedAt);

        public void NewRun(UnlockSnapshot unlocks, double startedAt)
        {
            Health = new HealthState(100);
            Battery = 100;
            XP = KillCount = 0;
            Upgrades.Clear();
            Array.Clear(choices, 0, choices.Length);
            ActiveLevel = RouteLevel.Forest;
            StartedAt = startedAt;
            AvailableUnlocks = unlocks;
            Changed?.Invoke();
        }

        public RouteChoice Choice(RouteLevel level) => ValidLevel(level) ? choices[(int)level] : RouteChoice.None;
        public static bool ValidLevel(RouteLevel level) => level >= RouteLevel.Forest && level <= RouteLevel.Feri;
        public static bool ValidChoice(RouteChoice route) => route >= RouteChoice.A && route <= RouteChoice.C;

        public GateState Gate(RouteLevel level, RouteChoice route)
        {
            if (!ValidLevel(level) || !ValidChoice(route)) return GateState.RouteLock;
            var selected = Choice(level);
            if (selected != RouteChoice.None)
                return selected == route ? GateState.Selected : GateState.RouteLock;
            if (level != ActiveLevel || !Linked(level, route)) return GateState.RouteLock;
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

        public bool TryAdvance(RouteLevel level)
        {
            if (!CanAdvance(level)) return false;
            ActiveLevel = (RouteLevel)((int)level + 1);
            Changed?.Invoke();
            return true;
        }
    }
}
