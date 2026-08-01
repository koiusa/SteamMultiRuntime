using Unity.Mathematics;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        internal void TickCrowdUpdate(float deltaTime)
        {
            _skillCoordinator?.TickSkills(deltaTime);

            if (_networkPlayerController != null)
            {
                if (!_networkPlayerController.IsSpawned)
                    return;
                if (!_networkPlayerController.IsServer)
                {
                    DisableClientSimulation();
                    return;
                }
            }

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;
            if (movement != null && movement.isActiveAndEnabled)
                movement.ObserveState();
            _agent.nextPosition = _rigidbody != null ? _rigidbody.position : transform.position;
        }

        private void DisableClientSimulation()
        {
            if (_clientSimulationDisabled)
                return;
            _clientSimulationDisabled = true;

            // Remote clients display authoritative NetworkTransform state and must not
            // duplicate NavMesh, AI, query or movement work performed by the server.
            if (movement != null) movement.enabled = false;
            if (speed != null) speed.enabled = false;
            if (jump != null) jump.enabled = false;
            if (steering != null) steering.enabled = false;
            if (avoidance != null) avoidance.enabled = false;
            if (_agent != null) _agent.enabled = false;
            _inputSource?.Disable();
            enabled = false;
        }

        internal NpcCrowdCommand BuildCrowdCommand()
        {
            UpdateAiInputSignal();
            var inputState = _inputSource.ReadState();
            _moveInput = inputState.Move;
            var jumpRequested = inputState.JumpPressed;
            if (_traversalTestDriver != null && _traversalTestDriver.IsControlling)
                jumpRequested = false;
            if (_traversalTestDriver != null && _traversalTestDriver.ShouldTick)
                _traversalTestDriver.TickTest(_traversalInput, _traversalCoordinator, IsGrounded);
            _traversalInput.Consume(ref _moveInput, ref jumpRequested, out var wireHeld,
                out var wireFire, out var reelInput, out var wireTarget);
            _moveDirection = ActorMotor.GetMoveDirection(transform, _moveInput);
            var wireOrigin = _rigidbody.worldCenterOfMass;
            if (wireHeld)
                _traversalCoordinator?.SetWireAimCursor(default, false, wireOrigin, wireTarget, true);
            _traversalCoordinator?.SetWireInput(wireHeld, wireFire, reelInput, wireOrigin, wireTarget);
            _traversalCoordinator?.ProcessMotorInput(_moveDirection, jumpRequested, IsGrounded);
            _traversalCoordinator?.ApplyTraversal(_moveDirection, _moveInput, transform.rotation, jumpRequested, IsGrounded);
            return new NpcCrowdCommand
            {
                DesiredVelocity = _moveDirection * MaxMoveSpeed,
                JumpRequested = jumpRequested,
                TraversalState = _traversalCoordinator != null
                    ? _traversalCoordinator.CurrentState
                    : ActorTraversalState.Grounded,
                WireAnchor = _traversalCoordinator != null ? _traversalCoordinator.WireAnchorPoint : Vector3.zero,
                WireRopeLength = _traversalCoordinator != null ? _traversalCoordinator.WireRopeLength : 0f
            };
        }

        internal Vector3 CrowdMoveDirection => _moveDirection;

        internal NpcCrowdAgentData CaptureCrowdAgentData()
        {
            var mode = avoidance != null ? (int)avoidance.Mode : 0;
            var isRvo = mode == (int)NpcNavMeshAvoidanceModule.AvoidanceMode.Rvo;
            return new NpcCrowdAgentData
            {
                Position = transform.position,
                Velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero,
                GoalVelocity = _cachedTargetPlanarVelocity,
                UpAxis = ActorMotor.GetUpAxis(),
                Radius = isRvo ? rvoNeighborRadius : boidSeparationRadius,
                TimeHorizon = isRvo ? rvoTimeHorizon : 1f,
                GoalWeight = isRvo ? rvoGoalWeight : boidGoalWeight,
                AvoidanceWeight = isRvo ? rvoAvoidanceWeight : boidSeparationWeight,
                SeparationExponent = isRvo ? 1f : boidSeparationExponent,
                MinApproachSpeed = isRvo ? rvoMinApproachSpeed : 0f,
                ForwardDotMin = isRvo ? -1f : boidNeighborForwardDotMin,
                MaxNeighbors = isRvo ? rvoMaxNeighbors : boidMaxNeighbors,
                Mode = mode,
                UseForwardFilter = !isRvo && boidUseForwardNeighborFilter ? 1 : 0
            };
        }

        internal bool TryApplyCrowdSteering(float3 steering, out Vector3 desiredVelocity)
        {
            if (_traversalTestDriver != null && _traversalTestDriver.IsControlling)
            {
                desiredVelocity = default;
                return false;
            }
            _crowdSteeringPlanar = new Vector3(steering.x, steering.y, steering.z);
            _hasCrowdSteering = true;
            desiredVelocity = _crowdSteeringPlanar;
            return true;
        }
    }
}
