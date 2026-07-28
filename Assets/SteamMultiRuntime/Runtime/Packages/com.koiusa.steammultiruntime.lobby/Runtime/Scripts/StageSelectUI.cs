using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// ステージ選択UI
    /// UIElementsのDropdownFieldを使用してステージを選択する
    /// </summary>
    public sealed class StageSelectUI
    {
        private readonly UIDocument uiDocument;
        private DropdownField stageSceneField;

        public event Action<string> StageSelected;

        public string SelectedStageName => stageSceneField != null ? stageSceneField.value : string.Empty;

        public StageSelectUI(UIDocument uiDocument)
        {
            this.uiDocument = uiDocument;
        }

        public void Build(string fieldName = "stage-scene-field")
        {
            if (uiDocument == null)
            {
                Debug.LogError("StageSelectUI: UIDocument is null.");
                return;
            }

            var root = uiDocument.rootVisualElement;
            stageSceneField = root.Q<DropdownField>(fieldName);

            if (stageSceneField == null)
            {
                Debug.LogError($"StageSelectUI: DropdownField '{fieldName}' not found in UIDocument.");
                return;
            }

            stageSceneField.RegisterValueChangedCallback(OnStageSceneChanged);
        }

        public void PopulateStageScenes(IReadOnlyList<string> sceneNames)
        {
            if (stageSceneField == null)
            {
                Debug.LogError("StageSelectUI: stageSceneField is null. Cannot populate stages.");
                return;
            }

            stageSceneField.choices.Clear();

            if (sceneNames == null || sceneNames.Count == 0)
            {
                stageSceneField.SetEnabled(false);
                stageSceneField.value = string.Empty;
                return;
            }

            foreach (var sceneName in sceneNames)
            {
                if (!string.IsNullOrWhiteSpace(sceneName))
                {
                    stageSceneField.choices.Add(sceneName);
                }
            }

            if (stageSceneField.choices.Count == 0)
            {
                stageSceneField.SetEnabled(false);
                stageSceneField.value = string.Empty;
                return;
            }

            stageSceneField.SetEnabled(true);
            if (string.IsNullOrWhiteSpace(stageSceneField.value) || !stageSceneField.choices.Contains(stageSceneField.value))
            {
                stageSceneField.value = stageSceneField.choices[0];
            }
        }

        public void Focus()
        {
            if (stageSceneField == null || !stageSceneField.enabledSelf)
                return;

            stageSceneField.schedule.Execute(stageSceneField.Focus);
        }

        public void Cleanup()
        {
            if (stageSceneField != null)
            {
                stageSceneField.UnregisterValueChangedCallback(OnStageSceneChanged);
            }
        }

        private void OnStageSceneChanged(ChangeEvent<string> evt)
        {
            StageSelected?.Invoke(evt.newValue);
        }

        public bool TrySelectStage(string stageName)
        {
            if (stageSceneField == null)
            {
                Debug.LogError("StageSelectUI: UI is not initialized.");
                return false;
            }

            if (!stageSceneField.choices.Contains(stageName))
            {
                Debug.LogError($"StageSelectUI: Stage '{stageName}' is not available.");
                return false;
            }

            stageSceneField.value = stageName;
            StageSelected?.Invoke(stageName);
            return true;
        }
    }
}
