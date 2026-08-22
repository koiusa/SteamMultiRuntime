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
            InputGuideToggleHintVisibility toggleHintVisibility = InputGuideToggleHintVisibility.PresetDefault)
        {
            DisplayMode = displayMode;
            LayoutPreset = layoutPreset;
            ToggleHintVisibility = toggleHintVisibility;
        }

        public InputGuideDisplayMode DisplayMode { get; }
        public InputGuideLayoutPreset LayoutPreset { get; }
        public InputGuideToggleHintVisibility ToggleHintVisibility { get; }

        public static InputGuideConfiguration CompactOperations()
        {
            return new InputGuideConfiguration(
                InputGuideDisplayMode.OperationsOnly,
                InputGuideLayoutPreset.CompactOperations);
        }
    }
}
