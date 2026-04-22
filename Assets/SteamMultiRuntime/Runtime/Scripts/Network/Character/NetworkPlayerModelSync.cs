using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    /// <summary>
    /// サーバー主導でモデルIDを同期し、各クライアントでローカルPrefabを切り替えるNetcode同期用NetworkBehaviour。
    /// </summary>
    public class NetworkPlayerModelSync : NetworkBehaviour
    {
        [Tooltip("モデルIDリスト（RuntimeUserProfileから自動設定されます）")]
        public CharacterModelIdList modelIdList;
        public CharacterPrefabLoader prefabLoader;

        // サーバーが決定し全クライアントに同期するモデルインデックス
        public NetworkVariable<int> SelectedModelIndex = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            EnsureModelIdList();
            EnsurePrefabLoader();
            SelectedModelIndex.OnValueChanged += OnModelChanged;
            ApplyCurrentModel();
        }

        private new void OnDestroy()
        {
            SelectedModelIndex.OnValueChanged -= OnModelChanged;
            base.OnDestroy();
        }

        // prefabLoaderがnullなら自身にAddComponent<CharacterPrefabLoader>()で自動追加
        private void EnsurePrefabLoader()
        {
            if (prefabLoader == null)
            {
                prefabLoader = GetComponent<CharacterPrefabLoader>();
                if (prefabLoader == null)
                {
                    prefabLoader = gameObject.AddComponent<CharacterPrefabLoader>();
                }
            }
        }

        // 現在のモデルIDを取得
        private string GetCurrentResourceId()
        {
            var ids = modelIdList != null ? modelIdList.modelIds : null;
            if (ids == null || SelectedModelIndex.Value < 0 || SelectedModelIndex.Value >= ids.Length)
                return null;

            var modelId = ids[SelectedModelIndex.Value];
            return modelIdList != null ? modelIdList.ResolveResourcePath(modelId) : modelId;
        }

        // Prefabのロードとセットを一元化
        private void ApplyCurrentModel()
        {
            EnsurePrefabLoader();
            if (prefabLoader == null)
                return;

            var resourceId = GetCurrentResourceId();
            if (string.IsNullOrEmpty(resourceId))
                return;

            prefabLoader.SetPrefabSource(new CharacterPrefabSourceSettings
            {
                characterPrefab = null,
                resourcePath = resourceId
            });

            if (!prefabLoader.IsLoaded)
            {
                Debug.LogWarning($"[NetworkPlayerModelSync] Prefab not found for resourceId: {resourceId}. Using default.");
                return;
            }

            prefabLoader.InstantiateLoaded(transform.position, transform.rotation, transform);
        }

        // NetworkVariable変更時のコールバック
        private void OnModelChanged(int oldIndex, int newIndex)
        {
            ApplyCurrentModel();
        }

        // クライアント→サーバー: モデルインデックスリクエスト
        [ServerRpc]
        public void SetModelIndexServerRpc(int index)
        {
            var ids = modelIdList != null ? modelIdList.modelIds : null;
            if (ids == null || index < 0 || index >= ids.Length) return;
            SelectedModelIndex.Value = index;
        }

        // modelIdListがnullの場合、シーン内のRuntimeUserProfileから取得
        private void EnsureModelIdList()
        {
            if (modelIdList != null)
                return;
            var profile = FindFirstObjectByType<RuntimeUserProfile>();
            if (profile != null)
                modelIdList = profile.ModelIdList;
        }
    }
}
