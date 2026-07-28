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

        public static void Register(ILocalPlayerProvider provider)
        {
            Current = provider;
        }

        public static void Unregister(ILocalPlayerProvider provider)
        {
            if (object.ReferenceEquals(Current, provider))
            {
                Current = null;
            }
        }
    }
}
