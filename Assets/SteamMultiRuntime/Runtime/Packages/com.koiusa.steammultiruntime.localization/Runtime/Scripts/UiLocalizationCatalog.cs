using System;
using System.Collections.Generic;

namespace Koiusa.SteamMultiRuntime.Localization
{
    /// <summary>Stable keys and package-owned default translations used to generate host-project tables.</summary>
    public static class UiLocalizationCatalog
    {
        public readonly struct Entry
        {
            public Entry(string key, string japanese, string english, params string[] aliases)
            {
                Key = key;
                Japanese = japanese;
                English = english;
                Aliases = aliases ?? Array.Empty<string>();
            }

            public string Key { get; }
            public string Japanese { get; }
            public string English { get; }
            public IReadOnlyList<string> Aliases { get; }
        }

        public static readonly IReadOnlyList<Entry> Entries = new[]
        {
            E("character.title", "キャラクター選択", "Character Select"),
            E("character.selected_none", "選択中: なし", "Selected: None"),
            E("character.selected", "選択中: {0}", "Selected: {0}"),
            E("common.none", "なし", "None"),
            E("common.ready", "準備完了", "Ready", "Ready"),
            E("common.close", "閉じる", "Close"),
            E("common.unknown", "不明", "Unknown", "Unknown"),
            E("loading.default", "読み込み中…", "Loading...", "Loading..."),
            E("stage.title", "ステージ選択", "Stage Selection", "Stage Selection"),
            E("stage.field", "ステージ", "Stage", "Stage"),

            E("keyconfig.title", "キーコンフィグ", "Key Configuration"),
            E("keyconfig.help", "キーやボタンを押すと、対応する操作がリアルタイムに点灯します。", "Press a key or button to highlight its action in real time."),
            E("keyconfig.input_monitor", "入力モニター", "INPUT MONITOR", "INPUT MONITOR"),
            E("keyconfig.waiting_input", "入力待機中", "WAITING FOR INPUT", "WAITING FOR INPUT"),
            E("keyconfig.input_detected", "入力を1件検出", "1 INPUT DETECTED"),
            E("keyconfig.inputs_detected", "入力を{0}件検出", "{0} INPUTS DETECTED"),
            E("keyconfig.action", "アクション", "Action"),
            E("keyconfig.binding", "キー / ボタン", "Key / Button"),
            E("keyconfig.binding_group", "バインドグループ", "Binding Group", "BindingGroup"),
            E("keyconfig.all", "すべて", "All", "すべて"),
            E("keyconfig.load", "読込", "Load"),
            E("keyconfig.save", "保存", "Save"),
            E("keyconfig.reset_all", "全リセット", "Reset All"),
            E("keyconfig.no_bindings", "対象バインドがありません。", "No bindings are available."),
            E("keyconfig.input_active", "● 入力中", "● ACTIVE"),
            E("keyconfig.change", "変更", "Change"),
            E("keyconfig.reset", "戻す", "Reset"),
            E("keyconfig.controls", "操作ガイド", "CONTROLS", "CONTROLS"),
            E("keyconfig.live_monitor", "リアルタイム入力モニター", "LIVE INPUT MONITOR", "LIVE INPUT MONITOR"),
            E("keyconfig.operations", "操作一覧", "OPERATIONS", "OPERATIONS"),
            E("keyconfig.section_movement", "移動", "MOVEMENT", "MOVEMENT"),
            E("keyconfig.section_combat", "戦闘・ターゲット", "COMBAT / TARGETING", "COMBAT / TARGETING"),
            E("keyconfig.section_grapple", "グラップル", "GRAPPLE", "GRAPPLE"),
            E("keyconfig.section_camera", "カメラ・インタラクション", "CAMERA / INTERACTION", "CAMERA / INTERACTION"),
            E("input.action.move", "移動", "Move", "Move"),
            E("input.action.look", "視点操作", "Look", "Look"),
            E("input.action.attack", "攻撃", "Attack", "Attack"),
            E("input.action.dash", "ダッシュ", "Dash", "Dash"),
            E("input.action.guard", "ガード", "Guard", "Guard"),
            E("input.action.heal", "回復", "Heal", "Heal"),
            E("input.action.interact", "インタラクト", "Interact", "Interact"),
            E("input.action.crouch", "しゃがむ", "Crouch", "Crouch"),
            E("input.action.jump", "ジャンプ", "Jump", "Jump"),
            E("input.action.previous", "前へ", "Previous", "Previous"),
            E("input.action.next", "次へ", "Next", "Next"),
            E("input.action.sprint", "スプリント", "Sprint", "Sprint"),
            E("input.action.lock_on", "ロックオン", "Lock On", "LockOn", "Lock On"),
            E("input.action.grapple", "グラップル", "Grapple", "Grapple"),
            E("input.action.reel", "巻き取り", "Reel", "Reel"),
            E("input.action.strafe_toggle", "ストレイフ切替", "Strafe Toggle", "StrafeToggle", "Strafe Toggle"),
            E("input.action.camera_zoom", "カメラズーム", "Camera Zoom", "CameraZoom", "Camera Zoom"),
            E("input.action.grapple_fire", "グラップル発射", "Grapple Fire", "GrappleFire", "Grapple Fire"),
            E("input.part.up", "上", "Up", "Up", "up"),
            E("input.part.down", "下", "Down", "Down", "down"),
            E("input.part.left", "左", "Left", "Left", "left"),
            E("input.part.right", "右", "Right", "Right", "right"),
            E("keyconfig.input_asset_missing", "Input Action Asset 未設定", "INPUT ASSET NOT SET", "INPUT ASSET NOT SET"),
            E("keyconfig.action_map_missing", "Action Map が見つかりません", "ACTION MAP NOT FOUND", "ACTION MAP NOT FOUND"),
            E("keyconfig.ready_fallback", "Ready（'{0}' に一致するバインドがないため全表示中）", "Ready (showing all; no bindings match '{0}')"),
            E("keyconfig.loaded", "設定を読み込みました。", "Settings loaded."),
            E("keyconfig.loaded_fallback", "設定を読み込みました。('{0}' に一致するバインドがないため全表示中)", "Settings loaded. (showing all; no bindings match '{0}')"),
            E("keyconfig.changed", "変更しました: {0}", "Changed: {0}"),
            E("keyconfig.changed_fallback", "変更しました: {0}（全表示中）", "Changed: {0} (showing all)"),
            E("keyconfig.group_all", "BindingGroup: すべて", "Binding group: All"),
            E("keyconfig.group", "BindingGroup: {0}", "Binding group: {0}"),
            E("keyconfig.group_fallback", "BindingGroup: {0}（一致なしのため全表示）", "Binding group: {0} (no match; showing all)"),
            E("keyconfig.no_saved_settings", "保存済み設定がありません。\n", "No saved settings.\n"),
            E("keyconfig.saved", "設定を保存しました。", "Settings saved."),
            E("keyconfig.reset_all_done", "すべてのキー設定をリセットしました。", "All key settings were reset."),
            E("keyconfig.rebind_start_failed", "リバインドを開始できませんでした。", "Could not start rebinding."),
            E("keyconfig.action_missing", "Action の取得に失敗しました。", "Could not find the action."),
            E("keyconfig.binding_reset", "バインドをリセットしました。", "Binding reset."),
            E("keyconfig.enter_new_key", "新しいキーを入力してください（Escでキャンセル）", "Press a new key (Esc to cancel)"),
            E("keyconfig.rebind_canceled", "リバインドをキャンセルしました。", "Rebinding canceled."),
            E("keyconfig.rebind_canceled_fallback", "リバインドをキャンセルしました。（全表示中）", "Rebinding canceled. (showing all)"),
            E("keyconfig.rebind_failed", "リバインドに失敗しました。", "Rebinding failed."),
            E("keyconfig.rebind_failed_fallback", "リバインドに失敗しました。（全表示中）", "Rebinding failed. (showing all)"),
            E("keyconfig.config_missing", "KeyConfigInputActionsConfig が未設定、またはInputActionAssetが未設定です。", "KeyConfigInputActionsConfig or InputActionAsset is not assigned."),

            E("lobby.title", "Steam ロビー", "Steam Lobby", "Steam Lobby"),
            E("lobby.group_create", "シーン選択・ロビー管理  [LB: 一覧 / RB: 検索]", "Scene and lobby management  [LB: List / RB: Search]"),
            E("lobby.group_search", "ロビー検索・直接参加  [LB: 作成 / RB: 一覧]", "Search or join directly  [LB: Create / RB: List]"),
            E("lobby.group_list", "ロビーを選択して決定  [↑↓ 選択 / 決定で参加 / LB: 検索 / RB: 作成]", "Choose a lobby  [Up/Down: Select / Confirm: Join / LB: Search / RB: Create]"),
            E("lobby.create", "ロビーを作成", "Create Lobby", "Create Lobby"),
            E("lobby.refresh", "更新", "Refresh", "Refresh"),
            E("lobby.leave", "ロビーを退出", "Leave Lobby", "Leave Lobby"),
            E("lobby.join_by_id", "IDで参加", "Join by ID", "Join by ID"),
            E("lobby.id", "ロビーID", "Lobby ID", "Lobby ID"),
            E("lobby.name", "ロビー名", "Lobby Name", "Lobby Name"),
            E("lobby.join", "参加", "Join", "Join"),
            E("lobby.host", "ホスト", "HOST", "HOST"),
            E("lobby.joined", "参加中", "JOINED", "JOINED"),
            E("lobby.full", "満員", "FULL", "FULL"),
            E("lobby.list", "ロビー一覧", "Lobbies", "Lobbies"),
            E("lobby.online_members", "オンラインメンバー", "Online Members", "Online Members"),
            E("lobby.search", "検索", "Search", "Search"),
            E("lobby.steam_connecting", "Steam: 接続中…", "Steam: connecting...", "Steam: connecting..."),
            E("lobby.current_none", "現在のロビー: なし", "Current Lobby: none", "Current Lobby: none"),
            E("lobby.transport_waiting", "NetworkManager/FacepunchTransport の初期化待ち", "Waiting for NetworkManager/FacepunchTransport"),
            E("lobby.no_member_info", "メンバー情報なし", "No member information"),
            E("lobby.not_joined", "ロビー未参加", "Not in a lobby"),
            E("lobby.not_found", "ロビーが見つかりませんでした。", "No lobbies found.", "Lobby が見つかりませんでした。"),
            E("lobby.service_missing", "Steam: SteamLobbyService が見つかりません", "Steam: SteamLobbyService not found", "Steam: SteamLobbyService not found"),
            E("lobby.not_connected", "Steam: 未接続", "Steam: not connected", "Steam: not connected"),
            E("lobby.steam_waiting", "Steam: NetworkManager/FacepunchTransport の初期化待ち…", "Steam: waiting for NetworkManager/FacepunchTransport...", "Steam: waiting for NetworkManager/FacepunchTransport..."),
            E("lobby.current", "現在のロビー: {0}", "Current Lobby: {0}", "Current Lobby: {0}"),
            E("lobby.steam_user", "Steam: {0}", "Steam: {0}"),
            E("lobby.stage_changed", "ステージを変更しました。", "Stage changed.", "Stage changed."),
            E("lobby.stage_change_failed", "ステージの変更に失敗しました。", "Stage change failed.", "Stage change failed."),
            E("lobby.created", "ロビーを作成しました。", "Lobby created.", "Lobby created."),
            E("lobby.create_failed", "ロビーの作成に失敗しました。", "Lobby create failed.", "Lobby create failed."),
            E("lobby.invalid_id", "Lobby ID が不正です。", "The lobby ID is invalid."),
            E("lobby.join_success", "ロビーに参加しました。", "Lobby joined.", "Lobby joined."),
            E("lobby.join_failed", "ロビーへの参加に失敗しました。", "Lobby join failed.", "Lobby join failed."),
            E("lobby.name_not_found", "該当するロビー名が見つかりませんでした。", "No matching lobby name was found.")
        };

