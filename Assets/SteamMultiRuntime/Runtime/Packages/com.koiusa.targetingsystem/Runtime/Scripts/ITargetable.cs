using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    public interface ITargetable
    {
        Transform Root { get; }
        Transform AimPoint { get; }
        bool IsTargetable { get; }
        float Priority { get; }
    }
}
