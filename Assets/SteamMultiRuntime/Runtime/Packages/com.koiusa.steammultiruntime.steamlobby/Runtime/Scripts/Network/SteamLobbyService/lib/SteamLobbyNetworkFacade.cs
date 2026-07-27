using System;
using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class SteamLobbyNetworkFacade : INetworkSessionController
    {
        private readonly bool useAdditiveClientSynchronization;
        private int? protectedClientSceneHandle;
        private NetworkManager stopEventSource;
        private NetworkManager clientSceneEventSource;
        private TaskCompletionSource<bool> stopCompletion;
        private bool hasObservedNetworkStop;

        public event Action Stopping;
        public event Action<ulong> ClientDisconnected;

        public SteamLobbyNetworkFacade(bool useAdditiveClientSynchronization)
        {
            this.useAdditiveClientSynchronization = useAdditiveClientSynchronization;
        }

        public bool TryStartHost()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return false;
            }

            EnsureStopEventSubscription(networkManager);

            if (networkManager.IsListening)
            {
                return networkManager.IsHost;
            }

            hasObservedNetworkStop = false;
            var started = networkManager.StartHost();
            if (!started)
            {
                return false;
            }

            ConfigureServerSynchronizationMode(networkManager);
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

            EnsureStopEventSubscription(networkManager);

            if (networkManager.IsListening)
            {
                return networkManager.IsServer && !networkManager.IsClient;
            }

            // Start server without the server itself being a player
            // Set server to not be part of the connected clients count
            hasObservedNetworkStop = false;
            if (!networkManager.StartServer())
            {
                return false;
            }

            ConfigureServerSynchronizationMode(networkManager);
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

            EnsureStopEventSubscription(networkManager);

            var transport = networkManager.GetComponent<FacepunchTransport>();
            if (transport == null)
            {
                return false;
            }

            EnsureClientSceneActivationSubscription(networkManager);

            if (networkManager.IsListening)
            {
                return networkManager.IsClient && !networkManager.IsServer;
            }

            transport.targetSteamId = hostSteamId;
            ConfigureClientSceneProtection(networkManager);
            hasObservedNetworkStop = false;
            return networkManager.StartClient();
        }

        public Task StopAsync()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || hasObservedNetworkStop)
            {
                return Task.CompletedTask;
            }

            EnsureStopEventSubscription(networkManager);
            Stopping?.Invoke();
            stopCompletion = new TaskCompletionSource<bool>();

            networkManager.Shutdown(discardMessageQueue: true);

            // When the remote host has already disappeared, NGO can complete the
            // client stop synchronously before/while Shutdown is called without
            // delivering another OnClientStopped callback to this subscriber.
            if (!networkManager.IsListening)
            {
                stopCompletion.TrySetResult(true);
            }

            return stopCompletion.Task;
        }

        private void EnsureStopEventSubscription(NetworkManager networkManager)
        {
            if (stopEventSource == networkManager)
            {
                return;
            }

            if (stopEventSource != null)
            {
                stopEventSource.OnClientStopped -= OnNetworkStopped;
                stopEventSource.OnServerStopped -= OnNetworkStopped;
                stopEventSource.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            stopEventSource = networkManager;
            stopEventSource.OnClientStopped += OnNetworkStopped;
            stopEventSource.OnServerStopped += OnNetworkStopped;
            stopEventSource.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnNetworkStopped(bool _)
        {
            hasObservedNetworkStop = true;
            stopCompletion?.TrySetResult(true);
        }

        private void EnsureClientSceneActivationSubscription(NetworkManager networkManager)
        {
            if (clientSceneEventSource == networkManager || networkManager.SceneManager == null)
            {
                return;
            }

            if (clientSceneEventSource != null && clientSceneEventSource.SceneManager != null)
            {
                clientSceneEventSource.SceneManager.OnSceneEvent -= OnClientSceneEvent;
            }

            clientSceneEventSource = networkManager;
            clientSceneEventSource.SceneManager.OnSceneEvent += OnClientSceneEvent;
        }

        private void OnClientSceneEvent(SceneEvent sceneEvent)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsClient
                || networkManager.IsServer
                || sceneEvent.ClientId != networkManager.LocalClientId
                || sceneEvent.SceneEventType != SceneEventType.LoadComplete)
            {
                return;
            }

            var scene = SceneUtilityEx.GetLoadedScene(sceneEvent.SceneName);
            ActivateClientScene(scene);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            ClientDisconnected?.Invoke(clientId);
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

            DisableStageSceneCameras(targetScene);
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
                return Task.FromResult(ActivateClientScene(loadedScene));
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
                var scene = SceneUtilityEx.GetLoadedScene(sceneReference);
                completion.TrySetResult(
                    ActivateClientScene(scene));
            }

            networkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            return completion.Task;
        }

        private static bool ActivateClientScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            DisableStageSceneCameras(scene);
            return SceneManager.GetActiveScene() == scene || SceneManager.SetActiveScene(scene);
        }

        private static void DisableStageSceneCameras(Scene scene)
        {
            // NGO's additive scene path bypasses SceneLoadUtility, so apply the
            // presentation-camera policy here as soon as that stage finishes loading.
            // Only the loaded stage is passed in; the persistent bootstrap/Root scene
            // and its gameplay camera are left untouched.
            SceneLoadUtility.ApplyLoadedSceneCameraSettings(scene, disableCamerasInLoadedScenes: true);
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
            protectedClientSceneHandle = null;

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
                    protectedClientSceneHandle = scene.handle;
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

        private void ConfigureServerSynchronizationMode(NetworkManager networkManager)
        {
            if (!useAdditiveClientSynchronization || networkManager.SceneManager == null)
            {
                return;
            }

            networkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
        }

        private void ConfigureClientSceneProtection(NetworkManager networkManager)
        {
            if (!useAdditiveClientSynchronization || networkManager.SceneManager == null)
            {
                return;
            }

            networkManager.SceneManager.VerifySceneBeforeUnloading = scene =>
                scene.handle != protectedClientSceneHandle;
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
