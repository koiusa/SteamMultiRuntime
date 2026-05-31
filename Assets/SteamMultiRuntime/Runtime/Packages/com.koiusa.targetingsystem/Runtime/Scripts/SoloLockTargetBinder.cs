using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// ScreenTargetDetector の候補から 1 体ずつ順番にターゲットを選択する Binder。
    /// ITargetBinder を実装し、SoloLockTargetInput から委譲される。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloLockTargetBinder : MonoBehaviour, ITargetBinder, ITransitionGuard
    {
        [Header("References")]
        [SerializeField] private ScreenTargetDetector detector;

        private CinemachineCamera targetCamera;
        private LookAtTarget playerLookAt;

        private readonly List<ITargetable> cachedTargets = new List<ITargetable>();
        private int currentIndex = -1;
        private bool isBound;

        public ITargetable CurrentTarget { get; private set; }

        public event Action<ITargetable> TargetSelected;

        /// <inheritdoc/>
        public bool CanTransition() => cachedTargets.Count > 0 && playerLookAt != null;

        private void Awake()
        {
            targetCamera = GetComponent<CinemachineCamera>();

            if (targetCamera != null && targetCamera.Follow != null)
            {
                playerLookAt = targetCamera.Follow.GetComponentInChildren<LookAtTarget>();
            }
        }

        private void OnEnable()
        {
            if (isBound) return;

            if (detector != null)
            {
                detector.TargetEntered += OnTargetEntered;
                detector.TargetExited  += OnTargetExited;
            }

            RebuildTargetList();
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound) return;

            if (detector != null)
            {
                detector.TargetEntered -= OnTargetEntered;
                detector.TargetExited  -= OnTargetExited;
            }

            isBound = false;
        }

        public void SelectNext()
        {
            if (cachedTargets.Count == 0) return;
            cachedTargets.Sort(CompareTargets);
            currentIndex = CurrentTarget != null ? cachedTargets.IndexOf(CurrentTarget) : -1;
            currentIndex = (currentIndex + 1) % cachedTargets.Count;
            SelectTarget(cachedTargets[currentIndex]);
        }

        public void SelectPrev()
        {
            if (cachedTargets.Count == 0) return;
            cachedTargets.Sort(CompareTargets);
            currentIndex = CurrentTarget != null ? cachedTargets.IndexOf(CurrentTarget) : 0;
            currentIndex = (currentIndex - 1 + cachedTargets.Count) % cachedTargets.Count;
            SelectTarget(cachedTargets[currentIndex]);
        }

        private void SelectTarget(ITargetable target)
        {
            CurrentTarget = target;
            ApplyLookAt(target);

            if (playerLookAt != null)
            {
                playerLookAt.Target = target?.AimPoint != null ? target.AimPoint : target?.Root;
            }
        }

        public void ClearLookAt()
        {
            CurrentTarget = null;
            currentIndex = -1;

            if (targetCamera != null)
            {
                targetCamera.LookAt = targetCamera.Follow;
            }

            if (playerLookAt != null)
            {
                playerLookAt.Target = null;
            }
        }

        private void ApplyLookAt(ITargetable target)
        {
            if (targetCamera == null) return;
            targetCamera.LookAt = target.AimPoint != null ? target.AimPoint : target.Root;
            TargetSelected?.Invoke(target);
        }

        private void OnTargetEntered(ITargetable target) => RebuildTargetList();
        private void OnTargetExited(ITargetable target)  => RebuildTargetList();

        private void RebuildTargetList()
        {
            cachedTargets.Clear();
            if (detector == null) return;

            foreach (var target in detector.Candidates)
            {
                cachedTargets.Add(target);
            }

            if (CurrentTarget != null)
            {
                var idx = cachedTargets.IndexOf(CurrentTarget);
                if (idx >= 0)
                {
                    currentIndex = idx;
                    return;
                }

                CurrentTarget = null;
            }

            currentIndex = cachedTargets.Count > 0 ? 0 : -1;
        }

        private int CompareTargets(ITargetable left, ITargetable right)
        {
            var cmp = right.Priority.CompareTo(left.Priority);
            if (cmp != 0) return cmp;

            var trackingTarget = targetCamera != null ? targetCamera.Follow : null;
            if (trackingTarget != null)
            {
                var origin = trackingTarget.position;
                var leftDist  = left.Root  != null ? (left.Root.position  - origin).sqrMagnitude : float.MaxValue;
                var rightDist = right.Root != null ? (right.Root.position - origin).sqrMagnitude : float.MaxValue;
                cmp = leftDist.CompareTo(rightDist);
                if (cmp != 0) return cmp;
            }

            var leftName  = left.Root  != null ? left.Root.name  : string.Empty;
            var rightName = right.Root != null ? right.Root.name : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }
    }
}
