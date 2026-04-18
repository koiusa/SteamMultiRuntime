using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class RuntimeUserProfile : MonoBehaviour
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

        public CharacterModelIdList ModelIdList => modelIdList;
        public int SelectedModelIndex => selectedModelIndex;

        public void SetSelectedModel(int index)
        {
            selectedModelIndex = index;
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
            if (networkManager == null || networkManager.NetworkConfig == null)
            {
                return;
            }

            var runtimePlayerObject = networkManager.LocalClient?.PlayerObject;
            if (runtimePlayerObject == null)
            {
                return;
            }

            if (!runtimePlayerObject.IsOwner)
            {
                return;
            }

            ApplyToLoader(runtimePlayerObject.gameObject);
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
            if (target == null)
            {
                return;
            }

            var modelIds = modelIdList != null ? modelIdList.modelIds : null;
            if (modelIds == null || selectedModelIndex < 0 || selectedModelIndex >= modelIds.Length)
            {
                Debug.LogWarning("[RuntimeUserProfile] Invalid model index or modelIdList not set.");
                return;
            }

            var sync = target.GetComponent<NetworkPlayerModelSync>();
            if (sync != null)
            {
                sync.modelIdList = modelIdList;
                sync.SetModelIndexServerRpc(selectedModelIndex);
                return;
            }

            var loader = target.GetComponent<CharacterPrefabLoader>();
            if (loader == null)
            {
                return;
            }
            var resourceId = modelIds[selectedModelIndex];
            loader.SetPrefabSource(new CharacterPrefabSourceSettings { characterPrefab = null, resourcePath = resourceId });
        }
    }
}
