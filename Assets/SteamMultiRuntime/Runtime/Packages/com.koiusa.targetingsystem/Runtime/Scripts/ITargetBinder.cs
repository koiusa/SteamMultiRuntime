using System;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// ターゲット選択・切り替えを担う Binder の共通インターフェース。
    /// SoloLock / MultiLock など複数の実装に対応する。
    /// </summary>
    public interface ITargetBinder
    {
        /// <summary>現在選択中のターゲット。</summary>
        ITargetable CurrentTarget { get; }

        /// <summary>ターゲットが選択されたときに発火する。</summary>
        event Action<ITargetable> TargetSelected;

        /// <summary>次のターゲットに切り替える。</summary>
        void SelectNext();

        /// <summary>前のターゲットに切り替える。</summary>
        void SelectPrev();

        /// <summary>LookAt 対象と選択状態を初期化する。NoLock 復帰時に呼び出す。</summary>
        void ClearLookAt();
    }
}
