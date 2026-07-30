using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PrimaryCenteredCinemachineTargetGroup : MonoBehaviour,
        ICinemachineTargetGroup,
        ITargetingCameraFramingGroup
    {
        private IReadOnlyList<Transform> members;
        private Transform primary;
        private float memberRadius;

        public bool IsValid => this != null && isActiveAndEnabled;
        public Transform Transform => transform;
        public bool IsEmpty => primary == null || members == null || members.Count == 0;
        public Bounds BoundingBox => CalculateWorldBounds();
        public BoundingSphere Sphere => CalculateWorldSphere();
        public Transform CameraTarget => transform;

        public void SetMembers(
            Transform newPrimary,
            IReadOnlyList<Transform> newMembers,
            float weight,
            float radius)
        {
            primary = newPrimary;
            members = newMembers;
            memberRadius = Mathf.Max(0f, radius);
            UpdateTransform();
        }

        public void Clear()
        {
            primary = null;
            members = null;
        }

        private void LateUpdate() => UpdateTransform();

        private void UpdateTransform()
        {
            if (primary != null) transform.position = primary.position;
        }

        public Bounds GetViewSpaceBoundingBox(Matrix4x4 observer, bool includeBehind)
        {
            var worldToView = Inverse(observer);
            var center = worldToView.MultiplyPoint3x4(primary != null ? primary.position : transform.position);
            var extents = Vector3.zero;
            if (members != null)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (!IsUsable(member)) continue;
                    var point = worldToView.MultiplyPoint3x4(member.position);
                    if (!includeBehind && point.z <= 0f) continue;
                    var delta = point - center;
                    extents = Vector3.Max(extents, Abs(delta) + Vector3.one * memberRadius);
                }
            }
            return new Bounds(center, extents * 2f);
        }

        public void GetViewSpaceAngularBounds(
            Matrix4x4 observer,
            out Vector2 minAngles,
            out Vector2 maxAngles,
            out Vector2 zRange)
        {
            var worldToView = Inverse(observer);
            var primaryView = worldToView.MultiplyPoint3x4(primary != null ? primary.position : transform.position);
            var primaryNormalized = NormalizeViewPoint(primaryView);
            var extent = Vector2.zero;
            var hasMember = false;
            zRange = new Vector2(primaryView.z, primaryView.z);

            if (members != null)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (!IsUsable(member)) continue;
                    var point = worldToView.MultiplyPoint3x4(member.position);
                    if (point.z <= 0.0001f) continue;

                    var normalized = NormalizeViewPoint(point);
                    var radius = memberRadius / point.z;
                    extent = Vector2.Max(
                        extent,
                        Abs(normalized - primaryNormalized) + Vector2.one * radius);
                    zRange.x = hasMember ? Mathf.Min(zRange.x, point.z - memberRadius) : point.z - memberRadius;
                    zRange.y = hasMember ? Mathf.Max(zRange.y, point.z + memberRadius) : point.z + memberRadius;
                    hasMember = true;
                }
            }

            var minimum = primaryNormalized - extent;
            var maximum = primaryNormalized + extent;
            minAngles = new Vector2(
                Vector3.SignedAngle(Vector3.forward, new Vector3(0f, maximum.y, 1f), Vector3.right),
                Vector3.SignedAngle(Vector3.forward, new Vector3(minimum.x, 0f, 1f), Vector3.up));
            maxAngles = new Vector2(
                Vector3.SignedAngle(Vector3.forward, new Vector3(0f, minimum.y, 1f), Vector3.right),
                Vector3.SignedAngle(Vector3.forward, new Vector3(maximum.x, 0f, 1f), Vector3.up));
        }

        private Bounds CalculateWorldBounds()
        {
            var center = primary != null ? primary.position : transform.position;
            var extents = Vector3.zero;
            if (members != null)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (!IsUsable(member)) continue;
                    extents = Vector3.Max(extents, Abs(member.position - center) + Vector3.one * memberRadius);
                }
            }
            return new Bounds(center, extents * 2f);
        }

        private BoundingSphere CalculateWorldSphere()
        {
            var center = primary != null ? primary.position : transform.position;
            var radius = 0f;
            if (members != null)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (IsUsable(member))
                        radius = Mathf.Max(radius, Vector3.Distance(center, member.position) + memberRadius);
                }
            }
            return new BoundingSphere(center, radius);
        }

        private static Matrix4x4 Inverse(Matrix4x4 matrix)
        {
            var inverse = matrix;
            return Matrix4x4.Inverse3DAffine(matrix, ref inverse) ? inverse : matrix.inverse;
        }

        private static bool IsUsable(Transform member) =>
            member != null && member.gameObject.activeInHierarchy;

        private static Vector2 NormalizeViewPoint(Vector3 point)
        {
            var depth = Mathf.Max(0.0001f, point.z);
            return new Vector2(point.x / depth, point.y / depth);
        }

        private static Vector3 Abs(Vector3 value) =>
            new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private static Vector2 Abs(Vector2 value) =>
            new(Mathf.Abs(value.x), Mathf.Abs(value.y));
    }
}
