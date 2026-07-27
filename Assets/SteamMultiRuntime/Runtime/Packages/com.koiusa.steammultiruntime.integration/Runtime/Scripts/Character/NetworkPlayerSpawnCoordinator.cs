using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Places server-authoritative player objects whenever a client joins or the stage changes.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkPlayerSpawnCoordinator : MonoBehaviour
    {
        private NetworkManager networkManager;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnClientConnected(ulong clientId)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!networkManager.IsServer
                || (activeScene == gameObject.scene && !PlayerSpawnService.HasSpawnPoint(activeScene))
                || !networkManager.ConnectedClients.TryGetValue(clientId, out var client)
                || client.PlayerObject == null)
            {
                return;
            }

            PlayerSpawnService.TryPlace(client.PlayerObject.gameObject, activeScene, clientId);
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            if (!networkManager.IsServer || !newScene.IsValid())
            {
                return;
            }

            foreach (var pair in networkManager.ConnectedClients)
            {
                var playerObject = pair.Value.PlayerObject;
                if (playerObject != null)
                {
                    PlayerSpawnService.TryPlace(playerObject.gameObject, newScene, pair.Key);
                }
            }
        }
    }
}
