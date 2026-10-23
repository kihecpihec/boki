using System;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    // Immortal pursuer (Gameplay §5 "Keep Guilt AI simple"). Has no EnemyHealth, so the laser never targets it.
    // Timing and the pressure table live in GuiltState; this component only moves and presents.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class GuiltChaser : MonoBehaviour
    {
        [SerializeField] private PlayerHealth target;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool pressureActive = true;
        [SerializeField, Min(0.5f)] private float contactDistance = 2f;
        [SerializeField, Min(0.1f)] private float contactCooldown = 1f;
        [SerializeField, Min(8f)] private float minimumSpawnDistance = 12f;
        [SerializeField, Min(10f)] private float repositionDistance = 45f;
        [SerializeField, Min(0.5f)] private float stuckSeconds = 2f;
        private readonly GuiltState state = new GuiltState();
        private CooldownTimer contact;
        private CharacterController controller;
        private CharacterController targetController;
        private Camera viewCamera;
        private float verticalSpeed;
        private float stuckTimer;
        private Vector3 stuckAnchor;
        private int nextSpawn;
        public GuiltPhase Phase => state.Phase;
        public event Action<GuiltPhase> PhaseChanged;
        public event Action Struck;

        // Callers pass false outside combat spaces, during pause, upgrade selection and scripted sequences.
        public void SetPressureActive(bool active) => pressureActive = active;
        public void ResetPressure()
        {
            state.Reset();
            Hide();
        }

        private void OnEnable()
        {
            controller = GetComponent<CharacterController>();
            if (target == null || visualRoot == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("Assign target, visual root and spawn points on GuiltChaser.", this);
                enabled = false;
                return;
            }
            targetController = target.GetComponent<CharacterController>();
            viewCamera = Camera.main;
            contact = new CooldownTimer(contactCooldown);
            state.PhaseChanged += OnPhaseChanged;
            Hide();
        }

        private void OnDisable() => state.PhaseChanged -= OnPhaseChanged;

        private void Update()
        {
            if (!target.isActiveAndEnabled || !target.IsAlive) return;
            float dt = Time.deltaTime;
            if (pressureActive) state.Tick(dt);
            if (state.Phase != GuiltPhase.Chase || !pressureActive) return;
            Chase(dt);
        }

        private void OnPhaseChanged(GuiltPhase phase)
        {
            switch (phase)
            {
                case GuiltPhase.Warning: Place(PickSpawnPoint()); break;
                case GuiltPhase.Reposition: Hide(); break;
            }
            PhaseChanged?.Invoke(phase);
        }

        private void Chase(float dt)
        {
            contact.Tick(dt);
            Vector3 delta = target.transform.position - transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.magnitude > repositionDistance || IsStuck(dt)) { Reposition(); return; }

            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed -= 25f * dt;
            Vector3 direction = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
            controller.Move((direction * state.ChaseSpeed + Vector3.up * verticalSpeed) * dt);
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);

            Vector3 ownCenter = transform.TransformPoint(controller.center);
            Vector3 playerCenter = targetController != null
                ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;
            if ((playerCenter - ownCenter).sqrMagnitude > contactDistance * contactDistance) return;
            if (Physics.Linecast(ownCenter, playerCenter, 1, QueryTriggerInteraction.Ignore)) return;
            if (!contact.TryTrigger()) return;
            target.TakeDamage(state.ContactDamage);
            Struck?.Invoke();
        }

        private bool IsStuck(float dt)
        {
            stuckTimer += dt;
            if (stuckTimer < stuckSeconds) return false;
            bool stuck = (transform.position - stuckAnchor).sqrMagnitude < 0.25f;
            stuckTimer = 0f;
            stuckAnchor = transform.position;
            return stuck;
        }

        private void Reposition()
        {
            if (state.RequestReposition()) Place(PickSpawnPoint());
        }

        private Transform PickSpawnPoint()
        {
            // Prefer an offscreen marker far enough from Boki; fall back to the next marker so Guilt never stalls.
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[(nextSpawn + i) % spawnPoints.Length];
                if ((point.position - target.transform.position).sqrMagnitude < minimumSpawnDistance * minimumSpawnDistance) continue;
                if (IsVisible(point.position)) continue;
                nextSpawn = (nextSpawn + i + 1) % spawnPoints.Length;
                return point;
            }
            Transform fallback = spawnPoints[nextSpawn % spawnPoints.Length];
            nextSpawn = (nextSpawn + 1) % spawnPoints.Length;
            return fallback;
        }

        private bool IsVisible(Vector3 position)
        {
            if (viewCamera == null) return false;
            Vector3 viewport = viewCamera.WorldToViewportPoint(position);
            return viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
        }

        private void Place(Transform point)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(point.position, point.rotation);
            controller.enabled = true;
            verticalSpeed = 0f;
            stuckTimer = 0f;
            stuckAnchor = point.position;
            visualRoot.SetActive(true);
        }

        private void Hide() => visualRoot.SetActive(false);
    }
}
