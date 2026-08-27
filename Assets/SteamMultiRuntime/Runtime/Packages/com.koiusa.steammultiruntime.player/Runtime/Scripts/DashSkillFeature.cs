using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class DashSkillFeature : ActorSkillFeature
    {
        [SerializeField, Min(0f)] private float speed = 12f;
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        [SerializeField] private bool useFacingWhenDirectionIsEmpty = true;

        private IActorMotorMotionSink motor;
        private IActorLocomotionState locomotionState;
        protected override float ActiveDuration => duration;
        private protected override ActorSkillSlot PresentationSlot => ActorSkillSlot.Dash;

        private void Awake()
        {
            motor = GetComponent<IActorMotorMotionSink>();
            locomotionState = GetComponent<IActorLocomotionState>();
        }

        protected override bool OnActivate(ActorSkillContext context)
        {
            if (motor == null) motor = GetComponent<IActorMotorMotionSink>();
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
                new ActorMotorMotionRequest(GetInstanceID(), direction, speed, duration));
        }

        protected override void OnCompleted() => motor?.StopMotion(GetInstanceID());
        protected override void OnCancelled() => motor?.StopMotion(GetInstanceID());
    }
}
