using System;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// ターゲットを見始めた / 見なくなったことを通知する共通インターフェース。
    /// </summary>
    public interface ILockOn
    {
        /// <summary>ターゲットを見始めたときに発火する。</summary>
        event Action<ITargetable> Looked;

        /// <summary>ターゲットを見なくなったときに発火する。</summary>
        event Action<ITargetable> Unlooked;
    }
}
