using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using Koiusa.SteamMultiRuntime.Localization;

namespace Koiusa.SteamMultiRuntime
{
    internal readonly struct SteamLobbyMemberQualityEntry
    {
        public readonly ulong SteamId;
        public readonly int PingMs;

        public SteamLobbyMemberQualityEntry(ulong steamId, int pingMs)
        {
            SteamId = steamId;
            PingMs = pingMs;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SteamLobbyConnectionStatus : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float broadcastIntervalSeconds = 1f;

        [Header("Connection Strength Display")]
        [SerializeField, Min(0)] private int excellentPingMaxMs = 50;
        [SerializeField, Min(0)] private int goodPingMaxMs = 90;
        [SerializeField, Min(0)] private int fairPingMaxMs = 140;
        [SerializeField, Min(0)] private int poorPingMaxMs = 220;
        [SerializeField, Min(1)] private int strengthStepCount = 5;

        private readonly Dictionary<ulong, int> memberPingBySteamId = new Dictionary<ulong, int>();

        public float BroadcastIntervalSeconds => Mathf.Max(0.2f, broadcastIntervalSeconds);

        public void Clear()
        {
            memberPingBySteamId.Clear();
        }

        public string GetConnectionStrengthText(bool isInLobby, bool isHost)
        {
            if (!isInLobby)
            {
                return string.Empty;
            }

            if (isHost)
            {
                return GameLocalization.Get("lobby.connection_host");
            }

            if (TryGetHostPing(out var pingMs))
            {
                var strength = BuildConnectionStrength(pingMs);
                return GameLocalization.Get("lobby.connection_strength", strength, pingMs);
            }

            return GameLocalization.Get("lobby.connection_measuring");
        }

        private string BuildConnectionStrength(int pingMs)
        {
            var steps = Mathf.Max(1, strengthStepCount);
            var excellentMax = Mathf.Max(0, excellentPingMaxMs);
            var goodMax = Mathf.Max(excellentMax, goodPingMaxMs);
            var fairMax = Mathf.Max(goodMax, fairPingMaxMs);
            var poorMax = Mathf.Max(fairMax, poorPingMaxMs);
            var level = pingMs <= excellentMax
                ? steps
                : pingMs <= goodMax
                    ? Mathf.Max(1, steps - 1)
                    : pingMs <= fairMax
                        ? Mathf.Max(1, steps - 2)
                        : pingMs <= poorMax
                            ? Mathf.Max(1, steps - 3)
                            : 1;

            var builder = new System.Text.StringBuilder();
            var filledSymbol = GameLocalization.Get("lobby.connection_symbol_filled");
            var emptySymbol = GameLocalization.Get("lobby.connection_symbol_empty");
            for (var i = 0; i < steps; i++)
                builder.Append(i < level ? filledSymbol : emptySymbol);
            return builder.ToString();
        }

        public string BuildMemberDisplayName(string memberName, ulong memberSteamId, bool isInLobby, bool isHost)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                memberName = GameLocalization.Get("common.unknown");
            }

            if (!isInLobby)
            {
                return memberName;
            }

            if (isHost && SteamClient.IsValid && memberSteamId == SteamClient.SteamId)
            {
                return GameLocalization.Get("lobby.member_host", memberName);
            }

            if (memberPingBySteamId.TryGetValue(memberSteamId, out var pingMs) && pingMs >= 0)
            {
                return $"{memberName} ({pingMs}ms)";
            }

            return memberName;
        }

        internal List<SteamLobbyMemberQualityEntry> BuildSnapshotEntries(IEnumerable<Friend> members)
        {
            var entries = new List<SteamLobbyMemberQualityEntry>();
            if (members == null)
            {
                return entries;
            }

            foreach (var member in members)
            {
                var pingMs = -1;

                if (SteamClient.IsValid && member.Id == SteamClient.SteamId)
                {
                    pingMs = 0;
                }
                entries.Add(new SteamLobbyMemberQualityEntry(member.Id, pingMs));
            }

            return entries;
        }

        internal void ApplySnapshot(IReadOnlyList<SteamLobbyMemberQualityEntry> entries)
        {
            memberPingBySteamId.Clear();

            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                memberPingBySteamId[entries[i].SteamId] = entries[i].PingMs;
            }
        }

        private static bool TryGetHostPing(out int pingMs)
        {
            pingMs = -1;
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || networkManager.IsServer)
            {
                return false;
            }

            var transport = networkManager.NetworkConfig?.NetworkTransport;
            if (transport == null)
            {
                return false;
            }

            var currentRtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
            if (currentRtt == 0 || currentRtt > int.MaxValue)
            {
                return false;
            }

            pingMs = (int)currentRtt;
            return true;
        }
    }
}
