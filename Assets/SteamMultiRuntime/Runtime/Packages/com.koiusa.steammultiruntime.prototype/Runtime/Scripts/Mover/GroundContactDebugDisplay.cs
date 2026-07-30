using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal readonly struct GroundContactDebugSample
    {
        internal GroundContactDebugSample(
            string objectName,
            Vector3 point,
            Vector3 normal,
            float upDot,
            int layer,
            bool inGroundLayer,
            bool isGround)
        {
            ObjectName = objectName;
            Point = point;
            Normal = normal;
            UpDot = upDot;
            Layer = layer;
            InGroundLayer = inGroundLayer;
            IsGround = isGround;
        }

        internal string ObjectName { get; }
        internal Vector3 Point { get; }
        internal Vector3 Normal { get; }
        internal float UpDot { get; }
        internal int Layer { get; }
        internal bool InGroundLayer { get; }
        internal bool IsGround { get; }
    }

    internal readonly struct GroundContactDebugSnapshot
    {
        internal GroundContactDebugSnapshot(
            string targetName,
            Vector3 upAxis,
            float minGroundNormalDot,
            int groundMask,
            int lastCapturedFrame,
            int validGroundCount,
            GroundContactDebugSample? bestGround,
            GroundContactDebugSample[] contacts)
        {
            TargetName = targetName;
            UpAxis = upAxis;
            MinGroundNormalDot = minGroundNormalDot;
            GroundMask = groundMask;
            LastCapturedFrame = lastCapturedFrame;
            ValidGroundCount = validGroundCount;
            BestGround = bestGround;
            Contacts = contacts;
        }

        internal string TargetName { get; }
        internal Vector3 UpAxis { get; }
        internal float MinGroundNormalDot { get; }
        internal int GroundMask { get; }
        internal int LastCapturedFrame { get; }
        internal int ValidGroundCount { get; }
        internal GroundContactDebugSample? BestGround { get; }
        internal GroundContactDebugSample[] Contacts { get; }
        internal bool IsGrounded => BestGround.HasValue;
    }

    [DisallowMultipleComponent]
    public class GroundContactDebugDisplay : MonoBehaviour
    {
        private sealed class ContactSample
        {
            public Collider Collider;
            public Transform GroundTransform;
            public Vector3 Point;
            public Vector3 Normal;
            public float UpDot;
            public int Layer;
            public bool InGroundLayer;
            public bool IsGround;
        }

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private Vector3 upAxis = Vector3.up;
        [Range(-1f, 1f)]
        [SerializeField] private float minGroundNormalDot = 0.55f;

        [Header("Scene Gizmos")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private float normalLength = 0.3f;
        [SerializeField] private Color validGroundColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color invalidGroundColor = new Color(1f, 0.3f, 0.2f, 1f);

        private readonly List<ContactSample> contacts = new List<ContactSample>();
        private int lastCapturedFrame = -1;

        private void Awake() => NormalizeUpAxis();

        private void OnValidate() => NormalizeUpAxis();

        private void OnDisable()
        {
            contacts.Clear();
            lastCapturedFrame = -1;
        }

        private void OnCollisionEnter(Collision collision) => CaptureCollision(collision);

        private void OnCollisionStay(Collision collision) => CaptureCollision(collision);

        private void OnCollisionExit(Collision collision)
        {
            var collider = collision.collider;
            if (collider == null)
            {
                return;
            }

            for (var i = contacts.Count - 1; i >= 0; i--)
            {
                if (contacts[i].Collider == collider)
                {
                    contacts.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos)
            {
                return;
            }

            for (var i = 0; i < contacts.Count; i++)
            {
                var sample = contacts[i];
                if (sample.Collider == null)
                {
                    continue;
                }

                Gizmos.color = sample.IsGround ? validGroundColor : invalidGroundColor;
                Gizmos.DrawSphere(sample.Point, 0.03f);
                Gizmos.DrawLine(sample.Point, sample.Point + sample.Normal * normalLength);
            }
        }

        internal GroundContactDebugSnapshot GetDebugSnapshot()
        {
            var snapshotContacts = new GroundContactDebugSample[contacts.Count];
            GroundContactDebugSample? bestGround = null;
            var validGroundCount = 0;

            for (var i = 0; i < contacts.Count; i++)
            {
                var sample = contacts[i];
                var snapshotSample = new GroundContactDebugSample(
                    GetGroundName(sample),
                    sample.Point,
                    sample.Normal,
                    sample.UpDot,
                    sample.Layer,
                    sample.InGroundLayer,
                    sample.IsGround);
                snapshotContacts[i] = snapshotSample;

                if (!sample.IsGround)
                {
                    continue;
                }

                validGroundCount++;
                if (!bestGround.HasValue || sample.UpDot > bestGround.Value.UpDot)
                {
                    bestGround = snapshotSample;
                }
            }

            return new GroundContactDebugSnapshot(
                transform.name,
                upAxis,
                minGroundNormalDot,
                groundLayer.value,
                lastCapturedFrame,
                validGroundCount,
                bestGround,
                snapshotContacts);
        }

        private void CaptureCollision(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            if (lastCapturedFrame != Time.frameCount)
            {
                contacts.Clear();
                lastCapturedFrame = Time.frameCount;
            }

            var layer = collision.gameObject.layer;
            var inGroundLayer = IsInGroundLayer(layer, groundLayer);
            var groundRigidbody = collision.rigidbody != null ? collision.rigidbody : collision.collider.attachedRigidbody;
            var groundTransform = groundRigidbody != null ? groundRigidbody.transform : collision.collider.transform;

            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                var upDot = Vector3.Dot(contact.normal, upAxis);

                contacts.Add(new ContactSample
                {
                    Collider = collision.collider,
                    GroundTransform = groundTransform,
                    Point = contact.point,
                    Normal = contact.normal,
                    UpDot = upDot,
                    Layer = layer,
                    InGroundLayer = inGroundLayer,
                    IsGround = inGroundLayer && upDot >= minGroundNormalDot
                });
            }
        }

        private static string GetGroundName(ContactSample sample)
        {
            if (sample.GroundTransform != null)
            {
                return sample.GroundTransform.name;
            }

            return sample.Collider != null ? sample.Collider.name : "null";
        }

        private void NormalizeUpAxis()
        {
            upAxis = upAxis.sqrMagnitude <= Mathf.Epsilon ? Vector3.up : upAxis.normalized;
        }

        private static bool IsInGroundLayer(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
