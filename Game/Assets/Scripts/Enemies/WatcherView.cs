using UnityEngine;

namespace Boki.Enemies
{
    // Presentation only: the Watcher has no authored clips, so firing is a short recoil on the visual root.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedEnemy))]
    public sealed class WatcherView : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float recoilDistance = 0.18f;
        [SerializeField, Min(0.05f)] private float recoilSeconds = 0.25f;
        private RangedEnemy ranged;
        private Vector3 restPosition;
        private float recoilRemaining;

        private void OnEnable()
        {
            ranged = GetComponent<RangedEnemy>();
            if (visual == null)
            {
                Debug.LogError("Assign the visual transform on WatcherView.", this);
                enabled = false;
                return;
            }
            restPosition = visual.localPosition;
            ranged.Fired += OnFired;
        }

        private void OnDisable()
        {
            ranged.Fired -= OnFired;
            if (visual != null) visual.localPosition = restPosition;
        }

        private void OnFired() => recoilRemaining = recoilSeconds;

        private void Update()
        {
            if (recoilRemaining <= 0f) return;
            recoilRemaining = Mathf.Max(0f, recoilRemaining - Time.deltaTime);
            float t = recoilRemaining / recoilSeconds;
            // The visual's local forward is the Watcher's aim direction; kick back, then settle.
            visual.localPosition = restPosition - Vector3.forward * (recoilDistance * t);
        }
    }
}
