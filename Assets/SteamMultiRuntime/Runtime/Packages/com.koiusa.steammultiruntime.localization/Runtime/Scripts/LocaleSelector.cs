using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Localization
{
    /// <summary>Optional UI Toolkit language selector. Add a DropdownField named "locale-dropdown".</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LocaleSelector : MonoBehaviour
    {
        [SerializeField] private string dropdownName = "locale-dropdown";
        private DropdownField dropdown;

        private void OnEnable()
        {
            if (!LocalizationSettings.HasSettings) return;
            LocalizationSettings.InitializationOperation.Completed += OnLocalizationReady;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDisable()
        {
            if (!LocalizationSettings.HasSettings) return;
            LocalizationSettings.InitializationOperation.Completed -= OnLocalizationReady;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            if (dropdown != null) dropdown.UnregisterValueChangedCallback(OnDropdownChanged);
            dropdown = null;
        }

        public void SetJapanese() => GameLocalization.SelectLocale("ja");
        public void SetEnglish() => GameLocalization.SelectLocale("en");

        private void OnLocalizationReady(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<LocalizationSettings> _)
        {
            dropdown = GetComponent<UIDocument>().rootVisualElement.Q<DropdownField>(dropdownName);
            if (dropdown == null) return;
            dropdown.choices = new List<string> { "日本語", "English" };
            dropdown.UnregisterValueChangedCallback(OnDropdownChanged);
            dropdown.RegisterValueChangedCallback(OnDropdownChanged);
            RefreshValue();
        }

        private void OnDropdownChanged(ChangeEvent<string> evt) =>
            GameLocalization.SelectLocale(evt.newValue == "English" ? "en" : "ja");

        private void OnLocaleChanged(UnityEngine.Localization.Locale _) => RefreshValue();

        private void RefreshValue()
        {
            if (dropdown == null) return;
            var value = LocalizationSettings.SelectedLocale?.Identifier.Code == "en" ? "English" : "日本語";
            dropdown.SetValueWithoutNotify(value);
        }
    }
}
