using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public readonly struct ActorMotorMotionRequest
    {
        public ActorMotorMotionRequest(int ownerId, Vector3 direction, float speed, float duration, int priority = 100)
        {
            OwnerId = ownerId;
            Direction = direction;
            Speed = Mathf.Max(0f, speed);
            Duration = Mathf.Max(0f, duration);
            Priority = priority;
        }

        public int OwnerId { get; }
        public Vector3 Direction { get; }
        public float Speed { get; }
        public float Duration { get; }
        public int Priority { get; }
    }

    public interface IActorMotorMotionSink
    {
        bool TryStartMotion(ActorMotorMotionRequest request);
        void StopMotion(int ownerId);
    }
}
