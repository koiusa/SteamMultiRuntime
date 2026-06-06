using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalRuntimeUserProfile : PlayerModelProfileBase
    {
        [Header("References")]
        [SerializeField] private LocalManager localManager;

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

        private void Awake()
        {
            ResolveLocalManager();
        }

        private void OnEnable()
        {
            ResolveLocalManager();

            if (localManager != null)
            {
                localManager.PlayerSpawned += OnPlayerSpawned;
            }

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
            if (localManager != null)
            {
                localManager.PlayerSpawned -= OnPlayerSpawned;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnPlayerSpawned(GameObject player)
        {
            RuntimeUserProfileModelApplyUtility.ApplyToLoader(player, this, nameof(LocalRuntimeUserProfile));
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            ApplyToLocalPlayerPrefabLoader();
        }

        public void ApplyToLocalPlayerPrefabLoader()
        {
            ResolveLocalManager();
            if (TryResolveLocalPlayerObject(out var target))
            {
                RuntimeUserProfileModelApplyUtility.ApplyToLoader(target, this, nameof(LocalRuntimeUserProfile));
            }
        }

        private void ResolveLocalManager()
        {
            if (localManager != null)
            {
                return;
            }

            var resolved = LocalManager.Singleton;
            if (resolved == null)
            {
                resolved = FindFirstObjectByType<LocalManager>();
            }

            if (resolved == null)
            {
                return;
            }

            localManager = resolved;

            // 有効時に購読を逃した場合のために、ここでも購読する
            if (isActiveAndEnabled)
            {
                localManager.PlayerSpawned += OnPlayerSpawned;
            }
        }

        private bool TryResolveLocalPlayerObject(out GameObject target)
        {
            if (localManager != null && localManager.LocalPlayerObject != null)
            {
                target = localManager.LocalPlayerObject;
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
