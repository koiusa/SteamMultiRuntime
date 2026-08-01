using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Owns the single frame-loop callback for network player input.</summary>
    [DisallowMultipleComponent]
    internal sealed class ServerDrivenActorInputLoop : MonoBehaviour
    {
        private static ServerDrivenActorInputLoop instance;
        private readonly List<ServerDrivenActorController> actors = new(8);
        private readonly HashSet<ServerDrivenActorController> actorSet = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Register(ServerDrivenActorController actor)
        {
            if (actor == null)
                return;
            var loop = EnsureInstance();
            if (loop.actorSet.Add(actor))
                loop.actors.Add(actor);
        }

        internal static void Unregister(ServerDrivenActorController actor)
        {
            if (instance == null || !instance.actorSet.Remove(actor))
                return;
            var index = instance.actors.IndexOf(actor);
            if (index < 0)
                return;
            var last = instance.actors.Count - 1;
            instance.actors[index] = instance.actors[last];
            instance.actors.RemoveAt(last);
        }

        private static ServerDrivenActorInputLoop EnsureInstance()
        {
            if (instance != null)
                return instance;
            var host = new GameObject(nameof(ServerDrivenActorInputLoop));
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ServerDrivenActorInputLoop>();
            return instance;
        }

        private void Update()
        {
            for (var i = actors.Count - 1; i >= 0; i--)
            {
                var actor = actors[i];
                if (actor == null)
                {
                    actorSet.Remove(actor);
                    actors.RemoveAt(i);
                    continue;
                }
                if (actor.isActiveAndEnabled)
                    actor.TickRegisteredInput();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
