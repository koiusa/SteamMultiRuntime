using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcNavMeshJumpModule : MonoBehaviour
    {
        [Header("Jump (Random)")]
        [SerializeField] private bool randomJumpEnabled = true;
        [SerializeField, Range(0f, 1f)] private float jumpChancePerSecond = 0.1f;
        [SerializeField, Min(0f)] private float jumpCooldownMin = 1.5f;
        [SerializeField, Min(0f)] private float jumpCooldownMax = 4.0f;
        [SerializeField, Min(0f)] private float minHorizontalSpeedToJump = 0.35f;

        private float _nextJumpAllowedTime;

        private void OnEnable()
        {
            ScheduleNextJump(true);
        }

        private void OnValidate()
        {
            if (jumpCooldownMin < 0f)
                jumpCooldownMin = 0f;
            if (jumpCooldownMax < jumpCooldownMin)
                jumpCooldownMax = jumpCooldownMin;
            if (minHorizontalSpeedToJump < 0f)
                minHorizontalSpeedToJump = 0f;
            jumpChancePerSecond = Mathf.Clamp01(jumpChancePerSecond);
        }

        public bool TryRequestJump(bool isGrounded, float horizontalSpeed)
        {
            if (!isActiveAndEnabled || !randomJumpEnabled || !isGrounded)
                return false;

            if (Time.time < _nextJumpAllowedTime)
                return false;
            if (horizontalSpeed < minHorizontalSpeedToJump)
                return false;

            var chanceThisFrame = jumpChancePerSecond * Time.deltaTime;
            if (Random.value > chanceThisFrame)
                return false;

            ScheduleNextJump();
            return true;
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
