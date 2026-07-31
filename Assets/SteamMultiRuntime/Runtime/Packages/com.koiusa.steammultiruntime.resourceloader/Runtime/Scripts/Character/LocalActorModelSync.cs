using Koiusa.SteamMultiRuntime.Character;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ローカルプレイヤー用のモデル同期コンポーネント。
    /// </summary>
    public class LocalActorModelSync : MonoBehaviour, IActorModelSync
    {
        [Tooltip("モデルIDリスト（RuntimeUserProfileから自動設定されます）")]
        public CharacterModelIdList modelIdList;
        public ICharacterPrefabLoader prefabLoaderBehaviour;

        [SerializeField] private int selectedModelIndex;

        public CharacterModelIdList ModelIdList
        {
            get => modelIdList;
            set => modelIdList = value;
        }

        public int CurrentModelIndex => selectedModelIndex;

        private void OnEnable()
        {
            ActorModelSyncUtility.EnsureModelIdList(ref modelIdList);
            ApplyCurrentModel();
        }

        public void ApplyModelIndex(int index)
        {
            selectedModelIndex = index;
            ApplyCurrentModel();
        }

        private void ApplyCurrentModel()
        {
            ActorModelSyncUtility.EnsureModelIdList(ref modelIdList);
            var resourceId = ActorModelSyncUtility.GetCurrentResourceId(modelIdList, selectedModelIndex);
            ActorModelSyncUtility.ApplyCurrentModel(gameObject, ref prefabLoaderBehaviour, resourceId, nameof(LocalActorModelSync));
        }
    }
}
