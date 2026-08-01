using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        private NpcControllerInputCommand _conventionalInputCommand;
        private bool _hasConventionalInputCommand;

        private void ConfigureMovementBackend()
        {
            if (useCrowdSimulation)
            {
                _networkPlayerController?.SetServerNpcConventionalMotorEnabled(false);
                _crowdMotor = GetComponent<NpcCrowdMotor>();
                if (_crowdMotor == null)
                    _crowdMotor = gameObject.AddComponent<NpcCrowdMotor>();
                _crowdMotor.enabled = true;
                _crowdMotor.Initialize(_baseMotor, crowdContactSettings);

                _crowdAgent = GetComponent<NpcCrowdAgent>();
                if (_crowdAgent == null)
                    _crowdAgent = gameObject.AddComponent<NpcCrowdAgent>();
                _crowdAgent.enabled = true;
                _crowdAgent.Initialize(this);

                if (_presentationSmoother != null)
                    _presentationSmoother.enabled = false;

                if (_baseMotor is Behaviour baseMotorBehaviour)
                    baseMotorBehaviour.enabled = false;
                if (_motor != null)
                    _motor.enabled = false;
                return;
            }

            _crowdAgent = GetComponent<NpcCrowdAgent>();
            _networkPlayerController?.SetServerNpcConventionalMotorEnabled(true);
            if (_crowdAgent != null)
            {
                _crowdAgent.Deactivate();
                _crowdAgent.enabled = false;
            }

            _crowdMotor = GetComponent<NpcCrowdMotor>();
            if (_crowdMotor != null)
                _crowdMotor.enabled = false;

            if (_baseMotor is Behaviour conventionalBaseMotor)
                conventionalBaseMotor.enabled = true;
            if (_motor != null)
                _motor.enabled = true;
            if (_presentationSmoother != null)
                _presentationSmoother.enabled = true;
            var groundMotionTracker = GetComponent<GroundMotionTracker>();
            if (groundMotionTracker != null)
                groundMotionTracker.enabled = true;
            var slopeContactResolver = GetComponent<SlopeContactResolver>();
            if (slopeContactResolver != null)
                slopeContactResolver.enabled = true;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = true;
                _rigidbody.freezeRotation = true;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }
            var movementCapsule = GetComponent<CapsuleCollider>();
            if (movementCapsule != null)
                movementCapsule.isTrigger = false;

            NpcConventionalCollisionRegistry.Register(this, _rigidbody);
            RegisterSpatialNpc(this);
            if (_networkPlayerController == null)
                NpcConventionalPhysicsLoop.Register(this);
        }

        private void Update()
        {
            if (useCrowdSimulation || _clientSimulationDisabled)
                return;

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

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                movement?.ObserveState();
                _agent.nextPosition = _rigidbody != null ? _rigidbody.position : transform.position;
            }

            // AI planning and pseudo-input generation belong to the render update, as in
            // the pre-crowd controller. FixedUpdate may run several times while recovering
            // from a slow frame and must only consume the latest command.
            _conventionalInputCommand = BuildNpcInputCommand();
            _hasConventionalInputCommand = true;
            if (_networkPlayerController != null)
                SubmitConventionalNetworkInput(_conventionalInputCommand);
        }

        internal void TickConventionalPhysics()
        {
            if (useCrowdSimulation || _clientSimulationDisabled)
                return;
            if (_inputSource == null)
                return;

            if (!_hasConventionalInputCommand)
            {
                _conventionalInputCommand = BuildNpcInputCommand();
                _hasConventionalInputCommand = true;
            }
            var input = _conventionalInputCommand;
            _conventionalInputCommand.JumpRequested = false;
            _conventionalInputCommand.WireFireRequested = false;
            if (_networkPlayerController != null)
            {
                return;
            }
            if (_motor == null)
                return;

            _moveInputReceiver?.SetMoveInput(input.MoveInput);
            _moveInputReceiver?.SetMoveReferenceRotation(transform.rotation);
            _motor.Tick(input.MoveDirection, input.JumpRequested);
            _presentationSmoother?.CapturePhysicsPose();
        }

        private void SubmitConventionalNetworkInput(NpcControllerInputCommand input)
        {
            _networkPlayerController.SubmitServerNpcInput(
                new ActorInputState(
                    input.MoveInput,
                    input.JumpRequested,
                    input.WireHeld,
                    input.ReelInput,
                    false,
                    input.WireFireRequested),
                input.MoveDirection,
                transform.rotation,
                input.WireTarget);
            _conventionalInputCommand.JumpRequested = false;
            _conventionalInputCommand.WireFireRequested = false;
        }
    }
}
