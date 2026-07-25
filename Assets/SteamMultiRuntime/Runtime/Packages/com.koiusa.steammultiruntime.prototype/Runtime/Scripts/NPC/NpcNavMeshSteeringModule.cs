using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcNavMeshSteeringModule : MonoBehaviour
    {
        [Header("Input Shaping")]
        [SerializeField, Range(0f, 1f)] private float minMoveInputMagnitude = 0.2f;
        [SerializeField, Range(0f, 1f)] private float corneringInputReduction = 0.35f;
        [SerializeField, Range(0f, 180f)] private float corneringInputMaxAngle = 90f;
        [SerializeField, Range(0f, 1f)] private float arrivalInputMinScale = 0.3f;
        [SerializeField, Range(0f, 1f)] private float navCornerDirectionWeight = 0.65f;
        [SerializeField, Min(0f)] private float navCornerMinDistance = 0.1f;

        [Header("Steering Filter")]
        [SerializeField, Min(0.1f)] private float steeringLowPassCutoffHz = 3f;
        [SerializeField, Min(0f)] private float steeringDeadband = 0.06f;
        [SerializeField, Min(1f)] private float steeringMaxTurnDegPerSec = 180f;
        public float MinMoveInputMagnitude => minMoveInputMagnitude;
        public float CorneringInputReduction => corneringInputReduction;
        public float CorneringInputMaxAngle => corneringInputMaxAngle;
        public float ArrivalInputMinScale => arrivalInputMinScale;
        public float NavCornerDirectionWeight => navCornerDirectionWeight;
        public float NavCornerMinDistance => navCornerMinDistance;
        public float SteeringLowPassCutoffHz => steeringLowPassCutoffHz;
        public float SteeringDeadband => steeringDeadband;
        public float SteeringMaxTurnDegPerSec => steeringMaxTurnDegPerSec;

        private void OnValidate()
        {
            minMoveInputMagnitude = Mathf.Clamp01(minMoveInputMagnitude);
            corneringInputReduction = Mathf.Clamp01(corneringInputReduction);
            corneringInputMaxAngle = Mathf.Clamp(corneringInputMaxAngle, 0f, 180f);
            arrivalInputMinScale = Mathf.Clamp01(arrivalInputMinScale);
            navCornerDirectionWeight = Mathf.Clamp01(navCornerDirectionWeight);
            navCornerMinDistance = Mathf.Max(0f, navCornerMinDistance);
            steeringLowPassCutoffHz = Mathf.Max(0.1f, steeringLowPassCutoffHz);
            steeringDeadband = Mathf.Max(0f, steeringDeadband);
            steeringMaxTurnDegPerSec = Mathf.Max(1f, steeringMaxTurnDegPerSec);
        }
    }
}
