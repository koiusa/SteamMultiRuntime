using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    public interface ITargetingCameraFramingGroup
    {
        Transform CameraTarget { get; }
        void SetMembers(Transform primary, IReadOnlyList<Transform> members, float memberWeight, float memberRadius);
        void Clear();
    }
}
