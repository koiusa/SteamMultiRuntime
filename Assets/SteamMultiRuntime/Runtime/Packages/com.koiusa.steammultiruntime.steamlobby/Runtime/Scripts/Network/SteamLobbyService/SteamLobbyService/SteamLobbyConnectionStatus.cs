using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
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

        private readonly Dictionary<ulong, int> memberPingBySteamId = new Dictionary<ulong, int>();

        public float BroadcastIntervalSeconds => Mathf.Max(0.2f, broadcastIntervalSeconds);

        public void Clear()
        {
            memberPingBySteamId.Clear();
        }

        public string GetConnectionStrengthText(bool isInLobby, bool isHost, ulong hostSteamId)
        {
            if (!isInLobby)
            {
                return string.Empty;
            }

            if (isHost)
            {
                return GameLocalization.Get("lobby.connection_host");
            }

            if (TryEstimatePingToSteamId(hostSteamId, out var pingMs))
            {
                var strength = pingMs <= 50
                    ? "★★★★★"
                    : pingMs <= 90
                        ? "★★★★☆"
                        : pingMs <= 140
                            ? "★★★☆☆"
                            : pingMs <= 220
                                ? "★★☆☆☆"
                                : "★☆☆☆☆";
                return GameLocalization.Get("lobby.connection_strength", strength, pingMs);
            }

            return GameLocalization.Get("lobby.connection_measuring");
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
                else if (TryEstimatePingToSteamId(member.Id, out var estimatedPingMs))
                {
                    pingMs = estimatedPingMs;
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

        private static bool TryEstimatePingToSteamId(ulong steamId, out int pingMs)
        {
            pingMs = -1;
            if (steamId == 0)
            {
                return false;
            }

            try
            {
                var steamAssembly = typeof(SteamClient).Assembly;
                var networkingUtilsType = steamAssembly.GetType("Steamworks.SteamNetworkingUtils");
                if (networkingUtilsType == null)
                {
                    return false;
                }

                var methods = networkingUtilsType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var method in methods)
                {
                    if (!string.Equals(method.Name, "EstimatePingTo", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 1)
                    {
                        continue;
                    }

                    object argument;
                    var parameterType = parameters[0].ParameterType;
                    if (parameterType == typeof(SteamId))
                    {
                        argument = (SteamId)steamId;
                    }
                    else if (parameterType == typeof(ulong))
                    {
                        argument = steamId;
                    }
                    else
                    {
                        continue;
                    }

                    var result = method.Invoke(null, new[] { argument });
                    if (result == null)
                    {
                        continue;
                    }

                    pingMs = Convert.ToInt32(result);
                    return pingMs >= 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
