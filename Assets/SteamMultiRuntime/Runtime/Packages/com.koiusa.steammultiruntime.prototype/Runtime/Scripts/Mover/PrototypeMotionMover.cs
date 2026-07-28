using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public class PrototypeMotionMover : NetworkBehaviour, IGroundMotionSource, IGroundMotionSnapshotSource
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
        private Matrix4x4 previousPhysicsInverseMatrix;
        private Matrix4x4 currentPhysicsMatrix;
        private Quaternion physicsRotationDelta = Quaternion.identity;
        private float localMotionStartFixedTime;
        private float lastPhysicsSampleTime = float.NegativeInfinity;

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
            previousPhysicsInverseMatrix = previousPhysicsMatrix.inverse;
            currentPhysicsMatrix = previousPhysicsMatrix;
            localMotionStartFixedTime = Time.fixedTime;
        }

        public override void OnNetworkSpawn()
        {
            localMotionStartFixedTime = Time.fixedTime;
            lastPhysicsSampleTime = float.NegativeInfinity;
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

            EnsurePhysicsSample();
            ApplyMotionMatrix(currentPhysicsMatrix);
        }

        private void Update()
        {
            if (!ShouldApplyMotionLocally() || presentationTransform == null || Time.fixedDeltaTime <= 0f)
            {
                return;
            }

            var alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            presentationTransform.SetPositionAndRotation(
                Vector3.Lerp(previousPhysicsMatrix.GetColumn(3), currentPhysicsMatrix.GetColumn(3), alpha),
                Quaternion.Slerp(previousPhysicsMatrix.rotation, currentPhysicsMatrix.rotation, alpha));
        }

        public Vector3 GetPointVelocity(Vector3 samplePoint)
        {
            var deltaTime = Time.fixedDeltaTime;
            GetGroundMotion(samplePoint, deltaTime, out var velocity, out _, out _);
            return velocity;
        }

        public Vector3 GetPointDisplacement(Vector3 samplePoint, float deltaTime)
        {
            GetGroundMotion(samplePoint, deltaTime, out _, out var displacement, out _);
            return displacement;
        }

        public Quaternion GetRotationDelta(float deltaTime)
        {
            GetGroundMotion(cachedTransform.position, deltaTime, out _, out _, out var rotationDelta);
            return rotationDelta;
        }

        public void GetGroundMotion(
            Vector3 samplePoint,
            float deltaTime,
            out Vector3 pointVelocity,
            out Vector3 pointDisplacement,
            out Quaternion rotationDelta)
        {
            if (deltaTime <= 0f)
            {
                pointVelocity = Vector3.zero;
                pointDisplacement = Vector3.zero;
                rotationDelta = Quaternion.identity;
                return;
            }

            if (ShouldUseObservedTransformMotion())
            {
                UpdateObservedMotion();
                var observedLocalPoint = Quaternion.Inverse(previousObservedRotation) * (samplePoint - previousObservedPosition);
                var observedMovedPoint = currentObservedPosition + currentObservedRotation * observedLocalPoint;
                pointDisplacement = observedMovedPoint - samplePoint;
                pointVelocity = pointDisplacement / deltaTime;
                rotationDelta = currentObservedRotation * Quaternion.Inverse(previousObservedRotation);
                return;
            }

            EnsurePhysicsSample();
            var localPoint = previousPhysicsInverseMatrix.MultiplyPoint3x4(samplePoint);
            var movedPoint = currentPhysicsMatrix.MultiplyPoint3x4(localPoint);
            pointDisplacement = movedPoint - samplePoint;
            pointVelocity = pointDisplacement / deltaTime;
            rotationDelta = physicsRotationDelta;
        }

        private void EnsurePhysicsSample()
        {
            var sampleTime = Time.fixedTime;
            if (lastPhysicsSampleTime == sampleTime) return;

            var motionTime = GetMotionTime();
            previousPhysicsMatrix = GetMotionMatrix(motionTime - Time.fixedDeltaTime);
            previousPhysicsInverseMatrix = previousPhysicsMatrix.inverse;
            currentPhysicsMatrix = GetMotionMatrix(motionTime);
            physicsRotationDelta = currentPhysicsMatrix.rotation * Quaternion.Inverse(previousPhysicsMatrix.rotation);
            lastPhysicsSampleTime = sampleTime;
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
            if (!IsSpawned || NetworkManager == null)
            {
                return Time.fixedTime;
            }

            // The authority moves physics at Unity's fixed rate. NetworkTime.FixedTime
            // advances at the network tick rate (often 30 Hz), which is lower than the
            // 50 Hz physics rate and therefore repeats platform poses.
            return Time.fixedTime - localMotionStartFixedTime;
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
