using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LockOnTargetGroupBinder : MonoBehaviour, ILockOnTargetBinder, ITransitionGuard
    {
        [Header("References")]
        [SerializeField] private ScreenTargetDetector detector;
        [SerializeField] private CinemachineTargetGroup cinemachineTargetGroup;

        [Header("Locked Target")]
        [SerializeField, Min(0f)] private float targetWeight = 1f;
        [SerializeField, Min(0f)] private float targetRadius;

        [Header("Target Selection")]
        [SerializeField, Range(0f, 1f)] private float centerDistWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] private float depthWeight = 0.5f;
        [SerializeField, Min(1f)] private float depthNormalizeRange = 100f;

        private readonly HashSet<ITargetable> visibleTargets = new HashSet<ITargetable>();
        private readonly Dictionary<ITargetable, Transform> lockedMembers = new Dictionary<ITargetable, Transform>();
        private readonly List<ITargetable> lockOrder = new List<ITargetable>();
        private int focusIndex = -1;
        private bool isFocusModeEnabled = false;
        private bool isBound;

        public IReadOnlyCollection<ITargetable> LockedTargets => lockedMembers.Keys;
        public ITargetable CurrentFocusTarget =>
            focusIndex >= 0 && focusIndex < lockOrder.Count ? lockOrder[focusIndex] : null;

        /// <summary>
        /// ロック中のターゲットがすべて解除されたときに発火する。
        /// NoLockOn 状態へ戻す処理をここで受け取る。
        /// </summary>
        public event Action AllLockedTargetsCleared;
        public event Action<ITargetable> Looked;
        public event Action<ITargetable> Unlooked;

        /// <inheritdoc/>
        public bool CanTransition()
        {
            ValidateReferences();
            detector.Refresh();
            SyncVisibleTargets();
            return visibleTargets.Count > 0;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ValidateReferences();
            BindDetector(true);
            detector.Refresh();
            SyncVisibleTargets();
        }

        private void OnDisable()
        {
            BindDetector(false);
            UnlockAllTargets();
            visibleTargets.Clear();
        }

        public bool LockClosestVisibleTarget()
        {
            ValidateReferences();
            detector.Refresh();
            SyncVisibleTargets();

            var target = SelectClosestVisibleTarget(excludeLocked: true);
            if (target == null)
            {
                return false;
            }

            return LockTarget(target);
        }

        public bool ToggleClosestVisibleTarget()
        {
            ValidateReferences();
            detector.Refresh();
            SyncVisibleTargets();

            var target = SelectClosestVisibleTarget(excludeLocked: false);
            if (target == null)
            {
                return false;
            }

            if (lockedMembers.ContainsKey(target))
            {
                return UnlockTarget(target);
            }

            return LockTarget(target);
        }

        public int LockAllVisibleTargets()
        {
            ValidateReferences();
            detector.Refresh();
            SyncVisibleTargets();

            var lockedCount = 0;
            foreach (var target in visibleTargets)
            {
                if (target != null && target.IsTargetable && !lockedMembers.ContainsKey(target))
                {
                    if (LockTarget(target))
                    {
                        lockedCount++;
                    }
                }
            }

            return lockedCount;
        }

        public bool LockTarget(ITargetable target)
        {
            ValidateReferences();

            if (target == null || !target.IsTargetable || lockedMembers.ContainsKey(target))
            {
                return false;
            }

            var trackingTarget = ResolveTrackingTarget(target);
            if (trackingTarget == null)
            {
                return false;
            }

            cinemachineTargetGroup.AddMember(trackingTarget, targetWeight, targetRadius);
            lockedMembers.Add(target, trackingTarget);
            lockOrder.Add(target);

            if (focusIndex < 0)
            {
                focusIndex = 0;
            }

            Looked?.Invoke(target);
            return true;
        }

        public bool UnlockLastLockedTarget()
        {
            if (lockOrder.Count == 0)
            {
                return false;
            }

            var target = lockOrder[lockOrder.Count - 1];
            return UnlockTarget(target);
        }

        public bool UnlockTarget(ITargetable target)
        {
            if (target == null || !lockedMembers.TryGetValue(target, out var trackingTarget))
            {
                return false;
            }

            if (trackingTarget != null)
            {
                cinemachineTargetGroup.RemoveMember(trackingTarget);
            }

            lockedMembers.Remove(target);
            var removedIndex = lockOrder.IndexOf(target);
            lockOrder.Remove(target);

            if (lockOrder.Count == 0)
            {
                focusIndex = -1;
                if (isFocusModeEnabled)
                {
                    RefreshGroupWeights();
                }
                AllLockedTargetsCleared?.Invoke();
            }
            else if (focusIndex >= lockOrder.Count)
            {
                focusIndex = lockOrder.Count - 1;
                UpdateFocus();
            }
            else if (focusIndex == removedIndex)
            {
                UpdateFocus();
            }

            Unlooked?.Invoke(target);
            return true;
        }

        public void UnlockAllTargets()
        {
            var removedTargets = new List<ITargetable>(lockedMembers.Keys);

            if (cinemachineTargetGroup != null)
            {
                foreach (var trackingTarget in lockedMembers.Values)
                {
                    if (trackingTarget != null)
                    {
                        cinemachineTargetGroup.RemoveMember(trackingTarget);
                    }
                }
            }

            lockedMembers.Clear();
            lockOrder.Clear();
            focusIndex = -1;
            isFocusModeEnabled = false;

            for (var i = 0; i < removedTargets.Count; i++)
            {
                Unlooked?.Invoke(removedTargets[i]);
            }
        }

        public void ClearLookAt()
        {
            UnlockAllTargets();
        }

        public bool IsFocusModeEnabled => isFocusModeEnabled;

        public void SetFocusModeEnabled(bool enabled)
        {
            if (isFocusModeEnabled == enabled)
            {
                return;
            }

            isFocusModeEnabled = enabled;

            if (isFocusModeEnabled && focusIndex < 0 && lockOrder.Count > 0)
            {
                focusIndex = 0;
            }

            RefreshGroupWeights();
        }

        public void SelectNext()
        {
            if (lockOrder.Count == 0)
            {
                return;
            }

            focusIndex = (focusIndex + 1) % lockOrder.Count;

            if (isFocusModeEnabled)
            {
                RefreshGroupWeights();
            }
        }

        public void SelectPrev()
        {
            if (lockOrder.Count == 0)
            {
                return;
            }

            focusIndex = (focusIndex - 1 + lockOrder.Count) % lockOrder.Count;

            if (isFocusModeEnabled)
            {
                RefreshGroupWeights();
            }
        }

        private void UpdateFocus()
        {
            if (isFocusModeEnabled)
            {
                RefreshGroupWeights();
            }
        }

        private void RefreshGroupWeights()
        {
            if (cinemachineTargetGroup == null)
            {
                return;
            }

            foreach (var kvp in lockedMembers)
            {
                var t = kvp.Value;
                if (t != null)
                {
                    cinemachineTargetGroup.RemoveMember(t);
                }
            }

            for (var i = 0; i < lockOrder.Count; i++)
            {
                if (!lockedMembers.TryGetValue(lockOrder[i], out var t) || t == null)
                {
                    continue;
                }

                var weight = (!isFocusModeEnabled || i == focusIndex) ? targetWeight : 0f;
                cinemachineTargetGroup.AddMember(t, weight, targetRadius);
            }
        }

        private ITargetable SelectClosestVisibleTarget(bool excludeLocked)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return null;
            }

            ITargetable bestTarget = null;
            var bestScore = float.MaxValue;

            foreach (var candidate in visibleTargets)
            {
                if (candidate == null || !candidate.IsTargetable)
                {
                    continue;
                }

                if (excludeLocked && lockedMembers.ContainsKey(candidate))
                {
                    continue;
                }

                var trackingTarget = ResolveTrackingTarget(candidate);
                if (trackingTarget == null)
                {
                    continue;
                }

                var viewportPoint = camera.WorldToViewportPoint(trackingTarget.position);
                if (viewportPoint.z <= 0f)
                {
                    continue;
                }

                if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f)
                {
                    continue;
                }

                var centerDist = (viewportPoint.x - 0.5f) * (viewportPoint.x - 0.5f) + (viewportPoint.y - 0.5f) * (viewportPoint.y - 0.5f);
                var normalizedDepth = viewportPoint.z / depthNormalizeRange;
                var score = centerDistWeight * centerDist + depthWeight * normalizedDepth;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private void SyncVisibleTargets()
        {
            visibleTargets.Clear();
            foreach (var candidate in detector.Candidates)
            {
                if (candidate != null)
                {
                    visibleTargets.Add(candidate);
                }
            }
        }

        private void ResolveReferences()
        {
            if (detector == null)
            {
                detector = GetComponent<ScreenTargetDetector>();
            }

            if (detector == null)
            {
                detector = GetComponentInParent<ScreenTargetDetector>();
            }

            if (detector == null)
            {
                detector = GetComponentInChildren<ScreenTargetDetector>(true);
            }

            if (cinemachineTargetGroup == null)
            {
                cinemachineTargetGroup = GetComponent<CinemachineTargetGroup>();
            }

            if (cinemachineTargetGroup == null)
            {
                cinemachineTargetGroup = GetComponentInChildren<CinemachineTargetGroup>(true);
            }

            if (cinemachineTargetGroup == null)
            {
                cinemachineTargetGroup = GetComponentInParent<CinemachineTargetGroup>();
            }
        }

        private void ValidateReferences()
        {
            if (detector == null)
            {
                throw new MissingReferenceException($"{nameof(LockOnTargetGroupBinder)} requires {nameof(ScreenTargetDetector)} reference.");
            }

            if (cinemachineTargetGroup == null)
            {
                throw new MissingReferenceException($"{nameof(LockOnTargetGroupBinder)} requires {nameof(CinemachineTargetGroup)} reference.");
            }
        }

        private void BindDetector(bool bind)
        {
            if (detector == null)
            {
                return;
            }

            if (bind)
            {
                if (isBound)
                {
                    return;
                }

                detector.TargetEntered += OnTargetEntered;
                detector.TargetExited += OnTargetExited;
                isBound = true;
                return;
            }

            if (!isBound)
            {
                return;
            }

            detector.TargetEntered -= OnTargetEntered;
            detector.TargetExited -= OnTargetExited;
            isBound = false;
        }

        private void OnTargetEntered(ITargetable target)
        {
            if (target != null)
            {
                visibleTargets.Add(target);
            }
        }

        private void OnTargetExited(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            visibleTargets.Remove(target);
            // ロック解除はプレイヤー操作のみで行う。
            // 画面外に出ただけではTargetGroupからは除外しない。
        }

        private static Transform ResolveTrackingTarget(ITargetable target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.AimPoint != null)
            {
                return target.AimPoint;
            }

            return target.Root;
        }
    }
}
