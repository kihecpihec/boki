using Boki.Player;
using UnityEngine;

namespace Boki.Battery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BatteryCombatSpace : MonoBehaviour
    {
        [SerializeField] private LightBattery battery;
        [SerializeField] private PlayerHealth player;
        [Tooltip("Unique within this scene; retain this ID when moving the volume.")]
        [SerializeField] private string spaceId;
        [SerializeField, Min(1)] private int chargeCount = 3;
        private BoxCollider volume;
        private CharacterController playerCollider;
        private string key;
        private ChargeSpaceState boundState;
        public LightBattery Battery => battery;
        public ChargeSpaceState State
        {
            get
            {
                if (key == null) key = gameObject.scene.path + ":" + spaceId;
                boundState = battery.State.GetSpace(key, chargeCount);
                return boundState;
            }
        }

        private void Awake() => volume = GetComponent<BoxCollider>();
        private void OnEnable()
        {
            if (player != null) playerCollider = player.GetComponent<CharacterController>();
            if (battery != null && playerCollider != null && !string.IsNullOrWhiteSpace(spaceId)
                && chargeCount > 0 && volume.isTrigger) return;
            Debug.LogError("Assign Battery, a Player with CharacterController, a unique Space Id, Charge Count and a trigger BoxCollider on BatteryCombatSpace.", this);
            enabled = false;
        }

        private void FixedUpdate()
        {
            // Trigger exit callbacks are not guaranteed when a controller is disabled or teleported.
            State.Occupied = volume.enabled && player.isActiveAndEnabled && player.IsAlive && playerCollider.enabled
                && Physics.ComputePenetration(volume, transform.position, transform.rotation,
                    playerCollider, playerCollider.transform.position, playerCollider.transform.rotation, out _, out _);
        }
        private void OnDisable()
        {
            if (boundState != null) boundState.Occupied = false;
        }
    }
}
