using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// Orients the GameObject that this script is attached in such a way that it always faces the center point of the TargetGroup.
    /// </summary>
    [ExecuteAlways]
    public class LookAtTargetGroup : MonoBehaviour
    {
        [Tooltip("TargetGroup component to look at.")]
        public CinemachineTargetGroup TargetGroup;

        [Tooltip("Lock rotation along the x axis to the initial value.")]
        public bool LockRotationX;
        [Tooltip("Lock rotation along the y axis to the initial value.")]
        public bool LockRotationY;
        [Tooltip("Lock rotation along the z axis to the initial value.")]
        public bool LockRotationZ;

        Vector3 m_Rotation;

        void OnEnable()
        {
            m_Rotation = transform.rotation.eulerAngles;
        }

        void Reset()
        {
            m_Rotation = transform.rotation.eulerAngles;
        }

        void Update()
        {
            if (!TryGetTargetGroupPosition(out var lookAtPosition))
            {
                return;
            }

            var direction = lookAtPosition - transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction);

            if (LockRotationX || LockRotationY || LockRotationZ)
            {
                var euler = transform.rotation.eulerAngles;
                euler.x = LockRotationX ? m_Rotation.x : euler.x;
                euler.y = LockRotationY ? m_Rotation.y : euler.y;
                euler.z = LockRotationZ ? m_Rotation.z : euler.z;
                transform.rotation = Quaternion.Euler(euler);
            }
        }

        bool TryGetTargetGroupPosition(out Vector3 position)
        {
            position = default;
            if (TargetGroup == null)
            {
                return false;
            }

            var targets = TargetGroup.Targets;
            Vector3 weightedSum = default;
            var totalWeight = 0f;

            for (var i = 0; i < targets.Count; i++)
            {
                var member = targets[i];
                if (member.Object == null || member.Weight <= 0f)
                {
                    continue;
                }

                weightedSum += member.Object.position * member.Weight;
                totalWeight += member.Weight;
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            position = weightedSum / totalWeight;
            return true;
        }
    }
}
