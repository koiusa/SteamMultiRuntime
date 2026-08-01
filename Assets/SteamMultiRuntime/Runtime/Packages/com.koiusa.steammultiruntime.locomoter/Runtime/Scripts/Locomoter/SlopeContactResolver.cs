using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public class SlopeContactResolver : MonoBehaviour
    {
        [SerializeField, Range(0f, 89f)] private float maxClimbAngle = 45f;
        [SerializeField] private bool allowJumpOnSteepSlope;

        private readonly HashSet<int> groundedColliderIds = new HashSet<int>();
        private readonly Dictionary<int, Vector3> groundNormalsByColliderId = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> steepSlopeNormalsByColliderId = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> obstacleNormalsByColliderId = new Dictionary<int, Vector3>();

        public bool IsGrounded => groundedColliderIds.Count > 0;
        public bool IsOnSteepSlope => steepSlopeNormalsByColliderId.Count > 0;
        public bool HasObstacleContact => obstacleNormalsByColliderId.Count > 0;
        public bool CanJumpOnSteepSlope => allowJumpOnSteepSlope;

        public void Clear()
        {
            groundedColliderIds.Clear();
            groundNormalsByColliderId.Clear();
            steepSlopeNormalsByColliderId.Clear();
            obstacleNormalsByColliderId.Clear();
        }

        public void RemoveCollision(Collider collider)
        {
            var colliderId = collider.GetInstanceID();
            groundedColliderIds.Remove(colliderId);
            groundNormalsByColliderId.Remove(colliderId);
            steepSlopeNormalsByColliderId.Remove(colliderId);
            obstacleNormalsByColliderId.Remove(colliderId);
        }

        public void SetSyntheticObstacleContact(int sourceId, Vector3 normal)
        {
            if (normal.sqrMagnitude > 0.0001f)
                obstacleNormalsByColliderId[sourceId] = normal.normalized;
            else
                obstacleNormalsByColliderId.Remove(sourceId);
        }

        public void RemoveSyntheticContact(int sourceId)
        {
            groundedColliderIds.Remove(sourceId);
            groundNormalsByColliderId.Remove(sourceId);
            steepSlopeNormalsByColliderId.Remove(sourceId);
            obstacleNormalsByColliderId.Remove(sourceId);
        }

        public float UpdateCollisionContacts(Collision collision, Vector3 upAxis, LayerMask groundLayer, float minGroundNormalDot)
        {
            var colliderId = collision.collider.GetInstanceID();
            var isGrounded = false;
            var groundNormal = Vector3.zero;
            var steepSlopeNormal = Vector3.zero;
            var obstacleNormal = Vector3.zero;
            var climbableNormalDot = GetClimbableNormalDot();
            var bestUpDot = float.MinValue;

            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                var upDot = Vector3.Dot(contact.normal, upAxis);
                if (upDot > bestUpDot)
                    bestUpDot = upDot;

                if (IsInGroundLayer(collision.gameObject.layer, groundLayer) && upDot >= minGroundNormalDot)
                {
                    if (upDot >= climbableNormalDot)
                    {
                        isGrounded = true;
                        groundNormal += contact.normal;
                    }
                    else
                    {
                        steepSlopeNormal += contact.normal;
                    }

                    continue;
                }

                if (upDot < minGroundNormalDot)
                {
                    obstacleNormal += contact.normal;
                }
            }

            if (isGrounded)
            {
                groundedColliderIds.Add(colliderId);
                groundNormalsByColliderId[colliderId] = groundNormal.normalized;
            }
            else
            {
                groundedColliderIds.Remove(colliderId);
                groundNormalsByColliderId.Remove(colliderId);
            }

            if (steepSlopeNormal.sqrMagnitude > 0f)
            {
                steepSlopeNormalsByColliderId[colliderId] = steepSlopeNormal.normalized;
            }
            else
            {
                steepSlopeNormalsByColliderId.Remove(colliderId);
            }

            if (obstacleNormal.sqrMagnitude > 0f)
            {
                obstacleNormalsByColliderId[colliderId] = obstacleNormal.normalized;
            }
            else
            {
                obstacleNormalsByColliderId.Remove(colliderId);
            }

            return bestUpDot;
        }

        public bool TryGetObstacleNormal(Vector3 upAxis, float wallMaxUpDot, out Vector3 obstacleNormal)
        {
            var combinedNormal = Vector3.zero;

            foreach (var candidate in obstacleNormalsByColliderId.Values)
            {
                var upDot = Mathf.Abs(Vector3.Dot(candidate, upAxis));
                if (upDot > wallMaxUpDot)
                {
                    continue;
                }

                combinedNormal += candidate;
            }

            if (combinedNormal.sqrMagnitude <= 0f)
            {
                obstacleNormal = Vector3.zero;
                return false;
            }

            obstacleNormal = combinedNormal.normalized;
            return true;
        }

        public Vector3 GetGroundNormal(Vector3 upAxis)
        {
            return GetCombinedNormal(groundNormalsByColliderId, upAxis);
        }

        public Vector3 GetSteepSlopeNormal(Vector3 upAxis)
        {
            return GetCombinedNormal(steepSlopeNormalsByColliderId, upAxis);
        }

        public Vector3 ConstrainHorizontalVelocity(Vector3 horizontalVelocity, Vector3 upAxis, float minGroundNormalDot)
        {
            foreach (var obstacleNormal in obstacleNormalsByColliderId.Values)
            {
                if (Vector3.Dot(obstacleNormal, upAxis) >= minGroundNormalDot)
                {
                    continue;
                }

                if (Vector3.Dot(horizontalVelocity, obstacleNormal) < 0f)
                {
                    horizontalVelocity = Vector3.ProjectOnPlane(horizontalVelocity, obstacleNormal);
                }
            }

            return horizontalVelocity;
        }

        private float GetClimbableNormalDot()
        {
            return Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
        }

        private static Vector3 GetCombinedNormal(Dictionary<int, Vector3> normalsByColliderId, Vector3 fallback)
        {
            var combinedNormal = Vector3.zero;

            foreach (var normal in normalsByColliderId.Values)
            {
                combinedNormal += normal;
            }

            return combinedNormal.sqrMagnitude > 0f ? combinedNormal.normalized : fallback;
        }

        private static bool IsInGroundLayer(int layer, LayerMask groundLayer)
        {
            return (groundLayer.value & (1 << layer)) != 0;
        }
    }
}
