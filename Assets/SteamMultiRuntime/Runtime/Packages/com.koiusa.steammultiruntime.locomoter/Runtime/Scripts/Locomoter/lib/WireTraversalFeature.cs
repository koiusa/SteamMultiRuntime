using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerTraversalCoordinator))]
    [RequireComponent(typeof(WireLineVisualFeature))]
    [DisallowMultipleComponent]
    public sealed class WireTraversalFeature : MonoBehaviour, IWireConnection
    {
        [SerializeField] private WireLineVisualFeature visual;
        [SerializeField, Tooltip("Elasticはバネ力、Ropeは非伸縮の位置制約として処理します。")]
        private WireConstraintMode constraintMode = WireConstraintMode.Elastic;
        [SerializeField, Min(0f), Tooltip("Elasticで許容する最大の伸び幅です。この距離を超えると位置を補正します。")]
        private float elasticStretchLimit = 1.5f;
        [SerializeField, Min(0.1f)] private float minimumRopeLength = 2f;
        [SerializeField, Min(0.1f), Tooltip("接続後に維持できるワイヤの最大長です。アタッチ可能距離とは独立しています。")]
        private float maximumRopeLength = 20f;
        [SerializeField, Min(0f), Tooltip("最大長より遠い場所へアタッチした際、不足分を自動で巻き取る速度です。")]
        private float excessReelSpeed = 12f;
        [SerializeField, Min(0f)] private float ropeSlack = 0.15f;
        [SerializeField, Min(0f)] private float pullAcceleration = 55f;
        [SerializeField, Range(0f, 1f)] private float radialVelocityDamping = 1f;

        private IWireLineVisualFeature wireVisual;
        private IWireGroundAction groundAction;
        private IWireReelAction reelAction;
        private Transform anchorTransform;
        private Vector3 anchorLocalPoint;
        private Transform visualAnchorTransform;
        private Vector3 visualAnchorLocalPoint;
        private Vector3 fixedAnchorPoint;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsAttached { get; private set; }
        public Vector3 AnchorPoint => anchorTransform != null ? anchorTransform.TransformPoint(anchorLocalPoint) : fixedAnchorPoint;
        public Transform AnchorTransform => anchorTransform;
        public float RopeLength { get; private set; }
        public float ActualLength => IsAttached && Body != null
            ? Vector3.Distance(Body.worldCenterOfMass, AnchorPoint)
            : 0f;
        public float MinimumRopeLength => minimumRopeLength;
        public float MaximumRopeLength => maximumRopeLength;
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
        }

        private void OnDisable() => Detach();
        private void OnValidate()
        {
            if (visual == null) visual = GetComponent<WireLineVisualFeature>();
            minimumRopeLength = Mathf.Max(0.1f, minimumRopeLength);
            maximumRopeLength = Mathf.Max(minimumRopeLength, maximumRopeLength);
            excessReelSpeed = Mathf.Max(0f, excessReelSpeed);
            ropeSlack = Mathf.Max(0f, ropeSlack);
            elasticStretchLimit = Mathf.Max(0f, elasticStretchLimit);
        }

        private void LateUpdate()
        {
            if (IsAttached) wireVisual?.UpdateEndpoints(VisualAnchorPoint);
        }

        private void FixedUpdate()
        {
            if (IsAttached && RopeLength > maximumRopeLength)
            {
                // A distant target remains attachable, but the excess wire is reeled in
                // progressively instead of snapping the player to the configured maximum.
                RopeLength = Mathf.MoveTowards(RopeLength, maximumRopeLength, excessReelSpeed * Time.fixedDeltaTime);
            }
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

        public void SetRopeLength(float value)
        {
            // While a distant attachment is being reeled in, allow its temporary
            // overlength to decrease continuously. Paying wire out can never increase it.
            var currentUpperLimit = Mathf.Max(maximumRopeLength, RopeLength);
            RopeLength = Mathf.Clamp(value, minimumRopeLength, currentUpperLimit);
        }

        public void CaptureCurrentLength()
        {
            if (!IsAttached || Body == null) return;
            RopeLength = Mathf.Max(minimumRopeLength, ActualLength);
        }

        public void Attach(Vector3 worldPoint, Transform movingAnchor = null)
        {
            anchorTransform = movingAnchor;
            AnchorBody = movingAnchor != null ? movingAnchor.GetComponentInParent<Rigidbody>() : null;
            fixedAnchorPoint = worldPoint;
            anchorLocalPoint = movingAnchor != null ? movingAnchor.InverseTransformPoint(worldPoint) : Vector3.zero;
            visualAnchorTransform = FindPresentationTransform(movingAnchor, out var presentationRoot) ?? movingAnchor;
            visualAnchorLocalPoint = visualAnchorTransform != null
                ? (presentationRoot ?? movingAnchor).InverseTransformPoint(worldPoint)
                : Vector3.zero;
            var attachDistance = Vector3.Distance(Body.worldCenterOfMass, worldPoint);
            // Keep normal ground traversal free inside the maximum. Beyond it, start at
            // the actual distance and let FixedUpdate reel the excess in at a stable rate.
            var isEnvironmentAnchor = AnchorBody == null || AnchorBody.isKinematic;
            RopeLength = Mathf.Max(minimumRopeLength, isEnvironmentAnchor
                ? Mathf.Max(maximumRopeLength, attachDistance)
                : attachDistance);
            IsAttached = true;
            wireVisual?.SetVisible(true);
        }

        public void Detach()
        {
            IsAttached = false;
            anchorTransform = null;
            visualAnchorTransform = null;
            AnchorBody = null;
            wireVisual?.SetVisible(false);
        }

        public void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float ropeLength, Transform movingAnchor = null)
        {
            if (!isAttached) { Detach(); return; }
            anchorTransform = movingAnchor;
            anchorLocalPoint = movingAnchor != null ? movingAnchor.InverseTransformPoint(anchorPoint) : Vector3.zero;
            AnchorBody = movingAnchor != null ? movingAnchor.GetComponentInParent<Rigidbody>() : null;
            visualAnchorTransform = FindPresentationTransform(movingAnchor, out var presentationRoot) ?? movingAnchor;
            visualAnchorLocalPoint = visualAnchorTransform != null
                ? (presentationRoot ?? movingAnchor).InverseTransformPoint(anchorPoint)
                : Vector3.zero;
            fixedAnchorPoint = anchorPoint;
            // Preserve an authoritative in-progress auto-reel length, which can
            // temporarily be longer than the configured maintained maximum.
            RopeLength = Mathf.Max(minimumRopeLength, ropeLength);
            IsAttached = true;
            wireVisual?.SetVisible(true);
        }

        private Vector3 VisualAnchorPoint => visualAnchorTransform != null
            ? visualAnchorTransform.TransformPoint(visualAnchorLocalPoint)
            : AnchorPoint;

        private static Transform FindPresentationTransform(Transform anchor, out Transform presentationRoot)
        {
            // Moving environment objects keep their collider on the physics root and
            // render a smoothed child pose. Follow that pose for the line endpoint,
            // while AnchorPoint continues to use the authoritative physics transform.
            for (var current = anchor; current != null; current = current.parent)
            {
                var presentation = current.Find("Presentation");
                if (presentation != null)
                {
                    presentationRoot = current;
                    return presentation;
                }
            }

            presentationRoot = null;
            return null;
        }
    }
}
