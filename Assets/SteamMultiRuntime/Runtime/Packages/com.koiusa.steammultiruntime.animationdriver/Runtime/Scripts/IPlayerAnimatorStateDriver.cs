using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerAnimatorStateDriver
    {
        Animator TargetAnimator { get; }
        void SetTargetAnimator(Animator animator);
    }
}
