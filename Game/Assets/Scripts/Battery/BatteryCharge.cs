using Boki.Player;
using UnityEngine;

namespace Boki.Battery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BatteryCharge : MonoBehaviour
    {
        [SerializeField] private BatteryCombatSpace space;
        [SerializeField] private PlayerHealth player;
        [SerializeField, Min(0)] private int slot;
        [SerializeField] private GameObject chargedVisual;
        [SerializeField] private GameObject depletedVisual;
        public event System.Action Collected;
        public bool Available => space.State.Available(slot);

        private void OnEnable()
        {
            if (space != null && player != null && slot >= 0 && slot < space.State.Count
                && chargedVisual != null && depletedVisual != null
                && chargedVisual != gameObject && depletedVisual != gameObject
                && chargedVisual != depletedVisual && GetComponent<SphereCollider>().isTrigger) return;
            Debug.LogError("Assign Space, Player, a valid unique Slot, separate child Charged/Depleted Visuals and a trigger SphereCollider on BatteryCharge.", this);
            enabled = false;
        }
        private void Update()
        {
            bool available = Available;
            if (chargedVisual.activeSelf != available) chargedVisual.SetActive(available);
            if (depletedVisual.activeSelf == available) depletedVisual.SetActive(!available);
        }
        private void OnTriggerEnter(Collider other) => Collect(other);
        private void OnTriggerStay(Collider other) => Collect(other);
        private void Collect(Collider other)
        {
            if (!isActiveAndEnabled || !space.isActiveAndEnabled || other.GetComponentInParent<PlayerHealth>() != player) return;
            if (space.State.TryCollect(slot, space.Battery.State, space.Battery.IsGameplayActive))
                Collected?.Invoke();
        }
    }
}
