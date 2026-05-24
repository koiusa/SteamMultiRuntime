using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ローカルプレイヤー用のモデル同期コンポーネント。
    /// </summary>
    public class LocalPlayerModelSync : MonoBehaviour, IPlayerModelSync
    {
        [Tooltip("モデルIDリスト（RuntimeUserProfileから自動設定されます）")]
        public CharacterModelIdList modelIdList;
        public CharacterPrefabLoader prefabLoader;

        [SerializeField] private int selectedModelIndex;

        public CharacterModelIdList ModelIdList
        {
            get => modelIdList;
            set => modelIdList = value;
        }

        public int CurrentModelIndex => selectedModelIndex;

        private void OnEnable()
        {
            PlayerModelSyncUtility.EnsureModelIdList(ref modelIdList);
            ApplyCurrentModel();
        }

        public void ApplyModelIndex(int index)
        {
            selectedModelIndex = index;
            ApplyCurrentModel();
        }

        private void ApplyCurrentModel()
        {
            PlayerModelSyncUtility.EnsureModelIdList(ref modelIdList);
            var resourceId = PlayerModelSyncUtility.GetCurrentResourceId(modelIdList, selectedModelIndex);
            PlayerModelSyncUtility.ApplyCurrentModel(gameObject, ref prefabLoader, resourceId, nameof(LocalPlayerModelSync));
        }
    }
}
