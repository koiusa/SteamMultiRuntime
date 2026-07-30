using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Core
{
    public interface ILocalPlayerProvider
    {
        GameObject LocalPlayerObject { get; }
    }

    public static class LocalPlayerProviderRegistry
    {
        public static ILocalPlayerProvider Current { get; private set; }
        public static event Action<ILocalPlayerProvider> CurrentChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Current = null;
            CurrentChanged = null;
        }

        public static void Register(ILocalPlayerProvider provider)
        {
            if (object.ReferenceEquals(Current, provider)) return;
            Current = provider;
            CurrentChanged?.Invoke(Current);
        }

        public static void Unregister(ILocalPlayerProvider provider)
        {
            if (object.ReferenceEquals(Current, provider))
            {
                Current = null;
                CurrentChanged?.Invoke(null);
            }
        }
    }
}
