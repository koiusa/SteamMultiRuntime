using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcNavMeshAvoidanceModule : MonoBehaviour
    {
        public enum AvoidanceMode { Boid, Rvo }

        [SerializeField] private AvoidanceMode mode = AvoidanceMode.Rvo;
        [SerializeField, Min(0.01f)] private float updateInterval = 0.08f;
        [SerializeField] private bool holdLastValueBetweenUpdates = true;

        [Header("Boid Separation")]
        [SerializeField, Min(0.1f)] private float boidSeparationRadius = 1.6f;
        [SerializeField, Min(0f)] private float boidGoalWeight = 1f;
        [SerializeField, Min(0f)] private float boidSeparationWeight = 1.25f;
        [SerializeField, Min(1f)] private float boidSeparationExponent = 2.2f;
        [SerializeField] private bool boidUseForwardNeighborFilter = true;
        [SerializeField, Range(-1f, 1f)] private float boidNeighborForwardDotMin;
        [SerializeField, Min(1)] private int boidMaxNeighbors = 8;

        [Header("RVO-style Local Avoidance")]
        [SerializeField, Min(0.1f)] private float rvoNeighborRadius = 2f;
        [SerializeField, Min(0.1f)] private float rvoTimeHorizon = 1.2f;
        [SerializeField, Min(0f)] private float rvoGoalWeight = 1f;
        [SerializeField, Min(0f)] private float rvoAvoidanceWeight = 1.35f;
        [SerializeField, Min(0f)] private float rvoMinApproachSpeed = 0.05f;
        [SerializeField, Min(1)] private int rvoMaxNeighbors = 10;
        [SerializeField, Min(0f)] private float rvoSideBias = 0.15f;
        [SerializeField, Min(0f)] private float rvoSideSwitchThreshold = 0.2f;
        [SerializeField, Min(0f)] private float rvoSideHoldTime = 0.35f;
        [SerializeField, Min(1)] private int rvoPrimaryNeighborCount = 2;

        private NavMeshAgent agent;
        private ObstacleAvoidanceType originalObstacleAvoidanceType;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            originalObstacleAvoidanceType = agent.obstacleAvoidanceType;
        }

        private void OnEnable()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
                originalObstacleAvoidanceType = agent.obstacleAvoidanceType;
            }
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void OnDisable()
        {
            if (agent != null)
                agent.obstacleAvoidanceType = originalObstacleAvoidanceType;
        }

        public AvoidanceMode Mode => mode;
        public float UpdateInterval => updateInterval;
        public bool HoldLastValueBetweenUpdates => holdLastValueBetweenUpdates;
        public float BoidSeparationRadius => boidSeparationRadius;
        public float BoidGoalWeight => boidGoalWeight;
        public float BoidSeparationWeight => boidSeparationWeight;
        public float BoidSeparationExponent => boidSeparationExponent;
        public bool BoidUseForwardNeighborFilter => boidUseForwardNeighborFilter;
        public float BoidNeighborForwardDotMin => boidNeighborForwardDotMin;
        public int BoidMaxNeighbors => boidMaxNeighbors;
        public float RvoNeighborRadius => rvoNeighborRadius;
        public float RvoTimeHorizon => rvoTimeHorizon;
        public float RvoGoalWeight => rvoGoalWeight;
        public float RvoAvoidanceWeight => rvoAvoidanceWeight;
        public float RvoMinApproachSpeed => rvoMinApproachSpeed;
        public int RvoMaxNeighbors => rvoMaxNeighbors;
        public float RvoSideBias => rvoSideBias;
        public float RvoSideSwitchThreshold => rvoSideSwitchThreshold;
        public float RvoSideHoldTime => rvoSideHoldTime;
        public int RvoPrimaryNeighborCount => rvoPrimaryNeighborCount;

        private void OnValidate()
        {
            updateInterval = Mathf.Max(0.01f, updateInterval);
            boidSeparationRadius = Mathf.Max(0.1f, boidSeparationRadius);
            boidSeparationExponent = Mathf.Max(1f, boidSeparationExponent);
            boidNeighborForwardDotMin = Mathf.Clamp(boidNeighborForwardDotMin, -1f, 1f);
            boidMaxNeighbors = Mathf.Max(1, boidMaxNeighbors);
            rvoNeighborRadius = Mathf.Max(0.1f, rvoNeighborRadius);
            rvoTimeHorizon = Mathf.Max(0.1f, rvoTimeHorizon);
            rvoGoalWeight = Mathf.Max(0f, rvoGoalWeight);
            rvoAvoidanceWeight = Mathf.Max(0f, rvoAvoidanceWeight);
            rvoMinApproachSpeed = Mathf.Max(0f, rvoMinApproachSpeed);
            rvoMaxNeighbors = Mathf.Max(1, rvoMaxNeighbors);
            rvoSideBias = Mathf.Max(0f, rvoSideBias);
            rvoSideSwitchThreshold = Mathf.Max(0f, rvoSideSwitchThreshold);
            rvoSideHoldTime = Mathf.Max(0f, rvoSideHoldTime);
            rvoPrimaryNeighborCount = Mathf.Max(1, rvoPrimaryNeighborCount);
        }
    }
}
