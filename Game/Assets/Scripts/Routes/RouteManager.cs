using System;
using Boki.Core;
using Boki.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.Routes
{
    public sealed class RouteManager : MonoBehaviour
    {
        [SerializeField] private RouteLevel level;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private Text status;
        private RunSession session;
        private bool startingNewRun;
        public RouteLevel Level => level;
        public event Action Changed;
        public bool IsReady => isActiveAndEnabled && session != null && !session.IsLoading && player != null
            && player.IsAlive && session.State.ActiveLevel == level;
        public bool IsPlayer(Collider other) => player != null && other.GetComponentInParent<PlayerHealth>() == player;
        public GateState Gate(RouteChoice choice) => (session != null ? session : RunSession.Ensure()).State.Gate(level, choice);

        private void OnEnable()
        {
            session = RunSession.Ensure();
            session.State.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (session != null) session.State.Changed -= Refresh;
        }
        private void Refresh()
        {
            if (status != null)
                status.text = session.State.ActiveLevel != level
                    ? "Start at Forest first. Use Start New Run."
                    : $"{level} | Forest: {session.State.Choice(RouteLevel.Forest)} | City: {session.State.Choice(RouteLevel.City)} | FERI: {session.State.Choice(RouteLevel.Feri)}\n{(level == RouteLevel.Feri ? "Reach the classroom" : "Reach the shared exit")}";
            Changed?.Invoke();
        }
        public bool Select(RouteChoice choice) => IsReady && session.State.TrySelect(level, choice);
        public bool Advance() => IsReady && session.Advance(level);
        public void StartNewRun()
        {
            if (startingNewRun) return;
            startingNewRun = true;
            if (!session.Retry()) startingNewRun = false;
        }
    }
}
