using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    public sealed class ScreenTargetDetector : MonoBehaviour, ITargetDetector
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField, Range(0f, 0.5f)] private float viewportPadding = 0.02f;
        [SerializeField] private float maxDetectionDistance = 20f;
        [SerializeField] private TargetMarkerRegistry registry;

        private readonly HashSet<ITargetable> candidates = new HashSet<ITargetable>();
        private readonly List<ITargetable> snapshot = new List<ITargetable>();
        private readonly List<ITargetable> registeredTargets = new List<ITargetable>();

        public IReadOnlyCollection<ITargetable> Candidates => candidates;

        public event Action<ITargetable> TargetEntered;
        public event Action<ITargetable> TargetExited;

        private void Awake()
        {
            ResolveCamera();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDisable()
        {
            ClearCandidates();
        }

        public void Refresh()
        {          
            ResolveCamera();

            snapshot.Clear();
            snapshot.AddRange(candidates);

            if (registry == null)
            {
                ClearCandidates();
                return;
            }

            registeredTargets.Clear();
            registeredTargets.AddRange(registry.Targets);
            for (var i = 0; i < registeredTargets.Count; i++)
            {
                var target = registeredTargets[i];
                if (target == null || !target.IsTargetable)
                {
                    continue;
                }

                var root = target.Root;
                if (root == null || !IsLayerAllowed(root.gameObject.layer))
                {
                    continue;
                }

                var aim = target.AimPoint != null ? target.AimPoint : root;
                if (!IsInViewport(aim))
                {
                    continue;
                }

                if (candidates.Add(target))
                {
                    TargetEntered?.Invoke(target);
                }

                snapshot.Remove(target);
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                var oldTarget = snapshot[i];
                if (candidates.Remove(oldTarget))
                {
                    TargetExited?.Invoke(oldTarget);
                }
            }
        }

        private void ClearCandidates()
        {
            if (candidates.Count == 0)
            {
                return;
            }

            snapshot.Clear();
            snapshot.AddRange(candidates);
            candidates.Clear();

            for (var i = 0; i < snapshot.Count; i++)
            {
                TargetExited?.Invoke(snapshot[i]);
            }
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private bool IsLayerAllowed(int layer)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private bool IsInViewport(Transform target)
        {
            if (target == null || targetCamera == null)
            {
                return false;
            }

            var viewportPoint = targetCamera.WorldToViewportPoint(target.position);
            if (viewportPoint.z <= 0f || viewportPoint.z > maxDetectionDistance)
            {
                return false;
            }

            var min = viewportPadding;
            var max = 1f - viewportPadding;
            return viewportPoint.x >= min && viewportPoint.x <= max
                && viewportPoint.y >= min && viewportPoint.y <= max;
        }
    }
}
