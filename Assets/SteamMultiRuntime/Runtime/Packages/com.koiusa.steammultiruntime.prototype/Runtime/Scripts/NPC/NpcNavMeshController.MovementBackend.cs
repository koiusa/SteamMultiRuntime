using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        private void ConfigureMovementBackend()
        {
            if (useCrowdSimulation)
            {
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
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.freezeRotation = true;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }
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

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;
            movement?.ObserveState();
            _agent.nextPosition = _rigidbody != null ? _rigidbody.position : transform.position;
        }

        private void FixedUpdate()
        {
            if (useCrowdSimulation || _clientSimulationDisabled)
                return;
            if (_inputSource == null)
                return;

            var input = BuildNpcInputCommand();
            if (_networkPlayerController != null)
            {
                _networkPlayerController.TickServerNpcPhysics(
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
                return;
            }
            if (_motor == null)
                return;

            _moveInputReceiver?.SetMoveInput(input.MoveInput);
            _moveInputReceiver?.SetMoveReferenceRotation(transform.rotation);
            _motor.Tick(input.MoveDirection, input.JumpRequested);
            _presentationSmoother?.CapturePhysicsPose();
        }
    }
}
