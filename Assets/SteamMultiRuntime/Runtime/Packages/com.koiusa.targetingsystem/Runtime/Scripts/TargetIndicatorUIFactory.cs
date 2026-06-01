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
        private const string LabelClassName = "marker-label";

        /// <summary>
        /// ターゲット用マーカーUIを生成。
        /// 円形の枠とターゲット名ラベルの組み合わせ。
        /// </summary>
        public VisualElement CreateTargetMarker(string targetName, float size, IndicatorVisualState state)
        {
            var marker = new VisualElement();
            marker.AddToClassList(MarkerClassName);
            marker.style.width = size;
            marker.style.height = size;
            marker.style.position = Position.Absolute;

            var circle = new VisualElement();
            circle.AddToClassList(CircleClassName);

            var label = new Label(targetName);
            label.AddToClassList(LabelClassName);

            marker.Add(circle);
            marker.Add(label);

            UpdateMarkerVisualState(marker, state);
            return marker;
        }

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
