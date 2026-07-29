using System;
using System.Collections.Generic;
using Koiusa.Input;
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
        private const string ThemeStyleSheetPath = "UI/StageSelect/LocalStageSelectTheme";

        private readonly UIDocument uiDocument;
        private DropdownField stageSceneField;
        private StyleSheet popupStyleSheet;
        private VisualElement popupStyleHost;
        private VisualElement pendingPanelRoot;

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
            ApplyPopupStyle(root);
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
            var preferredStageName = StageSelectionPreferences.SelectedStageName;
            if (stageSceneField.choices.Contains(preferredStageName))
            {
                stageSceneField.SetValueWithoutNotify(preferredStageName);
            }
            else if (string.IsNullOrWhiteSpace(stageSceneField.value) || !stageSceneField.choices.Contains(stageSceneField.value))
            {
                stageSceneField.SetValueWithoutNotify(stageSceneField.choices[0]);
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

            if (pendingPanelRoot != null)
            {
                pendingPanelRoot.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
                pendingPanelRoot = null;
            }

            if (popupStyleHost != null && popupStyleSheet != null)
            {
                popupStyleHost.styleSheets.Remove(popupStyleSheet);
                popupStyleHost = null;
            }
        }

        private void ApplyPopupStyle(VisualElement root)
        {
            popupStyleSheet ??= Resources.Load<StyleSheet>(ThemeStyleSheetPath);
            if (popupStyleSheet == null)
            {
                Debug.LogWarning($"StageSelectUI: Popup stylesheet not found at '{ThemeStyleSheetPath}'.");
                return;
            }

            if (root.panel != null)
            {
                AttachPopupStyle(root.panel.visualTree);
                return;
            }

            pendingPanelRoot = root;
            pendingPanelRoot.RegisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
        }

        private void OnRootAttachedToPanel(AttachToPanelEvent evt)
        {
            pendingPanelRoot?.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
            pendingPanelRoot = null;
            AttachPopupStyle(evt.destinationPanel.visualTree);
        }

        private void AttachPopupStyle(VisualElement panelRoot)
        {
            if (popupStyleHost == panelRoot)
            {
                return;
            }

            if (popupStyleHost != null)
            {
                popupStyleHost.styleSheets.Remove(popupStyleSheet);
            }

            popupStyleHost = panelRoot;
            if (!popupStyleHost.styleSheets.Contains(popupStyleSheet))
            {
                popupStyleHost.styleSheets.Add(popupStyleSheet);
            }
        }

        private void OnStageSceneChanged(ChangeEvent<string> evt)
        {
            StageSelectionPreferences.Save(evt.newValue);
            StageSelected?.Invoke(evt.newValue);
        }

        public void MoveSelection(UiNavigationDirection direction)
        {
            if (stageSceneField == null || stageSceneField.choices.Count == 0 ||
                direction == UiNavigationDirection.None)
            {
                return;
            }

            var offset = direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Left
                ? -1
                : 1;

            var currentIndex = stageSceneField.choices.IndexOf(stageSceneField.value);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = (currentIndex + offset + stageSceneField.choices.Count) %
                stageSceneField.choices.Count;
            stageSceneField.SetValueWithoutNotify(stageSceneField.choices[nextIndex]);
        }

        public void SubmitSelection()
        {
            if (stageSceneField == null || string.IsNullOrWhiteSpace(stageSceneField.value))
            {
                return;
            }

            StageSelectionPreferences.Save(stageSceneField.value);
            StageSelected?.Invoke(stageSceneField.value);
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
