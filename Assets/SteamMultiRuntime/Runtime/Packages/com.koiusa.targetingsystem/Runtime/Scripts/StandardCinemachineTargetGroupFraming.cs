using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StandardCinemachineTargetGroupFraming : MonoBehaviour, ITargetingCameraFramingGroup
    {
        [SerializeField] private CinemachineTargetGroup targetGroup;
        private readonly List<Transform> activeMembers = new();

        public Transform CameraTarget => targetGroup != null ? targetGroup.transform : null;

        private void Awake()
        {
            if (targetGroup == null) targetGroup = GetComponent<CinemachineTargetGroup>();
        }

        public void SetMembers(
            Transform primary,
            IReadOnlyList<Transform> members,
            float memberWeight,
            float memberRadius)
        {
            Clear();
            if (targetGroup == null || members == null) return;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || activeMembers.Contains(member)) continue;
                targetGroup.AddMember(member, memberWeight, memberRadius);
                activeMembers.Add(member);
            }
        }

        public void Clear()
        {
            if (targetGroup != null)
            {
                for (var i = 0; i < activeMembers.Count; i++)
                {
                    if (activeMembers[i] != null) targetGroup.RemoveMember(activeMembers[i]);
                }
            }
            activeMembers.Clear();
        }

        private void OnDisable() => Clear();
    }
}
