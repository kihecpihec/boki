using Boki.Core;
using Boki.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Routes
{
    public enum V1RouteEventKind { CityReveal, CitySideDoor, PowerOff }
    public sealed class V1RouteEvent : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        [SerializeField] private V1GameFlow flow;
        [SerializeField] private V1RouteEventKind kind;
        [SerializeField] private InputActionReference interact;
        [SerializeField] private GameObject reveal;
        private bool handled, ownsAction, playerInside;
        public bool PlayerInside => playerInside;
        private void OnEnable()
        {
            if (manager == null || flow == null || (kind == V1RouteEventKind.PowerOff && interact == null))
            { Debug.LogError("Assign route event manager, flow and interaction action.", this); enabled = false; return; }
            if (kind == V1RouteEventKind.PowerOff && interact.action != null)
            {
                ownsAction = !interact.action.enabled;
                interact.action.performed += Interact;
                if (ownsAction) interact.action.Enable();
            }
        }
        private void OnDisable()
        {
            if (kind == V1RouteEventKind.PowerOff && interact != null)
            {
                interact.action.performed -= Interact;
                if (ownsAction) interact.action.Disable();
            }
            playerInside = false;
        }
        private void OnTriggerExit(Collider other) { if (manager != null && manager.IsPlayer(other)) playerInside = false; }
        private void Interact(InputAction.CallbackContext context)
        {
            if (!playerInside || handled || !manager.IsReady || !RunSession.Instance.IsGameplayActive) return;
            if (!RunSession.Instance.State.SwitchPowerOff()) return;
            flow.SetObjective("POWER OFF activated. The professor will start at 85% HP.");
            handled = true;
        }
        private void OnTriggerEnter(Collider other) => Enter(other);
        private void OnTriggerStay(Collider other) => Enter(other);
        private void Enter(Collider other)
        {
            if (handled || !manager.IsPlayer(other) || !manager.IsReady || !RunSession.Instance.IsGameplayActive) return;
            playerInside = true;
            var state = RunSession.Instance.State;
            if (kind == V1RouteEventKind.CityReveal)
            {
                if (!state.BeginCityRetreat()) return;
                if (reveal != null) reveal.SetActive(true);
                flow.SetObjective("GUILT BLOCKS THE TUNNEL. Turn back and take the side door.");
            }
            else if (kind == V1RouteEventKind.CitySideDoor)
            {
                if (!state.CompleteCityEscape()) return;
                if (reveal != null) reveal.SetActive(false);
                flow.SetObjective("Side escape found. Continue through the combat space to the FERI portal.");
            }
            else
            {
                if (state.PowerOff) return;
                flow.SetObjective("Press E at the POWER OFF panel.");
                return;
            }
            handled = true;
        }
    }
}
