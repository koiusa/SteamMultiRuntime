using Koiusa.SteamMultiRuntime;
using Koiusa.SteamMultiRuntime.Character;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Network
{
    /// <summary>
    /// サーバー主導でモデルIDを同期し、各クライアントでローカルPrefabを切り替えるNetcode同期用NetworkBehaviour。
    /// </summary>
    public class NetworkActorModelSync : NetworkBehaviour, IActorModelSync
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
            ActorModelSyncUtility.EnsureModelIdList(ref modelIdList);
            ActorModelSyncUtility.EnsurePrefabLoader(gameObject, ref prefabLoaderBehaviour);
            SelectedModelIndex.OnValueChanged += OnModelChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ApplyCurrentModel();
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            base.OnNetworkDespawn();
        }

        private new void OnDestroy()
        {
            SelectedModelIndex.OnValueChanged -= OnModelChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            base.OnDestroy();
        }

        public void ApplyModelIndex(int index)
        {
            SetModelIndexServerRpc(index);
        }

        private void ApplyCurrentModel()
        {
            ActorModelSyncUtility.EnsureModelIdList(ref modelIdList);
            var resourceId = ActorModelSyncUtility.GetCurrentResourceId(modelIdList, SelectedModelIndex.Value);
            ActorModelSyncUtility.ApplyCurrentModel(gameObject, ref prefabLoaderBehaviour, resourceId, nameof(NetworkActorModelSync));
        }

        private void OnModelChanged(int oldIndex, int newIndex)
        {
            ApplyCurrentModel();
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            ApplyCurrentModelIfMissing();
        }

        private void OnActiveSceneChanged(Scene _, Scene __)
        {
            ApplyCurrentModelIfMissing();
        }

        private void ApplyCurrentModelIfMissing()
        {
            var loader = prefabLoaderBehaviour as ICharacterPrefabLoader;
            if (loader != null && loader.IsCharacterReady)
            {
                return;
            }

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
