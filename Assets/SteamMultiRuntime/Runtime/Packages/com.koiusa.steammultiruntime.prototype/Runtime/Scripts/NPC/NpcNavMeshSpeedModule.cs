using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcNavMeshSpeedModule : MonoBehaviour
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
        private IPlayerMotor _motor;
        private float _moveSpeedScale = 1f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _motor = GetComponent<IPlayerMotor>();
            RandomizeForSegment();
        }

        private void OnEnable()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_motor == null)
                _motor = GetComponent<IPlayerMotor>();
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

            var baseSpeed = GetBaseMoveSpeed();
            _agent.speed = Mathf.Max(0.01f, baseSpeed * _moveSpeedScale);
        }

        public void ApplyReturnToCenterSpeedBoost()
        {
            if (_agent == null)
                return;
            if (!returnToCenter.useBoost)
                return;

            var baseSpeed = GetBaseMoveSpeed();
            _agent.speed = Mathf.Max(0.01f, baseSpeed * returnToCenter.scale);
        }

        private float GetBaseMoveSpeed()
        {
            if (_motor != null)
            {
                var moveSpeed = _motor.GetSettings().MoveSpeed;
                if (moveSpeed > 0f)
                    return moveSpeed;
            }

            return _agent.speed;
        }

        public void NormalizeSettings()
        {
            var minScale = Mathf.Max(1f, scale.range.x);
            var maxScale = Mathf.Max(minScale, scale.range.y);
            scale.range = new Vector2(minScale, maxScale);

            if (returnToCenter.scale < 1f)
                returnToCenter.scale = 1f;
        }

        private void OnValidate()
        {
            NormalizeSettings();
            if (Application.isPlaying)
                ApplyAgentSpeedScale();
        }
    }
}
