using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Deterministic, opt-in input source for validating Crowd Motor traversal.
    /// It is ticked by NpcCrowdSimulation through NpcNavMeshController and does
    /// not add an Update/FixedUpdate callback per NPC.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcCrowdTraversalTestDriver : MonoBehaviour
    {
        public enum TestAction
        {
            None,
            MoveToTarget,
            Ladder,
            WallRun,
            Wire
        }

        [Header("Traversal Validation")]
        [SerializeField] private TestAction action = TestAction.MoveToTarget;
        [Tooltip("Place this at the approach point in front of a ladder/wall, or at the wire anchor for Wire.")]
        [SerializeField] private Transform target;
        [Min(0.05f)]
        [SerializeField] private float actionDistance = 1.25f;
        [Tooltip("Input used after reaching the target. (0,1) is forward/climb.")]
        [SerializeField] private Vector2 actionInput = Vector2.up;

        [Header("Wall Run")]
        [Min(0.1f)]
        [SerializeField] private float jumpInterval = 1f;

        [Header("Wire")]
        [SerializeField] private float wireReelInput;
        [Tooltip("Vertical offset from Target used as the grapple aim point.")]
        [SerializeField] private float wireAimHeightOffset = 1.5f;
        [Min(1f)]
        [SerializeField] private float wireApproachDistance = 30f;
        [Min(0.05f)]
        [SerializeField] private float wireRetryInterval = 0.25f;

        [Header("Runtime Status (Read Only)")]
        [SerializeField] private ActorTraversalState detectedState;
        [SerializeField] private float distanceToTarget;
        [SerializeField] private Vector2 sentMoveInput;
        [SerializeField] private string status = "Disabled";

        private bool ownsMoveOverride;
        private float nextWireFireTime;
        private float nextJumpTime;

        internal bool IsControlling => isActiveAndEnabled && action != TestAction.None && target != null;
        internal bool ShouldTick => ownsMoveOverride || IsControlling;

        internal void TickTest(NpcCrowdTraversalInput input, IActorTraversalCoordinator coordinator, bool isGrounded)
        {
            detectedState = coordinator != null ? coordinator.CurrentState : ActorTraversalState.Grounded;
            if (!isActiveAndEnabled || action == TestAction.None)
            {
                Release(input);
                status = "Disabled";
                return;
            }

            if (target == null)
            {
                Release(input);
                status = "Target is missing";
                return;
            }

            var up = ActorMotor.GetUpAxis();
            var toTarget = target.position - transform.position;
            var planar = Vector3.ProjectOnPlane(toTarget, up);
            distanceToTarget = planar.magnitude;

            if (action == TestAction.Wire)
            {
                var wireAimPoint = target.position + up * wireAimHeightOffset;
                var attached = coordinator != null && coordinator.IsWireAttached;
                if (!attached && toTarget.magnitude > wireApproachDistance && planar.sqrMagnitude > 0.0001f)
                {
                    var local = transform.InverseTransformDirection(planar.normalized);
                    SendMove(input, Vector2.ClampMagnitude(new Vector2(local.x, local.z), 1f));
                    input.SetWire(false, false, 0f, wireAimPoint);
                    status = $"Approaching wire range ({toTarget.magnitude:F2}m)";
                    return;
                }

                SendMove(input, attached ? actionInput : Vector2.zero);
                var fire = !attached && Time.time >= nextWireFireTime;
                if (fire)
                    nextWireFireTime = Time.time + wireRetryInterval;
                input.SetWire(true, fire, wireReelInput, wireAimPoint);
                if (attached && isGrounded && Time.time >= nextJumpTime)
                {
                    // Leave WireGround so the Crowd Motor enters its rope-constrained
                    // airborne WireSwing branch and the test visibly moves.
                    input.QueueJump();
                    nextJumpTime = Time.time + jumpInterval;
                }
                status = attached ? "WireSwing detected" : "Firing wire (retrying)";
                return;
            }

            input.SetWire(false, false, 0f, target.position);
            if (distanceToTarget > actionDistance)
            {
                var local = transform.InverseTransformDirection(planar / Mathf.Max(distanceToTarget, 0.0001f));
                SendMove(input, Vector2.ClampMagnitude(new Vector2(local.x, local.z), 1f));
                status = $"Approaching ({distanceToTarget:F2}m)";
                return;
            }

            switch (action)
            {
                case TestAction.MoveToTarget:
                    SendMove(input, Vector2.zero);
                    status = "Target reached";
                    break;
                case TestAction.Ladder:
                    SendMove(input, actionInput);
                    status = detectedState == ActorTraversalState.Ladder ? "Ladder detected" : "Waiting for ladder detection";
                    break;
                case TestAction.WallRun:
                    SendMove(input, actionInput);
                    if ((isGrounded || Time.time >= nextJumpTime) && Time.time >= nextJumpTime)
                    {
                        input.QueueJump();
                        nextJumpTime = Time.time + jumpInterval;
                    }
                    status = detectedState == ActorTraversalState.WallRun
                        ? "WallRun detected"
                        : detectedState == ActorTraversalState.WallSlide
                            ? "WallSlide detected"
                            : "Waiting for wall traversal";
                    break;
            }
        }

        private void SendMove(NpcCrowdTraversalInput input, Vector2 value)
        {
            sentMoveInput = Vector2.ClampMagnitude(value, 1f);
            input.SetMove(sentMoveInput, true);
            ownsMoveOverride = true;
        }

        private void Release(NpcCrowdTraversalInput input)
        {
            if (input == null)
                return;
            if (ownsMoveOverride)
                input.ClearMoveOverride();
            input.SetWire(false, false, 0f, Vector3.zero);
            ownsMoveOverride = false;
            nextWireFireTime = 0f;
            sentMoveInput = Vector2.zero;
            distanceToTarget = 0f;
        }

        private void OnDisable()
        {
            var input = GetComponent<NpcCrowdTraversalInput>();
            if (input != null)
                Release(input);
            status = "Disabled";
        }

        private void OnValidate()
        {
            actionDistance = Mathf.Max(0.05f, actionDistance);
            jumpInterval = Mathf.Max(0.1f, jumpInterval);
            wireReelInput = Mathf.Clamp(wireReelInput, -1f, 1f);
            wireApproachDistance = Mathf.Max(1f, wireApproachDistance);
            wireRetryInterval = Mathf.Max(0.05f, wireRetryInterval);
            actionInput = Vector2.ClampMagnitude(actionInput, 1f);
        }
    }
}
