using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    public interface IKeyConfigLocalizer
    {
        event Action LocaleChanged;
        string Get(string key, params object[] arguments);
        bool TryResolveKey(string keyOrSource, out string key);
    }

    public static class KeyConfigLocalization
    {
        private static readonly BuiltInKeyConfigLocalizer Fallback = new BuiltInKeyConfigLocalizer();
        private static IKeyConfigLocalizer provider = Fallback;

        static KeyConfigLocalization() => Fallback.LocaleChanged += OnProviderLocaleChanged;

        internal static event Action LocaleChanged;

        internal static IKeyConfigLocalizer Provider
        {
            get => provider;
            set
            {
                var next = value ?? Fallback;
                if (ReferenceEquals(provider, next)) return;
                provider.LocaleChanged -= OnProviderLocaleChanged;
                provider = next;
                provider.LocaleChanged += OnProviderLocaleChanged;
                LocaleChanged?.Invoke();
            }
        }

        internal static KeyConfigLanguage BuiltInLocale
        {
            get => Fallback.Locale;
            set => Fallback.Locale = value;
        }

        internal static string Get(string key, params object[] arguments) => provider.Get(key, arguments);

        internal static bool TryResolveKey(string keyOrSource, out string key) =>
            provider.TryResolveKey(keyOrSource, out key);

        internal static void Set(TextElement element, string key, params object[] arguments)
        {
            if (element != null) element.text = Get(key, arguments);
        }

        private static void OnProviderLocaleChanged() => LocaleChanged?.Invoke();

        public static void SetLocalizer(IKeyConfigLocalizer localizer) => Provider = localizer;

    }

    public enum KeyConfigLanguage
    {
        English,
        Japanese
    }

    public sealed class BuiltInKeyConfigLocalizer : IKeyConfigLocalizer
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["keyconfig.title"] = "Key Configuration", ["keyconfig.help"] = "Press a key or button to highlight its action.",
            ["keyconfig.input_monitor"] = "INPUT MONITOR", ["keyconfig.action"] = "Action", ["keyconfig.binding"] = "Key / Button",
            ["keyconfig.load"] = "Load", ["keyconfig.save"] = "Save", ["keyconfig.reset_all"] = "Reset All", ["keyconfig.close"] = "Close",
            ["keyconfig.change"] = "Change", ["keyconfig.reset"] = "Reset", ["keyconfig.binding_group"] = "Binding Group", ["keyconfig.all"] = "All",
            ["keyconfig.add_modifier"] = "+", ["keyconfig.remove_modifier"] = "−",
            ["keyconfig.add_modifier_tooltip"] = "Add one modifier key.", ["keyconfig.remove_modifier_tooltip"] = "Remove one modifier key.",
            ["keyconfig.modifier_added"] = "Modifier added.", ["keyconfig.modifier_removed"] = "Modifier removed.",
            ["keyconfig.no_bindings"] = "No bindings", ["keyconfig.waiting_input"] = "Waiting for input", ["keyconfig.input_detected"] = "Input detected",
            ["keyconfig.inputs_detected"] = "{0} inputs detected", ["keyconfig.config_missing"] = "Input Actions configuration is missing.",
            ["keyconfig.no_saved_settings"] = "No saved settings.", ["keyconfig.saved"] = "Settings saved.", ["keyconfig.reset_all_done"] = "All bindings reset.",
            ["keyconfig.persistence_missing"] = "No binding storage is configured.",
            ["keyconfig.rebind_start_failed"] = "Could not start rebinding.", ["keyconfig.action_missing"] = "Action was not found.",
            ["keyconfig.binding_reset"] = "Binding reset.", ["keyconfig.enter_new_key"] = "Press a new key or button.", ["keyconfig.changed"] = "Changed to {0}.",
            ["keyconfig.changed_fallback"] = "Changed to {0} using the fallback binding group.", ["keyconfig.rebind_canceled"] = "Change canceled.",
            ["keyconfig.rebind_canceled_fallback"] = "Change canceled (fallback binding group).", ["keyconfig.rebind_failed"] = "Change failed.",
            ["keyconfig.rebind_failed_fallback"] = "Change failed (fallback binding group).", ["keyconfig.ready_fallback"] = "Ready ({0}).",
            ["keyconfig.loaded_fallback"] = "Loaded ({0}).", ["keyconfig.loaded"] = "Settings loaded.", ["keyconfig.group_all"] = "Showing all binding groups.",
            ["keyconfig.group_fallback"] = "Binding group: {0} (fallback)", ["keyconfig.group"] = "Binding group: {0}",
            ["keyconfig.conflict_message"] = "{0} conflicts with {1}.", ["keyconfig.conflict_replace"] = "Replace Existing",
            ["keyconfig.conflict_keep"] = "Keep Both", ["keyconfig.conflict_cancel"] = "Cancel",
            ["keyconfig.section_movement"] = "MOVEMENT", ["keyconfig.section_combat"] = "COMBAT / TARGETING",
            ["keyconfig.section_grapple"] = "GRAPPLE", ["keyconfig.section_camera"] = "CAMERA / INTERACTION",
            ["keyconfig.input_asset_missing"] = "INPUT ASSET NOT SET", ["keyconfig.action_map_missing"] = "ACTION MAP NOT FOUND",
            ["keyconfig.switch_device_tooltip"] = "Click to switch keyboard/gamepad layout",
            ["キーコンフィグ"] = "Key Configuration", ["キーやボタンを押すと、対応する操作がリアルタイムに点灯します。"] = "Press a key or button to highlight its action.",
            ["アクション"] = "Action", ["キー / ボタン"] = "Key / Button", ["読込"] = "Load", ["保存"] = "Save", ["全リセット"] = "Reset All", ["閉じる"] = "Close"
        };

        private static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
        {
            ["keyconfig.title"] = "キーコンフィグ", ["keyconfig.help"] = "キーやボタンを押すと、対応する操作がリアルタイムに点灯します。",
            ["keyconfig.input_monitor"] = "入力モニター", ["keyconfig.action"] = "アクション", ["keyconfig.binding"] = "キー / ボタン",
            ["keyconfig.load"] = "読込", ["keyconfig.save"] = "保存", ["keyconfig.reset_all"] = "全リセット", ["keyconfig.close"] = "閉じる",
            ["keyconfig.change"] = "変更", ["keyconfig.reset"] = "リセット", ["keyconfig.binding_group"] = "バインドグループ", ["keyconfig.all"] = "すべて",
            ["keyconfig.add_modifier"] = "＋", ["keyconfig.remove_modifier"] = "－",
            ["keyconfig.add_modifier_tooltip"] = "修飾キーを1つ追加します。", ["keyconfig.remove_modifier_tooltip"] = "修飾キーを1つ削除します。",
            ["keyconfig.modifier_added"] = "修飾キーを追加しました。", ["keyconfig.modifier_removed"] = "修飾キーを削除しました。",
            ["keyconfig.no_bindings"] = "バインドがありません", ["keyconfig.waiting_input"] = "入力待ち", ["keyconfig.input_detected"] = "入力を検出しました",
            ["keyconfig.inputs_detected"] = "{0} 件の入力を検出", ["keyconfig.config_missing"] = "Input Actions設定がありません。", ["keyconfig.no_saved_settings"] = "保存済み設定がありません。",
            ["keyconfig.saved"] = "保存しました。", ["keyconfig.reset_all_done"] = "すべてリセットしました。", ["keyconfig.rebind_start_failed"] = "変更を開始できませんでした。",
            ["keyconfig.persistence_missing"] = "キー設定の保存先が設定されていません。",
            ["keyconfig.action_missing"] = "アクションが見つかりません。", ["keyconfig.binding_reset"] = "リセットしました。", ["keyconfig.enter_new_key"] = "新しいキーまたはボタンを押してください。",
            ["keyconfig.changed"] = "{0} に変更しました。", ["keyconfig.changed_fallback"] = "{0} に変更しました（代替グループ）。", ["keyconfig.rebind_canceled"] = "変更をキャンセルしました。",
            ["keyconfig.rebind_canceled_fallback"] = "変更をキャンセルしました（代替グループ）。", ["keyconfig.rebind_failed"] = "変更に失敗しました。",
            ["keyconfig.rebind_failed_fallback"] = "変更に失敗しました（代替グループ）。", ["keyconfig.ready_fallback"] = "準備完了（{0}）。", ["keyconfig.loaded_fallback"] = "読み込みました（{0}）。",
            ["keyconfig.loaded"] = "読み込みました。", ["keyconfig.group_all"] = "すべてのバインドグループを表示中。", ["keyconfig.group_fallback"] = "バインドグループ: {0}（代替）",
            ["keyconfig.group"] = "バインドグループ: {0}", ["keyconfig.conflict_message"] = "{0} は {1} と競合します。", ["keyconfig.conflict_replace"] = "既存を解除",
            ["keyconfig.conflict_keep"] = "両方に設定", ["keyconfig.conflict_cancel"] = "キャンセル",
            ["keyconfig.section_movement"] = "移動", ["keyconfig.section_combat"] = "戦闘・ターゲット",
            ["keyconfig.section_grapple"] = "グラップル", ["keyconfig.section_camera"] = "カメラ・インタラクション",
            ["keyconfig.input_asset_missing"] = "Input Action Asset 未設定", ["keyconfig.action_map_missing"] = "Action Map が見つかりません",
            ["keyconfig.switch_device_tooltip"] = "クリックしてキーボード・ゲームパッド表示を切り替え",
            ["キーコンフィグ"] = "キーコンフィグ", ["キーやボタンを押すと、対応する操作がリアルタイムに点灯します。"] = "キーやボタンを押すと、対応する操作がリアルタイムに点灯します。",
            ["アクション"] = "アクション", ["キー / ボタン"] = "キー / ボタン", ["読込"] = "読込", ["保存"] = "保存", ["全リセット"] = "全リセット", ["閉じる"] = "閉じる"
        };

        private KeyConfigLanguage locale;
        public event Action LocaleChanged;

        public BuiltInKeyConfigLocalizer() : this(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? KeyConfigLanguage.Japanese : KeyConfigLanguage.English) { }
        public BuiltInKeyConfigLocalizer(KeyConfigLanguage initialLocale) => locale = initialLocale;

        public KeyConfigLanguage Locale
        {
            get => locale;
            set { if (locale == value) return; locale = value; LocaleChanged?.Invoke(); }
        }

        public string Get(string key, params object[] arguments)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var table = locale == KeyConfigLanguage.Japanese ? Japanese : English;
            var text = table.TryGetValue(key, out var localized) ? localized : HumanizeUnknownKey(key);
            if (arguments == null || arguments.Length == 0) return text;
            try { return string.Format(text, arguments); }
            catch (FormatException) { return text; }
        }

        public bool TryResolveKey(string keyOrSource, out string key)
        {
            key = keyOrSource;
            return !string.IsNullOrWhiteSpace(keyOrSource) && (English.ContainsKey(keyOrSource) || Japanese.ContainsKey(keyOrSource));
        }

        private static string HumanizeUnknownKey(string key)
        {
            if (!key.StartsWith("keyconfig.", StringComparison.Ordinal)) return key;
            var value = key.Substring("keyconfig.".Length).Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
        }
    }

    internal sealed class LocalizedTextBinding : IDisposable
    {
        private readonly TextElement element;
        private string key;
        private object[] arguments = Array.Empty<object>();

        public LocalizedTextBinding(TextElement element)
        {
            this.element = element;
            KeyConfigLocalization.LocaleChanged += Refresh;
        }

        public void Set(string localizationKey, params object[] formatArguments)
        {
            key = localizationKey;
            arguments = formatArguments ?? Array.Empty<object>();
            Refresh();
        }

        public void Clear()
        {
            key = string.Empty;
            arguments = Array.Empty<object>();
            if (element != null) element.text = string.Empty;
        }

        public void Refresh()
        {
            if (element != null && !string.IsNullOrEmpty(key))
                element.text = KeyConfigLocalization.Get(key, arguments);
        }

        public void Dispose() => KeyConfigLocalization.LocaleChanged -= Refresh;
    }

    internal sealed class LocalizedVisualTree : IDisposable
    {
        private readonly List<(TextElement element, string key)> entries = new();

        private LocalizedVisualTree(VisualElement root, IReadOnlyCollection<TextElement> excluded)
        {
            var excludedSet = excluded == null ? null : new HashSet<TextElement>(excluded);
            foreach (var element in root.Query<TextElement>().ToList())
            {
                if (!string.IsNullOrWhiteSpace(element.text) &&
                    (excludedSet == null || !excludedSet.Contains(element)) &&
                    KeyConfigLocalization.TryResolveKey(element.text, out var key))
                    entries.Add((element, key));
            }
            KeyConfigLocalization.LocaleChanged += Refresh;
            Refresh();
        }

        public static LocalizedVisualTree Bind(VisualElement root, params TextElement[] excluded) =>
            root == null ? null : new LocalizedVisualTree(root, excluded);

        public void Refresh()
        {
            foreach (var (element, key) in entries)
                if (element != null) element.text = KeyConfigLocalization.Get(key);
        }

        public void Dispose()
        {
            KeyConfigLocalization.LocaleChanged -= Refresh;
            entries.Clear();
        }
    }
}
