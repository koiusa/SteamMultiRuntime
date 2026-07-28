using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Koiusa.SteamMultiRuntime.Editor
{
    public static class LocalizationTableInstaller
    {
        // Generated into the consuming project. UPM package contents can be read-only.
        private const string Root = "Assets/SteamMultiRuntimeGenerated/Localization";
        private const string TableName = "UI";

        private static readonly IReadOnlyDictionary<string, (string ja, string en)> Entries =
            new Dictionary<string, (string, string)>
            {
                ["キャラクター選択"] = ("キャラクター選択", "Character Select"),
                ["選択中: なし"] = ("選択中: なし", "Selected: None"),
                ["選択中: {0}"] = ("選択中: {0}", "Selected: {0}"),
                ["なし"] = ("なし", "None"),
                ["キーコンフィグ"] = ("キーコンフィグ", "Key Configuration"),
                ["キーやボタンを押すと、対応する操作がリアルタイムに点灯します。"] = ("キーやボタンを押すと、対応する操作がリアルタイムに点灯します。", "Press a key or button to highlight its action in real time."),
                ["INPUT MONITOR"] = ("入力モニター", "INPUT MONITOR"),
                ["WAITING FOR INPUT"] = ("入力待機中", "WAITING FOR INPUT"),
                ["Ready"] = ("準備完了", "Ready"),
                ["アクション"] = ("アクション", "Action"),
                ["キー / ボタン"] = ("キー / ボタン", "Key / Button"),
                ["読込"] = ("読込", "Load"),
                ["保存"] = ("保存", "Save"),
                ["全リセット"] = ("全リセット", "Reset All"),
                ["閉じる"] = ("閉じる", "Close"),
                ["対象バインドがありません。"] = ("対象バインドがありません。", "No bindings are available."),
                ["● 入力中"] = ("● 入力中", "● ACTIVE"),
                ["変更"] = ("変更", "Change"),
                ["戻す"] = ("戻す", "Reset"),
                ["CONTROLS"] = ("操作ガイド", "CONTROLS"),
                ["LIVE INPUT MONITOR"] = ("リアルタイム入力モニター", "LIVE INPUT MONITOR"),
                ["OPERATIONS"] = ("操作一覧", "OPERATIONS"),
                ["INPUT ASSET NOT SET"] = ("Input Action Asset 未設定", "INPUT ASSET NOT SET"),
                ["ACTION MAP NOT FOUND"] = ("Action Map が見つかりません", "ACTION MAP NOT FOUND"),
                ["Loading..."] = ("読み込み中…", "Loading..."),
                ["Stage Selection"] = ("ステージ選択", "Stage Selection"),
                ["Steam Lobby"] = ("Steam ロビー", "Steam Lobby"),
                ["シーン選択・ロビー管理  [LB: 一覧 / RB: 検索]"] = ("シーン選択・ロビー管理  [LB: 一覧 / RB: 検索]", "Scene and lobby management  [LB: List / RB: Search]"),
                ["ロビー検索・直接参加  [LB: 作成 / RB: 一覧]"] = ("ロビー検索・直接参加  [LB: 作成 / RB: 一覧]", "Search or join directly  [LB: Create / RB: List]"),
                ["ロビーを選択して決定  [↑↓ 選択 / 決定で参加 / LB: 検索 / RB: 作成]"] = ("ロビーを選択して決定  [↑↓ 選択 / 決定で参加 / LB: 検索 / RB: 作成]", "Choose a lobby  [Up/Down: Select / Confirm: Join / LB: Search / RB: Create]"),
                ["Create Lobby"] = ("ロビーを作成", "Create Lobby"),
                ["Refresh"] = ("更新", "Refresh"),
                ["Leave Lobby"] = ("ロビーを退出", "Leave Lobby"),
                ["Join by ID"] = ("IDで参加", "Join by ID"),
                ["Join"] = ("参加", "Join"),
                ["HOST"] = ("ホスト", "HOST"),
                ["JOINED"] = ("参加中", "JOINED"),
                ["FULL"] = ("満員", "FULL"),
                ["Lobbies"] = ("ロビー一覧", "Lobbies"),
                ["Online Members"] = ("オンラインメンバー", "Online Members"),
                ["Search"] = ("検索", "Search"),
                ["Steam: connecting..."] = ("Steam: 接続中…", "Steam: connecting..."),
                ["Current Lobby: none"] = ("現在のロビー: なし", "Current Lobby: none"),
                ["NetworkManager/FacepunchTransport の初期化待ち"] = ("NetworkManager/FacepunchTransport の初期化待ち", "Waiting for NetworkManager/FacepunchTransport"),
                ["メンバー情報なし"] = ("メンバー情報なし", "No member information"),
                ["ロビー未参加"] = ("ロビー未参加", "Not in a lobby"),
                ["Lobby が見つかりませんでした。"] = ("ロビーが見つかりませんでした。", "No lobbies found."),
                ["Unknown"] = ("不明", "Unknown"),
                ["Steam: SteamLobbyService not found"] = ("Steam: SteamLobbyService が見つかりません", "Steam: SteamLobbyService not found"),
                ["Steam: not connected"] = ("Steam: 未接続", "Steam: not connected"),
                ["Steam: waiting for NetworkManager/FacepunchTransport..."] = ("Steam: NetworkManager/FacepunchTransport の初期化待ち…", "Steam: waiting for NetworkManager/FacepunchTransport..."),
                ["Current Lobby: {0}"] = ("現在のロビー: {0}", "Current Lobby: {0}"),
                ["Steam: {0}"] = ("Steam: {0}", "Steam: {0}")
                ,["Ready（'{0}' に一致するバインドがないため全表示中）"] = ("Ready（'{0}' に一致するバインドがないため全表示中）", "Ready (showing all; no bindings match '{0}')")
                ,["設定を読み込みました。"] = ("設定を読み込みました。", "Settings loaded.")
                ,["設定を読み込みました。('{0}' に一致するバインドがないため全表示中)"] = ("設定を読み込みました。('{0}' に一致するバインドがないため全表示中)", "Settings loaded. (showing all; no bindings match '{0}')")
                ,["変更しました: {0}"] = ("変更しました: {0}", "Changed: {0}")
                ,["変更しました: {0}（全表示中）"] = ("変更しました: {0}（全表示中）", "Changed: {0} (showing all)")
                ,["BindingGroup: すべて"] = ("BindingGroup: すべて", "Binding group: All")
                ,["BindingGroup: {0}"] = ("BindingGroup: {0}", "Binding group: {0}")
                ,["BindingGroup: {0}（一致なしのため全表示）"] = ("BindingGroup: {0}（一致なしのため全表示）", "Binding group: {0} (no match; showing all)")
                ,["保存済み設定がありません。\n"] = ("保存済み設定がありません。\n", "No saved settings.\n")
                ,["設定を保存しました。"] = ("設定を保存しました。", "Settings saved.")
                ,["すべてのキー設定をリセットしました。"] = ("すべてのキー設定をリセットしました。", "All key settings were reset.")
                ,["リバインドを開始できませんでした。"] = ("リバインドを開始できませんでした。", "Could not start rebinding.")
                ,["Action の取得に失敗しました。"] = ("Action の取得に失敗しました。", "Could not find the action.")
                ,["バインドをリセットしました。"] = ("バインドをリセットしました。", "Binding reset.")
                ,["新しいキーを入力してください（Escでキャンセル）"] = ("新しいキーを入力してください（Escでキャンセル）", "Press a new key (Esc to cancel)")
                ,["リバインドをキャンセルしました。"] = ("リバインドをキャンセルしました。", "Rebinding canceled.")
                ,["リバインドをキャンセルしました。（全表示中）"] = ("リバインドをキャンセルしました。（全表示中）", "Rebinding canceled. (showing all)")
                ,["リバインドに失敗しました。"] = ("リバインドに失敗しました。", "Rebinding failed.")
                ,["リバインドに失敗しました。（全表示中）"] = ("リバインドに失敗しました。（全表示中）", "Rebinding failed. (showing all)")
                ,["KeyConfigInputActionsConfig が未設定、またはInputActionAssetが未設定です。"] = ("KeyConfigInputActionsConfig が未設定、またはInputActionAssetが未設定です。", "KeyConfigInputActionsConfig or InputActionAsset is not assigned.")
                ,["Stage changed."] = ("ステージを変更しました。", "Stage changed.")
                ,["Stage change failed."] = ("ステージの変更に失敗しました。", "Stage change failed.")
                ,["Lobby created."] = ("ロビーを作成しました。", "Lobby created.")
                ,["Lobby create failed."] = ("ロビーの作成に失敗しました。", "Lobby create failed.")
                ,["Lobby ID が不正です。"] = ("Lobby ID が不正です。", "The lobby ID is invalid.")
                ,["Lobby joined."] = ("ロビーに参加しました。", "Lobby joined.")
                ,["Lobby join failed."] = ("ロビーへの参加に失敗しました。", "Lobby join failed.")
                ,["該当するロビー名が見つかりませんでした。"] = ("該当するロビー名が見つかりませんでした。", "No matching lobby name was found.")
            };

        [MenuItem("Tools/Steam Multi Runtime/Localization/Install or Update Japanese-English Tables")]
        public static void Install()
        {
            Directory.CreateDirectory(Root);
            EnsureLocalizationSettings();
            var ja = GetOrCreateLocale("ja", "Japanese");
            var en = GetOrCreateLocale("en", "English");
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName) ??
                             LocalizationEditorSettings.CreateStringTableCollection(TableName, Root);
            UpdateTable(collection, ja, true);
            UpdateTable(collection, en, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"Localization: installed {Entries.Count} UI entries for Japanese and English.");
        }

        private static void EnsureLocalizationSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "SteamMultiRuntime Localization Settings";
            AssetDatabase.CreateAsset(settings, Root + "/LocalizationSettings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        private static Locale GetOrCreateLocale(string code, string displayName)
        {
            var locale = LocalizationEditorSettings.GetLocale(code);
            if (locale != null) return locale;
            locale = Locale.CreateLocale(code);
            locale.name = displayName + " (" + code + ")";
            AssetDatabase.CreateAsset(locale, $"{Root}/{code}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void UpdateTable(StringTableCollection collection, Locale locale, bool japanese)
        {
            var table = collection.GetTable(locale.Identifier) as StringTable;
            if (table == null)
            {
                collection.AddNewTable(locale.Identifier);
                table = collection.GetTable(locale.Identifier) as StringTable;
            }
            foreach (var pair in Entries)
                table.AddEntry(pair.Key, japanese ? pair.Value.ja : pair.Value.en);
            LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
        }
    }
}
