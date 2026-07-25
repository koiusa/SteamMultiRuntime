using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcNavMeshMovementModule : MonoBehaviour
    {
        [SerializeField] private PathSettings path = new();
        [SerializeField] private StuckSettings stuck = new();
        [SerializeField] private ReturnToCenterSettings returnToCenter = new();

        [System.Serializable]
        public sealed class PathSettings
        {
            public bool randomMoveEnabled = true;
            [Min(0f)] public float navMeshSearchRadius = 5f;
            [Min(0.1f)] public float radius = 8f;
            [Min(0.1f)] public float minDistance = 0.8f;
            [Min(0f)] public float initialDelayMax = 2.0f;
            [Min(0f)] public float reachedBuffer = 0.15f;
            [Min(0f)] public float repathCooldown = 0.1f;
            [Min(0f)] public float noPathRetryCooldown = 0.6f;
            [Min(1)] public int maxAttempts = 8;
            [Min(1)] public int maxConsecutiveFailures = 4;
            [Range(0f, 0.5f)] public float centerBiasWeight = 0.05f;
            public bool useWaitBeforeNextDestination = true;
            [Range(0f, 1f)] public float waitChance = 0.5f;
            [Min(0f)] public float waitDurationMin = 0f;
            [Min(0f)] public float waitDurationMax = 1.2f;
        }

        [System.Serializable]
        public sealed class StuckSettings
        {
            public bool repathWhenStuck = true;
            [Min(0f)] public float speedThreshold = 0.05f;
            [Min(0.1f)] public float timeout = 1.5f;
            [Min(0f)] public float remainingDistanceEpsilon = 0.03f;
            [Min(0.1f)] public float noProgressTimeout = 1.0f;
            [Min(0f)] public float movementEpsilon = 0.015f;
            [Min(0.1f)] public float noMovementTimeout = 0.8f;
            [Min(0f)] public float minDesiredSpeedForMovementCheck = 0.25f;
        }

        [System.Serializable]
        public sealed class ReturnToCenterSettings
        {
            [Min(0.1f)] public float maxDistance = 14f;
            [Range(0.1f, 1f)] public float exitRatio = 0.5f;
            [Min(0f)] public float targetRadius = 2f;
        }

        private NavMeshAgent _agent;
        private Transform _transform;
        private bool _isReturningToCenter;
        private Vector3 _moveCenter;
        private float _nextRepathAllowedTime;
        private int _consecutiveDestinationFailures;
        private float _lowSpeedDuration;
        private float _noProgressDuration;
        private float _noMovementDuration;
        private float _previousRemainingDistance;
        private bool _hasPreviousRemainingDistance;
        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;
        private bool _waitingBeforeNextRandomDestination;

        public Vector3 SmoothedMoveDirection { get; private set; }

        public event System.Action OnDestinationNeeded;
        public event System.Action OnReturnToCenterStarted;
        public event System.Action OnRandomDestinationNeeded;
        public event System.Action OnCenterDestinationNeeded;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _transform = transform;
            InitializeState();
        }

        private void InitializeState()
        {
            _moveCenter = _transform.position;

            var upAxis = PlayerMotor.GetUpAxis();
            SmoothedMoveDirection = GetRandomPlanarDirection(upAxis);
            _nextRepathAllowedTime = Time.time + Random.Range(0f, path.initialDelayMax);
            _consecutiveDestinationFailures = 0;
            _waitingBeforeNextRandomDestination = false;
            ResetStuckTracking();
        }

        private void OnEnable()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            if (_transform == null)
                _transform = transform;
            var upAxis = PlayerMotor.GetUpAxis();
            if (SmoothedMoveDirection.sqrMagnitude <= 0.0001f)
                SmoothedMoveDirection = GetRandomPlanarDirection(upAxis);
            _nextRepathAllowedTime = Time.time + Random.Range(0f, path.initialDelayMax);
            _consecutiveDestinationFailures = 0;
            _waitingBeforeNextRandomDestination = false;
            ResetStuckTracking();
        }

        public void ObserveState()
        {
            if (!path.randomMoveEnabled)
                return;

            var wasReturningToCenter = _isReturningToCenter;
            UpdateReturnToCenterState();

            if (wasReturningToCenter != _isReturningToCenter)
                return;

            UpdateDestinationOnArrival();
        }

        public void NormalizeSettings()
        {
            if (path.maxConsecutiveFailures < 1)
                path.maxConsecutiveFailures = 1;
            if (path.repathCooldown < 0f)
                path.repathCooldown = 0f;
            if (path.noPathRetryCooldown < 0f)
                path.noPathRetryCooldown = 0f;
            path.centerBiasWeight = Mathf.Clamp(path.centerBiasWeight, 0f, 0.5f);
            path.waitChance = Mathf.Clamp01(path.waitChance);
            if (path.waitDurationMin < 0f)
                path.waitDurationMin = 0f;
            if (path.waitDurationMax < path.waitDurationMin)
                path.waitDurationMax = path.waitDurationMin;
            if (returnToCenter.targetRadius < 0f)
                returnToCenter.targetRadius = 0f;
            if (returnToCenter.targetRadius > returnToCenter.maxDistance)
                returnToCenter.targetRadius = returnToCenter.maxDistance;
            if (stuck.remainingDistanceEpsilon < 0f)
                stuck.remainingDistanceEpsilon = 0f;
            if (stuck.noProgressTimeout < 0.1f)
                stuck.noProgressTimeout = 0.1f;
            if (stuck.movementEpsilon < 0f)
                stuck.movementEpsilon = 0f;
            if (stuck.noMovementTimeout < 0.1f)
                stuck.noMovementTimeout = 0.1f;
            if (stuck.minDesiredSpeedForMovementCheck < 0f)
                stuck.minDesiredSpeedForMovementCheck = 0f;
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        public void RequestRandomDestination()
        {
            if (_isReturningToCenter)
                return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            _waitingBeforeNextRandomDestination = false;
            ResetStuckTracking();
            var success = TrySetRandomDestination();
            _nextRepathAllowedTime = Time.time + (success ? path.repathCooldown : path.noPathRetryCooldown);
        }

        public void RequestCenterDestination()
        {
            if (!_isReturningToCenter)
                return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            _waitingBeforeNextRandomDestination = false;
            ResetStuckTracking();
            var success = SetCenterDestination();
            _nextRepathAllowedTime = Time.time + (success ? path.repathCooldown : path.noPathRetryCooldown);
        }

        private void ApplyDestination(Vector3 sampledPosition)
        {
            _agent.isStopped = false;
            _agent.SetDestination(sampledPosition);
        }

        private void ApplyDestinationWithEvent(Vector3 sampledPosition)
        {
            ApplyDestination(sampledPosition);
            OnDestinationNeeded?.Invoke();
        }

        private void UpdateReturnToCenterState()
        {
            var upAxis = PlayerMotor.GetUpAxis();
            var toCenter = Vector3.ProjectOnPlane(_moveCenter - _transform.position, upAxis);
            var distance = toCenter.magnitude;

            if (_isReturningToCenter)
            {
                var withinExitDistance = distance <= returnToCenter.maxDistance * returnToCenter.exitRatio;
                if (!withinExitDistance)
                    return;

                if (_agent.pathPending)
                    return;

                var reachedCenter = !_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + path.reachedBuffer;
                if (!reachedCenter)
                    return;

                _isReturningToCenter = false;
                _waitingBeforeNextRandomDestination = false;
                _nextRepathAllowedTime = Time.time + path.repathCooldown;
                ResetStuckTracking();
                return;
            }

            if (distance > returnToCenter.maxDistance)
            {
                _isReturningToCenter = true;
                _waitingBeforeNextRandomDestination = false;
                ResetStuckTracking();
                OnReturnToCenterStarted?.Invoke();
                OnCenterDestinationNeeded?.Invoke();
            }
        }

        private bool SetCenterDestination()
        {
            var upAxis = PlayerMotor.GetUpAxis();
            var target = _moveCenter;
            if (returnToCenter.targetRadius > 0.001f)
                target += GetRandomPlanarDirection(upAxis) * Random.Range(0f, returnToCenter.targetRadius);

            if (!TrySamplePosition(target, path.navMeshSearchRadius * 2f, out var sampled))
                return false;
            if (!IsWithinReturnBounds(sampled, upAxis))
                return false;

            ApplyDestinationWithEvent(sampled);
            return true;
        }

        private void UpdateDestinationOnArrival()
        {
            if (_isReturningToCenter)
            {
                if (Time.time < _nextRepathAllowedTime || _agent.pathPending)
                    return;

                if (!_agent.hasPath)
                {
                    _waitingBeforeNextRandomDestination = false;
                    OnCenterDestinationNeeded?.Invoke();
                    return;
                }

                if (ShouldForceRepathByPathStatus())
                {
                    _waitingBeforeNextRandomDestination = false;
                    ResetStuckTracking();
                    OnCenterDestinationNeeded?.Invoke();
                    return;
                }

                if (!stuck.repathWhenStuck)
                    return;

                var shouldRepathByNoProgress = UpdateNoProgressStuckState();
                var shouldRepathByNoMovement = UpdateNoMovementStuckState();

                var shouldRepathByLowSpeed = false;
                var planarSpeed = Vector3.ProjectOnPlane(_agent.velocity, PlayerMotor.GetUpAxis()).magnitude;
                if (_agent.remainingDistance > _agent.stoppingDistance + path.reachedBuffer && planarSpeed <= stuck.speedThreshold)
                {
                    _lowSpeedDuration += Time.deltaTime;
                    shouldRepathByLowSpeed = _lowSpeedDuration >= stuck.timeout;
                }
                else
                {
                    _lowSpeedDuration = 0f;
                }

                if (shouldRepathByNoProgress || shouldRepathByLowSpeed || shouldRepathByNoMovement)
                {
                    _waitingBeforeNextRandomDestination = false;
                    ResetStuckTracking();
                    OnCenterDestinationNeeded?.Invoke();
                }

                return;
            }

            if (Time.time < _nextRepathAllowedTime || _agent.pathPending)
                return;

            var noRandomPath = !_agent.hasPath;
            var randomReached = !noRandomPath && _agent.remainingDistance <= _agent.stoppingDistance + path.reachedBuffer;

            if (!noRandomPath && !randomReached)
            {
                _waitingBeforeNextRandomDestination = false;

                if (ShouldForceRepathByPathStatus())
                {
                    ResetStuckTracking();
                    OnRandomDestinationNeeded?.Invoke();
                    return;
                }

                if (!stuck.repathWhenStuck)
                    return;

                var shouldRepathByNoProgress = UpdateNoProgressStuckState();
                var shouldRepathByNoMovement = UpdateNoMovementStuckState();

                var shouldRepathByLowSpeed = false;
                var planarSpeed = Vector3.ProjectOnPlane(_agent.velocity, PlayerMotor.GetUpAxis()).magnitude;
                if (_agent.remainingDistance > _agent.stoppingDistance + path.reachedBuffer && planarSpeed <= stuck.speedThreshold)
                {
                    _lowSpeedDuration += Time.deltaTime;
                    shouldRepathByLowSpeed = _lowSpeedDuration >= stuck.timeout;
                }
                else
                {
                    _lowSpeedDuration = 0f;
                }

                if (shouldRepathByNoProgress || shouldRepathByLowSpeed || shouldRepathByNoMovement)
                {
                    ResetStuckTracking();
                    OnRandomDestinationNeeded?.Invoke();
                }

                return;
            }

            if (randomReached)
            {
                if (!_waitingBeforeNextRandomDestination)
                {
                    _waitingBeforeNextRandomDestination = true;
                    _nextRepathAllowedTime = Time.time + GetRandomWaitBeforeNextDestination();
                    if (Time.time < _nextRepathAllowedTime)
                        return;
                }
                else if (Time.time < _nextRepathAllowedTime)
                {
                    return;
                }
            }
            else
            {
                _waitingBeforeNextRandomDestination = false;
            }

            _waitingBeforeNextRandomDestination = false;
            ResetStuckTracking();
            OnRandomDestinationNeeded?.Invoke();
        }

        private float GetRandomWaitBeforeNextDestination()
        {
            if (!path.useWaitBeforeNextDestination)
                return 0f;
            if (path.waitChance <= 0f)
                return 0f;
            if (path.waitDurationMax <= 0f)
                return 0f;
            if (Random.value > path.waitChance)
                return 0f;

            return Random.Range(path.waitDurationMin, path.waitDurationMax);
        }

        private bool TrySetRandomDestination()
        {
            var upAxis = PlayerMotor.GetUpAxis();
            if (!TryResolveDesiredDirection(upAxis, out var desiredDirection))
                return false;

            if (TryFindBestRandomDestination(desiredDirection, upAxis, out var bestSampled, out var bestDelta))
            {
                SmoothedMoveDirection = bestDelta.normalized;
                ApplyDestinationWithEvent(bestSampled);
                _consecutiveDestinationFailures = 0;
                return true;
            }

            HandleRandomDestinationFailure(upAxis);
            return false;
        }

        private bool TryResolveDesiredDirection(Vector3 upAxis, out Vector3 desiredDirection)
        {
            desiredDirection = SmoothedMoveDirection;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
                desiredDirection = Vector3.ProjectOnPlane(_transform.forward, upAxis);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
                desiredDirection = Vector3.ProjectOnPlane(_transform.right, upAxis);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
                desiredDirection = GetRandomPlanarDirection(upAxis);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
                return false;

            desiredDirection = desiredDirection.normalized;
            return true;
        }

        private bool TryFindBestRandomDestination(Vector3 desiredDirection, Vector3 upAxis, out Vector3 bestSampled, out Vector3 bestDelta)
        {
            var minDistSqr = path.minDistance * path.minDistance;
            var bestScore = float.MinValue;
            var hasBest = false;
            bestSampled = Vector3.zero;
            bestDelta = Vector3.zero;

            for (var i = 0; i < path.maxAttempts; i++)
            {
                if (!TryGenerateCandidate(desiredDirection, upAxis, out var sampled))
                    continue;
                if (!IsWithinReturnBounds(sampled, upAxis))
                    continue;

                var delta = Vector3.ProjectOnPlane(sampled - _transform.position, upAxis);
                if (delta.sqrMagnitude < minDistSqr)
                    continue;

                var score = ScoreCandidate(sampled, delta, desiredDirection, upAxis);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestSampled = sampled;
                bestDelta = delta;
                hasBest = true;
            }

            return hasBest;
        }

        private bool TryGenerateCandidate(Vector3 desiredDirection, Vector3 upAxis, out Vector3 sampled)
        {
            var randomDirection = GetRandomPlanarDirection(upAxis);
            var searchDirection = Vector3.Slerp(desiredDirection, randomDirection, 0.5f).normalized;
            if (searchDirection.sqrMagnitude <= 0.0001f)
            {
                sampled = Vector3.zero;
                return false;
            }

            var dist = Random.Range(path.minDistance, path.radius);
            var candidate = _transform.position + searchDirection * dist;
            return TrySamplePosition(candidate, path.navMeshSearchRadius, out sampled);
        }

        private void HandleRandomDestinationFailure(Vector3 upAxis)
        {
            _consecutiveDestinationFailures++;
            if (_consecutiveDestinationFailures < path.maxConsecutiveFailures)
                return;

            SmoothedMoveDirection = GetRandomPlanarDirection(upAxis);
            ResetStuckTracking();
            _consecutiveDestinationFailures = 0;
        }

        private float ScoreCandidate(Vector3 sampledPosition, Vector3 planarDelta, Vector3 desiredDirection, Vector3 upAxis)
        {
            var distance = planarDelta.magnitude;
            var distanceScore = Mathf.Clamp01(distance / Mathf.Max(path.minDistance, path.radius));

            var dir = planarDelta.sqrMagnitude > 0.0001f ? planarDelta.normalized : desiredDirection;
            var directionScore = (Vector3.Dot(dir, desiredDirection) + 1f) * 0.5f;

            var fromCenter = Vector3.ProjectOnPlane(sampledPosition - _moveCenter, upAxis).magnitude;
            var centerScore = 1f - Mathf.Clamp01(fromCenter / Mathf.Max(0.1f, returnToCenter.maxDistance));

            var centerWeight = path.centerBiasWeight;
            var baseWeight = 1f - centerWeight;
            var directionWeight = baseWeight * 0.625f;
            var distanceWeight = baseWeight * 0.375f;

            return directionScore * directionWeight + distanceScore * distanceWeight + centerScore * centerWeight + Random.Range(0f, 0.03f);
        }

        private bool IsWithinReturnBounds(Vector3 position, Vector3 upAxis)
        {
            var fromCenter = Vector3.ProjectOnPlane(position - _moveCenter, upAxis).magnitude;
            return fromCenter <= returnToCenter.maxDistance;
        }

        private bool TrySamplePosition(Vector3 source, float maxDist, out Vector3 result)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent != null ? _agent.agentTypeID : 0,
                areaMask = _agent != null ? _agent.areaMask : NavMesh.AllAreas
            };

            if (NavMesh.SamplePosition(source, out var hit, maxDist, filter))
            {
                result = hit.position;
                return true;
            }

            result = source;
            return false;
        }

        private Vector3 GetRandomPlanarDirection(Vector3 upAxis)
        {
            var dir = Vector3.ProjectOnPlane(Random.insideUnitSphere, upAxis);
            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector3.ProjectOnPlane(_transform.forward, upAxis);
            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector3.ProjectOnPlane(_transform.right, upAxis);
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }

        private void ResetStuckTracking()
        {
            _lowSpeedDuration = 0f;
            _noProgressDuration = 0f;
            _noMovementDuration = 0f;
            _previousRemainingDistance = 0f;
            _hasPreviousRemainingDistance = false;
            _previousPosition = _transform != null ? _transform.position : Vector3.zero;
            _hasPreviousPosition = _transform != null;
        }

        private bool UpdateNoProgressStuckState()
        {
            if (_agent == null || !_agent.hasPath)
            {
                _noProgressDuration = 0f;
                _hasPreviousRemainingDistance = false;
                return false;
            }

            var remaining = _agent.remainingDistance;
            if (!_hasPreviousRemainingDistance)
            {
                _previousRemainingDistance = remaining;
                _hasPreviousRemainingDistance = true;
                _noProgressDuration = 0f;
                return false;
            }

            var progressed = _previousRemainingDistance - remaining;
            if (progressed <= stuck.remainingDistanceEpsilon)
                _noProgressDuration += Time.deltaTime;
            else
                _noProgressDuration = 0f;

            _previousRemainingDistance = remaining;
            return _noProgressDuration >= stuck.noProgressTimeout;
        }

        private bool UpdateNoMovementStuckState()
        {
            if (_agent == null || _transform == null || !_agent.hasPath)
            {
                _noMovementDuration = 0f;
                _hasPreviousPosition = false;
                return false;
            }

            var upAxis = PlayerMotor.GetUpAxis();
            var desiredPlanarSpeed = Vector3.ProjectOnPlane(_agent.desiredVelocity, upAxis).magnitude;
            if (desiredPlanarSpeed < stuck.minDesiredSpeedForMovementCheck)
            {
                _noMovementDuration = 0f;
                _previousPosition = _transform.position;
                _hasPreviousPosition = true;
                return false;
            }

            var currentPosition = _transform.position;
            if (!_hasPreviousPosition)
            {
                _previousPosition = currentPosition;
                _hasPreviousPosition = true;
                _noMovementDuration = 0f;
                return false;
            }

            var planarDelta = Vector3.ProjectOnPlane(currentPosition - _previousPosition, upAxis).magnitude;
            if (planarDelta <= stuck.movementEpsilon)
                _noMovementDuration += Time.deltaTime;
            else
                _noMovementDuration = 0f;

            _previousPosition = currentPosition;
            return _noMovementDuration >= stuck.noMovementTimeout;
        }

        private bool ShouldForceRepathByPathStatus()
        {
            if (_agent == null || !_agent.hasPath || _agent.pathPending)
                return false;

            return _agent.pathStatus == NavMeshPathStatus.PathInvalid || _agent.pathStatus == NavMeshPathStatus.PathPartial;
        }
    }
}
