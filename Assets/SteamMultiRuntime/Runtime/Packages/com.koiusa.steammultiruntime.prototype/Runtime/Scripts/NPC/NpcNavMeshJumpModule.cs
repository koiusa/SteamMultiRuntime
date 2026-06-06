using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public class NpcNavMeshJumpModule
    {
        [Header("Jump (Random)")]
        [SerializeField] private bool randomJumpEnabled = true;
        [SerializeField, Range(0f, 1f)] private float jumpChancePerSecond = 0.1f;
        [SerializeField, Min(0f)] private float jumpVerticalVelocity = 6f;
        [SerializeField, Min(1f)] private float fallMultiplier = 2f;
        [SerializeField, Min(0f)] private float jumpCooldownMin = 1.5f;
        [SerializeField, Min(0f)] private float jumpCooldownMax = 4.0f;
        [SerializeField, Min(0f)] private float minHorizontalSpeedToJump = 0.35f;

        private NavMeshAgent _agent;
        private bool _isJumpActive;
        private bool _jumpRequested;
        private float _nextJumpAllowedTime;
        private float _simulatedVerticalVelocity;

        public bool IsJumpActive => _isJumpActive;
        public bool IsJumping => _isJumpActive && _simulatedVerticalVelocity > 0f;
        public bool IsFallingAfterJump => _isJumpActive && _simulatedVerticalVelocity <= 0f;
        public float VerticalVelocity => _isJumpActive ? _simulatedVerticalVelocity : 0f;
        public float JumpVerticalVelocity => jumpVerticalVelocity;
        public float FallMultiplier => fallMultiplier;

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
            ResetState();
            ScheduleNextJump(true);
        }

        public void OnEnable()
        {
            ResetState();
            ScheduleNextJump(true);
        }

        public void OnDisable()
        {
            ResetState();
        }

        public void NormalizeSettings()
        {
            if (jumpCooldownMin < 0f)
                jumpCooldownMin = 0f;
            if (jumpCooldownMax < jumpCooldownMin)
                jumpCooldownMax = jumpCooldownMin;
            if (jumpVerticalVelocity < 0f)
                jumpVerticalVelocity = 0f;
            if (fallMultiplier < 1f)
                fallMultiplier = 1f;
            if (minHorizontalSpeedToJump < 0f)
                minHorizontalSpeedToJump = 0f;
            jumpChancePerSecond = Mathf.Clamp01(jumpChancePerSecond);
        }

        public void UpdateState()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                ResetState();
                return;
            }

            if (_isJumpActive)
            {
                var upAxis = PlayerMotor.GetUpAxis();
                var gravityAlongUp = Vector3.Dot(Physics.gravity, upAxis);
                var gravityScale = _simulatedVerticalVelocity < 0f ? fallMultiplier : 1f;
                _simulatedVerticalVelocity += gravityAlongUp * gravityScale * Time.deltaTime;
            }

            if (!randomJumpEnabled || _isJumpActive)
                return;

            if (Time.time < _nextJumpAllowedTime)
                return;

            if (_agent.isStopped || _agent.pathPending || !_agent.hasPath)
                return;

            var planarSpeed = Vector3.ProjectOnPlane(_agent.desiredVelocity, PlayerMotor.GetUpAxis()).magnitude;
            if (planarSpeed < minHorizontalSpeedToJump)
                return;

            var chanceThisFrame = jumpChancePerSecond * Time.deltaTime;
            if (Random.value > chanceThisFrame)
                return;

            _jumpRequested = true;
            ScheduleNextJump();
        }

        public bool ConsumeJumpRequest()
        {
            if (!_jumpRequested)
                return false;

            _jumpRequested = false;
            return true;
        }

        public void NotifyJumpStarted(float initialVerticalVelocity)
        {
            _isJumpActive = true;
            _simulatedVerticalVelocity = initialVerticalVelocity;
        }

        public void NotifyLanded()
        {
            _isJumpActive = false;
            _simulatedVerticalVelocity = 0f;
        }

        private void ResetState()
        {
            _isJumpActive = false;
            _jumpRequested = false;
            _simulatedVerticalVelocity = 0f;
        }

        private void ScheduleNextJump(bool allowImmediate = false)
        {
            var minCooldown = jumpCooldownMin;
            var maxCooldown = Mathf.Max(minCooldown, jumpCooldownMax);
            if (allowImmediate)
            {
                _nextJumpAllowedTime = Time.time + Random.Range(0f, maxCooldown);
                return;
            }

            _nextJumpAllowedTime = Time.time + Random.Range(minCooldown, maxCooldown);
        }
    }
}
