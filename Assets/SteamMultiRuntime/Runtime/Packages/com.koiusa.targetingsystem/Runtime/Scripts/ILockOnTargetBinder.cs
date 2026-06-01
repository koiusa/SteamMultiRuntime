using System;
using System.Collections.Generic;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// 複数ターゲットのロック管理を担う Binder の共通インターフェース。
    /// MultiLock 用の実装（LockOnTargetGroupBinder 等）に対応する。
    /// </summary>
    public interface ILockOnTargetBinder : ILockOn
    {
        /// <summary>現在ロック中のターゲット一覧。</summary>
        IReadOnlyCollection<ITargetable> LockedTargets { get; }

        /// <summary>ロック中のターゲットがすべて解除されたときに発火する。</summary>
        event Action AllLockedTargetsCleared;

        /// <summary>最も近い可視ターゲットをロックする。</summary>
        bool LockClosestVisibleTarget();

        /// <summary>最も近い可視ターゲットのロック状態をトグルする。</summary>
        bool ToggleClosestVisibleTarget();

        /// <summary>指定ターゲットをロックする。</summary>
        bool LockTarget(ITargetable target);

        /// <summary>指定ターゲットのロックを解除する。</summary>
        bool UnlockTarget(ITargetable target);

        /// <summary>最後にロックしたターゲットのロックを解除する。</summary>
        bool UnlockLastLockedTarget();

        /// <summary>すべてのロックを解除する。</summary>
        void UnlockAllTargets();

        /// <summary>LookAt 対象と選択状態を初期化する。NoLock 復帰時に呼び出す。</summary>
        void ClearLookAt();

        /// <summary>次のロック中ターゲットに注視を切り替える。</summary>
        void SelectNext();

        /// <summary>前のロック中ターゲットに注視を切り替える。</summary>
        void SelectPrev();

        /// <summary>フォーカスモード（注視点を1ターゲットに限定）の有効/無効を設定。</summary>
        void SetFocusModeEnabled(bool enabled);

        /// <summary>フォーカスモードが有効か取得。</summary>
        bool IsFocusModeEnabled { get; }

        /// <summary>画面内の全ての見えているターゲットをロックする。</summary>
        int LockAllVisibleTargets();
    }
}
