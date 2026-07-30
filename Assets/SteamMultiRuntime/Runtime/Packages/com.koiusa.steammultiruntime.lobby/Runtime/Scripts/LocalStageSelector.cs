using System;
using System.Collections.Generic;
using System.Linq;
using TNRD;
using UnityEngine;
using UnityEngine.Serialization;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local用ステージ選択コンポーネント
    /// SteamLobbyServiceに依存せず、ステージシーン選択UIのみを提供する
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalStageSelector : MonoBehaviour
    {
        [FormerlySerializedAs("sceneLoader")]
        [SerializeField] private SerializableInterface<IStageSceneCatalog> stageSceneCatalog;

        private string selectedStageName = string.Empty;

        public event Action<string> StageSelected;

        public string SelectedStageName => selectedStageName;

        private IStageSceneCatalog StageSceneCatalog => stageSceneCatalog != null ? stageSceneCatalog.Value : null;

        public IReadOnlyList<string> AvailableStageNames =>
            StageSceneCatalog != null ? StageSceneCatalog.CreatableStageSceneNames : new List<string>();

        private void OnEnable()
        {
            ResolveSceneLoader();

            if (StageSceneCatalog != null && StageSceneCatalog.CreatableStageSceneNames.Count > 0)
            {
                var preferredStageName = StageSelectionPreferences.SelectedStageName;
                selectedStageName = StageSceneCatalog.CreatableStageSceneNames.Contains(preferredStageName)
                    ? preferredStageName
                    : StageSceneCatalog.CreatableStageSceneNames[0];
            }
        }

        private void ResolveSceneLoader()
        {
            if (StageSceneCatalog != null)
            {
                return;
            }

            var loader = GetComponent<IStageSceneCatalog>()
                ?? GetComponentInChildren<IStageSceneCatalog>(true);

            if (loader != null)
            {
                stageSceneCatalog = new SerializableInterface<IStageSceneCatalog>(loader);
            }
        }

        public bool TrySelectStage(string stageName)
        {
            if (StageSceneCatalog == null)
            {
                Debug.LogError("LocalStageSelector: stageSceneCatalog is not set.");
                return false;
            }

            var availableStages = StageSceneCatalog.CreatableStageSceneNames;
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
            var availableStages = StageSceneCatalog?.CreatableStageSceneNames;
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
            var availableStages = StageSceneCatalog?.CreatableStageSceneNames;
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
