using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Boundary between NPC decision logic and the shared Crowd simulation.
    /// The simulation depends on this adapter instead of NavMesh controller details.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class NpcCrowdAgent : MonoBehaviour
    {
        private NpcNavMeshController controller;
        private NpcCrowdMotor motor;
        private Rigidbody body;
        private NavMeshAgent navMeshAgent;
        private PhysicsPresentationSmoother presentationSmoother;
        private ServerDrivenActorController networkController;
        private Core.FallRecovery fallRecovery;
        private NpcCrowdMovingPlatformAction movingPlatform;
        private bool registered;

        internal void Initialize(NpcNavMeshController source)
        {
            controller = source;
            motor = GetComponent<NpcCrowdMotor>();
            body = GetComponent<Rigidbody>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            presentationSmoother = GetComponent<PhysicsPresentationSmoother>();
            networkController = GetComponent<ServerDrivenActorController>();
            fallRecovery = GetComponent<Core.FallRecovery>();
            if (movingPlatform != null)
                movingPlatform.PhysicsPoseSourceBindingChanged -= OnPhysicsPoseSourceBindingChanged;
            movingPlatform = GetComponent<NpcCrowdMovingPlatformAction>();
            if (movingPlatform != null)
                movingPlatform.PhysicsPoseSourceBindingChanged += OnPhysicsPoseSourceBindingChanged;
        }

        internal void Activate()
        {
            if (registered || controller == null)
                return;
            registered = true;
            NpcCrowdSimulation.Register(this);
        }

        internal void Deactivate()
        {
            if (!registered)
                return;
            registered = false;
            NpcCrowdSimulation.Unregister(this);
        }

        private void OnDestroy()
        {
            Deactivate();
            if (movingPlatform != null)
                movingPlatform.PhysicsPoseSourceBindingChanged -= OnPhysicsPoseSourceBindingChanged;
        }

        private void OnPhysicsPoseSourceBindingChanged(IGroundMotionPhysicsPoseSource source) =>
            NpcCrowdSimulation.SetMovingPlatformBinding(this, source);

        internal void TickCrowdSkill(float deltaTime) => controller.TickCrowdSkill(deltaTime);

        internal void TickCrowdNavigation(bool observeMovementState) =>
            controller.TickCrowdNavigation(observeMovementState);

        internal Vector3 Position => body.position;

        internal void TickRecovery() => fallRecovery?.TickRecovery();

        internal void BeginSimulationStep(float deltaTime) => motor.BeginSimulationStep(deltaTime);

        internal void BuildAndApplyCommand() => motor.ApplyCommand(controller.BuildCrowdCommand());

        internal NpcCrowdAgentData CaptureAgentData(Vector3 upAxis) =>
            controller.CaptureCrowdAgentData(body.position, motor.Velocity, upAxis);
        internal NpcCrowdMovementData CaptureMovementData() => motor.CaptureMovementData();
        internal void ApplySteering(float3 value)
        {
            if (controller.TryApplyCrowdSteering(value, out var desiredVelocity))
                motor.SetCommand(desiredVelocity, false);
        }

        internal void ApplyMovement(NpcCrowdMovementResult result, float deltaTime)
        {
            motor.ApplyMovement(result);
            // A physics-pose floor supplies fixed-rate samples immediately after it
            // moves. Mixing these 50 Hz samples with the 30 Hz Crowd sample stream
            // continually changes the interpolator interval and appears as judder.
            // While bound, the floor notification is the sole presentation clock.
            if (!motor.UsesMovingPlatformPhysicsPresentation)
                presentationSmoother?.CapturePhysicsPose(deltaTime);
            // Movement is the authoritative Crowd pose update. Synchronize the
            // NavMesh shadow position here instead of repeating it every render frame.
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                navMeshAgent.nextPosition = body.position;
            networkController?.ApplyServerNpcCrowdState(
                motor.HorizontalVelocity,
                motor.VerticalVelocity,
                motor.IsGrounded,
                motor.IsJumping,
                motor.IsFreefall,
                motor.IsFallingAfterJump);
        }

        internal void FollowMovingPlatformPhysicsPose(IGroundMotionPhysicsPoseSource source, float deltaTime)
        {
            if (!motor.FollowMovingPlatformPhysicsPose(source, deltaTime))
                return;
            presentationSmoother?.CapturePhysicsPose(deltaTime);
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                navMeshAgent.nextPosition = body.position;
        }

        internal void IgnorePhysicsPair(Collider collider) => movingPlatform?.IgnorePhysicsPair(collider);

        internal void ClearMovingPlatformSource(IGroundMotionPhysicsPoseSource source) =>
            movingPlatform?.ClearPhysicsPoseSource(source);

        internal void CreateGroundProbes(out CapsulecastCommand cast, out OverlapCapsuleCommand overlap) =>
            motor.CreateGroundProbes(out cast, out overlap);
        internal void ApplyGroundProbe(
            RaycastHit hit,
            ColliderHit overlap0,
            ColliderHit overlap1,
            ColliderHit overlap2,
            ColliderHit overlap3) =>
            motor.ApplyGroundProbe(hit, overlap0, overlap1, overlap2, overlap3);
        internal void ResolveEnvironmentOverlaps(ColliderHit hit0, ColliderHit hit1, ColliderHit hit2, ColliderHit hit3) =>
            motor.ResolveEnvironmentOverlaps(hit0, hit1, hit2, hit3);

        internal bool ShouldProbeWalls => motor.ShouldProbeWalls;
        internal bool ShouldProbeWallsEveryStep => motor.ShouldProbeWallsEveryFixedStep;
        internal void ClearWallProbe() => motor.ClearWallProbe();
        internal void CreateWallProbes(out CapsulecastCommand forward, out CapsulecastCommand left, out CapsulecastCommand right) =>
            motor.CreateWallProbes(controller.CrowdMoveDirection, out forward, out left, out right);
        internal void ApplyWallProbes(RaycastHit forward, RaycastHit left, RaycastHit right) =>
            motor.ApplyWallProbes(forward, left, right);
    }
}
