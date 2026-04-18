using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public class NpcNavMeshSpeedModule
    {
        [SerializeField] private ScaleSettings scale = new();
        [SerializeField] private ReturnToCenterSpeedSettings returnToCenter = new();
        [SerializeField] private AccelerationSettings acceleration = new();

        [System.Serializable]
        public sealed class ScaleSettings
        {
            public Vector2 range = new(1.0f, 1.2f);
        }

        [System.Serializable]
        public sealed class ReturnToCenterSpeedSettings
        {
            public bool useBoost = true;
            [Min(1f)] public float scale = 1.35f;
        }

        [System.Serializable]
        public sealed class AccelerationSettings
        {
            public bool useBoost = true;
            [Min(1f)] public float scale = 2.25f;
            [Min(0.01f)] public float minValue = 20f;
        }

        private NavMeshAgent _agent;
        private float _moveSpeedScale = 1f;
        private float _baseAgentSpeed;
        private float _baseAgentAcceleration;

        public float BaseAgentSpeed => _baseAgentSpeed;

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
            if (agent != null)
            {
                _baseAgentSpeed = agent.speed;
                _baseAgentAcceleration = agent.acceleration;
            }
            RandomizeForSegment();
        }

        public void OnEnable()
        {
            ApplyAgentSpeedScale();
        }

        public void RandomizeForSegment()
        {
            _moveSpeedScale = Random.Range(scale.range.x, scale.range.y);
            ApplyAgentSpeedScale();
        }

        public void ApplyAgentSpeedScale()
        {
            if (_agent == null) return;
            var baseSpeed = _baseAgentSpeed > 0f ? _baseAgentSpeed : _agent.speed;
            _agent.speed = Mathf.Max(0.01f, baseSpeed * _moveSpeedScale);
            ApplyAgentAccelerationScale();
        }

        public void ApplyReturnToCenterSpeedBoost()
        {
            if (_agent == null)
                return;
            if (!returnToCenter.useBoost)
                return;

            var baseSpeed = _baseAgentSpeed > 0f ? _baseAgentSpeed : _agent.speed;
            _agent.speed = Mathf.Max(0.01f, baseSpeed * returnToCenter.scale);
            ApplyAgentAccelerationScale();
        }

        private void ApplyAgentAccelerationScale()
        {
            if (_agent == null)
                return;

            var baseAcceleration = _baseAgentAcceleration > 0f ? _baseAgentAcceleration : _agent.acceleration;
            var scaleValue = acceleration.useBoost ? acceleration.scale : 1f;
            var boosted = baseAcceleration * scaleValue;
            _agent.acceleration = Mathf.Max(0.01f, acceleration.minValue, boosted);
        }

        public void NormalizeSettings()
        {
            var minScale = Mathf.Max(1f, scale.range.x);
            var maxScale = Mathf.Max(minScale, scale.range.y);
            scale.range = new Vector2(minScale, maxScale);

            if (returnToCenter.scale < 1f)
                returnToCenter.scale = 1f;
            if (acceleration.scale < 1f)
                acceleration.scale = 1f;
            if (acceleration.minValue < 0.01f)
                acceleration.minValue = 0.01f;
        }
    }
}
