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

        private CharacterController controller;
        private float verticalSpeed;
        private float boostRemaining;
        private float cooldownRemaining;
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
            verticalSpeed = 0f;
            boostRemaining = cooldownRemaining = 0f;
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
                boostRemaining = 0f;
            }
            if (awaitingRelease && canRead && input.sqrMagnitude < 0.001f
                && !jumpAction.action.IsPressed() && !boostAction.action.IsPressed())
                awaitingRelease = false;
            canRead &= !awaitingRelease;
            if (!canRead) input = Vector2.zero;

            Vector3 forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), turnSpeed * dt);

            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (canRead && controller.isGrounded && jumpAction.action.WasPressedThisFrame())
                verticalSpeed = Mathf.Sqrt(-2f * gravity * jumpHeight);
            if (canRead && controller.isGrounded && boostRemaining <= 0f && cooldownRemaining <= 0f
                && boostAction.action.WasPressedThisFrame())
                boostRemaining = boostDuration;

            verticalSpeed += gravity * dt;
            // Integrate partial boost frames so its extra distance is frame-rate independent.
            float boostedTime = Mathf.Min(boostRemaining, dt);
            float movementSpeed = speed * (1f + (dt > 0f ? boostedTime / dt : 0f) * (boostMultiplier - 1f));
            CollisionFlags flags = controller.Move((direction * movementSpeed + Vector3.up * verticalSpeed) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
            if (boostRemaining > 0f)
            {
                boostRemaining = Mathf.Max(0f, boostRemaining - dt);
                if (boostRemaining == 0f)
                    cooldownRemaining = Mathf.Max(0f, boostCooldown - (dt - boostedTime));
            }
            else cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
        }
    }
}
