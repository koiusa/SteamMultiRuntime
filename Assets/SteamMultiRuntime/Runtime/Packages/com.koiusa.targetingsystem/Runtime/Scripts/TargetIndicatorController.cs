using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// Presents the selected targets owned by a TargetingController on one screen-space panel.
    /// Target membership is push-based; only moving marker positions are sampled while rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetIndicatorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TargetingController controller;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private TargetIndicatorThemeProvider themeProvider;

        [Header("UI Settings")]
        [SerializeField, Min(1f)] private float markerSize = 40f;

        private readonly Dictionary<ITargetable, VisualElement> activeMarkers = new();
        private readonly List<ITargetable> obsoleteTargets = new();
        private readonly TargetIndicatorUIFactory uiFactory = new();
        private VisualElement rootPanel;
        private VisualElement markersContainer;

        public void SetController(TargetingController value)
        {
            if (controller == value)
                return;

            UnsubscribeController();
            controller = value;
            SubscribeController();
            SynchronizeState(controller != null ? controller.State : TargetingState.Empty);
        }

        public void SetCamera(Camera value) => targetCamera = value;

        private void Awake() => ResolveReferences();

        private void OnEnable()
        {
            ResolveReferences();
            InitializeDocument();
            SubscribeController();
            SynchronizeState(controller != null ? controller.State : TargetingState.Empty);
        }

        private void OnDisable()
        {
            UnsubscribeController();
            ClearMarkers();
        }

        private void LateUpdate()
        {
            if (markersContainer == null || activeMarkers.Count == 0)
                return;

            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
                targetCamera = Camera.main;
            if (targetCamera == null || rootPanel?.panel == null)
                return;

            foreach (var pair in activeMarkers)
                UpdateMarkerPosition(pair.Key, pair.Value);
        }

        private void InitializeDocument()
        {
            if (uiDocument == null)
                return;

            rootPanel = uiDocument.rootVisualElement;
            var styleSheet = themeProvider != null ? themeProvider.TargetIndicatorStyleSheet : null;
            if (styleSheet != null && !rootPanel.styleSheets.Contains(styleSheet))
                rootPanel.styleSheets.Add(styleSheet);

            markersContainer = rootPanel.Q<VisualElement>("target-indicators");
            var visualTree = themeProvider != null ? themeProvider.TargetIndicatorVisualTree : null;
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
        }

        private void SubscribeController()
        {
            if (isActiveAndEnabled && controller != null)
            {
                controller.StateChanged -= OnStateChanged;
                controller.StateChanged += OnStateChanged;
            }
        }

        private void UnsubscribeController()
        {
            if (controller != null)
                controller.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(TargetingStateChange change) => SynchronizeState(change.Current);

        private void SynchronizeState(TargetingState state)
        {
            if (markersContainer == null)
                return;

            obsoleteTargets.Clear();
            foreach (var target in activeMarkers.Keys)
            {
                if (!Contains(state.SelectedTargets, target))
                    obsoleteTargets.Add(target);
            }

            for (var i = 0; i < obsoleteTargets.Count; i++)
                RemoveMarker(obsoleteTargets[i]);

            for (var i = 0; i < state.SelectedTargets.Count; i++)
            {
                var target = state.SelectedTargets[i];
                if (target == null)
                    continue;

                var marker = EnsureMarker(target);
                uiFactory.UpdateMarkerVisualState(
                    marker,
                    ReferenceEquals(target, state.PrimaryTarget)
                        ? TargetIndicatorUIFactory.IndicatorVisualState.Focused
                        : TargetIndicatorUIFactory.IndicatorVisualState.Locked);
            }
        }

        private VisualElement EnsureMarker(ITargetable target)
        {
            if (activeMarkers.TryGetValue(target, out var marker))
                return marker;

            marker = uiFactory.CreateTargetMarker(target.Root != null ? target.Root.name : "Target", markerSize,
                TargetIndicatorUIFactory.IndicatorVisualState.Locked);
            markersContainer.Add(marker);
            activeMarkers.Add(target, marker);
            return marker;
        }

        private void RemoveMarker(ITargetable target)
        {
            if (!activeMarkers.Remove(target, out var marker))
                return;

            marker.RemoveFromHierarchy();
        }

        private void ClearMarkers()
        {
            foreach (var marker in activeMarkers.Values)
                marker.RemoveFromHierarchy();
            activeMarkers.Clear();
            obsoleteTargets.Clear();
        }

        private void UpdateMarkerPosition(ITargetable target, VisualElement marker)
        {
            var trackingTransform = target?.AimPoint != null ? target.AimPoint : target?.Root;
            if (trackingTransform == null)
            {
                marker.style.display = DisplayStyle.None;
                return;
            }

            var screenPosition = targetCamera.WorldToScreenPoint(trackingTransform.position);
            if (screenPosition.z <= 0f)
            {
                marker.style.display = DisplayStyle.None;
                return;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(
                rootPanel.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            marker.style.left = panelPosition.x - markerSize * 0.5f;
            marker.style.top = panelPosition.y - markerSize * 0.5f;
            marker.style.display = DisplayStyle.Flex;
        }

        private void ResolveReferences()
        {
            controller ??= GetComponentInParent<TargetingController>();
            uiDocument ??= GetComponent<UIDocument>();
            themeProvider ??= GetComponent<TargetIndicatorThemeProvider>();
            targetCamera ??= Camera.main;
        }

        private static bool Contains(IReadOnlyList<ITargetable> targets, ITargetable target)
        {
            for (var i = 0; i < targets.Count; i++)
                if (ReferenceEquals(targets[i], target))
                    return true;
            return false;
        }
    }
}
