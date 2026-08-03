using Unity.Mathematics;
using UnityEngine;
using Koiusa.SteamMultiRuntime.Core;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NpcCrowdMotor : MonoBehaviour, IFallRecoveryMotionReset, ITraversalVelocityAdapter
    {
        [SerializeField] private ActorMotorSettings settings = default;
        private Rigidbody body;
        private NpcCrowdMovingPlatformAction movingPlatform;
        private ServerDrivenActorController networkController;
        private CapsuleCollider movementCapsule;
        private Vector3 velocity;
        private Vector3 desiredPlanarVelocity;
        private Vector3 groundDisplacement;
        private Vector3 groundVelocity;
        private Quaternion groundRotationDelta = Quaternion.identity;
        private float groundCoordinate;
        private float groundClearance;
        private float groundProbeRadius = 0.15f;
        private float groundProbeLift;
        private bool hasGroundSurface = true;
        private bool grounded = true;
        private int consecutiveGroundMisses;
        private bool airborneFromJump;
        private bool jumpRequested;
        private NpcCrowdContactSettings contactSettings;
        private ActorTraversalState traversalState;
        private Vector3 wireAnchor;
        private float wireRopeLength;
        private SlopeContactResolver slopeContactResolver;
        private int wallProbeContactId;
        private Vector3 detectedWallNormal;
        private float detectedWallDistance;
        private bool hasDetectedWall;
        private float simulationDeltaTime = 0.02f;

        public Vector3 TraversalVelocity
        {
            get => velocity;
            set => velocity = value;
        }

        public bool IsGrounded => grounded;
        public bool IsJumping => !grounded && airborneFromJump && VerticalVelocity > 0f;
        public bool IsFreefall => !grounded && !airborneFromJump;
        public bool IsFallingAfterJump => !grounded && airborneFromJump && VerticalVelocity <= 0f;
        public float HorizontalVelocity => Vector3.ProjectOnPlane(velocity, UpAxis).magnitude;
        public float VerticalVelocity => Vector3.Dot(velocity, UpAxis);
        public Vector3 Velocity => velocity;
        internal bool UsesMovingPlatformPhysicsPresentation =>
            movingPlatform != null && movingPlatform.HasPhysicsPoseSource;
        internal bool ShouldProbeWalls => desiredPlanarVelocity.sqrMagnitude > 0.0025f || !grounded
            || traversalState == ActorTraversalState.WallRun
            || traversalState == ActorTraversalState.WallSlide
            || traversalState == ActorTraversalState.WallJump;
        internal bool ShouldProbeWallsEveryFixedStep => !grounded
            || traversalState == ActorTraversalState.WallRun
            || traversalState == ActorTraversalState.WallSlide
            || traversalState == ActorTraversalState.WallJump;
        private Vector3 UpAxis => Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;

        internal void BeginSimulationStep(float deltaTime) =>
            simulationDeltaTime = Mathf.Clamp(deltaTime, 0.001f, 1f / 15f);

        internal void Initialize(IActorMotor settingsSource, NpcCrowdContactSettings crowdContactSettings)
        {
            body = GetComponent<Rigidbody>();
            movingPlatform = GetComponent<NpcCrowdMovingPlatformAction>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            wallProbeContactId = GetInstanceID() ^ unchecked((int)0x6a09e667);
            if (movingPlatform == null)
                movingPlatform = gameObject.AddComponent<NpcCrowdMovingPlatformAction>();
            movingPlatform.MovingPlatformBindingChanged -= OnMovingPlatformBindingChanged;
            movingPlatform.MovingPlatformBindingChanged += OnMovingPlatformBindingChanged;
            networkController = GetComponent<ServerDrivenActorController>();
            settings = settingsSource != null ? settingsSource.GetSettings() : ActorMotorSettings.CreateDefault();
            contactSettings = crowdContactSettings;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            // The actor-root collider defines locomotion. Model/animation colliders vary per skin
            // and must not change the Crowd Motor's foot coordinate.
            var colliders = body.GetComponents<Collider>();
            movementCapsule = body.GetComponent<CapsuleCollider>();
            movingPlatform.Initialize(movementCapsule);
            var bodyCoordinate = Vector3.Dot(body.position, UpAxis);
            var lowestColliderCoordinate = float.PositiveInfinity;
            for (var i = 0; i < colliders.Length; i++)
            {
                // Attack/sensor triggers must not affect the character's foot position.
                if (colliders[i].enabled && !colliders[i].isTrigger)
                {
                    var bounds = colliders[i].bounds;
                    var projectedExtent = Mathf.Abs(bounds.extents.x * UpAxis.x)
                        + Mathf.Abs(bounds.extents.y * UpAxis.y)
                        + Mathf.Abs(bounds.extents.z * UpAxis.z);
                    lowestColliderCoordinate = Mathf.Min(
                        lowestColliderCoordinate,
                        Vector3.Dot(bounds.center, UpAxis) - projectedExtent);
                    var horizontalExtent = Mathf.Sqrt(Mathf.Max(0f, bounds.extents.sqrMagnitude - projectedExtent * projectedExtent));
                    groundProbeRadius = Mathf.Max(groundProbeRadius, Mathf.Min(0.35f, horizontalExtent * 0.5f));
                }
                colliders[i].isTrigger = true;
            }
            groundClearance = float.IsPositiveInfinity(lowestColliderCoordinate)
                ? 0f
                : Mathf.Max(0f, bodyCoordinate - lowestColliderCoordinate);
            velocity = Vector3.zero;
            groundCoordinate = bodyCoordinate;
            grounded = true;
        }

        private void OnDestroy()
        {
            if (movingPlatform != null)
                movingPlatform.MovingPlatformBindingChanged -= OnMovingPlatformBindingChanged;
            networkController?.SetServerNpcMovingPlatformSync(false);
        }

        private void OnMovingPlatformBindingChanged(bool isBound) =>
            networkController?.SetServerNpcMovingPlatformSync(isBound);

        internal bool FollowMovingPlatformPhysicsPose(IGroundMotionPhysicsPoseSource source, float deltaTime)
        {
            if (!grounded || movingPlatform == null
                || !movingPlatform.TrySamplePhysicsFollow(
                    source,
                    body.position,
                    deltaTime,
                    out var displacement,
                    out var rotationDelta))
                return false;

            body.position += displacement;
            body.rotation = rotationDelta * body.rotation;
            groundCoordinate += Vector3.Dot(displacement, UpAxis);
            groundDisplacement = Vector3.zero;
            groundRotationDelta = Quaternion.identity;
            return true;
        }

        internal void SetCommand(Vector3 desiredVelocity, bool wantsJump)
        {
            desiredPlanarVelocity = Vector3.ProjectOnPlane(desiredVelocity, UpAxis);
            jumpRequested |= wantsJump;
        }

        internal void ApplyCommand(NpcCrowdCommand command)
        {
            SetCommand(command.DesiredVelocity, command.JumpRequested);
            traversalState = command.TraversalState;
            wireAnchor = command.WireAnchor;
            wireRopeLength = command.WireRopeLength;
        }

        public void ResetAfterFallRecovery(Vector3 position, Quaternion rotation)
        {
            velocity = Vector3.zero;
            desiredPlanarVelocity = Vector3.zero;
            groundDisplacement = Vector3.zero;
            groundVelocity = Vector3.zero;
            groundRotationDelta = Quaternion.identity;
            groundCoordinate = Vector3.Dot(position, UpAxis);
            hasGroundSurface = false;
            grounded = false;
            consecutiveGroundMisses = 0;
            airborneFromJump = false;
            jumpRequested = false;
        }

        internal void CreateGroundProbes(out CapsulecastCommand castCommand, out OverlapCapsuleCommand overlapCommand)
        {
            var query = new QueryParameters(settings.GroundLayer, false, QueryTriggerInteraction.Ignore, false);
            if (movementCapsule == null)
            {
                var center = body != null ? body.worldCenterOfMass : transform.position;
                var halfSegment = UpAxis * 0.01f;
                groundProbeLift = Mathf.Max(0.05f, settings.NearbyGroundDistance + 0.02f);
                center += UpAxis * groundProbeLift;
                var fallSweep = Mathf.Max(0f, -VerticalVelocity) * simulationDeltaTime + 0.1f;
                var distance = groundProbeLift + groundClearance + Mathf.Max(0.25f, settings.NearbyGroundDistance + fallSweep);
                castCommand = new CapsulecastCommand(center + halfSegment, center - halfSegment, groundProbeRadius, -UpAxis, query, distance);
                center -= UpAxis * groundProbeLift;
                overlapCommand = new OverlapCapsuleCommand(center + halfSegment, center - halfSegment, groundProbeRadius + 0.03f, query);
                return;
            }

            var capsuleTransform = movementCapsule.transform;
            var centerWorld = capsuleTransform.TransformPoint(movementCapsule.center);
            var localAxis = movementCapsule.direction == 0 ? Vector3.right
                : movementCapsule.direction == 2 ? Vector3.forward
                : Vector3.up;
            var axisWorld = capsuleTransform.TransformDirection(localAxis).normalized;
            var scale = capsuleTransform.lossyScale;
            var axisScale = movementCapsule.direction == 0 ? Mathf.Abs(scale.x)
                : movementCapsule.direction == 2 ? Mathf.Abs(scale.z)
                : Mathf.Abs(scale.y);
            var radiusScale = movementCapsule.direction == 0
                ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                : movementCapsule.direction == 2
                    ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
                    : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var radius = Mathf.Max(0.01f, movementCapsule.radius * radiusScale);
            var halfSegmentLength = Mathf.Max(0f, movementCapsule.height * axisScale * 0.5f - radius);
            var pointOffset = axisWorld * halfSegmentLength;
            // Shape casts do not report colliders already overlapping the start shape.
            // Lift the locomotion capsule first, then sweep through its standing position.
            groundProbeLift = Mathf.Max(0.05f, settings.NearbyGroundDistance + 0.02f);
            var liftedCenter = centerWorld + UpAxis * groundProbeLift;
            var fallSweepDistance = Mathf.Max(0f, -VerticalVelocity) * simulationDeltaTime + 0.1f;
            var castDistance = groundProbeLift + Mathf.Max(0.25f, settings.NearbyGroundDistance + fallSweepDistance);
            castCommand = new CapsulecastCommand(liftedCenter + pointOffset, liftedCenter - pointOffset, radius, -UpAxis, query, castDistance);
            overlapCommand = new OverlapCapsuleCommand(centerWorld + pointOffset, centerWorld - pointOffset, radius + 0.03f, query);
        }

        internal void CreateWallProbes(Vector3 desiredDirection, out CapsulecastCommand forward,
            out CapsulecastCommand left, out CapsulecastCommand right)
        {
            var up = UpAxis;
            var forwardDirection = Vector3.ProjectOnPlane(desiredDirection, up).normalized;
            if (forwardDirection.sqrMagnitude <= 0.0001f)
                forwardDirection = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up).normalized;
            var rightDirection = Vector3.Cross(up, forwardDirection).normalized;
            forward = CreateWallProbe(forwardDirection);
            left = CreateWallProbe(-rightDirection);
            right = CreateWallProbe(rightDirection);
        }

        private CapsulecastCommand CreateWallProbe(Vector3 direction)
        {
            var query = new QueryParameters(settings.GroundLayer, false, QueryTriggerInteraction.Ignore, false);
            var center = movementCapsule != null
                ? movementCapsule.transform.TransformPoint(movementCapsule.center)
                : body.worldCenterOfMass;
            var radius = groundProbeRadius;
            var halfSegment = UpAxis * 0.01f;
            if (movementCapsule != null)
            {
                var capsuleTransform = movementCapsule.transform;
                var scale = capsuleTransform.lossyScale;
                var radiusScale = movementCapsule.direction == 0
                    ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                    : movementCapsule.direction == 2
                        ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
                        : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                radius = Mathf.Max(0.01f, movementCapsule.radius * radiusScale);
                var axisScale = movementCapsule.direction == 0 ? Mathf.Abs(scale.x)
                    : movementCapsule.direction == 2 ? Mathf.Abs(scale.z) : Mathf.Abs(scale.y);
                var localAxis = movementCapsule.direction == 0 ? Vector3.right
                    : movementCapsule.direction == 2 ? Vector3.forward : Vector3.up;
                var axis = capsuleTransform.TransformDirection(localAxis).normalized;
                halfSegment = axis * Mathf.Max(0f, movementCapsule.height * axisScale * 0.5f - radius);
            }
            const float startInset = 0.05f;
            const float wallSearchDistance = 0.35f;
            var startCenter = center - direction * startInset;
            return new CapsulecastCommand(startCenter + halfSegment, startCenter - halfSegment,
                radius, direction, query, startInset + wallSearchDistance);
        }

        internal void ApplyWallProbes(RaycastHit forward, RaycastHit left, RaycastHit right)
        {
            var best = default(RaycastHit);
            TrySelectWallHit(forward, ref best);
            TrySelectWallHit(left, ref best);
            TrySelectWallHit(right, ref best);
            if (best.collider != null)
            {
                hasDetectedWall = true;
                detectedWallNormal = best.normal;
                detectedWallDistance = Mathf.Max(0f, best.distance - 0.05f);
                slopeContactResolver?.SetSyntheticObstacleContact(wallProbeContactId, best.normal);
            }
            else
            {
                hasDetectedWall = false;
                detectedWallNormal = Vector3.zero;
                detectedWallDistance = 0f;
                slopeContactResolver?.RemoveSyntheticContact(wallProbeContactId);
            }
        }

        internal void ClearWallProbe()
        {
            hasDetectedWall = false;
            detectedWallNormal = Vector3.zero;
            detectedWallDistance = 0f;
            slopeContactResolver?.RemoveSyntheticContact(wallProbeContactId);
        }

        private void TrySelectWallHit(RaycastHit candidate, ref RaycastHit best)
        {
            if (candidate.collider == null || Mathf.Abs(Vector3.Dot(candidate.normal, UpAxis)) >= settings.MinGroundNormalDot)
                return;
            if (best.collider == null || candidate.distance < best.distance)
                best = candidate;
        }

        internal void ApplyGroundProbe(
            RaycastHit hit,
            ColliderHit overlapHit0,
            ColliderHit overlapHit1,
            ColliderHit overlapHit2,
            ColliderHit overlapHit3)
        {
            var valid = hit.collider != null && Vector3.Dot(hit.normal, UpAxis) >= settings.MinGroundNormalDot;
            if (!valid)
            {
                var overlapCollider = SelectGroundOverlap(
                    overlapHit0.collider,
                    overlapHit1.collider,
                    overlapHit2.collider,
                    overlapHit3.collider);
                if (grounded && overlapCollider != null)
                {
                    consecutiveGroundMisses = 0;
                    hasGroundSurface = true;
                    movingPlatform.Sample(overlapCollider, body.position, simulationDeltaTime,
                        out groundVelocity, out groundDisplacement, out groundRotationDelta);
                    groundCoordinate += Vector3.Dot(groundDisplacement, UpAxis);
                    return;
                }
                consecutiveGroundMisses++;
                var movingPlatformRetentionDistance = groundClearance
                    + groundProbeRadius
                    + Mathf.Max(0.25f, settings.NearbyGroundDistance);
                if (grounded && (consecutiveGroundMisses <= 2
                    || movingPlatform.CanRetainMovingPlatformBinding(
                        body.position,
                        movingPlatformRetentionDistance)))
                {
                    // Keep moving-platform binding while its collider remains within the
                    // reachable support volume. A fixed two-tick grace was too short for
                    // vertical + rotating floors and caused non-deterministic fall-through.
                    movingPlatform.SampleBound(body.position, simulationDeltaTime,
                        out groundVelocity, out groundDisplacement, out groundRotationDelta);
                    groundCoordinate += Vector3.Dot(groundDisplacement, UpAxis);
                    return;
                }
                hasGroundSurface = false;
                grounded = false;
                groundDisplacement = Vector3.zero;
                groundVelocity = Vector3.zero;
                groundRotationDelta = Quaternion.identity;
                movingPlatform.Clear();
                return;
            }
            consecutiveGroundMisses = 0;
            hasGroundSurface = true;
            // Derive the body coordinate from the swept distance. This remains stable at
            // capsule seams and slopes where RaycastHit.point can jump across the footprint.
            groundCoordinate = Vector3.Dot(body.position, UpAxis) + groundProbeLift - hit.distance;
            if (VerticalVelocity <= 0f)
            {
                var gap = Vector3.Dot(body.position, UpAxis) - groundCoordinate;
                grounded = gap <= Mathf.Max(0.05f, settings.NearbyGroundDistance);
            }
            movingPlatform.Sample(hit.collider, body.position, simulationDeltaTime, out groundVelocity, out groundDisplacement, out groundRotationDelta);
        }

        private Collider SelectGroundOverlap(Collider hit0, Collider hit1, Collider hit2, Collider hit3)
        {
            if (movingPlatform.IsBoundTo(hit0)) return hit0;
            if (movingPlatform.IsBoundTo(hit1)) return hit1;
            if (movingPlatform.IsBoundTo(hit2)) return hit2;
            if (movingPlatform.IsBoundTo(hit3)) return hit3;

            var best = default(Collider);
            var bestUpDot = settings.MinGroundNormalDot;
            SelectGroundOverlapCandidate(hit0, ref best, ref bestUpDot);
            SelectGroundOverlapCandidate(hit1, ref best, ref bestUpDot);
            SelectGroundOverlapCandidate(hit2, ref best, ref bestUpDot);
            SelectGroundOverlapCandidate(hit3, ref best, ref bestUpDot);
            return best;
        }

        private void SelectGroundOverlapCandidate(Collider candidate, ref Collider best, ref float bestUpDot)
        {
            if (movementCapsule == null || candidate == null || candidate == movementCapsule || candidate.isTrigger
                || candidate.transform.IsChildOf(transform))
                return;
            if (!Physics.ComputePenetration(
                    movementCapsule,
                    body.position,
                    body.rotation,
                    candidate,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    out var direction,
                    out _))
                return;
            var upDot = Vector3.Dot(direction, UpAxis);
            if (upDot < bestUpDot)
                return;
            best = candidate;
            bestUpDot = upDot;
        }

        internal void ResolveEnvironmentOverlaps(ColliderHit hit0, ColliderHit hit1, ColliderHit hit2, ColliderHit hit3)
        {
            if (movementCapsule == null)
                return;
            ResolveEnvironmentOverlap(hit0.collider);
            ResolveEnvironmentOverlap(hit1.collider);
            ResolveEnvironmentOverlap(hit2.collider);
            ResolveEnvironmentOverlap(hit3.collider);
        }

        private void ResolveEnvironmentOverlap(Collider other)
        {
            if (other == null || other == movementCapsule || !other.enabled || other.isTrigger ||
                other.transform.IsChildOf(transform))
                return;
            if (!Physics.ComputePenetration(
                    movementCapsule,
                    body.position,
                    body.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out var direction,
                    out var distance))
                return;
            // Ground overlap is resolved by the ground query. Only horizontal/steep
            // contacts belong to the wall solver.
            if (Vector3.Dot(direction, UpAxis) >= settings.MinGroundNormalDot)
                return;

            body.position += direction * distance;
            // Transfer the pre-resolution closing velocity to the dynamic body.
            // Resolving our motor velocity first would make the physical impulse zero.
            ApplyOverlapImpulse(other, direction, body.position);
            var inwardSpeed = Vector3.Dot(velocity, direction);
            if (inwardSpeed < 0f)
                velocity -= direction * inwardSpeed;
            detectedWallNormal = direction;
            detectedWallDistance = 0f;
            hasDetectedWall = true;
            slopeContactResolver?.SetSyntheticObstacleContact(wallProbeContactId, direction);
        }

        private void ApplyOverlapImpulse(Collider other, Vector3 separationDirection,
            Vector3 contactPosition)
        {
            if (!contactSettings.ApplyImpulse)
                return;
            var otherBody = other.attachedRigidbody;
            if (otherBody == null || otherBody == body || otherBody.isKinematic)
                return;
            var relativeVelocity = velocity - otherBody.linearVelocity;
            var inwardSpeed = Mathf.Max(0f, -Vector3.Dot(relativeVelocity, separationDirection));
            var impulse = CalculateDynamicContactImpulse(otherBody, inwardSpeed);
            otherBody.AddForceAtPosition(-separationDirection * impulse, contactPosition, ForceMode.Impulse);
            otherBody.WakeUp();
        }

        private float CalculateDynamicContactImpulse(Rigidbody otherBody, float inwardSpeed)
        {
            if (inwardSpeed <= 0f)
                return 0f;
            var inverseNpcMass = 1f / Mathf.Max(body.mass, Mathf.Epsilon);
            var inverseOtherMass = 1f / Mathf.Max(otherBody.mass, Mathf.Epsilon);
            return inwardSpeed / (inverseNpcMass + inverseOtherMass);
        }

        internal NpcCrowdMovementData CaptureMovementData()
        {
            var acceleration = grounded ? settings.GroundAcceleration : settings.AirAcceleration;
            return new NpcCrowdMovementData
            {
                Position = body.position,
                Velocity = velocity,
                DesiredPlanarVelocity = desiredPlanarVelocity,
                UpAxis = UpAxis,
                GroundDisplacement = groundDisplacement,
                GroundVelocity = groundVelocity,
                GroundCoordinate = groundCoordinate,
                HasGroundSurface = hasGroundSurface ? 1 : 0,
                MoveSpeed = settings.MoveSpeed,
                Acceleration = acceleration,
                RotationSpeed = settings.RotationSpeed,
                JumpSpeed = settings.JumpForce,
                Gravity = Vector3.Dot(Physics.gravity, UpAxis),
                Grounded = grounded ? 1 : 0,
                JumpRequested = jumpRequested ? 1 : 0,
                AirborneFromJump = airborneFromJump ? 1 : 0
                ,TraversalMode = (int)traversalState
                ,WireAnchor = wireAnchor
                ,WireRopeLength = wireRopeLength
                ,WallNormal = detectedWallNormal
                ,WallDistance = detectedWallDistance
                ,HasWall = hasDetectedWall ? 1 : 0
            };
        }

        internal void ApplyMovement(NpcCrowdMovementResult result)
        {
            jumpRequested = false;
            velocity = result.Velocity;
            grounded = result.Grounded != 0;
            airborneFromJump = result.AirborneFromJump != 0;
            body.position = result.Position;
            body.rotation = groundRotationDelta * body.rotation;
            var planar = Vector3.ProjectOnPlane(velocity - groundVelocity, UpAxis);
            if (planar.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(planar.normalized, UpAxis);
                body.rotation = Quaternion.RotateTowards(body.rotation, target, settings.RotationSpeed * simulationDeltaTime);
            }
            if (!grounded)
            {
                groundDisplacement = Vector3.zero;
                groundVelocity = Vector3.zero;
                groundRotationDelta = Quaternion.identity;
            }
        }

        internal void ResolveNetworkPhysicsContacts()
        {
            if (movementCapsule == null)
                return;

            var objects = ServerDrivenNetworkRigidbody.InteractionInstances;
            if (contactSettings.EnableNetworkPhysicsObjectContacts)
            {
                for (var objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                {
                    var physicsObject = objects[objectIndex];
                    if (physicsObject == null || physicsObject.Body == null
                        || !physicsObject.CanReceiveAuthoritativeCrowdContact)
                        continue;
                    ResolveExternalBodyContact(physicsObject.Body, physicsObject.InteractionColliders);
                }
            }

            if (contactSettings.EnablePlayerContacts)
            {
                var players = CrowdPhysicsBodyRegistry.Players;
                for (var playerIndex = 0; playerIndex < players.Count; playerIndex++)
                    ResolveExternalBodyContact(players[playerIndex].Body, players[playerIndex].Colliders);
            }
        }

        private void ResolveExternalBodyContact(Rigidbody otherBody, Collider[] colliders)
        {
            if (otherBody == null || colliders == null || otherBody == body)
                return;

            var npcPosition = body.position;
            var npcRotation = body.rotation;
            for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                var other = colliders[colliderIndex];
                if (other == null || !other.enabled || other.isTrigger)
                    continue;
                var reach = groundClearance + groundProbeRadius + Mathf.Max(0f, contactSettings.BroadphasePadding);
                if (other.bounds.SqrDistance(npcPosition) > reach * reach)
                    continue;
                if (!Physics.ComputePenetration(
                        movementCapsule,
                        npcPosition,
                        npcRotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out var separationDirection,
                        out var separationDistance))
                    continue;

                npcPosition += separationDirection * separationDistance * Mathf.Clamp01(contactSettings.PenetrationResolution);
                body.position = npcPosition;

                var relativeVelocity = velocity - otherBody.linearVelocity;
                var inwardSpeed = Mathf.Max(0f, -Vector3.Dot(relativeVelocity, separationDirection));
                velocity += separationDirection * inwardSpeed;
                if (contactSettings.ApplyImpulse && !otherBody.isKinematic)
                {
                    var impulse = CalculateDynamicContactImpulse(otherBody, inwardSpeed);
                    otherBody.AddForceAtPosition(-separationDirection * impulse, npcPosition, ForceMode.Impulse);
                    otherBody.WakeUp();
                }
            }
        }
    }
}
