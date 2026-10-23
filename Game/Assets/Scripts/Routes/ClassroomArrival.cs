using Boki.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.Routes
{
    // Arena entry earns the M5 unlock; M6 owns boss startup.
    public sealed class ClassroomArrival : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        [SerializeField] private Text message;
        public bool HasArrived { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || HasArrived || manager == null || !manager.IsPlayer(other) || !manager.IsReady
                || manager.Level != RouteLevel.Feri
                || RunSession.Instance.State.Choice(RouteLevel.Feri) == RouteChoice.None) return;
            if (!RunSession.Instance.EnterBossArena()) return;
            HasArrived = true;
            if (message != null) message.text = "CLASSROOM REACHED — C ROUTES AVAILABLE NEXT RUN";
        }
    }
}
