using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public class NpcNavMeshSpeedModule
    {
        [SerializeField] private ScaleSettings scale = new();
        [SerializeField] private ReturnToCenterSpeedSettings returnToCenter = new();

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

        private NavMeshAgent _agent;
        private float _moveSpeedScale = 1f;
        private float _baseAgentSpeed;

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
            if (agent != null)
            {
                _baseAgentSpeed = agent.speed;
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
            if (_agent == null)
                return;

            var baseSpeed = _baseAgentSpeed > 0f ? _baseAgentSpeed : _agent.speed;
            _agent.speed = Mathf.Max(0.01f, baseSpeed * _moveSpeedScale);
        }

        public void ApplyReturnToCenterSpeedBoost()
        {
            if (_agent == null)
                return;
            if (!returnToCenter.useBoost)
                return;

            var baseSpeed = _baseAgentSpeed > 0f ? _baseAgentSpeed : _agent.speed;
            _agent.speed = Mathf.Max(0.01f, baseSpeed * returnToCenter.scale);
        }

        public void NormalizeSettings()
        {
            var minScale = Mathf.Max(1f, scale.range.x);
            var maxScale = Mathf.Max(minScale, scale.range.y);
            scale.range = new Vector2(minScale, maxScale);

            if (returnToCenter.scale < 1f)
                returnToCenter.scale = 1f;
        }
    }
}
