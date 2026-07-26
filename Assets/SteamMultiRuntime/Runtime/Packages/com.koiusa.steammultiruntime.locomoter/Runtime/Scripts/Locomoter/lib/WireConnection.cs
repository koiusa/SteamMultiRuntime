using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerTraversalCoordinator))]
    [RequireComponent(typeof(WireLineVisualFeature))]
    [DisallowMultipleComponent]
    public sealed class WireConnection : MonoBehaviour, IWireConnection
    {
        [SerializeField] private WireLineVisualFeature visual;
        [SerializeField, Min(0.1f)] private float minimumRopeLength = 2f;
        [SerializeField, Min(0f)] private float ropeSlack = 0.15f;
        [SerializeField, Min(0f)] private float pullAcceleration = 55f;
        [SerializeField, Range(0f, 1f)] private float radialVelocityDamping = 1f;

        private IWireLineVisualFeature wireVisual;
        private Transform anchorTransform;
        private Vector3 anchorLocalPoint;
        private Vector3 fixedAnchorPoint;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsAttached { get; private set; }
        public Vector3 AnchorPoint => anchorTransform != null ? anchorTransform.TransformPoint(anchorLocalPoint) : fixedAnchorPoint;
        public float RopeLength { get; private set; }
        public float MinimumRopeLength => minimumRopeLength;
        public float MaximumRopeLength { get; private set; } = 45f;
        public Rigidbody Body { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (visual == null) visual = GetComponent<WireLineVisualFeature>();
            wireVisual = visual != null ? visual : GetComponent<IWireLineVisualFeature>();
            wireVisual?.Initialize();
            var target = GetComponent<IWireGrappleTargetingFeature>();
            if (target != null) MaximumRopeLength = target.MaximumRange;
        }

        private void OnDisable() => Detach();
        private void OnValidate()
        {
            if (visual == null) visual = GetComponent<WireLineVisualFeature>();
            minimumRopeLength = Mathf.Max(0.1f, minimumRopeLength);
            ropeSlack = Mathf.Max(0f, ropeSlack);
        }

        private void Update()
        {
            if (IsAttached) wireVisual?.UpdateEndpoints(AnchorPoint);
        }

        private void FixedUpdate()
        {
            if (!IsAttached || Body == null || Body.isKinematic) return;
            var toAnchor = AnchorPoint - Body.worldCenterOfMass;
            var distance = toAnchor.magnitude;
            if (distance <= RopeLength + ropeSlack || distance < 0.001f) return;
            var towardAnchor = toAnchor / distance;
            Body.AddForce(towardAnchor * ((distance - RopeLength) * pullAcceleration), ForceMode.Acceleration);
            var awaySpeed = Vector3.Dot(Body.linearVelocity, -towardAnchor);
            if (awaySpeed > 0f) Body.linearVelocity += towardAnchor * (awaySpeed * radialVelocityDamping);
        }

        public void SetRopeLength(float value) => RopeLength = Mathf.Clamp(value, minimumRopeLength, MaximumRopeLength);

        public void Attach(Vector3 worldPoint, Transform movingAnchor = null)
        {
            anchorTransform = movingAnchor;
            fixedAnchorPoint = worldPoint;
            anchorLocalPoint = movingAnchor != null ? movingAnchor.InverseTransformPoint(worldPoint) : Vector3.zero;
            SetRopeLength(Vector3.Distance(Body.worldCenterOfMass, worldPoint));
            IsAttached = true;
            wireVisual?.SetVisible(true);
        }

        public void Detach()
        {
            IsAttached = false;
            anchorTransform = null;
            wireVisual?.SetVisible(false);
        }

        public void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float ropeLength)
        {
            if (!isAttached) { Detach(); return; }
            anchorTransform = null;
            fixedAnchorPoint = anchorPoint;
            SetRopeLength(ropeLength);
            IsAttached = true;
            wireVisual?.SetVisible(true);
        }
    }
}
