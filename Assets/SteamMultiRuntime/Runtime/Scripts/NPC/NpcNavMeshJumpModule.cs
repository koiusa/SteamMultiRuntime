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
        [SerializeField, Min(0.1f)] private float jumpDuration = 0.45f;
        [SerializeField, Min(0f)] private float jumpVerticalVelocity = 2.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 0.45f;
        [SerializeField, Min(0f)] private float jumpCooldownMin = 1.5f;
        [SerializeField, Min(0f)] private float jumpCooldownMax = 4.0f;
        [SerializeField, Min(0f)] private float minHorizontalSpeedToJump = 0.35f;

        private NavMeshAgent _agent;
        private bool _isJumpActive;
        private float _jumpStartedTime;
        private float _nextJumpAllowedTime;
        private float _simulatedVerticalVelocity;
        private float _baseOffset;

        public bool IsJumpActive => _isJumpActive;
        public bool IsJumping => _isJumpActive && _simulatedVerticalVelocity > 0f;
        public bool IsFallingAfterJump => _isJumpActive && _simulatedVerticalVelocity <= 0f;
        public float VerticalVelocity => _isJumpActive ? _simulatedVerticalVelocity : 0f;

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
            _baseOffset = _agent != null ? _agent.baseOffset : 0f;
            ResetState();
            ScheduleNextJump(true);
        }

        public void OnEnable()
        {
            if (_agent != null)
                _baseOffset = _agent.baseOffset;
            ResetState();
            ScheduleNextJump(true);
        }

        public void OnDisable()
        {
            ResetState();
        }

        public void NormalizeSettings()
        {
            if (jumpDuration < 0.1f)
                jumpDuration = 0.1f;
            if (jumpCooldownMin < 0f)
                jumpCooldownMin = 0f;
            if (jumpCooldownMax < jumpCooldownMin)
                jumpCooldownMax = jumpCooldownMin;
            if (jumpVerticalVelocity < 0f)
                jumpVerticalVelocity = 0f;
            if (jumpHeight < 0f)
                jumpHeight = 0f;
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

            if (!randomJumpEnabled)
            {
                ResetState();
                return;
            }

            if (_isJumpActive)
            {
                var elapsed = Time.time - _jumpStartedTime;
                if (elapsed >= jumpDuration)
                {
                    ResetState();
                    ScheduleNextJump();
                    return;
                }

                var halfDuration = jumpDuration * 0.5f;
                var vertical = jumpVerticalVelocity;
                _simulatedVerticalVelocity = elapsed < halfDuration ? vertical : -vertical;

                var t = Mathf.Clamp01(elapsed / jumpDuration);
                var height = 4f * jumpHeight * t * (1f - t);
                _agent.baseOffset = _baseOffset + height;
                return;
            }

            if (Time.time < _nextJumpAllowedTime)
                return;

            if (_agent.isStopped || _agent.pathPending || !_agent.hasPath)
                return;

            var planarSpeed = Vector3.ProjectOnPlane(_agent.velocity, PlayerMotor.GetUpAxis()).magnitude;
            if (planarSpeed < minHorizontalSpeedToJump)
                return;

            var chanceThisFrame = jumpChancePerSecond * Time.deltaTime;
            if (Random.value > chanceThisFrame)
                return;

            _isJumpActive = true;
            _jumpStartedTime = Time.time;
            _simulatedVerticalVelocity = jumpVerticalVelocity;
            _baseOffset = _agent.baseOffset;
        }

        private void ResetState()
        {
            _isJumpActive = false;
            _jumpStartedTime = 0f;
            _simulatedVerticalVelocity = 0f;
            if (_agent != null)
                _agent.baseOffset = _baseOffset;
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
