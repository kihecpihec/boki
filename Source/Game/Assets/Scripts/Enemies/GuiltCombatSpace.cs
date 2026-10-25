using System.Collections.Generic;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    // Add to an owned combat-space trigger; authored route activation controls unchosen spaces.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GuiltCombatSpace : MonoBehaviour
    {
        [SerializeField] private GuiltChaser guilt;
        [SerializeField] private PlayerHealth player;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();

        private void OnEnable()
        {
            if (guilt != null && player != null && GetComponent<BoxCollider>().isTrigger) return;
            Debug.LogError("Assign Guilt and Player, and enable Is Trigger on the combat-space BoxCollider.", this);
            enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || other.GetComponentInParent<PlayerHealth>() != player || !occupants.Add(other)) return;
            guilt.SetCombatSpaceOccupied(GetEntityId().GetRawData(), true);
        }

        private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

        private void OnTriggerExit(Collider other)
        {
            if (!occupants.Remove(other) || occupants.Count > 0) return;
            guilt.SetCombatSpaceOccupied(GetEntityId().GetRawData(), false);
        }

        private void OnDisable()
        {
            occupants.Clear();
            if (guilt != null) guilt.SetCombatSpaceOccupied(GetEntityId().GetRawData(), false);
        }
    }
}
