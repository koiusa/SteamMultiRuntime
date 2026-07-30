using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class DashSkillFeature : PlayerSkillFeature
    {
        [SerializeField, Min(0f)] private float speed = 12f;
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        [SerializeField] private bool useFacingWhenDirectionIsEmpty = true;

        private IPlayerMotorMotionSink motor;
        private IActorLocomotionState locomotionState;
        protected override float ActiveDuration => duration;

        private void Awake()
        {
            motor = GetComponent<IPlayerMotorMotionSink>();
            locomotionState = GetComponent<IActorLocomotionState>();
        }

        protected override bool OnActivate(PlayerSkillContext context)
        {
            if (motor == null) motor = GetComponent<IPlayerMotorMotionSink>();
            var direction = context.Direction;
            if (locomotionState == null) locomotionState = GetComponent<IActorLocomotionState>();
            if (locomotionState != null
                && locomotionState.IsStrafeMode
                && locomotionState.MoveDirection.sqrMagnitude > 0.0001f)
            {
                direction = locomotionState.MoveDirection;
            }
            if (direction.sqrMagnitude <= 0.0001f && useFacingWhenDirectionIsEmpty) direction = transform.forward;
            return motor != null && motor.TryStartMotion(
                new PlayerMotorMotionRequest(GetInstanceID(), direction, speed, duration));
        }

        protected override void OnCompleted() => motor?.StopMotion(GetInstanceID());
        protected override void OnCancelled() => motor?.StopMotion(GetInstanceID());
    }
}
