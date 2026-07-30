using Koiusa.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class WireAimCursorOverlay : MonoBehaviour, IScreenAimCursor
    {
        private const float LineThickness = 2f;
        private const float CrossArmLength = 9f;
        private const float ClosedCrossGap = 3f;
        private const float OpenCrossGap = 16f;
        private const float ClosedHalfSize = 7f;
        private const float OpenHalfSize = 18f;
        private const float CloseDelay = 0.08f;
        private const float CloseDuration = 0.22f;
        private const float RootSize = 64f;

        private Vector2 screenPosition;
        private bool isVisible;
        private bool isAiming;
        private float aimStartedAt;
        private ScreenAimTargetState targetState = ScreenAimTargetState.Invalid;
        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement indicatorRoot;
        private VisualElement diamond;
        private VisualElement slash;
        private VisualElement[] crossArms;
        private bool? appliedVisibility;

        public void SetPosition(Vector2 position)
        {
            screenPosition = position;
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (visible)
                EnsureDocument();
            ApplyVisibility();
        }

        public void SetAiming(bool aiming)
        {
            if (aiming && !isAiming)
            {
                aimStartedAt = Time.unscaledTime;
            }

            isAiming = aiming;
        }

        public void SetTargetState(ScreenAimTargetState state)
        {
            targetState = state;
        }

        private void Update()
        {
            ApplyVisibility();
            if (!isVisible || UiNavigationInputSession.OwnsCursorVisibility || indicatorRoot?.panel == null)
                return;

            var panelPosition = RuntimePanelUtils.ScreenToPanel(
                indicatorRoot.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            indicatorRoot.style.left = panelPosition.x - RootSize * 0.5f;
            indicatorRoot.style.top = panelPosition.y - RootSize * 0.5f;

            var closeProgress = isAiming
                ? Mathf.Clamp01((Time.unscaledTime - aimStartedAt - CloseDelay) / CloseDuration)
                : 0f;
            closeProgress = closeProgress * closeProgress * (3f - 2f * closeProgress);
            var halfSize = Mathf.Lerp(OpenHalfSize, ClosedHalfSize, closeProgress);
            var crossGap = Mathf.Lerp(OpenCrossGap, ClosedCrossGap, closeProgress);
            var color = targetState switch
            {
                ScreenAimTargetState.Valid => new Color(0.2f, 0.9f, 1f, 0.95f),
                ScreenAimTargetState.Obstructed => new Color(1f, 0.58f, 0.1f, 0.95f),
                _ => new Color(1f, 0.2f, 0.15f, 0.95f),
            };

            var showCross = targetState == ScreenAimTargetState.Valid;
            SetCrossVisible(showCross);
            diamond.style.display = showCross ? DisplayStyle.None : DisplayStyle.Flex;
            slash.style.display = targetState == ScreenAimTargetState.Obstructed
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            UpdateCross(crossGap, color);
            UpdateDiamond(targetState == ScreenAimTargetState.Invalid ? OpenHalfSize : halfSize, color);
            UpdateLine(slash, RootSize * 0.5f, RootSize * 0.5f, halfSize * 2.4f, LineThickness, -45f, color);
        }

        private void EnsureDocument()
        {
            if (document != null)
                return;

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "Wire Aim Cursor Panel Settings";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 1f;

            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = short.MaxValue;
            var documentRoot = document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;

            indicatorRoot = CreateElement("wire-aim-cursor");
            indicatorRoot.style.position = Position.Absolute;
            indicatorRoot.style.width = RootSize;
            indicatorRoot.style.height = RootSize;
            documentRoot.Add(indicatorRoot);
            appliedVisibility = null;

            diamond = CreateElement("wire-aim-diamond");
            slash = CreateElement("wire-aim-slash");
            indicatorRoot.Add(diamond);
            indicatorRoot.Add(slash);
            crossArms = new[]
            {
                CreateElement("wire-aim-left"),
                CreateElement("wire-aim-right"),
                CreateElement("wire-aim-top"),
                CreateElement("wire-aim-bottom")
            };
            for (var i = 0; i < crossArms.Length; i++)
                indicatorRoot.Add(crossArms[i]);
        }

        private void ApplyVisibility()
        {
            if (indicatorRoot == null)
                return;
            var visible = isVisible && !UiNavigationInputSession.OwnsCursorVisibility;
            if (appliedVisibility == visible)
                return;
            appliedVisibility = visible;
            indicatorRoot.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void SetCrossVisible(bool visible)
        {
            if (crossArms == null)
                return;
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < crossArms.Length; i++)
                crossArms[i].style.display = display;
        }

        private void UpdateCross(float gap, Color color)
        {
            if (crossArms == null)
                return;
            var center = RootSize * 0.5f;
            UpdateLine(crossArms[0], center - gap - CrossArmLength * 0.5f, center, CrossArmLength, LineThickness, 0f, color);
            UpdateLine(crossArms[1], center + gap + CrossArmLength * 0.5f, center, CrossArmLength, LineThickness, 0f, color);
            UpdateLine(crossArms[2], center, center - gap - CrossArmLength * 0.5f, CrossArmLength, LineThickness, 90f, color);
            UpdateLine(crossArms[3], center, center + gap + CrossArmLength * 0.5f, CrossArmLength, LineThickness, 90f, color);
        }

        private void UpdateDiamond(float halfSize, Color color)
        {
            var side = halfSize * 1.41421356f;
            diamond.style.position = Position.Absolute;
            diamond.style.left = (RootSize - side) * 0.5f;
            diamond.style.top = (RootSize - side) * 0.5f;
            diamond.style.width = side;
            diamond.style.height = side;
            diamond.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            diamond.style.borderLeftWidth = LineThickness;
            diamond.style.borderRightWidth = LineThickness;
            diamond.style.borderTopWidth = LineThickness;
            diamond.style.borderBottomWidth = LineThickness;
            diamond.style.borderLeftColor = color;
            diamond.style.borderRightColor = color;
            diamond.style.borderTopColor = color;
            diamond.style.borderBottomColor = color;
        }

        private static void UpdateLine(
            VisualElement element,
            float centerX,
            float centerY,
            float width,
            float height,
            float angle,
            Color color)
        {
            element.style.position = Position.Absolute;
            element.style.left = centerX - width * 0.5f;
            element.style.top = centerY - height * 0.5f;
            element.style.width = width;
            element.style.height = height;
            element.style.backgroundColor = color;
            element.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
        }

        private static VisualElement CreateElement(string name)
        {
            return new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore
            };
        }

        private void OnDisable()
        {
            isVisible = false;
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (document != null)
                Destroy(document);
            if (panelSettings != null)
                Destroy(panelSettings);
        }
    }
}
