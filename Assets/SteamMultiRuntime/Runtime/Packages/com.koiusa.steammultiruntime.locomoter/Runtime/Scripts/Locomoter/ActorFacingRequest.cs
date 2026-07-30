using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class ActorFacingPriority
    {
        public const int Targeting = 100;
        public const int WireGround = 200;
    }

    public readonly struct ActorFacingRequest
    {
        public ActorFacingRequest(Vector3 direction, int priority, float blend = 1f, float rotationSpeed = 0f)
        {
            Direction = direction;
            Priority = priority;
            Blend = Mathf.Clamp01(blend);
            RotationSpeed = Mathf.Max(0f, rotationSpeed);
        }

        public Vector3 Direction { get; }
        public int Priority { get; }
        public float Blend { get; }
        public float RotationSpeed { get; }
        public bool IsValid => Direction.sqrMagnitude > 0.0001f && Blend > 0f;
    }

    public interface IActorFacingRequestSource
    {
        bool TryGetFacingRequest(Vector3 origin, bool isStrafeMode, out ActorFacingRequest request);
    }

    public sealed class ActorFacingRequestResolver
    {
        private readonly IActorFacingRequestSource[] sources;

        public ActorFacingRequestResolver(GameObject owner)
        {
            var components = owner != null ? owner.GetComponents<MonoBehaviour>() : System.Array.Empty<MonoBehaviour>();
            var count = 0;
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IActorFacingRequestSource) count++;
            }

            sources = new IActorFacingRequestSource[count];
            var destination = 0;
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IActorFacingRequestSource source)
                {
                    sources[destination++] = source;
                }
            }
        }

        public ActorFacingRequest Resolve(Vector3 origin, bool isStrafeMode)
        {
            var result = default(ActorFacingRequest);
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null
                    && sources[i].TryGetFacingRequest(origin, isStrafeMode, out var candidate)
                    && candidate.IsValid
                    && (!result.IsValid || candidate.Priority > result.Priority))
                {
                    result = candidate;
                }
            }

            return result;
        }
    }
}
