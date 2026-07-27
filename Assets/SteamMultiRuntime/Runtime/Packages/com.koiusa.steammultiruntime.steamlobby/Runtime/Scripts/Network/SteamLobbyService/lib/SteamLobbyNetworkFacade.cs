using System;
using Netcode.Transports.Facepunch;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class SteamLobbyNetworkFacade : INetworkSessionController
    {
        private readonly bool useAdditiveClientSynchronization;

        public event Action Stopping;

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

            if (networkManager.IsListening)
            {
                return networkManager.IsHost;
            }

            ConfigureClientSynchronizationMode(networkManager);
            var started = networkManager.StartHost();
            if (!started)
            {
                return false;
            }

            EnableActiveSceneSynchronization(networkManager);
            return true;
        }

        public bool TryStartServer()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return false;
            }

            if (networkManager.IsListening)
            {
                return networkManager.IsServer && !networkManager.IsClient;
            }

            // Start server without the server itself being a player
            // Set server to not be part of the connected clients count
            if (!networkManager.StartServer())
            {
                return false;
            }

            EnableActiveSceneSynchronization(networkManager);
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

            if (networkManager.IsListening)
            {
                return networkManager.IsClient && !networkManager.IsServer;
            }

            transport.targetSteamId = hostSteamId;
            ConfigureClientSynchronizationMode(networkManager);
            return networkManager.StartClient();
        }

        public Task StopAsync()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return Task.CompletedTask;
            }

            Stopping?.Invoke();
            var completion = new TaskCompletionSource<bool>();
            var wasServer = networkManager.IsServer;

            void OnStopped(bool _)
            {
                if (wasServer)
                    networkManager.OnServerStopped -= OnStopped;
                else
                    networkManager.OnClientStopped -= OnStopped;
                completion.TrySetResult(true);
            }

            if (wasServer)
                networkManager.OnServerStopped += OnStopped;
            else
                networkManager.OnClientStopped += OnStopped;

            networkManager.Shutdown(discardMessageQueue: true);
            return completion.Task;
        }

        public async Task<bool> SwitchStageSceneAsync(string targetSceneReference, string previousSceneReference)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer || networkManager.SceneManager == null)
            {
                return false;
            }

            var targetSceneName = SceneUtilityEx.ToSceneName(targetSceneReference);
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                return false;
            }

            var targetScene = SceneUtilityEx.GetLoadedScene(targetSceneReference);
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                if (!await ExecuteSceneEventAsync(
                        networkManager.SceneManager,
                        targetSceneName,
                        SceneEventType.LoadEventCompleted,
                        () => networkManager.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Additive)))
                {
                    return false;
                }

                targetScene = SceneUtilityEx.GetLoadedScene(targetSceneReference);
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                return false;
            }

            SceneManager.SetActiveScene(targetScene);

            var previousScene = SceneUtilityEx.GetLoadedScene(previousSceneReference);
            if (previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene != targetScene)
            {
                if (!await ExecuteSceneEventAsync(
                        networkManager.SceneManager,
                        previousScene.name,
                        SceneEventType.UnloadEventCompleted,
                        () => networkManager.SceneManager.UnloadScene(previousScene)))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> UnloadStageSceneAsync(string sceneReference)
        {
            var networkManager = NetworkManager.Singleton;
            var scene = SceneUtilityEx.GetLoadedScene(sceneReference);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return true;
            }

            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer || networkManager.SceneManager == null)
            {
                return false;
            }

            if (SceneManager.GetActiveScene() == scene)
            {
                for (var index = 0; index < SceneManager.sceneCount; index++)
                {
                    var fallbackScene = SceneManager.GetSceneAt(index);
                    if (fallbackScene.IsValid() && fallbackScene.isLoaded && fallbackScene != scene)
                    {
                        SceneManager.SetActiveScene(fallbackScene);
                        break;
                    }
                }
            }

            return await ExecuteSceneEventAsync(
                networkManager.SceneManager,
                scene.name,
                SceneEventType.UnloadEventCompleted,
                () => networkManager.SceneManager.UnloadScene(scene));
        }

        public Task<bool> WaitForClientStageSceneAsync(string sceneReference)
        {
            var loadedScene = SceneUtilityEx.GetLoadedScene(sceneReference);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                return Task.FromResult(true);
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsClient || networkManager.SceneManager == null)
            {
                return Task.FromResult(false);
            }

            var sceneName = SceneUtilityEx.ToSceneName(sceneReference);
            var completion = new TaskCompletionSource<bool>();

            void OnSceneEvent(SceneEvent sceneEvent)
            {
                if (sceneEvent.SceneEventType != SceneEventType.LoadComplete
                    || sceneEvent.ClientId != networkManager.LocalClientId
                    || !string.Equals(sceneEvent.SceneName, sceneName, StringComparison.Ordinal))
                {
                    return;
                }

                networkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
                completion.TrySetResult(true);
            }

            networkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            return completion.Task;
        }

        private static async Task<bool> ExecuteSceneEventAsync(
            NetworkSceneManager sceneManager,
            string sceneName,
            SceneEventType completionType,
            Func<SceneEventProgressStatus> beginOperation)
        {
            var completion = new TaskCompletionSource<bool>();

            void OnSceneEvent(SceneEvent sceneEvent)
            {
                if (sceneEvent.SceneEventType != completionType
                    || !string.Equals(sceneEvent.SceneName, sceneName, StringComparison.Ordinal))
                {
                    return;
                }

                sceneManager.OnSceneEvent -= OnSceneEvent;
                completion.TrySetResult(sceneEvent.ClientsThatTimedOut == null || sceneEvent.ClientsThatTimedOut.Count == 0);
            }

            sceneManager.OnSceneEvent += OnSceneEvent;
            var status = beginOperation();
            if (status != SceneEventProgressStatus.Started)
            {
                sceneManager.OnSceneEvent -= OnSceneEvent;
                return false;
            }

            return await completion.Task;
        }

        private static void EnableActiveSceneSynchronization(NetworkManager networkManager)
        {
            if (networkManager.SceneManager != null)
            {
                networkManager.SceneManager.ActiveSceneSynchronizationEnabled = true;
            }
        }

        public bool TryActivateBootstrapScene(string excludedPresentationSceneReference)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || SceneLoadUtility.AreSameSceneReference(scene.path, excludedPresentationSceneReference)
                    || SceneLoadUtility.AreSameSceneReference(scene.name, excludedPresentationSceneReference))
                {
                    continue;
                }

                if (SceneManager.SetActiveScene(scene))
                {
                    return true;
                }
            }

            return false;
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
