using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Ticks only currently attached wire traversal stacks.</summary>
    [DisallowMultipleComponent]
    internal sealed class WireTraversalUpdateLoop : MonoBehaviour
    {
        private static WireTraversalUpdateLoop instance;
        private readonly List<WireTraversalFeature> connections = new();
        private readonly HashSet<WireTraversalFeature> connectionSet = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Register(WireTraversalFeature connection)
        {
            if (connection == null)
                return;
            var loop = EnsureInstance();
            if (loop.connectionSet.Add(connection))
                loop.connections.Add(connection);
        }

        internal static void Unregister(WireTraversalFeature connection)
        {
            if (instance == null || !instance.connectionSet.Remove(connection))
                return;
            var index = instance.connections.IndexOf(connection);
            if (index < 0)
                return;
            var last = instance.connections.Count - 1;
            instance.connections[index] = instance.connections[last];
            instance.connections.RemoveAt(last);
        }

        private static WireTraversalUpdateLoop EnsureInstance()
        {
            if (instance != null)
                return instance;
            var host = new GameObject(nameof(WireTraversalUpdateLoop));
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<WireTraversalUpdateLoop>();
            return instance;
        }

        private void FixedUpdate()
        {
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                var connection = connections[i];
                if (connection == null)
                {
                    connectionSet.Remove(connection);
                    connections.RemoveAt(i);
                    continue;
                }
                connection.TickAttachedFixed();
                connection.TickAttachedActionsFixed();
            }
        }

        private void LateUpdate()
        {
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                var connection = connections[i];
                if (connection != null)
                    connection.TickAttachedLate();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
