using System.Collections.Generic;
using Boki.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform followPivot;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.12f;
        [SerializeField, Min(0.5f)] private float followDistance = 5f;
        [SerializeField] private float minimumPitch = -25f;
        [SerializeField] private float maximumPitch = 65f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
        [SerializeField, Min(0.01f)] private float collisionPadding = 0.1f;
        [SerializeField, Min(0.01f)] private float distanceRecoverySpeed = 8f;
        [Tooltip("Include environment layers; exclude the player layer.")]
        [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;

        private float yaw;
        private float pitch;
        private float currentDistance;
        private bool enabledLookAction;
        private bool hasFocus;
        private bool skipLookFrame;
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

        public void SnapHeading(float heading)
        {
            yaw = Mathf.Repeat(heading, 360f);
            currentDistance = followDistance;
            skipLookFrame = true;
        }

        private void OnEnable()
        {
            if (followPivot == null || lookAction == null || lookAction.action == null)
            {
                Debug.LogError("Assign the camera follow pivot and look action before enabling ThirdPersonCamera.", this);
                enabled = false;
                return;
            }

            yaw = transform.eulerAngles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), minimumPitch, maximumPitch);
            currentDistance = followDistance;
            enabledLookAction = !lookAction.action.enabled;
            if (enabledLookAction)
                lookAction.action.Enable();

            hasFocus = Application.isFocused;
            SetCursorCaptured(hasFocus);
        }

        private void OnDisable()
        {
            if (enabledLookAction && lookAction != null && lookAction.action != null)
                lookAction.action.Disable();
            enabledLookAction = false;
            SetCursorCaptured(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            // Require a fresh click after returning to the application.
            SetCursorCaptured(false);
        }

        private void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
            skipLookFrame = true;
        }

        private void Update()
        {
            if (RunSession.Instance != null && RunSession.Instance.State.Pause.IsPaused)
            { skipLookFrame = true; return; }
            if (!hasFocus)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);
            else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null
                     && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                SetCursorCaptured(true);
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null || Mouse.current == null) return false;
            // Query this click's position directly: EventSystem may update after the camera.
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
            pointerHits.Clear();
            EventSystem.current.RaycastAll(pointer, pointerHits);
            foreach (var hit in pointerHits)
                if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (followPivot == null || (RunSession.Instance != null && RunSession.Instance.State.Pause.IsPaused))
                return;

            if (hasFocus && Cursor.lockState == CursorLockMode.Locked && !skipLookFrame)
            {
                Vector2 delta = lookAction.action.ReadValue<Vector2>();
                yaw = Mathf.Repeat(yaw + delta.x * sensitivity, 360f);
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minimumPitch, maximumPitch);
            }
            skipLookFrame = false;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 origin = followPivot.position;
            Vector3 backward = rotation * Vector3.back;
            float desiredDistance = followDistance;

            if (Physics.SphereCast(origin, collisionRadius, backward, out RaycastHit hit,
                    followDistance, obstructionMask, QueryTriggerInteraction.Ignore))
                desiredDistance = Mathf.Max(0f, hit.distance - collisionPadding);

            // Pull in immediately so smoothing cannot carry the camera through a wall.
            currentDistance = desiredDistance < currentDistance
                ? desiredDistance
                : Mathf.Lerp(currentDistance, desiredDistance,
                    1f - Mathf.Exp(-distanceRecoverySpeed * Time.deltaTime));
            transform.SetPositionAndRotation(origin + backward * currentDistance, rotation);
        }
    }
}
