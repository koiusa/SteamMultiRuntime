using System;
using Koiusa.TargetingSystem.Runtime;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    public static class LocalTargetingControllerRegistry
    {
        public static TargetingController Current { get; private set; }
        public static event Action<TargetingController> CurrentChanged;

        public static void Register(TargetingController controller)
        {
            if (controller == null || Current == controller) return;
            Current = controller;
            CurrentChanged?.Invoke(controller);
        }

        public static void Unregister(TargetingController controller)
        {
            if (Current != controller) return;
            Current = null;
            CurrentChanged?.Invoke(null);
        }
    }
}
