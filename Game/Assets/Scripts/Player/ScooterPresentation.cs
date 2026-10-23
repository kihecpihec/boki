using UnityEngine;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    public sealed class ScooterPresentation : MonoBehaviour
    {
        [SerializeField] private ScooterController movement;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform frontWheel;
        [SerializeField] private Transform rearWheel;
        [SerializeField] private Camera speedCamera;
        [SerializeField, Min(0.01f)] private float wheelRadius = 0.1f;
        [SerializeField, Range(0f, 30f)] private float maximumLean = 12f;
        [SerializeField, Range(0f, 45f)] private float maximumPitch = 25f;
        [SerializeField, Min(0.01f)] private float smoothing = 10f;
        [SerializeField, Range(30f, 110f)] private float restingFieldOfView = 70f;
        [SerializeField, Range(30f, 110f)] private float fastFieldOfView = 80f;

        private Quaternion visualRest, frontRest, rearRest;
        private Vector3 previousPosition;
        private float previousYaw, wheelAngle, originalFieldOfView;
        private bool initialized;

        private void OnEnable()
        {
            if (movement == null || visualRoot == null || frontWheel == null || rearWheel == null || speedCamera == null)
            {
                Debug.LogError("Assign movement, visual root, both wheel pivots and the camera on ScooterPresentation.", this);
                enabled = false;
                return;
            }
            visualRest = visualRoot.localRotation;
            frontRest = frontWheel.localRotation;
            rearRest = rearWheel.localRotation;
            originalFieldOfView = speedCamera.fieldOfView;
            previousPosition = movement.transform.position;
            previousYaw = movement.transform.eulerAngles.y;
            wheelAngle = 0f;
            initialized = true;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            if (visualRoot != null) visualRoot.localRotation = visualRest;
            if (frontWheel != null) frontWheel.localRotation = frontRest;
            if (rearWheel != null) rearWheel.localRotation = rearRest;
            if (speedCamera != null) speedCamera.fieldOfView = originalFieldOfView;
            initialized = false;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 travel = movement.transform.position - previousPosition;
            float yaw = movement.transform.eulerAngles.y;
            float turnRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
            previousPosition = movement.transform.position;
            previousYaw = yaw;
            if (movement.IsGrounded && travel.sqrMagnitude < 25f)
            {
                float distance = Vector3.Dot(travel, movement.transform.forward);
                wheelAngle = Mathf.Repeat(wheelAngle + distance / wheelRadius * Mathf.Rad2Deg, 360f);
            }
            frontWheel.localRotation = frontRest * Quaternion.AngleAxis(wheelAngle, Vector3.right);
            rearWheel.localRotation = rearRest * Quaternion.AngleAxis(wheelAngle, Vector3.right);

            float speedFraction = Mathf.Clamp01(movement.HorizontalSpeed / Mathf.Max(0.01f, movement.BoostSpeed));
            float lean = -Mathf.Clamp(turnRate / 240f, -1f, 1f) * maximumLean * speedFraction;
            Vector3 normal = movement.transform.InverseTransformDirection(movement.GroundNormal);
            float pitch = Mathf.Clamp(Mathf.Atan2(normal.z, normal.y) * Mathf.Rad2Deg, -maximumPitch, maximumPitch);
            float blend = 1f - Mathf.Exp(-smoothing * dt);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation,
                visualRest * Quaternion.Euler(pitch, 0f, lean), blend);
            speedCamera.fieldOfView = Mathf.Lerp(speedCamera.fieldOfView,
                Mathf.Lerp(restingFieldOfView, fastFieldOfView, speedFraction), 1f - Mathf.Exp(-5f * dt));
        }
    }
}
