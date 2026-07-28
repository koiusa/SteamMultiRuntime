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
        private static readonly string[] LocaleCodes = { "ja", "en" };
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
            dropdown.UnregisterValueChangedCallback(OnDropdownChanged);
            dropdown.RegisterValueChangedCallback(OnDropdownChanged);
            RefreshChoicesAndValue();
        }

        private void OnDropdownChanged(ChangeEvent<string> evt)
        {
            var index = dropdown?.choices?.IndexOf(evt.newValue) ?? -1;
            if (index >= 0 && index < LocaleCodes.Length)
                GameLocalization.SelectLocale(LocaleCodes[index]);
        }

        private void OnLocaleChanged(UnityEngine.Localization.Locale _) => RefreshChoicesAndValue();

        private void RefreshChoicesAndValue()
        {
            if (dropdown == null) return;
            dropdown.label = GameLocalization.Get("locale.label");
            dropdown.choices = new List<string>
            {
                GameLocalization.Get("locale.japanese"),
                GameLocalization.Get("locale.english")
            };
            var selectedCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
            var index = System.Array.IndexOf(LocaleCodes, selectedCode);
            dropdown.SetValueWithoutNotify(dropdown.choices[index >= 0 ? index : 0]);
        }
    }
}
