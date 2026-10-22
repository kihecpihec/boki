using UnityEngine;

namespace Boki.Routes
{
    public sealed class RouteGate : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        [SerializeField] private RouteChoice choice;
        [SerializeField] private Collider blocker;
        [SerializeField] private Collider selectionTrigger;
        [SerializeField] private Renderer barrierVisual;
        [SerializeField] private TextMesh sign;
        // Optional on the standalone M3 gates; assigned on M4 branches.
        [SerializeField] private Collider exitBlocker;
        [SerializeField] private Renderer exitVisual;
        [SerializeField] private GameObject encounterRoot;
        private void OnEnable()
        {
            if (manager == null || blocker == null || selectionTrigger == null || sign == null)
            {
                Debug.LogError("Assign gate manager, blocker, trigger, and sign.", this);
                enabled = false;
                return;
            }
            manager.Changed += Refresh;
            Refresh();
        }
        private void OnDisable() { if (manager != null) manager.Changed -= Refresh; }
        private void Refresh()
        {
            var state = manager.Gate(choice);
            bool locked = state == GateState.RouteLock || state == GateState.PersistentLock;
            blocker.enabled = locked;
            selectionTrigger.enabled = state == GateState.Available;
            if (barrierVisual != null) barrierVisual.enabled = locked;
            bool selected = state == GateState.Selected;
            if (exitBlocker != null) exitBlocker.enabled = !selected;
            if (exitVisual != null) exitVisual.enabled = !selected;
            if (encounterRoot != null && encounterRoot.activeSelf != selected)
                encounterRoot.SetActive(selected);
            string message = state == GateState.RouteLock ? "SEALED BY YOUR PREVIOUS PATH"
                : state == GateState.PersistentLock ? "DISCOVER ON ANOTHER RUN" : state.ToString().ToUpperInvariant();
            sign.text = choice + "\n" + message;
            sign.color = locked ? new Color(1f, 0.65f, 0.5f) : Color.green;
        }
        private void OnTriggerEnter(Collider other)
        {
            if (isActiveAndEnabled && manager.IsPlayer(other)) manager.Select(choice);
        }
    }
}
