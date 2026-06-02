using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal class SteamLobbyQualityTracker
    {
        private const string MemberQualityMessageName = "SteamLobby.MemberQualitySnapshot";

        private bool isHostListening;
        private float nextMemberQualityBroadcastAt;
        private bool isSubscribedToMemberQualityMessage;
        private SteamLobbyNetworkFacade networkFacade;
        private Func<Lobby?> getCurrentLobby;
        private Func<List<Friend>> getCurrentLobbyMembers;
        private Action<List<SteamLobbyMemberQualityEntry>> applySnapshot;
        private Action notifyStateChanged;

        public SteamLobbyQualityTracker()
        {
        }

        public void Initialize(SteamLobbyNetworkFacade facade, bool useAdditiveClientSynchronization)
        {
            networkFacade = facade;
        }

        public void SetCallbacks(
            Func<Lobby?> getCurrentLobby,
            Func<List<Friend>> getCurrentLobbyMembers,
            Action<List<SteamLobbyMemberQualityEntry>> applySnapshot,
            Action notifyStateChanged)
        {
            this.getCurrentLobby = getCurrentLobby;
            this.getCurrentLobbyMembers = getCurrentLobbyMembers;
            this.applySnapshot = applySnapshot;
            this.notifyStateChanged = notifyStateChanged;
        }

        public void SetBroadcastInterval(float intervalSeconds)
        {
            // Will be used when determining next broadcast time
        }

        public void OnMemberJoined()
        {
            if (isHostListening)
            {
                BroadcastMemberQualitySnapshot();
            }
        }

        public void OnMemberLeft()
        {
            if (isHostListening)
            {
                BroadcastMemberQualitySnapshot();
            }
        }

        public void Update(float unscaledTime, float broadcastIntervalSeconds)
        {
            if (!isHostListening)
            {
                return;
            }

            if (unscaledTime < nextMemberQualityBroadcastAt)
            {
                return;
            }

            nextMemberQualityBroadcastAt = unscaledTime + broadcastIntervalSeconds;
            BroadcastMemberQualitySnapshot();
        }

        public void OnNetworkHostStarted()
        {
            isHostListening = true;
            nextMemberQualityBroadcastAt = 0f;
            SubscribeMemberQualityMessage();
        }

        public void OnNetworkShutdown()
        {
            isHostListening = false;
            UnsubscribeMemberQualityMessage();
        }

        public void Reset()
        {
            nextMemberQualityBroadcastAt = 0f;
            isHostListening = false;
            UnsubscribeMemberQualityMessage();
        }

        private void BroadcastMemberQualitySnapshot()
        {
            var currentLobby = getCurrentLobby?.Invoke();
            if (!currentLobby.HasValue)
            {
                return;
            }

            var entries = BuildMemberQualitySnapshotEntries();
            ApplyMemberQualitySnapshot(entries);
            if (entries.Count == 0)
            {
                return;
            }

            var bufferSize = sizeof(int) + entries.Count * (sizeof(ulong) + sizeof(int));
            using var writer = new FastBufferWriter(bufferSize, Allocator.Temp);
            writer.WriteValueSafe(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                writer.WriteValueSafe(entries[i].SteamId);
                writer.WriteValueSafe(entries[i].PingMs);
            }

            networkFacade?.TrySendNamedMessageToAllClients(MemberQualityMessageName, writer, NetworkDelivery.Unreliable);
        }

        private List<SteamLobbyMemberQualityEntry> BuildMemberQualitySnapshotEntries()
        {
            var members = getCurrentLobbyMembers?.Invoke();
            if (members == null || members.Count == 0)
            {
                return new List<SteamLobbyMemberQualityEntry>();
            }

            // This will be properly implemented when SteamLobbyConnectionStatus is available
            return new List<SteamLobbyMemberQualityEntry>();
        }

        private void ApplyMemberQualitySnapshot(List<SteamLobbyMemberQualityEntry> entries)
        {
            applySnapshot?.Invoke(entries);
            notifyStateChanged?.Invoke();
        }

        private void OnMemberQualitySnapshotReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (isHostListening)
            {
                return;
            }

            if (!reader.TryBeginRead(sizeof(int)))
            {
                return;
            }

            reader.ReadValueSafe(out int count);
            if (count < 0)
            {
                return;
            }

            var entries = new List<SteamLobbyMemberQualityEntry>(count);
            for (var i = 0; i < count; i++)
            {
                if (!reader.TryBeginRead(sizeof(ulong) + sizeof(int)))
                {
                    break;
                }

                reader.ReadValueSafe(out ulong steamId);
                reader.ReadValueSafe(out int pingMs);
                entries.Add(new SteamLobbyMemberQualityEntry(steamId, pingMs));
            }

            ApplyMemberQualitySnapshot(entries);
        }

        private void SubscribeMemberQualityMessage()
        {
            if (isSubscribedToMemberQualityMessage || networkFacade == null)
            {
                return;
            }

            isSubscribedToMemberQualityMessage = networkFacade.SubscribeNamedMessage(MemberQualityMessageName, OnMemberQualitySnapshotReceived);
        }

        private void UnsubscribeMemberQualityMessage()
        {
            if (!isSubscribedToMemberQualityMessage || networkFacade == null)
            {
                return;
            }

            networkFacade.UnsubscribeNamedMessage(MemberQualityMessageName);
            isSubscribedToMemberQualityMessage = false;
        }
    }
}
