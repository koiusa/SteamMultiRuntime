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
        public ICharacterPrefabLoader prefabLoaderBehaviour;

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
            PlayerModelSyncUtility.EnsurePrefabLoader(gameObject, ref prefabLoaderBehaviour);
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

        private void ApplyCurrentModel()
        {
            PlayerModelSyncUtility.EnsureModelIdList(ref modelIdList);
            var resourceId = PlayerModelSyncUtility.GetCurrentResourceId(modelIdList, SelectedModelIndex.Value);
            PlayerModelSyncUtility.ApplyCurrentModel(gameObject, ref prefabLoaderBehaviour, resourceId, nameof(NetworkPlayerModelSync));
        }

        private void OnModelChanged(int oldIndex, int newIndex)
        {
            ApplyCurrentModel();
        }

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
