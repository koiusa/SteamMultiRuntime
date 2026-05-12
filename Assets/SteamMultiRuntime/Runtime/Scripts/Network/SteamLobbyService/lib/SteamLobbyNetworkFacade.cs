using System;
using Netcode.Transports.Facepunch;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class SteamLobbyNetworkFacade
    {
        private readonly bool useAdditiveClientSynchronization;

        public SteamLobbyNetworkFacade(bool useAdditiveClientSynchronization)
        {
            this.useAdditiveClientSynchronization = useAdditiveClientSynchronization;
        }

        public bool SubscribeClientDisconnect(Action<ulong> callback)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return false;
            }

            networkManager.OnClientDisconnectCallback += callback;
            return true;
        }

        public void UnsubscribeClientDisconnect(Action<ulong> callback)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnClientDisconnectCallback -= callback;
            }
        }

        public bool TryStartHost()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return false;
            }

            var started = networkManager.StartHost();
            if (!started)
            {
                return false;
            }

            ConfigureClientSynchronizationMode(networkManager);
            return true;
        }

        public bool TryStartServer()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return false;
            }

            // Start server without the server itself being a player
            // Set server to not be part of the connected clients count
            if (!networkManager.StartServer())
            {
                return false;
            }

            return true;
        }

        public bool TryStartClient(ulong hostSteamId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return false;
            }

            var transport = networkManager.GetComponent<FacepunchTransport>();
            if (transport == null)
            {
                return false;
            }

            transport.targetSteamId = hostSteamId;
            return networkManager.StartClient();
        }

        public void ShutdownIfListening()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        public bool TryGetLocalClientId(out ulong localClientId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                localClientId = 0;
                return false;
            }

            localClientId = networkManager.LocalClientId;
            return true;
        }

        private void ConfigureClientSynchronizationMode(NetworkManager networkManager)
        {
            if (!useAdditiveClientSynchronization || networkManager.SceneManager == null)
            {
                return;
            }

            networkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
        }

        public bool IsHostListening()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening && networkManager.IsHost;
        }

        public bool IsClientListening()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening && networkManager.IsClient;
        }

        public bool SubscribeNamedMessage(string messageName, CustomMessagingManager.HandleNamedMessageDelegate handler)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager?.CustomMessagingManager == null)
            {
                return false;
            }

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(messageName, handler);
            return true;
        }

        public void UnsubscribeNamedMessage(string messageName)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager?.CustomMessagingManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(messageName);
        }

        public bool TrySendNamedMessageToAllClients(string messageName, FastBufferWriter writer, NetworkDelivery delivery)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager?.CustomMessagingManager == null)
            {
                return false;
            }

            var sentAny = false;
            foreach (var clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                networkManager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer, delivery);
                sentAny = true;
            }

            return sentAny;
        }
    }
}
