using Boki.Core;
using UnityEngine;

namespace Boki.Player
{
    // Sandbox contact feedback only; production upgrade selection is a separate system.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class ArenaXpPickup : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        private bool collected;

        private void OnTriggerEnter(Collider other) => Collect(other);
        private void OnTriggerStay(Collider other) => Collect(other);

        private void Collect(Collider other)
        {
            var session = RunSession.Instance;
            if (collected || player == null || !player.IsAlive || session == null
                || !session.IsGameplayActive || session.State.Upgrades.Count >= 6
                || other.GetComponentInParent<PlayerHealth>() != player) return;
            collected = true;
            session.State.XP++;
            Destroy(gameObject);
        }
    }
}
