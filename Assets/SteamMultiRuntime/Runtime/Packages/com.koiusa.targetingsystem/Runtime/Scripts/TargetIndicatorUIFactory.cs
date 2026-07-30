using UnityEngine.UIElements;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// UIToolkitのマーカー要素を生成するファクトリー。
    /// ターゲット表示用のUI要素を構築する責務を持つ。
    /// </summary>
    public sealed class TargetIndicatorUIFactory
    {
        public enum IndicatorVisualState
        {
            Available,
            Locked,
            Focused,
        }

        private const string MarkerClassName = "target-marker";
        private const string FocusedClassName = "focused";
        private const string LockedClassName = "locked";
        private const string UnfocusedClassName = "unfocused";
        private const string CircleClassName = "marker-circle";
        private const string InnerRingClassName = "marker-inner-ring";
        private const string CenterDotClassName = "marker-center-dot";
        private const string CornerClassName = "marker-corner";
        private const string LabelClassName = "marker-label";

        /// <summary>
        /// ターゲット用マーカーUIを生成。
        /// 二重リング、照準コーナー、中心点、ターゲット名ラベルの組み合わせ。
        /// </summary>
        public VisualElement CreateTargetMarker(string targetName, float size, IndicatorVisualState state)
        {
            var marker = CreateNonPickingElement();
            marker.AddToClassList(MarkerClassName);
            marker.style.width = size;
            marker.style.height = size;
            marker.style.position = Position.Absolute;

            var circle = CreateNonPickingElement();
            circle.AddToClassList(CircleClassName);

            var innerRing = CreateNonPickingElement();
            innerRing.AddToClassList(InnerRingClassName);

            var centerDot = CreateNonPickingElement();
            centerDot.AddToClassList(CenterDotClassName);

            var topLeft = CreateCorner("top-left");
            var topRight = CreateCorner("top-right");
            var bottomLeft = CreateCorner("bottom-left");
            var bottomRight = CreateCorner("bottom-right");

            var label = new Label(targetName) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(LabelClassName);

            marker.Add(circle);
            marker.Add(innerRing);
            marker.Add(centerDot);
            marker.Add(topLeft);
            marker.Add(topRight);
            marker.Add(bottomLeft);
            marker.Add(bottomRight);
            marker.Add(label);

            UpdateMarkerVisualState(marker, state);
            return marker;
        }

        private static VisualElement CreateCorner(string positionClassName)
        {
            var corner = CreateNonPickingElement();
            corner.AddToClassList(CornerClassName);
            corner.AddToClassList(positionClassName);
            return corner;
        }

        private static VisualElement CreateNonPickingElement() => new()
        {
            pickingMode = PickingMode.Ignore
        };

        public void UpdateMarkerVisualState(VisualElement marker, IndicatorVisualState state)
        {
            if (marker == null)
            {
                return;
            }

            marker.RemoveFromClassList(UnfocusedClassName);
            marker.RemoveFromClassList(LockedClassName);
            marker.RemoveFromClassList(FocusedClassName);

            switch (state)
            {
                case IndicatorVisualState.Focused:
                    marker.AddToClassList(FocusedClassName);
                    break;
                case IndicatorVisualState.Locked:
                    marker.AddToClassList(LockedClassName);
                    break;
                default:
                    marker.AddToClassList(UnfocusedClassName);
                    break;
            }
        }
    }
}
