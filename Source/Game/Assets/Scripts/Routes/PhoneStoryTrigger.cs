using Boki.Core;
using Boki.Player;
using Boki.UI;
using UnityEngine;

namespace Boki.Routes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PhoneStoryTrigger : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private PhoneStoryView view;
        [SerializeField] private RouteLevel level;
        [SerializeField, Min(0f)] private float safeRidingSeconds;
        private float ridingAge;
        private bool attempted;

        private void OnEnable()
        {
            if (player == null || view == null || !GetComponent<BoxCollider>().isTrigger)
            {
                Debug.LogError("Assign PhoneStoryTrigger player/view and enable Is Trigger.", this);
                enabled = false;
            }
        }
        private void OnTriggerStay(Collider other)
        {
            if (attempted || other.GetComponentInParent<PlayerHealth>() != player) return;
            var session = RunSession.Instance;
            if (session == null || !session.IsGameplayActive) return;
            ridingAge += Time.deltaTime;
            if (ridingAge < safeRidingSeconds) return;
            attempted = true;
            view.TryShow(level);
        }
        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerHealth>() == player) ridingAge = 0f;
        }
    }
}
