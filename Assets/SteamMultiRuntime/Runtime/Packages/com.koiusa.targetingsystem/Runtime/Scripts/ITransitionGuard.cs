namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// カメラモード遷移の可否を判断するインターフェース。
    /// 各 Binder が実装し、TargetingCameraRig 経由で呼び出される。
    /// </summary>
    public interface ITransitionGuard
    {
        /// <summary>
        /// このBinder に対応するカメラモードへ遷移可能かどうかを返す。
        /// </summary>
        bool CanTransition();
    }
}
