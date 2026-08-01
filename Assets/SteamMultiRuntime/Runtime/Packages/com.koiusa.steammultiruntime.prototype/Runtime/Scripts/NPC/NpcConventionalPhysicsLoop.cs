using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Owns the single FixedUpdate callback for conventional NPC motors.</summary>
    [DisallowMultipleComponent]
    internal sealed class NpcConventionalPhysicsLoop : MonoBehaviour
    {
        private static NpcConventionalPhysicsLoop instance;
        private readonly List<NpcNavMeshController> controllers = new();
        private readonly HashSet<NpcNavMeshController> controllerSet = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Register(NpcNavMeshController controller)
        {
            if (controller == null)
                return;
            var loop = EnsureInstance();
            if (loop.controllerSet.Add(controller))
                loop.controllers.Add(controller);
        }

        internal static void Unregister(NpcNavMeshController controller)
        {
            if (instance == null || !instance.controllerSet.Remove(controller))
                return;
            var index = instance.controllers.IndexOf(controller);
            if (index < 0)
                return;
            var last = instance.controllers.Count - 1;
            instance.controllers[index] = instance.controllers[last];
            instance.controllers.RemoveAt(last);
        }

        private static NpcConventionalPhysicsLoop EnsureInstance()
        {
            if (instance != null)
                return instance;
            var host = new GameObject(nameof(NpcConventionalPhysicsLoop));
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<NpcConventionalPhysicsLoop>();
            return instance;
        }

        private void FixedUpdate()
        {
            for (var i = controllers.Count - 1; i >= 0; i--)
            {
                var controller = controllers[i];
                if (controller == null)
                {
                    controllerSet.Remove(controller);
                    controllers.RemoveAt(i);
                    continue;
                }
                if (controller.isActiveAndEnabled)
                    controller.TickConventionalPhysics();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
