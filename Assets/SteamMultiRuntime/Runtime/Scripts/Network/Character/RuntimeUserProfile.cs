using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class RuntimeUserProfile : PlayerModelProfileBase
    {
        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Character Prefab Loader Source")]
        [Tooltip("モデルIDリスト（ScriptableObject参照）")]
        [SerializeField] private CharacterModelIdList modelIdList;
        [Tooltip("選択するモデルIDのインデックス")]
        [SerializeField] private int selectedModelIndex;
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool applyOnSceneLoaded = true;

        public override CharacterModelIdList ModelIdList => modelIdList;
        public override int SelectedModelIndex => selectedModelIndex;

        public override void SetSelectedModel(int index)
        {
            selectedModelIndex = index;
        }

        public override void ApplySelectedModel()
        {
            ApplyToNetworkPlayerPrefabLoader();
        }

        private void Awake()
        {
            ResolveNetworkManager();
        }

        private void OnEnable()
        {
            ResolveNetworkManager();
            SubscribeNetworkEvents();

            if (applyOnEnable)
            {
                ApplyToNetworkPlayerPrefabLoader();
            }

            if (applyOnSceneLoaded)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeNetworkEvents();
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            ApplyToNetworkPlayerPrefabLoader();
        }

        public void ApplyToNetworkPlayerPrefabLoader()
        {
            ResolveNetworkManager();
            TryApplyToNetworkLocalPlayer();
        }

        private bool TryApplyToNetworkLocalPlayer()
        {
            if (networkManager == null || networkManager.NetworkConfig == null)
            {
                return false;
            }

            var runtimePlayerObject = networkManager.LocalClient?.PlayerObject;
            if (runtimePlayerObject == null || !runtimePlayerObject.IsOwner)
            {
                return false;
            }

            ApplyToLoader(runtimePlayerObject.gameObject);
            return true;
        }

        private void ResolveNetworkManager()
        {
            if (networkManager != null)
            {
                return;
            }

            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }
        }

        private void SubscribeNetworkEvents()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
        }

        private void UnsubscribeNetworkEvents()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager == null || clientId != networkManager.LocalClientId)
            {
                return;
            }

            ApplyToNetworkPlayerPrefabLoader();
        }

        private void ApplyToLoader(GameObject target)
        {
            RuntimeUserProfileModelApplyUtility.ApplyToLoader(target, this, nameof(RuntimeUserProfile));
        }
    }
}
