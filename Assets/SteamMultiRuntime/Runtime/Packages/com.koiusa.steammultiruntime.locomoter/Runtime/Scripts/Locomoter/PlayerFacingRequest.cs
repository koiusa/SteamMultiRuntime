using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class PlayerFacingPriority
    {
        public const int Targeting = 100;
        public const int WireGround = 200;
    }

    public readonly struct PlayerFacingRequest
    {
        public PlayerFacingRequest(Vector3 direction, int priority, float blend = 1f, float rotationSpeed = 0f)
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

    public interface IPlayerFacingRequestSource
    {
        bool TryGetFacingRequest(Vector3 origin, bool isStrafeMode, out PlayerFacingRequest request);
    }

    public sealed class PlayerFacingRequestResolver
    {
        private readonly IPlayerFacingRequestSource[] sources;

        public PlayerFacingRequestResolver(GameObject owner)
        {
            var components = owner != null ? owner.GetComponents<MonoBehaviour>() : System.Array.Empty<MonoBehaviour>();
            var count = 0;
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IPlayerFacingRequestSource) count++;
            }

            sources = new IPlayerFacingRequestSource[count];
            var destination = 0;
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IPlayerFacingRequestSource source)
                {
                    sources[destination++] = source;
                }
            }
        }

        public PlayerFacingRequest Resolve(Vector3 origin, bool isStrafeMode)
        {
            var result = default(PlayerFacingRequest);
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
