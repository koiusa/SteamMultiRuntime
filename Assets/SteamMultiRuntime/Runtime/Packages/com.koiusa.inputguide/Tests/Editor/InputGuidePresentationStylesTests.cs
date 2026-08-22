using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide.Tests
{
    public sealed class InputGuidePresentationStylesTests
    {
        private VisualElement overlay;
        private VisualElement operationPanel;
        private Label hint;

        [SetUp]
        public void SetUp()
        {
            overlay = new VisualElement();
            operationPanel = new VisualElement();
            hint = new Label("F1 / TOUCH PAD");
            hint.AddToClassList("input-operation-toggle-hint");
            operationPanel.Add(hint);
            overlay.Add(operationPanel);
        }

        [Test]
        public void Standard_PreservesFullWidthClassAndShowsDefaultHint()
        {
            InputGuidePresentationStyles.Apply(overlay, operationPanel,
                InputGuideLayoutPreset.Standard, InputGuideToggleHintVisibility.PresetDefault);

            Assert.That(overlay.ClassListContains(InputGuidePresentationStyles.CompactClass), Is.False);
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void CompactOperations_AddsOfficialClassAndHidesDefaultHint()
        {
            InputGuidePresentationStyles.Apply(overlay, operationPanel,
                InputGuideLayoutPreset.CompactOperations, InputGuideToggleHintVisibility.PresetDefault);

            Assert.That(overlay.ClassListContains(InputGuidePresentationStyles.CompactClass), Is.True);
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ToggleHintVisibility_OverridesPresetDefault()
        {
            InputGuidePresentationStyles.Apply(overlay, operationPanel,
                InputGuideLayoutPreset.CompactOperations, InputGuideToggleHintVisibility.Visible);
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            InputGuidePresentationStyles.Apply(overlay, operationPanel,
                InputGuideLayoutPreset.Standard, InputGuideToggleHintVisibility.Hidden);
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void PublicPresetApi_DoesNotChangeDisplayModeOrSortingOrder()
        {
            var gameObject = new GameObject("InputGuidePresentationTest");
            try
            {
                var document = gameObject.AddComponent<UIDocument>();
                document.sortingOrder = 37;
                IInputGuideOverlay inputGuide = gameObject.AddComponent<InputGuideOverlay>();
                inputGuide.ApplyConfiguration(new InputGuideConfiguration(
                    InputGuideDisplayMode.OperationsOnly,
                    InputGuideLayoutPreset.CompactOperations,
                    InputGuideToggleHintVisibility.Hidden));

                Assert.That(inputGuide.DisplayMode,
                    Is.EqualTo(InputGuideDisplayMode.OperationsOnly));
                Assert.That(document.sortingOrder, Is.EqualTo(37));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Selection_NormalizesMapNamesAndCannotBeMutatedByCaller()
        {
            var source = new[] { "ScreenLayout", "", "ScreenLayout", "Calibration" };
            var selection = InputGuideSelection.Specified(source, " Keyboard&Mouse ");
            source[0] = "Changed";

            Assert.That(selection.MapFilter, Is.EqualTo(InputGuideMapFilter.Specified));
            Assert.That(selection.ActionMapNames, Is.EqualTo(new[] { "ScreenLayout", "Calibration" }));
            Assert.That(selection.BindingGroup, Is.EqualTo("Keyboard&Mouse"));
        }

        [Test]
        public void Configuration_CanRestoreCapturedPublicState()
        {
            var gameObject = new GameObject("InputGuideConfigurationTest");
            try
            {
                gameObject.SetActive(false);
                gameObject.AddComponent<UIDocument>();
                IInputGuideOverlay inputGuide = gameObject.AddComponent<InputGuideOverlay>();
                var initial = inputGuide.CaptureConfiguration();

                inputGuide.ApplyConfiguration(
                    InputGuideConfiguration.CompactOperations());
                gameObject.SetActive(true);
                Assert.That(inputGuide.DisplayMode,
                    Is.EqualTo(InputGuideDisplayMode.OperationsOnly));
                Assert.That(inputGuide.LayoutPreset, Is.EqualTo(InputGuideLayoutPreset.CompactOperations));

                inputGuide.ApplyConfiguration(initial);
                Assert.That(inputGuide.DisplayMode, Is.EqualTo(initial.DisplayMode));
                Assert.That(inputGuide.LayoutPreset, Is.EqualTo(initial.LayoutPreset));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SelectionController_StoresAtomicSelectionChange()
        {
            var gameObject = new GameObject("InputGuideSelectionControllerTest");
            try
            {
                var controller = gameObject.AddComponent<InputGuideSelectionController>();

                controller.ApplySelection(InputGuideSelection.Specified(
                    new[] { "Calibration" }, "Gamepad"));

                Assert.That(controller.Current.MapFilter, Is.EqualTo(InputGuideMapFilter.Specified));
                Assert.That(controller.Current.ActionMapNames, Is.EqualTo(new[] { "Calibration" }));
                Assert.That(controller.Current.BindingGroup, Is.EqualTo("Gamepad"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
