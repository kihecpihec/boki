using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.Progression
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class XPPickup : MonoBehaviour
    {
        private bool collected;
        private void OnTriggerEnter(Collider other) => Collect(other);
        private void OnTriggerStay(Collider other) => Collect(other);
        private void Collect(Collider other)
        {
            var session = RunSession.Instance;
            if (collected || session == null || !session.IsGameplayActive
                || other.GetComponentInParent<PlayerHealth>() == null || !session.State.Progression.CollectXP()) return;
            collected = true;
            Destroy(gameObject);
        }
    }
}
