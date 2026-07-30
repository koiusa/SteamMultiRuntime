using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IActorAnimatorStateDriver
    {
        Animator TargetAnimator { get; }
        void SetTargetAnimator(Animator animator);
    }
}
