using Boki.Core;
using UnityEngine;

namespace Boki.Routes
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LevelPortal : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        [SerializeField] private TextMesh label;
        [SerializeField] private string destinationLabel;
        private bool accepted;
        private void OnEnable()
        {
            if (manager == null || label == null) { Debug.LogError("Assign portal route manager and label.", this); enabled = false; return; }
            manager.Changed += Refresh;
            Refresh();
        }
        private void OnDisable() { if (manager != null) manager.Changed -= Refresh; }
        public bool TryTravel()
        {
            if (accepted || manager == null || !manager.IsReady) return false;
            var state = RunSession.Instance.State;
            if (manager.Level == RouteLevel.City && state.Choice(RouteLevel.City) == RouteChoice.C
                && !state.CitySideEscapeCompleted) return false;
            accepted = manager.Advance();
            return accepted;
        }
        private void OnTriggerEnter(Collider other) => Enter(other);
        private void OnTriggerStay(Collider other) => Enter(other);
        private void Enter(Collider other)
        {
            if (manager != null && manager.IsPlayer(other)) TryTravel();
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
