using UnityEngine;

namespace Boki.Routes
{
    public sealed class LevelExit : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        private void OnTriggerEnter(Collider other)
        {
            if (isActiveAndEnabled && manager != null && manager.IsPlayer(other)) manager.Advance();
        }
    }
}
