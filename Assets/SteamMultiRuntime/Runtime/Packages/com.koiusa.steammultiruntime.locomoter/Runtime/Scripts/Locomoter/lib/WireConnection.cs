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
        [SerializeField, Tooltip("Elasticはバネ力、Ropeは非伸縮の位置制約として処理します。")]
        private WireConstraintMode constraintMode = WireConstraintMode.Elastic;
        [SerializeField, Min(0f), Tooltip("Elasticで許容する最大の伸び幅です。この距離を超えると位置を補正します。")]
        private float elasticStretchLimit = 1.5f;
        [SerializeField, Min(0.1f)] private float minimumRopeLength = 2f;
        [SerializeField, Min(0f)] private float ropeSlack = 0.15f;
        [SerializeField, Min(0f)] private float pullAcceleration = 55f;
        [SerializeField, Range(0f, 1f)] private float radialVelocityDamping = 1f;

        private IWireLineVisualFeature wireVisual;
        private IWireGroundAction groundAction;
        private IWireReelAction reelAction;
        private Transform anchorTransform;
        private Vector3 anchorLocalPoint;
        private Vector3 fixedAnchorPoint;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsAttached { get; private set; }
        public Vector3 AnchorPoint => anchorTransform != null ? anchorTransform.TransformPoint(anchorLocalPoint) : fixedAnchorPoint;
        public float RopeLength { get; private set; }
        public float ActualLength => IsAttached && Body != null
            ? Vector3.Distance(Body.worldCenterOfMass, AnchorPoint)
            : 0f;
        public float MinimumRopeLength => minimumRopeLength;
        public float MaximumRopeLength { get; private set; } = 45f;
        public Rigidbody Body { get; private set; }
        public Rigidbody AnchorBody { get; private set; }
        public WireConstraintMode ConstraintMode => constraintMode;
        public float ElasticStretchLimit => elasticStretchLimit;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (visual == null) visual = GetComponent<WireLineVisualFeature>();
            wireVisual = visual != null ? visual : GetComponent<IWireLineVisualFeature>();
            groundAction = GetComponent<IWireGroundAction>();
            reelAction = GetComponent<IWireReelAction>();
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
            elasticStretchLimit = Mathf.Max(0f, elasticStretchLimit);
        }

        private void Update()
        {
            if (IsAttached) wireVisual?.UpdateEndpoints(AnchorPoint);
        }

        private void FixedUpdate()
        {
            if (!IsAttached || Body == null || Body.isKinematic || (groundAction != null && groundAction.HandlesConnectionPhysics)) return;
            var toAnchor = AnchorPoint - Body.worldCenterOfMass;
            var distance = toAnchor.magnitude;
            var constraintLength = RopeLength;
            var allowedLength = constraintLength + ropeSlack;
            if (distance <= allowedLength || distance < 0.001f) return;
            var towardAnchor = toAnchor / distance;
            var stretch = distance - allowedLength;
            var useRopeConstraint = constraintMode == WireConstraintMode.Rope
                || (reelAction != null && reelAction.IsReelingIn);
            if (useRopeConstraint)
            {
                Body.MovePosition(Body.position + towardAnchor * stretch);
            }
            else
            {
                Body.AddForce(towardAnchor * (stretch * pullAcceleration), ForceMode.Acceleration);
                var hardLimit = allowedLength + elasticStretchLimit;
                if (distance > hardLimit)
                {
                    Body.MovePosition(Body.position + towardAnchor * (distance - hardLimit));
                }
            }
            var awaySpeed = Vector3.Dot(Body.linearVelocity, -towardAnchor);
            if (awaySpeed > 0f)
            {
                var reachedHardLimit = !useRopeConstraint
                    && distance >= allowedLength + elasticStretchLimit;
                var damping = useRopeConstraint || reachedHardLimit ? 1f : radialVelocityDamping;
                Body.linearVelocity += towardAnchor * (awaySpeed * damping);
            }
        }

        public void SetRopeLength(float value) => RopeLength = Mathf.Clamp(value, minimumRopeLength, MaximumRopeLength);

        public void Attach(Vector3 worldPoint, Transform movingAnchor = null)
        {
            anchorTransform = movingAnchor;
            AnchorBody = movingAnchor != null ? movingAnchor.GetComponentInParent<Rigidbody>() : null;
            fixedAnchorPoint = worldPoint;
            anchorLocalPoint = movingAnchor != null ? movingAnchor.InverseTransformPoint(worldPoint) : Vector3.zero;
            var isEnvironmentAnchor = AnchorBody == null || AnchorBody.isKinematic;
            SetRopeLength(isEnvironmentAnchor
                ? MaximumRopeLength
                : Vector3.Distance(Body.worldCenterOfMass, worldPoint));
            IsAttached = true;
            wireVisual?.SetVisible(true);
        }

        public void Detach()
        {
            IsAttached = false;
            anchorTransform = null;
            AnchorBody = null;
            wireVisual?.SetVisible(false);
        }

        public void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float ropeLength)
        {
            if (!isAttached) { Detach(); return; }
            anchorTransform = null;
            AnchorBody = null;
            fixedAnchorPoint = anchorPoint;
            SetRopeLength(ropeLength);
            IsAttached = true;
            wireVisual?.SetVisible(true);
        }
    }
}
