using UnityEngine;
using UnityEngine.Rendering;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Camera-aimed, Rigidbody based wire swinging. Add this component to the
    /// same GameObject as the player Rigidbody and assign the input actions.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class WireSwingTraversalFeature : MonoBehaviour, IWireSwingTraversalFeature
    {
        [Header("Aiming")]
        [SerializeField] private Transform aimTransform;
        [SerializeField] private Transform wireOrigin;
        [SerializeField, Min(1f)] private float maximumRange = 45f;
        [SerializeField] private LayerMask grappleLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Swing")]
        [SerializeField, Min(0.1f)] private float minimumRopeLength = 2f;
        [SerializeField, Min(0f)] private float ropeSlack = 0.15f;
        [SerializeField, Min(0f)] private float pullAcceleration = 55f;
        [SerializeField, Min(0f)] private float swingAcceleration = 16f;
        [SerializeField, Min(0f)] private float maximumInputSwingSpeed = 8f;
        [SerializeField, Min(0f)] private float reelSpeed = 12f;
        [SerializeField, Min(0f)] private float jumpReelDistance = 1.5f;
        [SerializeField, Min(0f)] private float releaseBoost = 2.5f;
        [SerializeField, Range(0f, 1f)] private float radialVelocityDamping = 1f;

        [Header("Rendering")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Material wireMaterial;
        [SerializeField, Min(0f)] private float wireWidth = 0.035f;
        [SerializeField] private Color wireColor = Color.white;

        private static Material sharedBuiltInMaterial;
        private static Material sharedUniversalMaterial;
        private static Material sharedHighDefinitionMaterial;
        private static Material sharedCustomPipelineMaterial;

        private Rigidbody rb;
        private SlopeContactResolver slopeContactResolver;
        private Collider[] ownColliders;
        private Transform anchorTransform;
        private Vector3 anchorLocalPoint;
        private Vector3 fixedAnchorPoint;
        private float ropeLength;
        private Vector3 motorMoveDirection;
        private Vector3 grappleAimDirection;
        private float externalReelInput;
        private MaterialPropertyBlock materialPropertyBlock;

        public bool IsAttached { get; private set; }
        public bool IsEnabled => isActiveAndEnabled;
        public Transform AimTransform => aimTransform;
        public float MaximumRange => maximumRange;
        public Vector3 AnchorPoint => anchorTransform != null
            ? anchorTransform.TransformPoint(anchorLocalPoint)
            : fixedAnchorPoint;
        public float RopeLength => ropeLength;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            ownColliders = GetComponentsInChildren<Collider>();
            EnsureLineRenderer();
        }

        private void OnDisable()
        {
            Detach(false);
        }

        private void OnValidate()
        {
            maximumRange = Mathf.Max(1f, maximumRange);
            minimumRopeLength = Mathf.Max(0.1f, minimumRopeLength);
            ropeSlack = Mathf.Max(0f, ropeSlack);
            maximumInputSwingSpeed = Mathf.Max(0f, maximumInputSwingSpeed);
            jumpReelDistance = Mathf.Max(0f, jumpReelDistance);
            if (lineRenderer != null)
            {
                ConfigureLineRenderer();
            }
        }

        private void Update()
        {
            UpdateWireVisual();
        }

        private void FixedUpdate()
        {
            if (rb == null || rb.isKinematic)
            {
                return;
            }

            if (!IsAttached)
            {
                return;
            }

            ApplyReelInput();
            ApplySwingForce();
            ConstrainRope();
        }

        /// <summary>Supplies the world-space movement intent produced by PlayerMotor.</summary>
        public void SetMoveDirection(Vector3 moveDirection)
        {
            motorMoveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
        }

        public void SetGrappleInput(bool held, Vector3 origin, Vector3 aimDirection)
        {
            grappleAimDirection = aimDirection.sqrMagnitude > 0.0001f
                ? aimDirection.normalized
                : Vector3.zero;

            if (held)
            {
                if (!IsAttached && grappleAimDirection.sqrMagnitude > 0f)
                {
                    TryAttach(origin, grappleAimDirection);
                }
            }
            else if (IsAttached)
            {
                Detach();
            }
        }

        public void SetReelInput(float reelInput)
        {
            externalReelInput = Mathf.Clamp(reelInput, -1f, 1f);
        }

        public void ReelByJump()
        {
            if (!IsAttached || jumpReelDistance <= 0f)
            {
                return;
            }

            ropeLength = Mathf.Clamp(ropeLength - jumpReelDistance, minimumRopeLength, maximumRange);
        }

        public void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float replicatedRopeLength)
        {
            if (!isAttached)
            {
                Detach(false);
                return;
            }

            anchorTransform = null;
            fixedAnchorPoint = anchorPoint;
            ropeLength = Mathf.Clamp(replicatedRopeLength, minimumRopeLength, maximumRange);
            IsAttached = true;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = true;
            }
        }

        /// <summary>Attaches to the first valid collider along a world-space ray.</summary>
        public bool TryAttach(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            var hits = Physics.RaycastAll(origin, direction.normalized, maximumRange, grappleLayers, triggerInteraction);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IsOwnCollider(hit.collider))
                {
                    continue;
                }

                Attach(hit.point, hit.collider.transform);
                return true;
            }

            return false;
        }

        /// <summary>Attaches directly; useful for AI or server-authoritative input.</summary>
        public void Attach(Vector3 worldPoint, Transform movingAnchor = null)
        {
            anchorTransform = movingAnchor;
            fixedAnchorPoint = worldPoint;
            anchorLocalPoint = movingAnchor != null ? movingAnchor.InverseTransformPoint(worldPoint) : Vector3.zero;
            ropeLength = Mathf.Clamp(Vector3.Distance(rb.worldCenterOfMass, worldPoint), minimumRopeLength, maximumRange);
            IsAttached = true;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = true;
            }
        }

        public void Detach(bool applyBoost = false)
        {
            if (!IsAttached)
            {
                return;
            }

            if (applyBoost && releaseBoost > 0f)
            {
                var up = Physics.gravity.sqrMagnitude > 0.001f ? -Physics.gravity.normalized : Vector3.up;
                var forward = grappleAimDirection.sqrMagnitude > 0.0001f
                    ? grappleAimDirection
                    : GetAimTransform().forward;
                var boostDirection = Vector3.ProjectOnPlane(forward, up).normalized + up * 0.45f;
                rb.AddForce(boostDirection.normalized * releaseBoost, ForceMode.VelocityChange);
            }

            IsAttached = false;
            anchorTransform = null;
            motorMoveDirection = Vector3.zero;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        private void ApplyReelInput()
        {
            ropeLength = Mathf.Clamp(ropeLength - externalReelInput * reelSpeed * Time.fixedDeltaTime, minimumRopeLength, maximumRange);
        }

        private void ApplySwingForce()
        {
            if (swingAcceleration <= 0f
                || maximumInputSwingSpeed <= 0f
                || (slopeContactResolver != null && slopeContactResolver.IsGrounded))
            {
                return;
            }

            var desired = motorMoveDirection;
            var ropeDirection = (AnchorPoint - rb.worldCenterOfMass).normalized;
            var tangent = Vector3.ProjectOnPlane(desired, ropeDirection);
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var tangentialVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, ropeDirection);
            var targetSpeed = maximumInputSwingSpeed * Mathf.Clamp01(tangent.magnitude);
            var remainingSpeedRatio = Mathf.Clamp01((targetSpeed - tangentialVelocity.magnitude) / targetSpeed);
            if (remainingSpeedRatio <= 0f)
            {
                return;
            }

            var accelerationFactor = remainingSpeedRatio * remainingSpeedRatio;
            rb.AddForce(tangent.normalized * (swingAcceleration * accelerationFactor), ForceMode.Acceleration);
        }

        private void ConstrainRope()
        {
            var toAnchor = AnchorPoint - rb.worldCenterOfMass;
            var distance = toAnchor.magnitude;
            if (distance <= ropeLength + ropeSlack || distance < 0.001f)
            {
                return;
            }

            var towardAnchor = toAnchor / distance;
            var stretch = distance - ropeLength;
            rb.AddForce(towardAnchor * (stretch * pullAcceleration), ForceMode.Acceleration);

            // Remove only velocity that would stretch the rope. Tangential momentum,
            // which produces the pendulum motion, is deliberately preserved.
            var awaySpeed = Vector3.Dot(rb.linearVelocity, -towardAnchor);
            if (awaySpeed > 0f)
            {
                rb.linearVelocity += towardAnchor * (awaySpeed * radialVelocityDamping);
            }
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            ConfigureLineRenderer();
            EnsureCompatibleMaterial();
            lineRenderer.enabled = false;
        }

        private void EnsureCompatibleMaterial()
        {
            if (lineRenderer == null)
            {
                return;
            }

            if (wireMaterial != null)
            {
                lineRenderer.sharedMaterial = wireMaterial;
                return;
            }

            var material = GetSharedPipelineMaterial();
            if (material != null)
            {
                lineRenderer.sharedMaterial = material;
            }
            else
            {
                Debug.LogWarning(
                    "No compatible wire shader was found. Assign Wire Material explicitly or include an Unlit shader in the build.",
                    this);
            }
        }

        private static Material GetSharedPipelineMaterial()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                return sharedBuiltInMaterial != null
                    ? sharedBuiltInMaterial
                    : sharedBuiltInMaterial = CreateSharedMaterial(
                        "Particles/Standard Unlit",
                        "Sprites/Default");
            }

            var pipelineTypeName = pipeline.GetType().FullName ?? string.Empty;
            if (pipelineTypeName.Contains("UniversalRenderPipeline"))
            {
                return sharedUniversalMaterial != null
                    ? sharedUniversalMaterial
                    : sharedUniversalMaterial = CreateSharedMaterial(
                        "Universal Render Pipeline/Particles/Unlit",
                        "Universal Render Pipeline/Unlit");
            }

            if (pipelineTypeName.Contains("HDRenderPipeline"))
            {
                return sharedHighDefinitionMaterial != null
                    ? sharedHighDefinitionMaterial
                    : sharedHighDefinitionMaterial = CreateSharedMaterial(
                        "HDRP/Unlit",
                        "HDRenderPipeline/Unlit");
            }

            // Custom SRPs can still supply one of the common Unlit shaders. An
            // explicitly assigned Wire Material remains the reliable override.
            return sharedCustomPipelineMaterial != null
                ? sharedCustomPipelineMaterial
                : sharedCustomPipelineMaterial = CreateSharedMaterial(
                    "Universal Render Pipeline/Particles/Unlit",
                    "HDRP/Unlit",
                    "Particles/Standard Unlit",
                    "Sprites/Default");
        }

        private static Material CreateSharedMaterial(params string[] shaderNames)
        {
            for (var i = 0; i < shaderNames.Length; i++)
            {
                var shader = Shader.Find(shaderNames[i]);
                if (shader == null || !shader.isSupported)
                {
                    continue;
                }

                return new Material(shader)
                {
                    name = $"Wire ({shaderNames[i]})",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return null;
        }

        private void ConfigureLineRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = wireWidth;
            lineRenderer.endWidth = wireWidth;
            lineRenderer.startColor = wireColor;
            lineRenderer.endColor = wireColor;
            materialPropertyBlock ??= new MaterialPropertyBlock();
            lineRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor("_BaseColor", wireColor);
            materialPropertyBlock.SetColor("_UnlitColor", wireColor);
            materialPropertyBlock.SetColor("_Color", wireColor);
            lineRenderer.SetPropertyBlock(materialPropertyBlock);
            if (Application.isPlaying)
            {
                EnsureCompatibleMaterial();
            }
        }

        private void UpdateWireVisual()
        {
            if (!IsAttached || lineRenderer == null)
            {
                return;
            }

            lineRenderer.SetPosition(0, wireOrigin != null ? wireOrigin.position : rb.worldCenterOfMass);
            lineRenderer.SetPosition(1, AnchorPoint);
        }

        private Transform GetAimTransform()
        {
            if (aimTransform != null)
            {
                return aimTransform;
            }

            return Camera.main != null ? Camera.main.transform : transform;
        }

        private bool IsOwnCollider(Collider candidate)
        {
            foreach (var ownCollider in ownColliders)
            {
                if (candidate == ownCollider)
                {
                    return true;
                }
            }

            return candidate.attachedRigidbody == rb;
        }

    }
}
