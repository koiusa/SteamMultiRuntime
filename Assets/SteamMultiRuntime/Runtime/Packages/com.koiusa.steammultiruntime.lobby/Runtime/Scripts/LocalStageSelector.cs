using System;
using System.Collections.Generic;
using System.Linq;
using TNRD;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local用ステージ選択コンポーネント
    /// SteamLobbyServiceに依存せず、ステージシーン選択UIのみを提供する
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalStageSelector : MonoBehaviour
    {
        [SerializeField] private SerializableInterface<ISteamLobbySceneLoader> sceneLoader;

        private string selectedStageName = string.Empty;

        public event Action<string> StageSelected;

        public string SelectedStageName => selectedStageName;

        private ISteamLobbySceneLoader SceneLoader => sceneLoader != null ? sceneLoader.Value : null;

        public IReadOnlyList<string> AvailableStageNames =>
            SceneLoader != null ? SceneLoader.CreatableStageSceneNames : new List<string>();

        private void OnEnable()
        {
            ResolveSceneLoader();

            if (SceneLoader != null && SceneLoader.CreatableStageSceneNames.Count > 0)
            {
                var preferredStageName = StageSelectionPreferences.SelectedStageName;
                selectedStageName = SceneLoader.CreatableStageSceneNames.Contains(preferredStageName)
                    ? preferredStageName
                    : SceneLoader.CreatableStageSceneNames[0];
            }
        }

        private void ResolveSceneLoader()
        {
            if (SceneLoader != null)
            {
                return;
            }

            var loader = GetComponent<ISteamLobbySceneLoader>()
                ?? GetComponentInChildren<ISteamLobbySceneLoader>(true)
                ?? FindFirstObjectByType<LocalSceneFlowLoader>(FindObjectsInactive.Include) as ISteamLobbySceneLoader;

            if (loader != null)
            {
                sceneLoader = new SerializableInterface<ISteamLobbySceneLoader>(loader);
            }
        }

        public bool TrySelectStage(string stageName)
        {
            if (SceneLoader == null)
            {
                Debug.LogError("LocalStageSelector: sceneLoader is not set.");
                return false;
            }

            var availableStages = SceneLoader.CreatableStageSceneNames;
            if (availableStages == null || availableStages.Count == 0)
            {
                Debug.LogError("LocalStageSelector: No stages available.");
                return false;
            }

            if (!availableStages.Any(s => s == stageName))
            {
                Debug.LogError($"LocalStageSelector: Stage '{stageName}' is not available.");
                return false;
            }

            selectedStageName = stageName;
            StageSelectionPreferences.Save(stageName);
            StageSelected?.Invoke(stageName);
            return true;
        }

        public void SelectNextStage()
        {
            var availableStages = SceneLoader?.CreatableStageSceneNames;
            if (availableStages == null || availableStages.Count == 0)
            {
                return;
            }

            var currentIndex = FindStageIndex(availableStages, selectedStageName);
            var nextIndex = (currentIndex + 1) % availableStages.Count;
            TrySelectStage(availableStages[nextIndex]);
        }

        public void SelectPreviousStage()
        {
            var availableStages = SceneLoader?.CreatableStageSceneNames;
            if (availableStages == null || availableStages.Count == 0)
            {
                return;
            }

            var currentIndex = FindStageIndex(availableStages, selectedStageName);
            var prevIndex = currentIndex - 1 < 0 ? availableStages.Count - 1 : currentIndex - 1;
            TrySelectStage(availableStages[prevIndex]);
        }

        private static int FindStageIndex(IReadOnlyList<string> stages, string stageName)
        {
            for (var i = 0; i < stages.Count; i++)
            {
                if (stages[i] == stageName)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
