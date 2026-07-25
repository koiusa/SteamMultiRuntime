namespace Koiusa.SteamMultiRuntime
{
    public enum NetworkControlMode
    {
        Player = 0,
        ServerNpc = 1
    }

    internal readonly struct NetworkControlPolicy
    {
        public readonly float StateSyncInterval;
        public readonly bool BroadcastInputState;

        public NetworkControlPolicy(float stateSyncInterval, bool broadcastInputState)
        {
            StateSyncInterval = stateSyncInterval;
            BroadcastInputState = broadcastInputState;
        }
    }

    internal static class NetworkControlPolicies
    {
        private static readonly NetworkControlPolicy Player = new NetworkControlPolicy(
            stateSyncInterval: 0.05f,
            broadcastInputState: true);

        private static readonly NetworkControlPolicy ServerNpc = new NetworkControlPolicy(
            stateSyncInterval: 0.2f,
            broadcastInputState: false);

        public static NetworkControlPolicy Get(NetworkControlMode mode)
        {
            return mode == NetworkControlMode.ServerNpc ? ServerNpc : Player;
        }
    }
}
