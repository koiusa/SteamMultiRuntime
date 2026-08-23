using UnityEngine.UIElements;

namespace Koiusa.InputGuide
{
    public enum InputGuideMapFilter
    {
        Specified,
        All,
        EnabledOnly
    }

    public enum InputGuideDisplayMode
    {
        Both,
        DeviceOnly,
        OperationsOnly,
        Hidden
    }

    public enum InputGuideLayoutPreset
    {
        Standard,
        CompactOperations
    }

    public enum InputGuideToggleHintVisibility
    {
        PresetDefault,
        Visible,
        Hidden
    }

    public enum InputGuidePanelAnchor
    {
        TopRight,
        TopLeft,
        TopCenter,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    /// <summary>Identifies a presentation panel, not a simulated device type.</summary>
    public enum InputGuidePanelSlot
    {
        Device,
        Operations
    }

    /// <summary>Public control surface for temporarily changing an input guide screen.</summary>
    public interface IInputGuideOverlay
    {
        bool IsVisible { get; }
        InputGuideDisplayMode DisplayMode { get; }
        InputGuideSelection Selection { get; }
        InputGuideLayoutPreset LayoutPreset { get; }
        InputGuideToggleHintVisibility ToggleHintVisibility { get; }
        InputGuideConfiguration CaptureConfiguration();
        void ApplyConfiguration(InputGuideConfiguration configuration);

        /// <summary>Gets the current anchor owned by the requested panel.</summary>
        InputGuidePanelAnchor GetPanelAnchor(InputGuidePanelSlot panelSlot);

        /// <summary>Changes one panel anchor and immediately reapplies it to the current VisualTree.</summary>
        void SetPanelAnchor(InputGuidePanelSlot panelSlot, InputGuidePanelAnchor anchor);

        /// <summary>Gets the optional UXML override; null means the panel's serialized default.</summary>
        VisualTreeAsset GetPanelLayoutOverride(InputGuidePanelSlot panelSlot);

        /// <summary>Changes one panel UXML override and rebuilds the current VisualTree once.</summary>
        void SetPanelLayoutOverride(InputGuidePanelSlot panelSlot, VisualTreeAsset layout);
        VisualTreeAsset GetDeviceLayoutOverride(string layoutId);
        void SetDeviceLayoutOverride(string layoutId, VisualTreeAsset layout);
        void SetDeviceLayoutVisible(string layoutId, bool visible);
        void ShowDeviceLayout(string layoutId);
        void ApplySelection(InputGuideSelection selection);
        void SelectPreviousMapTab();
        void SelectNextMapTab();
        void ScrollOperationList(float direction, float distance = 90f);
    }

    /// <summary>
    /// Immutable public configuration that can be captured, applied atomically, and restored later.
    /// </summary>
    public sealed class InputGuideConfiguration
    {
        public InputGuideConfiguration(
            InputGuideDisplayMode displayMode,
            InputGuideLayoutPreset layoutPreset,
            InputGuideToggleHintVisibility toggleHintVisibility = InputGuideToggleHintVisibility.PresetDefault,
            InputGuidePanelAnchor devicePanelAnchor = InputGuidePanelAnchor.BottomLeft,
            InputGuidePanelAnchor operationsPanelAnchor = InputGuidePanelAnchor.TopRight)
        {
            DisplayMode = displayMode;
            LayoutPreset = layoutPreset;
            ToggleHintVisibility = toggleHintVisibility;
            DevicePanelAnchor = devicePanelAnchor;
            OperationsPanelAnchor = operationsPanelAnchor;
        }

        public InputGuideDisplayMode DisplayMode { get; }
        public InputGuideLayoutPreset LayoutPreset { get; }
        public InputGuideToggleHintVisibility ToggleHintVisibility { get; }
        public InputGuidePanelAnchor DevicePanelAnchor { get; }
        public InputGuidePanelAnchor OperationsPanelAnchor { get; }

        public static InputGuideConfiguration CompactOperations(
            InputGuidePanelAnchor panelAnchor = InputGuidePanelAnchor.TopRight)
        {
            return new InputGuideConfiguration(
                InputGuideDisplayMode.OperationsOnly,
                InputGuideLayoutPreset.CompactOperations,
                InputGuideToggleHintVisibility.PresetDefault,
                InputGuidePanelAnchor.BottomLeft,
                panelAnchor);
        }
    }
}
