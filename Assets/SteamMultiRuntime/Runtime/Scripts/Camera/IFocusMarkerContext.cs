using System;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// FocusMarker が依存するコンテキストの抽象インターフェース。
    /// Network（SteamLobby）用と Local 用の両方に対応する。
    /// </summary>
    public interface IFocusMarkerContext
    {
        /// <summary>
        /// カメラのトラッキング対象を解決すべき状態かどうか。
        /// Network: IsInLobby に相当。 Local: LocalPlayerObject が存在するかどうか。
        /// </summary>
        bool IsActive { get; }

        /// <summary>Camera が追従するローカル PlayerObject。</summary>
        GameObject PlayerObject { get; }

        /// <summary>
        /// 状態が変化したときに発火するイベント。
        /// </summary>
        event Action StateChanged;
    }
}
