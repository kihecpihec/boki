using System;
using Boki.Player;
using Boki.Core;
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
        [SerializeField] private bool useRunSession;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField, Min(0.5f)] private float contactDistance = 2f;
        [SerializeField, Min(0.1f)] private float contactCooldown = 1f;
        [SerializeField, Min(8f)] private float minimumSpawnDistance = 12f;
        [SerializeField, Min(10f)] private float repositionDistance = 45f;
        [SerializeField, Min(0.5f)] private float stuckSeconds = 2f;
        private readonly GuiltState standaloneState = new GuiltState();
        private GuiltState state;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private readonly Collider[] overlaps = new Collider[16];
        private bool placed;
        private bool cycleHasWarning;
        private bool repositionPending;
        private float retryRemaining;
        private CooldownTimer contact;
        private CharacterController controller;
        private CharacterController targetController;
        private Camera viewCamera;
        private float verticalSpeed;
        private float stuckTimer;
        private Vector3 stuckAnchor;
        private int nextSpawn;
        public GuiltState Pressure
        {
            get
            {
                if (state == null) BindState();
                return state;
            }
        }
        public GuiltPhase Phase => state != null ? state.Phase : GuiltPhase.Distant;
        public event Action<GuiltPhase> PhaseChanged;
        public event Action Struck;

        // Standalone test-space convenience. Production volumes use their own IDs so overlaps are safe.
        public void SetPressureActive(bool active) => SetCombatSpaceOccupied(GetEntityId().GetRawData(), active);
        public void SetCombatSpaceOccupied(ulong spaceId, bool occupied)
        {
            if (state == null) BindState();
            state.SetCombatSpaceOccupied(spaceId, occupied);
            if (!state.IsActive) SuspendPresentation();
        }

        private void BindState() => state = useRunSession ? RunSession.Ensure().State.Guilt : standaloneState;

        private void OnEnable()
        {
            BindState();
            controller = GetComponent<CharacterController>();
            if (target == null || visualRoot == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("Assign target, visual root and spawn points on GuiltChaser.", this);
                enabled = false;
                return;
            }
            targetController = target.GetComponent<CharacterController>();
            viewCamera = Camera.main;
            if (contact == null) contact = new CooldownTimer(contactCooldown);
            if (viewCamera == null)
            {
                Debug.LogError("GuiltChaser needs a camera tagged MainCamera for safe offscreen placement.", this);
                enabled = false;
                return;
            }
            state.PhaseChanged += OnPhaseChanged;
            cycleHasWarning = !state.AwaitingPlacement
                && (state.Phase == GuiltPhase.Warning || state.Phase == GuiltPhase.Chase);
            SuspendPresentation();
        }

        private void OnDisable()
        {
            if (state != null) state.PhaseChanged -= OnPhaseChanged;
            if (visualRoot != null) SuspendPresentation();
        }

        private void Update()
        {
            if (!target.isActiveAndEnabled || !target.IsAlive || !state.IsActive)
            {
                SuspendPresentation();
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            state.Tick(dt);
            contact.Tick(dt);
            if (state.Phase != GuiltPhase.Warning && state.Phase != GuiltPhase.Chase) return;
            if (!placed && (state.Phase == GuiltPhase.Warning || cycleHasWarning || repositionPending))
            {
                retryRemaining -= dt;
                if (retryRemaining <= 0f)
                {
                    retryRemaining = 0.25f;
                    if (TryPlace())
                    {
                        repositionPending = false;
                        if (state.Phase == GuiltPhase.Warning)
                        {
                            state.ConfirmPlacement();
                            cycleHasWarning = true;
                        }
                        PhaseChanged?.Invoke(state.Phase);
                    }
                }
            }
            if (state.Phase == GuiltPhase.Chase && placed) Chase(dt);
        }

        private void OnPhaseChanged(GuiltPhase phase)
        {
            switch (phase)
            {
                case GuiltPhase.Warning:
                    cycleHasWarning = TryPlace();
                    if (!cycleHasWarning)
                    {
                        state.WaitForPlacement();
                        return;
                    }
                    break;
                case GuiltPhase.Chase:
                    if (!cycleHasWarning) return;
                    break;
                case GuiltPhase.Reposition:
                    Hide();
                    placed = false;
                    break;
                case GuiltPhase.Distant:
                    Hide();
                    placed = cycleHasWarning = repositionPending = false;
                    verticalSpeed = stuckTimer = 0f;
                    if (state.AccumulatedCombatSeconds == 0f) contact = new CooldownTimer(contactCooldown);
                    break;
            }
            PhaseChanged?.Invoke(phase);
        }

        private void SuspendPresentation()
        {
            Hide();
            placed = false;
            retryRemaining = 0f;
            verticalSpeed = stuckTimer = 0f;
        }

        private void Chase(float dt)
        {
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
            if (!state.RequestReposition()) return;
            repositionPending = true;
            retryRemaining = 0f;
        }

        private bool TryPlace()
        {
            if (viewCamera == null) return false;
            GeometryUtility.CalculateFrustumPlanes(viewCamera, frustumPlanes);
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f);
            Vector3 centerOffset = Vector3.up * (controller.center.y * Mathf.Abs(scale.y));
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int index = (nextSpawn + i) % spawnPoints.Length;
                Transform point = spawnPoints[index];
                if (point == null) continue;
                Vector3 fromPlayer = Vector3.ProjectOnPlane(point.position - target.transform.position, Vector3.up);
                if (fromPlayer.sqrMagnitude < minimumSpawnDistance * minimumSpawnDistance) continue;
                if (Vector3.Dot(fromPlayer, target.transform.forward) >= 0f) continue;
                if (!Physics.Raycast(point.position + Vector3.up * 2f, Vector3.down, out RaycastHit ground,
                    4f, groundMask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(ground.normal, Vector3.up) > controller.slopeLimit) continue;
                Vector3 position = ground.point + Vector3.up * controller.skinWidth;
                Vector3 center = position + centerOffset;
                var bounds = new Bounds(center, new Vector3(radius * 2f, height, radius * 2f));
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) continue;
                Vector3 halfSegment = Vector3.up * (height * 0.5f - radius);
                int count = Physics.OverlapCapsuleNonAlloc(center - halfSegment, center + halfSegment,
                    radius, overlaps, obstructionMask, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) continue;
                bool blocked = false;
                for (int j = 0; j < count; j++)
                {
                    if (overlaps[j].transform.IsChildOf(transform)) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                nextSpawn = (index + 1) % spawnPoints.Length;
                controller.enabled = false;
                transform.SetPositionAndRotation(position, Quaternion.LookRotation(-fromPlayer.normalized));
                controller.enabled = true;
                verticalSpeed = stuckTimer = 0f;
                stuckAnchor = position;
                placed = true;
                visualRoot.SetActive(true);
                return true;
            }
            return false;
        }

        private void Hide()
        {
            if (visualRoot != null) visualRoot.SetActive(false);
            if (controller != null) controller.enabled = false;
        }
    }
}
