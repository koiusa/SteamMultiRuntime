using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalRuntimeUserProfile : PlayerModelProfileBase
    {
        [Header("References")]
        [SerializeField] private GameObject localPlayerObject;

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
            ApplyToLocalPlayerPrefabLoader();
        }

        private void OnEnable()
        {
            if (applyOnEnable)
            {
                ApplyToLocalPlayerPrefabLoader();
            }

            if (applyOnSceneLoaded)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            ApplyToLocalPlayerPrefabLoader();
        }

        public void ApplyToLocalPlayerPrefabLoader()
        {
            if (TryResolveLocalPlayerObject(out var target))
            {
                RuntimeUserProfileModelApplyUtility.ApplyToLoader(target, this, nameof(LocalRuntimeUserProfile));
            }
        }

        private bool TryResolveLocalPlayerObject(out GameObject target)
        {
            if (localPlayerObject != null)
            {
                target = localPlayerObject;
                return true;
            }

            var localSync = FindFirstObjectByType<LocalPlayerModelSync>();
            if (localSync != null)
            {
                target = localSync.gameObject;
                return true;
            }

            var localController = FindFirstObjectByType<LocalPlayerController>();
            if (localController != null)
            {
                target = localController.gameObject;
                return true;
            }

            target = null;
            return false;
        }
    }
}
