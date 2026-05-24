using Koiusa.SteamMultiRuntime;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    /// <summary>
    /// サーバー主導でモデルIDを同期し、各クライアントでローカルPrefabを切り替えるNetcode同期用NetworkBehaviour。
    /// </summary>
    public class NetworkPlayerModelSync : NetworkBehaviour, IPlayerModelSync
    {
        [Tooltip("モデルIDリスト（RuntimeUserProfileから自動設定されます）")]
        public CharacterModelIdList modelIdList;
        public CharacterPrefabLoader prefabLoader;

        // サーバーが決定し全クライアントに同期するモデルインデックス
        public NetworkVariable<int> SelectedModelIndex = new NetworkVariable<int>(0);

        public CharacterModelIdList ModelIdList
        {
            get => modelIdList;
            set => modelIdList = value;
        }

        public int CurrentModelIndex => SelectedModelIndex.Value;

        public override void OnNetworkSpawn()
        {
            PlayerModelSyncUtility.EnsureModelIdList(ref modelIdList);
            PlayerModelSyncUtility.EnsurePrefabLoader(gameObject, ref prefabLoader);
            SelectedModelIndex.OnValueChanged += OnModelChanged;
            ApplyCurrentModel();
        }

        private new void OnDestroy()
        {
            SelectedModelIndex.OnValueChanged -= OnModelChanged;
            base.OnDestroy();
        }

        public void ApplyModelIndex(int index)
        {
            SetModelIndexServerRpc(index);
        }

        // Prefabのロードとセットを一元化
        private void ApplyCurrentModel()
        {
            PlayerModelSyncUtility.EnsureModelIdList(ref modelIdList);
            var resourceId = PlayerModelSyncUtility.GetCurrentResourceId(modelIdList, SelectedModelIndex.Value);
            PlayerModelSyncUtility.ApplyCurrentModel(gameObject, ref prefabLoader, resourceId, nameof(NetworkPlayerModelSync));
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
            if (ids == null || index < 0 || index >= ids.Length)
            {
                return;
            }

            SelectedModelIndex.Value = index;
        }
    }
}
