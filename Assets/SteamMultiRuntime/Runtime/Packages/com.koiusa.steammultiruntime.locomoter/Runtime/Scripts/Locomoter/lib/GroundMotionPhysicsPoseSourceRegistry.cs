using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Lifecycle registry for physics-pose ground sources. Consumers use this to
    /// prepare collision relationships before the first physical contact.
    /// </summary>
    public static class GroundMotionPhysicsPoseSourceRegistry
    {
        private static readonly List<IGroundMotionPhysicsPoseSource> Sources = new(8);

        public static event Action<IGroundMotionPhysicsPoseSource> SourceRegistered;
        public static event Action<IGroundMotionPhysicsPoseSource> SourceUnregistered;

        public static IReadOnlyList<IGroundMotionPhysicsPoseSource> RegisteredSources => Sources;

        public static void Register(IGroundMotionPhysicsPoseSource source)
        {
            if (source == null || Sources.Contains(source))
                return;
            Sources.Add(source);
            SourceRegistered?.Invoke(source);
        }

        public static void Unregister(IGroundMotionPhysicsPoseSource source)
        {
            if (source == null || !Sources.Remove(source))
                return;
            SourceUnregistered?.Invoke(source);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sources.Clear();
            SourceRegistered = null;
            SourceUnregistered = null;
        }
    }
}
