using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LockOnTargetDebugMaterialApplier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonoBehaviour binder;

        private ILockOn targetBinder;

        [Header("Debug")]
        [SerializeField] private Material debugLockOnMaterial;

        private readonly HashSet<ITargetable> desiredTargets = new HashSet<ITargetable>();

        private readonly Dictionary<ITargetable, List<Renderer>> targetRenderers = new Dictionary<ITargetable, List<Renderer>>();
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            targetBinder = binder as ILockOn;

            if (targetBinder != null)
            {
                targetBinder.Looked += OnLooked;
                targetBinder.Unlooked += OnUnlooked;
            }

            RefreshAllHighlights();
        }

        private void OnDisable()
        {
            if (targetBinder != null)
            {
                targetBinder.Looked -= OnLooked;
                targetBinder.Unlooked -= OnUnlooked;
            }

            targetBinder = null;

            ClearAllHighlights();
        }

        private void ResolveReferences()
        {
            if (binder != null && binder is ILockOn)
            {
                return;
            }

            var lockOn = GetComponent<ILockOn>();
            if (lockOn == null) lockOn = GetComponentInChildren<ILockOn>(true);
            if (lockOn == null) lockOn = GetComponentInParent<ILockOn>();

            binder = lockOn as MonoBehaviour;
        }

        private void RefreshAllHighlights()
        {
            if (targetBinder == null)
            {
                return;
            }

            ClearAllHighlights();

            if (targetBinder is SoloLockTargetBinder solo && solo.CurrentTarget != null)
            {
                desiredTargets.Add(solo.CurrentTarget);
                HighlightTarget(solo.CurrentTarget);
            }
            else if (targetBinder is ILockOnTargetBinder multi)
            {
                foreach (var target in multi.LockedTargets)
                {
                    if (target != null)
                    {
                        desiredTargets.Add(target);
                        HighlightTarget(target);
                    }
                }
            }
        }

        private void Update()
        {
            if (desiredTargets.Count == 0)
            {
                return;
            }

            foreach (var target in desiredTargets)
            {
                if (!targetRenderers.ContainsKey(target))
                {
                    HighlightTarget(target);
                }
            }
        }

        private void OnLooked(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            desiredTargets.Add(target);
            HighlightTarget(target);
        }

        private void OnUnlooked(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            desiredTargets.Remove(target);
            RestoreTarget(target);
        }

        private void HighlightTarget(ITargetable target)
        {
            if (target == null || targetRenderers.ContainsKey(target))
            {
                return;
            }

            var root = target.Root;
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var rendererList = new List<Renderer>(renderers.Length);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                rendererList.Add(renderer);

                if (!originalMaterials.ContainsKey(renderer))
                {
                    originalMaterials[renderer] = renderer.sharedMaterials;
                }

                ApplyDebugMaterial(renderer);
            }

            if (rendererList.Count == 0)
            {
                return;
            }

            targetRenderers[target] = rendererList;
        }

        private void ApplyDebugMaterial(Renderer renderer)
        {
            if (renderer == null || debugLockOnMaterial == null)
            {
                return;
            }

            var current = renderer.sharedMaterials;
            if (current == null || current.Length == 0)
            {
                return;
            }

            var replaced = new Material[current.Length];
            for (var i = 0; i < replaced.Length; i++)
            {
                replaced[i] = debugLockOnMaterial;
            }

            renderer.sharedMaterials = replaced;
        }

        private void RestoreTarget(ITargetable target)
        {
            if (target == null || !targetRenderers.TryGetValue(target, out var renderers))
            {
                return;
            }

            targetRenderers.Remove(target);

            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!IsRendererUsedByAnyTarget(renderer))
                {
                    if (originalMaterials.TryGetValue(renderer, out var materials))
                    {
                        renderer.sharedMaterials = materials;
                        originalMaterials.Remove(renderer);
                    }
                }
            }
        }

        private bool IsRendererUsedByAnyTarget(Renderer renderer)
        {
            foreach (var pair in targetRenderers)
            {
                if (pair.Value != null && pair.Value.Contains(renderer))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearAllHighlights()
        {
            foreach (var pair in targetRenderers)
            {
                var renderers = pair.Value;
                if (renderers == null)
                {
                    continue;
                }

                for (var i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (originalMaterials.TryGetValue(renderer, out var materials))
                    {
                        renderer.sharedMaterials = materials;
                    }
                }
            }

            desiredTargets.Clear();
            targetRenderers.Clear();
            originalMaterials.Clear();
        }
    }
}
