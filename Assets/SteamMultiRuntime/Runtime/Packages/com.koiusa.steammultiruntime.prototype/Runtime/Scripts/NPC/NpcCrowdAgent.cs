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

        private void OnDestroy() => Deactivate();

        internal void TickPresentation(float deltaTime)
        {
            presentationSmoother?.TickPresentation();
            controller.TickCrowdUpdate(deltaTime);
        }

        internal void TickRecovery() => fallRecovery?.TickRecovery();

        internal void Prepare(float deltaTime)
        {
            motor.BeginSimulationStep(deltaTime);
            motor.ApplyCommand(controller.BuildCrowdCommand());
        }

        internal NpcCrowdAgentData CaptureAgentData() => controller.CaptureCrowdAgentData();
        internal NpcCrowdMovementData CaptureMovementData() => motor.CaptureMovementData();
        internal void ApplySteering(float3 value)
        {
            if (controller.TryApplyCrowdSteering(value, out var desiredVelocity))
                motor.SetCommand(desiredVelocity, false);
        }

        internal void ApplyMovement(NpcCrowdMovementResult result, float deltaTime)
        {
            motor.ApplyMovement(result);
            presentationSmoother?.CapturePhysicsPose(deltaTime);
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && motor.IsGrounded)
                navMeshAgent.nextPosition = body.position;
            networkController?.ApplyServerNpcCrowdState(
                motor.HorizontalVelocity,
                motor.VerticalVelocity,
                motor.IsGrounded,
                motor.IsJumping,
                motor.IsFreefall,
                motor.IsFallingAfterJump);
        }

        internal void CreateGroundProbes(out CapsulecastCommand cast, out OverlapCapsuleCommand overlap) =>
            motor.CreateGroundProbes(out cast, out overlap);
        internal void ApplyGroundProbe(RaycastHit hit, ColliderHit overlap) =>
            motor.ApplyGroundProbe(hit, overlap);
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
