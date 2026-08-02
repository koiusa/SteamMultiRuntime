using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public class GroundMotionTracker : MonoBehaviour
    {
        private sealed class GroundContactInfo
        {
            public GroundContactInfo(Transform groundTransform, Rigidbody rigidbody, IGroundMotionSource motionSource, float upDot)
            {
                GroundTransform = groundTransform;
                Rigidbody = rigidbody;
                MotionSource = motionSource;
                UpDot = upDot;
                LastPosition = groundTransform.position;
                LastRotation = groundTransform.rotation;
                LastSampleTime = -1f;
            }

            public Transform GroundTransform { get; }
            public Rigidbody Rigidbody { get; }
            public IGroundMotionSource MotionSource { get; }
            public float UpDot { get; set; }
            public Vector3 LastPosition { get; set; }
            public Quaternion LastRotation { get; set; }
            public Vector3 LinearVelocity { get; set; }
            public Vector3 AngularVelocity { get; set; }
            public float LastSampleTime { get; set; }
        }

        private readonly Dictionary<int, GroundContactInfo> groundContactsByColliderId = new Dictionary<int, GroundContactInfo>();

        public void UpdateGroundContact(Collision collision, Vector3 upAxis, LayerMask groundLayer, float minGroundNormalDot)
        {
            var bestUpDot = float.MinValue;
            for (var i = 0; i < collision.contactCount; i++)
            {
                var upDot = Vector3.Dot(collision.GetContact(i).normal, upAxis);
                if (upDot > bestUpDot)
                    bestUpDot = upDot;
            }

            UpdateGroundContact(collision, bestUpDot, groundLayer, minGroundNormalDot);
        }

        public void UpdateGroundContact(Collision collision, float bestUpDot, LayerMask groundLayer, float minGroundNormalDot)
        {
            var colliderId = collision.collider.GetInstanceID();
            if (!IsInGroundLayer(collision.gameObject.layer, groundLayer))
            {
                groundContactsByColliderId.Remove(colliderId);
                return;
            }

            if (bestUpDot < minGroundNormalDot)
            {
                groundContactsByColliderId.Remove(colliderId);
                return;
            }

            var groundRigidbody = collision.rigidbody != null ? collision.rigidbody : collision.collider.attachedRigidbody;
            var groundTransform = groundRigidbody != null ? groundRigidbody.transform : collision.collider.transform;
            if (groundTransform == null)
            {
                groundContactsByColliderId.Remove(colliderId);
                return;
            }

            if (groundContactsByColliderId.TryGetValue(colliderId, out var existingGroundContact) && existingGroundContact.GroundTransform == groundTransform)
            {
                existingGroundContact.UpDot = bestUpDot;
                return;
            }

            var motionSource = GetMotionSource(groundTransform);
            groundContactsByColliderId[colliderId] = new GroundContactInfo(groundTransform, groundRigidbody, motionSource, bestUpDot);
        }

        public void RemoveGroundContact(Collider collider)
        {
            groundContactsByColliderId.Remove(collider.GetInstanceID());
        }

        public void ClearGroundContacts()
        {
            groundContactsByColliderId.Clear();
        }

        public bool TryGetGroundMotion(Vector3 samplePoint, out Vector3 groundVelocity, out Vector3 groundDisplacement, out Quaternion groundRotationDelta)
        {
            var bestGroundContact = GetBestGroundContact();
            if (bestGroundContact == null)
            {
                groundVelocity = Vector3.zero;
                groundDisplacement = Vector3.zero;
                groundRotationDelta = Quaternion.identity;
                return false;
            }

            var sampleTime = Time.fixedTime;
            if (bestGroundContact.MotionSource is IGroundMotionSnapshotSource snapshotSource)
            {
                snapshotSource.GetGroundMotion(
                    samplePoint,
                    Time.fixedDeltaTime,
                    out groundVelocity,
                    out groundDisplacement,
                    out groundRotationDelta);
                return true;
            }

            groundVelocity = GetPointVelocity(bestGroundContact, samplePoint, sampleTime);
            groundDisplacement = GetPointDisplacement(bestGroundContact, samplePoint, Time.fixedDeltaTime, sampleTime);
            groundRotationDelta = GetRotationDelta(bestGroundContact, Time.fixedDeltaTime, sampleTime);
            return true;
        }

        public Vector3 GetGroundVelocity(Vector3 samplePoint)
        {
            return TryGetGroundMotion(samplePoint, out var groundVelocity, out _, out _)
                ? groundVelocity
                : Vector3.zero;
        }

        private GroundContactInfo GetBestGroundContact()
        {
            GroundContactInfo bestGroundContact = null;
            var bestUpDot = float.MinValue;

            foreach (var groundContact in groundContactsByColliderId.Values)
            {
                if (groundContact.GroundTransform == null)
                {
                    continue;
                }

                if (groundContact.UpDot < bestUpDot)
                {
                    continue;
                }

                bestUpDot = groundContact.UpDot;
                bestGroundContact = groundContact;
            }

            return bestGroundContact;
        }

        private static Vector3 GetPointVelocity(GroundContactInfo groundContact, Vector3 samplePoint, float sampleTime)
        {
            if (groundContact.MotionSource != null)
            {
                return groundContact.MotionSource.GetPointVelocity(samplePoint);
            }

            if (groundContact.Rigidbody != null)
            {
                return groundContact.Rigidbody.GetPointVelocity(samplePoint);
            }

            UpdateMotion(groundContact, sampleTime);
            return groundContact.LinearVelocity + Vector3.Cross(groundContact.AngularVelocity, samplePoint - groundContact.GroundTransform.position);
        }

        private static Vector3 GetPointDisplacement(GroundContactInfo groundContact, Vector3 samplePoint, float deltaTime, float sampleTime)
        {
            if (groundContact.MotionSource != null)
            {
                return groundContact.MotionSource.GetPointDisplacement(samplePoint, deltaTime);
            }

            UpdateMotion(groundContact, sampleTime);
            var previousPosition = groundContact.LastPosition - groundContact.LinearVelocity * deltaTime;
            var previousRotation = Quaternion.Euler(-groundContact.AngularVelocity * Mathf.Rad2Deg * deltaTime) * groundContact.LastRotation;
            var localPoint = Quaternion.Inverse(previousRotation) * (samplePoint - previousPosition);
            var movedPoint = groundContact.LastPosition + groundContact.LastRotation * localPoint;
            return movedPoint - samplePoint;
        }

        private static Quaternion GetRotationDelta(GroundContactInfo groundContact, float deltaTime, float sampleTime)
        {
            if (groundContact.MotionSource != null)
            {
                return groundContact.MotionSource.GetRotationDelta(deltaTime);
            }

            UpdateMotion(groundContact, sampleTime);
            return Quaternion.Euler(groundContact.AngularVelocity * Mathf.Rad2Deg * deltaTime);
        }

        private static void UpdateMotion(GroundContactInfo groundContact, float sampleTime)
        {
            if (groundContact.LastSampleTime >= 0f && Mathf.Approximately(groundContact.LastSampleTime, sampleTime))
            {
                return;
            }

            var previousPosition = groundContact.GroundTransform.position;
            var previousRotation = groundContact.GroundTransform.rotation;

            if (groundContact.LastSampleTime < 0f)
            {
                groundContact.LastPosition = previousPosition;
                groundContact.LastRotation = previousRotation;
                groundContact.LastSampleTime = sampleTime;
                groundContact.LinearVelocity = Vector3.zero;
                groundContact.AngularVelocity = Vector3.zero;
                return;
            }

            var deltaTime = sampleTime - groundContact.LastSampleTime;
            if (deltaTime > 0f)
            {
                groundContact.LinearVelocity = (previousPosition - groundContact.LastPosition) / deltaTime;
                groundContact.AngularVelocity = GetAngularVelocity(groundContact.LastRotation, previousRotation, deltaTime);
            }
            else
            {
                groundContact.LinearVelocity = Vector3.zero;
                groundContact.AngularVelocity = Vector3.zero;
            }

            groundContact.LastPosition = previousPosition;
            groundContact.LastRotation = previousRotation;
            groundContact.LastSampleTime = sampleTime;
        }

        private static Vector3 GetAngularVelocity(Quaternion previousRotation, Quaternion currentRotation, float deltaTime)
        {
            var deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
            deltaRotation.ToAngleAxis(out var angleInDegrees, out var axis);

            if (angleInDegrees > 180f)
            {
                angleInDegrees -= 360f;
            }

            if (Mathf.Abs(angleInDegrees) <= Mathf.Epsilon || axis.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return axis.normalized * (angleInDegrees * Mathf.Deg2Rad / deltaTime);
        }

        private static IGroundMotionSource GetMotionSource(Transform groundTransform)
        {
            GroundMotionSourceResolver.Resolve(groundTransform, out var motionSource, out _);
            return motionSource;
        }

        private static bool IsInGroundLayer(int layer, LayerMask groundLayer)
        {
            return (groundLayer.value & (1 << layer)) != 0;
        }
    }
}
