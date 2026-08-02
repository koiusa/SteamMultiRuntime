using Unity.Mathematics;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        internal void TickCrowdSkill(float deltaTime)
        {
            if (_skillCoordinator != null && _skillCoordinator.ActiveSkill != null)
                _skillCoordinator.TickSkills(deltaTime);
        }

        internal void TickCrowdNavigation(bool observeMovementState)
        {
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
            if (observeMovementState && movement != null && movement.isActiveAndEnabled)
                movement.ObserveExternalMotionState(
                    HorizontalVelocity,
                    _cachedTargetPlanarVelocity.magnitude,
                    _rigidbody != null ? _rigidbody.position : transform.position);
        }

        private void DisableClientSimulation()
        {
            if (_clientSimulationDisabled)
                return;
            _clientSimulationDisabled = true;
            RestoreOnDemandTraversalFeatures();

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
            var input = BuildNpcInputCommand();
            _traversalCoordinator?.ProcessMotorInput(input.MoveDirection, input.JumpRequested, IsGrounded);
            _traversalCoordinator?.ApplyTraversal(
                input.MoveDirection, input.MoveInput, transform.rotation, input.JumpRequested, IsGrounded);
            return new NpcCrowdCommand
            {
                DesiredVelocity = input.MoveDirection * MaxMoveSpeed,
                JumpRequested = input.JumpRequested,
                TraversalState = _traversalCoordinator != null
                    ? _traversalCoordinator.CurrentState
                    : ActorTraversalState.Grounded,
                WireAnchor = _traversalCoordinator != null ? _traversalCoordinator.WireAnchorPoint : Vector3.zero,
                WireRopeLength = _traversalCoordinator != null ? _traversalCoordinator.WireRopeLength : 0f
            };
        }

        private NpcControllerInputCommand BuildNpcInputCommand()
        {
            UpdateAiInputSignal();
            var inputState = _inputSource.ReadState();
            _moveInput = inputState.Move;
            var jumpRequested = inputState.JumpPressed;
            if (_traversalTestDriver != null && _traversalTestDriver.IsControlling)
                jumpRequested = false;
            if (_traversalTestDriver != null && _traversalTestDriver.ShouldTick)
                _traversalTestDriver.TickTest(_traversalInput, _traversalCoordinator, IsGrounded);
            if ((_traversalInput == null || !_traversalInput.HasPendingInput)
                && (_traversalTestDriver == null
                    || (!_traversalTestDriver.ShouldTick && !_traversalTestDriver.IsControlling))
                && !_onDemandWireFeaturesActive
                && !_onDemandWallFeaturesActive
                && !_onDemandLadderFeaturesActive)
            {
                return new NpcControllerInputCommand
                {
                    MoveInput = _moveInput,
                    MoveDirection = _moveDirection,
                    JumpRequested = jumpRequested
                };
            }

            var wireHeld = false;
            var wireFire = false;
            var reelInput = 0f;
            var wireTarget = Vector3.zero;
            var hasTraversalInput = _traversalInput != null && _traversalInput.HasPendingInput;
            var hasWallIntent = _traversalInput != null && _traversalInput.HasWallIntent;
            var hasLadderIntent = _traversalInput != null && _traversalInput.HasLadderIntent;
            if (hasTraversalInput)
            {
                _traversalInput.Consume(ref _moveInput, ref jumpRequested, out wireHeld,
                    out wireFire, out reelInput, out wireTarget);
            }
            UpdateOnDemandTraversalActivity(wireHeld || wireFire, hasWallIntent, hasLadderIntent);
            _moveDirection = ActorMotor.GetMoveDirection(transform, _moveInput);
            if (hasTraversalInput || _onDemandWireFeaturesActive)
            {
                var wireOrigin = _rigidbody.worldCenterOfMass;
                UpdateOnDemandWireFeatures(wireHeld || wireFire);
                if (wireHeld)
                    _traversalCoordinator?.SetWireAimCursor(default, false, wireOrigin, wireTarget, true);
                _traversalCoordinator?.SetWireInput(wireHeld, wireFire, reelInput, wireOrigin, wireTarget);
                SuspendDetachedOnDemandWireFeatures(wireHeld || wireFire);
            }
            return new NpcControllerInputCommand
            {
                MoveInput = _moveInput,
                MoveDirection = _moveDirection,
                JumpRequested = jumpRequested,
                WireHeld = wireHeld,
                WireFireRequested = wireFire,
                ReelInput = reelInput,
                WireTarget = wireTarget
            };
        }

        internal Vector3 CrowdMoveDirection => _moveDirection;

        private void RefreshCrowdAgentTemplate()
        {
            var mode = avoidance != null
                ? (int)avoidance.Mode
                : (int)NpcNavMeshAvoidanceModule.AvoidanceMode.Boid;
            var isRvo = mode == (int)NpcNavMeshAvoidanceModule.AvoidanceMode.Rvo;
            _crowdAgentTemplate = new NpcCrowdAgentData
            {
                Radius = avoidance == null ? 1.6f : isRvo ? rvoNeighborRadius : boidSeparationRadius,
                TimeHorizon = avoidance != null && isRvo ? rvoTimeHorizon : 1f,
                GoalWeight = avoidance == null ? 1f : isRvo ? rvoGoalWeight : boidGoalWeight,
                AvoidanceWeight = avoidance == null ? 0f : isRvo ? rvoAvoidanceWeight : boidSeparationWeight,
                SeparationExponent = avoidance != null && !isRvo ? boidSeparationExponent : 1f,
                MinApproachSpeed = avoidance != null && isRvo ? rvoMinApproachSpeed : 0f,
                ForwardDotMin = avoidance != null && !isRvo ? boidNeighborForwardDotMin : -1f,
                MaxNeighbors = avoidance == null ? 0 : isRvo ? rvoMaxNeighbors : boidMaxNeighbors,
                Mode = mode,
                UseForwardFilter = avoidance != null && !isRvo && boidUseForwardNeighborFilter ? 1 : 0
            };
        }

        internal NpcCrowdAgentData CaptureCrowdAgentData(
            Vector3 position,
            Vector3 velocity,
            Vector3 upAxis)
        {
            var data = _crowdAgentTemplate;
            data.Position = position;
            data.Velocity = velocity;
            data.GoalVelocity = _cachedTargetPlanarVelocity;
            data.UpAxis = upAxis;
            return data;
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
