using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public class TestMotionMover : NetworkBehaviour, IGroundMotionSource
    {
        private static readonly Vector3 UnitScale = Vector3.one;

        [System.Flags]
        private enum MotionType
        {
            None = 0,
            CircleOrbit = 1 << 0,
            VerticalMove = 1 << 1,
            Spin = 1 << 2,
            HorizontalMove = 1 << 3,
            Tilt = 1 << 4,
        }

        [Header("Motion")]
        [EnumFlags]
        [SerializeField] private MotionType motionType = MotionType.CircleOrbit;
        [SerializeField] private bool useLocalSpace = false;
        [SerializeField] private bool serverAuthoritativeWhenNetworkSpawned = true;
        [Tooltip("Optional renderer root smoothed between physics ticks on the authority.")]
        [SerializeField] private Transform presentationTransform;

        [Header("Move")]
        [SerializeField] private float amplitude = 1f;
        [SerializeField] private float moveSpeed = 1f;

        [Header("Spin")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float spinSpeed = 1f;
        [SerializeField] private Space rotationSpace = Space.Self;

        [Header("Tilt")]
        [SerializeField] private Vector3 tiltAxis = Vector3.forward;
        [SerializeField] private float tiltAngle = 15f;
        [SerializeField] private float tiltSpeed = 1f;

        private readonly NetworkVariable<double> motionStartServerTime = new NetworkVariable<double>(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform cachedTransform;
        private Vector3 initialPosition;
        private Vector3 initialLocalPosition;
        private Quaternion initialRotation;
        private Vector3 previousObservedPosition;
        private Quaternion previousObservedRotation;
        private Vector3 currentObservedPosition;
        private Quaternion currentObservedRotation;
        private float lastObservedSampleTime = -1f;
        private Matrix4x4 previousPhysicsMatrix;
        private Matrix4x4 currentPhysicsMatrix;

        private void Awake()
        {
            cachedTransform = transform;
            initialPosition = cachedTransform.position;
            initialLocalPosition = cachedTransform.localPosition;
            initialRotation = cachedTransform.rotation;
            previousObservedPosition = cachedTransform.position;
            previousObservedRotation = cachedTransform.rotation;
            currentObservedPosition = cachedTransform.position;
            currentObservedRotation = cachedTransform.rotation;
            previousPhysicsMatrix = Matrix4x4.TRS(cachedTransform.position, cachedTransform.rotation, UnitScale);
            currentPhysicsMatrix = previousPhysicsMatrix;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && motionStartServerTime.Value <= 0d && NetworkManager != null)
            {
                motionStartServerTime.Value = NetworkManager.ServerTime.Time;
            }
        }

        private void FixedUpdate()
        {
            if (!ShouldApplyMotionLocally())
            {
                return;
            }

            previousPhysicsMatrix = currentPhysicsMatrix;
            currentPhysicsMatrix = GetMotionMatrix(GetMotionTime());
            ApplyMotionMatrix(currentPhysicsMatrix);
        }

        private void LateUpdate()
        {
            if (!ShouldApplyMotionLocally() || presentationTransform == null || Time.fixedDeltaTime <= 0f)
            {
                return;
            }

            // Match Rigidbody interpolation: render between the previous and latest
            // physics poses while the collider remains entirely in FixedUpdate.
            var alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            presentationTransform.SetPositionAndRotation(
                Vector3.Lerp(previousPhysicsMatrix.GetColumn(3), currentPhysicsMatrix.GetColumn(3), alpha),
                Quaternion.Slerp(previousPhysicsMatrix.rotation, currentPhysicsMatrix.rotation, alpha));
        }

        public Vector3 GetPointVelocity(Vector3 samplePoint)
        {
            var deltaTime = Time.fixedDeltaTime;
            return deltaTime > 0f ? GetPointDisplacement(samplePoint, deltaTime) / deltaTime : Vector3.zero;
        }

        public Vector3 GetPointDisplacement(Vector3 samplePoint, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return Vector3.zero;
            }

            if (ShouldUseObservedTransformMotion())
            {
                UpdateObservedMotion();
                var observedLocalPoint = Quaternion.Inverse(previousObservedRotation) * (samplePoint - previousObservedPosition);
                var observedMovedPoint = currentObservedPosition + currentObservedRotation * observedLocalPoint;
                return observedMovedPoint - samplePoint;
            }

            var currentTime = GetMotionTime();
            var previousMatrix = GetMotionMatrix(currentTime - deltaTime);
            var currentMatrix = GetMotionMatrix(currentTime);
            var localPoint = previousMatrix.inverse.MultiplyPoint3x4(samplePoint);
            var movedPoint = currentMatrix.MultiplyPoint3x4(localPoint);
            return movedPoint - samplePoint;
        }

        public Quaternion GetRotationDelta(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return Quaternion.identity;
            }

            if (ShouldUseObservedTransformMotion())
            {
                UpdateObservedMotion();
                return currentObservedRotation * Quaternion.Inverse(previousObservedRotation);
            }

            var currentTime = GetMotionTime();
            var previousRotation = GetWorldRotation(currentTime - deltaTime);
            var currentRotation = GetWorldRotation(currentTime);
            return currentRotation * Quaternion.Inverse(previousRotation);
        }

        private bool ShouldApplyMotionLocally()
        {
            if (!serverAuthoritativeWhenNetworkSpawned)
            {
                return true;
            }

            return !IsSpawned || IsServer;
        }

        private float GetMotionTime()
        {
            if (!IsSpawned || NetworkManager == null || motionStartServerTime.Value <= 0d)
            {
                return Time.fixedTime;
            }

            return (float)(NetworkManager.ServerTime.Time - motionStartServerTime.Value);
        }

        private void ApplyMotionMatrix(Matrix4x4 motionMatrix)
        {
            cachedTransform.SetPositionAndRotation(
                motionMatrix.GetColumn(3),
                motionMatrix.rotation);
        }

        private Matrix4x4 GetMotionMatrix(float time)
        {
            return Matrix4x4.TRS(GetWorldPosition(time), GetWorldRotation(time), UnitScale);
        }

        private Vector3 GetWorldPosition(float time)
        {
            var localOrWorldPosition = GetBasePosition() + GetTranslationOffset(time);
            if (!useLocalSpace || cachedTransform.parent == null)
            {
                return localOrWorldPosition;
            }

            return cachedTransform.parent.TransformPoint(localOrWorldPosition);
        }

        private Vector3 GetBasePosition()
        {
            return useLocalSpace ? initialLocalPosition : initialPosition;
        }

        private Vector3 GetTranslationOffset(float time)
        {
            return GetCircleOrbitOffset(time) +
                   GetLinearOffset(MotionType.VerticalMove, Vector3.up, time) +
                   GetLinearOffset(MotionType.HorizontalMove, Vector3.right, time);
        }

        private Quaternion GetWorldRotation(float time)
        {
            var spinRotation = GetAxisRotation(MotionType.Spin, rotationAxis, time * spinSpeed * 360f);
            var tiltRotation = GetAxisRotation(MotionType.Tilt, tiltAxis, Mathf.Sin(time * tiltSpeed) * tiltAngle);

            return rotationSpace == Space.Self
                ? initialRotation * spinRotation * tiltRotation
                : tiltRotation * spinRotation * initialRotation;
        }

        private Vector3 GetCircleOrbitOffset(float time)
        {
            if (!HasMotion(MotionType.CircleOrbit))
            {
                return Vector3.zero;
            }

            var angle = time * moveSpeed;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * amplitude;
        }

        private Vector3 GetLinearOffset(MotionType target, Vector3 axis, float time)
        {
            return HasMotion(target)
                ? axis * Mathf.Sin(time * moveSpeed) * amplitude
                : Vector3.zero;
        }

        private Quaternion GetAxisRotation(MotionType target, Vector3 axis, float angle)
        {
            if (!HasMotion(target) || axis.sqrMagnitude <= 0f || Mathf.Approximately(angle, 0f))
            {
                return Quaternion.identity;
            }

            return Quaternion.AngleAxis(angle, axis.normalized);
        }

        private bool HasMotion(MotionType target)
        {
            return (motionType & target) != 0;
        }

        private bool ShouldUseObservedTransformMotion()
        {
            return IsSpawned && serverAuthoritativeWhenNetworkSpawned && !IsServer;
        }

        private void UpdateObservedMotion()
        {
            var sampleTime = Time.fixedTime;
            if (lastObservedSampleTime >= 0f && Mathf.Approximately(lastObservedSampleTime, sampleTime))
            {
                return;
            }

            var observedPosition = cachedTransform.position;
            var observedRotation = cachedTransform.rotation;

            if (lastObservedSampleTime < 0f)
            {
                previousObservedPosition = observedPosition;
                previousObservedRotation = observedRotation;
                currentObservedPosition = observedPosition;
                currentObservedRotation = observedRotation;
                lastObservedSampleTime = sampleTime;
                return;
            }

            previousObservedPosition = currentObservedPosition;
            previousObservedRotation = currentObservedRotation;
            currentObservedPosition = observedPosition;
            currentObservedRotation = observedRotation;
            lastObservedSampleTime = sampleTime;
        }
    }
}
