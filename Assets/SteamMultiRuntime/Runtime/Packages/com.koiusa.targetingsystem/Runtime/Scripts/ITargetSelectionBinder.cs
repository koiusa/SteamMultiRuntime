namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// ターゲット選択の共通操作を表す Binder インターフェース。
    /// Solo/Group の両方で自然に共通化できる選択切り替え操作だけを含む。
    /// </summary>
    public interface ITargetSelectionBinder
    {
        /// <summary>次のターゲットに切り替える。</summary>
        void SelectNext();

        /// <summary>前のターゲットに切り替える。</summary>
        void SelectPrev();

        /// <summary>LookAt 対象と選択状態を初期化する。NoLock 復帰時に呼び出す。</summary>
        void ClearLookAt();
    }
}
