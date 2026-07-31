using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Core
{
    public readonly struct CrowdPhysicsBody
    {
        public CrowdPhysicsBody(Rigidbody body)
        {
            Body = body;
            Colliders = body != null ? body.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
        }

        public Rigidbody Body { get; }
        public Collider[] Colliders { get; }
    }

    public static class CrowdPhysicsBodyRegistry
    {
        private static readonly List<CrowdPhysicsBody> players = new();
        public static IReadOnlyList<CrowdPhysicsBody> Players => players;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => players.Clear();

        public static void RegisterPlayer(Rigidbody body)
        {
            if (body == null)
                return;
            for (var i = 0; i < players.Count; i++)
                if (players[i].Body == body)
                    return;
            players.Add(new CrowdPhysicsBody(body));
        }

        public static void UnregisterPlayer(Rigidbody body)
        {
            for (var i = players.Count - 1; i >= 0; i--)
                if (players[i].Body == null || players[i].Body == body)
                    players.RemoveAt(i);
        }
    }
}
