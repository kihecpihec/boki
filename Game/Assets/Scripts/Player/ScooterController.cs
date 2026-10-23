using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ScooterController : MonoBehaviour
    {
        [SerializeField] private Transform movementCamera;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference boostAction;
        [SerializeField, Min(0f)] private float speed = 6f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;
        [SerializeField, Min(1f)] private float boostMultiplier = 1.75f;
        [SerializeField, Min(0f)] private float boostDuration = 0.35f;
        [SerializeField, Min(0f)] private float boostCooldown = 1f;

        // Movement.md §10: first-pass momentum tuning; serialized M1 speed/boost values stay intact.
        [SerializeField, Min(0f)] private float groundAcceleration = 60f;
        [SerializeField, Min(0f)] private float airAcceleration = 20f;
        [SerializeField, Min(0f)] private float brakeDeceleration = 25f;
        [SerializeField, Min(0f)] private float groundOverspeedDecay = 6f;
        [SerializeField, Min(0f)] private float airOverspeedDecay = 2f;
        [SerializeField, Min(0f)] private float overspeedTurnRate = 240f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
        [SerializeField, Min(0f)] private float jumpBuffer = 0.1f;
        [SerializeField, Min(0f)] private float landingGrace = 0.1f;
        [SerializeField, Min(0f)] private float snapDistance = 0.3f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0f)] private float slopeGain = 0.5f;
        [SerializeField, Min(0f)] private float airStrafeAcceleration = 6f;
        [SerializeField, Min(1f)] private float airStrafeMaxMultiplier = 2f;

        private CharacterController controller;
        private ScooterMotion motion;
        private bool wasGrounded;
        private float walkableNormalY;
        public float HorizontalSpeed => motion?.Speed ?? 0f;
        public float BoostSpeed => speed * boostMultiplier;
        public float BoostRemaining => motion?.BoostRemaining ?? 0f;
        public float CooldownRemaining => motion?.CooldownRemaining ?? 0f;
        public float BoostDuration => boostDuration;
        public float BoostCooldown => boostCooldown;
        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        private bool ownsMove;
        private bool ownsJump;
        private bool ownsBoost;
        private bool awaitingRelease;

        private void OnEnable()
        {
            controller = GetComponent<CharacterController>();
            if (movementCamera == null && Camera.main != null)
                movementCamera = Camera.main.transform;
            if (movementCamera == null || !Valid(moveAction) || !Valid(jumpAction) || !Valid(boostAction))
            {
                Debug.LogError("Assign the movement camera and Move, Jump, and Boost actions on ScooterController.", this);
                enabled = false;
                return;
            }
            motion = new ScooterMotion(new ScooterMotionTuning(speed, speed * boostMultiplier,
                boostDuration, boostCooldown, jumpHeight, Mathf.Abs(gravity), groundAcceleration,
                airAcceleration, brakeDeceleration, groundOverspeedDecay, airOverspeedDecay,
                overspeedTurnRate, coyoteTime, jumpBuffer, landingGrace, airStrafeAcceleration, airStrafeMaxMultiplier));
            walkableNormalY = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
            ownsMove = EnableAction(moveAction);
            ownsJump = EnableAction(jumpAction);
            ownsBoost = EnableAction(boostAction);
            ResetInput();
        }

        private static bool Valid(InputActionReference reference) => reference != null && reference.action != null;

        private static bool EnableAction(InputActionReference reference)
        {
            bool owned = !reference.action.enabled;
            if (owned) reference.action.Enable();
            return owned;
        }

        private void OnDisable()
        {
            if (ownsMove && Valid(moveAction)) moveAction.action.Disable();
            if (ownsJump && Valid(jumpAction)) jumpAction.action.Disable();
            if (ownsBoost && Valid(boostAction)) boostAction.action.Disable();
            ownsMove = ownsJump = ownsBoost = false;
            ResetInput();
        }

        private void OnApplicationFocus(bool focused) => ResetInput();

        private void ResetInput()
        {
            motion?.Reset();
            wasGrounded = false;
            IsGrounded = false;
            GroundNormal = Vector3.up;
            awaitingRelease = true;
        }

        private void Update()
        {
            if (movementCamera == null || !controller.enabled) return;

            float dt = Time.deltaTime;
            Vector2 input = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
            bool canRead = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (!canRead)
            {
                awaitingRelease = true;
                motion.SuspendInput();
            }
            if (awaitingRelease && canRead && input.sqrMagnitude < 0.001f
                && !jumpAction.action.IsPressed() && !boostAction.action.IsPressed())
                awaitingRelease = false;
            canRead &= !awaitingRelease;
            if (!canRead) input = Vector2.zero;

            Vector3 forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;
            bool grounded = ProbeGround(out Vector3 groundNormal, out float groundDistance);
            grounded &= motion.VerticalSpeed <= 0f;
            bool snap = !grounded && wasGrounded && motion.VerticalSpeed <= 0f
                && groundDistance <= snapDistance;
            grounded |= snap;
            Vector3 slopeAcceleration = grounded
                ? Vector3.ProjectOnPlane(Vector3.down * Mathf.Abs(gravity), groundNormal) * slopeGain : Vector3.zero;
            motion.Tick(direction.x, direction.z, grounded,
                canRead && jumpAction.action.WasPressedThisFrame(),
                canRead && boostAction.action.WasPressedThisFrame(), dt, transform.forward.x, transform.forward.z,
                canRead ? slopeAcceleration.x : 0f, canRead ? slopeAcceleration.z : 0f);

            Vector3 horizontal = new Vector3(motion.DisplacementX, 0f, motion.DisplacementZ);
            if (grounded && motion.VerticalSpeed <= 0f)
                horizontal = Vector3.ProjectOnPlane(horizontal, groundNormal);
            Vector3 displacement = horizontal + Vector3.up * motion.DisplacementY;
            if (snap && motion.VerticalSpeed <= 0f) displacement.y -= groundDistance;
            CollisionFlags flags = controller.Move(displacement);
            motion.ResolveVertical((flags & CollisionFlags.Below) != 0, (flags & CollisionFlags.Above) != 0);
            wasGrounded = grounded || (flags & CollisionFlags.Below) != 0;
            IsGrounded = wasGrounded && motion.VerticalSpeed <= 0f;
            GroundNormal = IsGrounded ? groundNormal : Vector3.up;

            Vector3 heading = new Vector3(motion.HorizontalX, 0f, motion.HorizontalZ);
            if (heading.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(heading), turnSpeed * dt);
        }

        private bool ProbeGround(out Vector3 normal, out float distance)
        {
            normal = Vector3.up;
            distance = float.PositiveInfinity;
            float radius = controller.radius * 0.9f;
            Vector3 origin = transform.TransformPoint(controller.center);
            float centerToFeet = controller.height * 0.5f - radius;
            if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit,
                    centerToFeet + snapDistance, groundMask, QueryTriggerInteraction.Ignore))
                return controller.isGrounded;
            if (hit.normal.y < walkableNormalY) return false;
            normal = hit.normal;
            distance = Mathf.Max(0f, hit.distance - centerToFeet);
            return distance <= controller.skinWidth + 0.03f;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (motion != null && hit.normal.y < walkableNormalY && hit.normal.y > -0.9f)
                motion.ResolveWall(hit.normal.x, hit.normal.z);
        }
    }
}