        private static readonly Dictionary<string, Entry> ByKey = BuildKeyMap();
        private static readonly Dictionary<string, string> KeyBySource = BuildSourceMap();

        internal static string ResolveKey(string keyOrSource) =>
            !string.IsNullOrEmpty(keyOrSource) && KeyBySource.TryGetValue(keyOrSource, out var key) ? key : keyOrSource;

        internal static bool TryResolveKey(string keyOrSource, out string key)
        {
            if (!string.IsNullOrEmpty(keyOrSource) && ByKey.ContainsKey(keyOrSource))
            {
                key = keyOrSource;
                return true;
            }
            return KeyBySource.TryGetValue(keyOrSource ?? string.Empty, out key);
        }

        internal static string GetJapaneseFallback(string keyOrSource)
        {
            var key = ResolveKey(keyOrSource);
            return key != null && ByKey.TryGetValue(key, out var entry) ? entry.Japanese : keyOrSource;
        }

        private static Entry E(string key, string japanese, string english, params string[] aliases) =>
            new Entry(key, japanese, english, aliases);

        private static Dictionary<string, Entry> BuildKeyMap()
        {
            var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in Entries) result.Add(entry.Key, entry);
            return result;
        }

        private static Dictionary<string, string> BuildSourceMap()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in Entries)
            {
                result[entry.Japanese] = entry.Key;
                result[entry.English] = entry.Key;
                foreach (var alias in entry.Aliases) result[alias] = entry.Key;
            }
            return result;
        }
    }
}
