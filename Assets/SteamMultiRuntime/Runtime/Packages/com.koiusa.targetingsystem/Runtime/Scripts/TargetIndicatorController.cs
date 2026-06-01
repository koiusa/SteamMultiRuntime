using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// lockON可能対象をUIToolkitで視覚的に表示するコントローラー。
    /// ScreenTargetDetectorから対象一覧を取得し、各ターゲットの
    /// スクリーン座標を計算してUI マーカーを管理する。
    /// </summary>
    [RequireComponent(typeof(ScreenTargetDetector))]
    [DisallowMultipleComponent]
    public sealed class TargetIndicatorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScreenTargetDetector detector;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private TargetIndicatorThemeProvider themeProvider;

        [Header("UI Settings")]
        [SerializeField, Min(20f)] private float markerSize = 40f;
        [SerializeField] private bool enableFocusHighlight = true;

        private VisualElement rootPanel;
        private VisualElement markersContainer;
        private readonly Dictionary<ITargetable, VisualElement> activeMarkers = new Dictionary<ITargetable, VisualElement>();
        private readonly HashSet<ITargetable> lockedTargets = new HashSet<ITargetable>();
        private readonly TargetIndicatorUIFactory uiFactory = new TargetIndicatorUIFactory();

        private Camera targetCamera;
        private ITargetable currentFocusTarget;
        private bool isInitialized;

        public ITargetable CurrentFocusTarget => currentFocusTarget;

        public void SetFocusTarget(ITargetable target)
        {
            if (ReferenceEquals(currentFocusTarget, target))
            {
                return;
            }

            var previousFocus = currentFocusTarget;
            currentFocusTarget = target;

            if (previousFocus != null)
            {
                RefreshMarkerState(previousFocus);
                RemoveMarkerIfUnused(previousFocus);
            }

            if (currentFocusTarget != null)
            {
                EnsureMarker(currentFocusTarget);
                RefreshMarkerState(currentFocusTarget);
            }
        }

        public void SetTargetLocked(ITargetable target, bool locked)
        {
            if (target == null)
            {
                return;
            }

            if (locked)
            {
                lockedTargets.Add(target);
                EnsureMarker(target);
            }
            else
            {
                lockedTargets.Remove(target);
            }

            RefreshMarkerState(target);
            RemoveMarkerIfUnused(target);
        }

        public void ClearLockedTargets()
        {
            if (lockedTargets.Count == 0)
            {
                return;
            }

            var snapshot = new List<ITargetable>(lockedTargets);
            lockedTargets.Clear();

            for (var i = 0; i < snapshot.Count; i++)
            {
                RefreshMarkerState(snapshot[i]);
            }
        }

        public void SetTargetsState(IEnumerable<ITargetable> locked, ITargetable focus)
        {
            var snapshot = new List<ITargetable>(lockedTargets);
            lockedTargets.Clear();

            if (locked != null)
            {
                foreach (var target in locked)
                {
                    if (target != null)
                    {
                        lockedTargets.Add(target);
                        if (!snapshot.Contains(target))
                        {
                            snapshot.Add(target);
                        }
                    }
                }
            }

            if (!ReferenceEquals(currentFocusTarget, focus) && currentFocusTarget != null && !snapshot.Contains(currentFocusTarget))
            {
                snapshot.Add(currentFocusTarget);
            }

            currentFocusTarget = focus;

            if (currentFocusTarget != null && !snapshot.Contains(currentFocusTarget))
            {
                snapshot.Add(currentFocusTarget);
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                RefreshMarkerState(snapshot[i]);
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (!isInitialized)
            {
                Initialize();
            }

            if (detector != null)
            {
                detector.TargetEntered += OnTargetEntered;
                detector.TargetExited += OnTargetExited;
            }
        }

        private void OnDisable()
        {
            if (detector != null)
            {
                detector.TargetEntered -= OnTargetEntered;
                detector.TargetExited -= OnTargetExited;
            }
        }

        private void LateUpdate()
        {
            if (!isInitialized || targetCamera == null)
            {
                return;
            }

            UpdateMarkerPositions();
        }

        private void Initialize()
        {
            if (uiDocument == null)
            {
                Debug.LogError($"{nameof(TargetIndicatorController)}: {nameof(uiDocument)} is not assigned.");
                return;
            }

            rootPanel = uiDocument.rootVisualElement;

            var visualTree = themeProvider != null ? themeProvider.TargetIndicatorVisualTree : null;
            var styleSheet = themeProvider != null ? themeProvider.TargetIndicatorStyleSheet : null;

            if (styleSheet != null && !rootPanel.styleSheets.Contains(styleSheet))
            {
                rootPanel.styleSheets.Add(styleSheet);
            }

            markersContainer = rootPanel.Q<VisualElement>("target-indicators");
            if (markersContainer == null && visualTree != null)
            {
                visualTree.CloneTree(rootPanel);
                markersContainer = rootPanel.Q<VisualElement>("target-indicators");
            }

            if (markersContainer == null)
            {
                markersContainer = new VisualElement { name = "target-indicators" };
                markersContainer.AddToClassList("target-indicators");
                rootPanel.Add(markersContainer);
            }

            isInitialized = true;

            if (detector != null)
            {
                foreach (var target in detector.Candidates)
                {
                    if (target != null)
                    {
                        CreateMarker(target);
                    }
                }
            }
        }

        private void OnTargetEntered(ITargetable target)
        {
            if (target != null && !activeMarkers.ContainsKey(target))
            {
                CreateMarker(target);
            }
        }

        private void OnTargetExited(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            RemoveMarkerIfUnused(target);
        }

        private void CreateMarker(ITargetable target)
        {
            if (markersContainer == null || target == null)
            {
                return;
            }

            var marker = uiFactory.CreateTargetMarker(
                target.Root.name,
                markerSize,
                GetVisualState(target));
            marker.style.position = Position.Absolute;
            markersContainer.Add(marker);
            activeMarkers[target] = marker;
        }

        private void EnsureMarker(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            if (!activeMarkers.ContainsKey(target))
            {
                CreateMarker(target);
            }
        }

        private void RemoveMarkerIfUnused(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            if (lockedTargets.Contains(target) || ReferenceEquals(currentFocusTarget, target))
            {
                EnsureMarker(target);
                return;
            }

            if (activeMarkers.TryGetValue(target, out var marker))
            {
                marker.RemoveFromHierarchy();
                activeMarkers.Remove(target);
            }
        }

        private void UpdateMarkerPositions()
        {
            if (rootPanel == null)
            {
                return;
            }

            var panel = rootPanel.panel;
            if (panel == null)
            {
                return;
            }

            foreach (var kvp in activeMarkers)
            {
                var target = kvp.Key;
                var marker = kvp.Value;

                if (target == null || marker == null)
                {
                    continue;
                }

                var trackingTransform = target.AimPoint != null ? target.AimPoint : target.Root;
                if (trackingTransform == null)
                {
                    marker.style.display = DisplayStyle.None;
                    continue;
                }

                var screenPos = targetCamera.WorldToScreenPoint(trackingTransform.position);
                if (screenPos.z <= 0f)
                {
                    marker.style.display = DisplayStyle.None;
                    continue;
                }

                var panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                marker.style.left = panelPos.x - (markerSize * 0.5f);
                marker.style.top = panelPos.y - (markerSize * 0.5f);
                marker.style.display = DisplayStyle.Flex;
            }
        }

        private void RefreshMarkerState(ITargetable target)
        {
            if (target == null || !activeMarkers.TryGetValue(target, out var marker))
            {
                return;
            }

            uiFactory.UpdateMarkerVisualState(marker, GetVisualState(target));
        }

        private TargetIndicatorUIFactory.IndicatorVisualState GetVisualState(ITargetable target)
        {
            if (enableFocusHighlight && ReferenceEquals(currentFocusTarget, target))
            {
                return TargetIndicatorUIFactory.IndicatorVisualState.Focused;
            }

            if (lockedTargets.Contains(target))
            {
                return TargetIndicatorUIFactory.IndicatorVisualState.Locked;
            }

            return TargetIndicatorUIFactory.IndicatorVisualState.Available;
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

            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                uiDocument = GetComponentInParent<UIDocument>();
            }

            if (themeProvider == null)
            {
                themeProvider = GetComponent<TargetIndicatorThemeProvider>();
            }

            if (themeProvider == null)
            {
                themeProvider = GetComponentInParent<TargetIndicatorThemeProvider>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }
    }
}
