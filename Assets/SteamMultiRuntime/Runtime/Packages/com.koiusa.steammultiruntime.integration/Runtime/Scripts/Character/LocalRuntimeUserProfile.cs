using Koiusa.SteamMultiRuntime.Network;
using Koiusa.SteamMultiRuntime.Character;
using UnityEngine;

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
        }

        private void OnDisable()
        {
            if (localManager != null)
            {
                localManager.PlayerSpawned -= OnPlayerSpawned;
            }
        }

        private void OnPlayerSpawned(GameObject player)
        {
            RuntimeUserProfileModelApplyUtility.ApplyToLoader(player, this, nameof(LocalRuntimeUserProfile));
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

            localManager = LocalManager.Singleton ?? FindFirstObjectByType<LocalManager>();
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

