using System;
using Koiusa.TargetingSystem.Runtime;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.TargetingSystem
{
    public static class LocalTargetingControllerRegistry
    {
        public static TargetingController Current { get; private set; }
        public static event Action<TargetingController> CurrentChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            CurrentChanged = null;
        }

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
