using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.Routes
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LevelPortal : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        [SerializeField] private TextMesh label;
        [SerializeField] private string destinationLabel;
        [SerializeField, Min(0.01f)] private float bossSkipSpeed = 18f;
        private bool accepted;
        private void OnEnable()
        {
            if (manager == null || label == null) { Debug.LogError("Assign portal route manager and label.", this); enabled = false; return; }
            manager.Changed += Refresh;
            Refresh();
        }
        private void OnDisable() { if (manager != null) manager.Changed -= Refresh; }
        public bool TryTravel() => TryTravel(0f);

        private bool TryTravel(float horizontalSpeed)
        {
            if (accepted || manager == null || !manager.IsReady) return false;
            var session = RunSession.Instance;
            if (!session.IsGameplayActive) return false;
            var state = session.State;
            if (manager.Level == RouteLevel.City && state.Choice(RouteLevel.City) == RouteChoice.C
                && !state.CitySideEscapeCompleted) return false;
            accepted = session.TrySkipToV1Boss(manager.Level, horizontalSpeed, bossSkipSpeed) || manager.Advance();
            return accepted;
        }
        private void OnTriggerEnter(Collider other) => Enter(other);
        private void OnTriggerStay(Collider other) => Enter(other);
        private void Enter(Collider other)
        {
            if (manager == null || !manager.IsPlayer(other)) return;
            var movement = other.GetComponentInParent<ScooterController>();
            TryTravel(movement != null ? movement.HorizontalSpeed : 0f);
        }
        private void Refresh()
        {
            if (label == null || manager == null) return;
            string message = manager.IsReady && RunSession.Instance.State.Choice(manager.Level) != RouteChoice.None
                ? "PORTAL TO " + destinationLabel : "CHOOSE A ROUTE FIRST";
            if (manager.Level == RouteLevel.City && RunSession.Instance.State.Choice(RouteLevel.City) == RouteChoice.C
                && !RunSession.Instance.State.CitySideEscapeCompleted) message = "USE THE SIDE DOOR FIRST";
            if (label.text != message) label.text = message;
        }
    }
}
