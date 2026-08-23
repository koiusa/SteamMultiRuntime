using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide.Tests
{
    public sealed class InputGuidePresentationStylesTests
    {
        private VisualElement overlay;
        private VisualElement operationPanel;
        private Label hint;
        private GameObject layoutObject;

        [SetUp]
        public void SetUp()
        {
            overlay = new VisualElement();
            operationPanel = new VisualElement();
            hint = new Label("F1 / TOUCH PAD");
            hint.AddToClassList("input-operation-toggle-hint");
            operationPanel.Add(hint);
            overlay.Add(operationPanel);
            layoutObject = new GameObject("InputGuideLayoutTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(layoutObject);
        }

        [Test]
        public void Standard_PreservesFullWidthClassAndShowsDefaultHint()
        {
            InputGuideOverlay.ApplyLayoutPreset(overlay, operationPanel, new InputGuideConfiguration(
                InputGuideDisplayMode.Both, InputGuideLayoutPreset.Standard));

            Assert.That(overlay.ClassListContains("input-guide-screen--compact-operations"), Is.False);
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void CompactOperations_AddsOfficialClassAndHidesDefaultHint()
        {
            InputGuideOverlay.ApplyLayoutPreset(
                overlay, operationPanel, InputGuideConfiguration.CompactOperations());

            Assert.That(overlay.ClassListContains("input-guide-screen--compact-operations"), Is.True);
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void CompactOperations_AnchorDefaultsTopRight()
        {
            Assert.That(InputGuideConfiguration.CompactOperations().OperationsPanelAnchor,
                Is.EqualTo(InputGuidePanelAnchor.TopRight));
        }

        [Test]
        public void OfficialPrefabContainsIndependentLayoutsForEveryPanel()
        {
            var prefab = Resources.Load<GameObject>("System/InputGuideOverlay");
            var layouts = prefab.GetComponents<InputGuidePanelLayout>();
            IInputGuideOverlay inputGuide = prefab.GetComponent<InputGuideOverlay>();

            Assert.That(layouts, Has.Length.EqualTo(3));
            Assert.That(System.Array.FindAll(layouts,
                value => value.PanelSlot == InputGuidePanelSlot.Device), Has.Length.EqualTo(2));
            Assert.That(System.Array.TrueForAll(layouts, value => value.DefaultLayout != null), Is.True);
            Assert.That(inputGuide.GetPanelAnchor(InputGuidePanelSlot.Device),
                Is.EqualTo(InputGuidePanelAnchor.BottomLeft));
            Assert.That(inputGuide.GetPanelAnchor(InputGuidePanelSlot.Operations),
                Is.EqualTo(InputGuidePanelAnchor.TopRight));
            Assert.That(prefab.GetComponent<InputGuideDeviceLayoutCollection>().Get("keyboard").DefaultLayout,
                Is.Not.Null);
            var mouse = prefab.GetComponent<InputGuideDeviceLayoutCollection>().Get("mouse");
            Assert.That(mouse.DefaultLayout, Is.Not.Null);
            Assert.That(mouse.PanelSlot, Is.EqualTo(InputGuidePanelSlot.Device));
            Assert.That(prefab.GetComponent<InputGuideDeviceLayoutCollection>().Get("gamepad").DefaultLayout,
                Is.Not.Null);

            var root = new VisualElement();
            Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideOverlay").CloneTree(root);
            prefab.GetComponent<InputGuidePanelCollection>().Build(root);
            var deviceHost = root.Q<VisualElement>("device-panel-host");
            var mouseHost = root.Q<VisualElement>("mouse-panel-host");
            Assert.That(mouseHost.parent, Is.SameAs(deviceHost.parent),
                "Mouse must be an independently anchored sibling of the primary Device panel.");
        }

        [TestCase(InputGuidePanelAnchor.TopRight, false, false, true, true, false, false)]
        [TestCase(InputGuidePanelAnchor.TopLeft, true, false, false, true, false, false)]
        [TestCase(InputGuidePanelAnchor.TopCenter, false, true, false, true, false, false)]
        [TestCase(InputGuidePanelAnchor.MiddleLeft, true, false, false, false, true, false)]
        [TestCase(InputGuidePanelAnchor.Center, false, true, false, false, true, false)]
        [TestCase(InputGuidePanelAnchor.MiddleRight, false, false, true, false, true, false)]
        [TestCase(InputGuidePanelAnchor.BottomLeft, true, false, false, false, false, true)]
        [TestCase(InputGuidePanelAnchor.BottomCenter, false, true, false, false, false, true)]
        [TestCase(InputGuidePanelAnchor.BottomRight, false, false, true, false, false, true)]
        public void PanelLayout_AppliesNineAnchorsAcrossRebuilds(
            InputGuidePanelAnchor anchor,
            bool left,
            bool horizontalCenter,
            bool right,
            bool top,
            bool middle,
            bool bottom)
        {
            var component = layoutObject.AddComponent<InputGuidePanelLayout>();
            var root = new VisualElement();
            var rebuiltPanel = new VisualElement { name = "device-panel-host" };
            root.Add(rebuiltPanel);
            var mouseLayout = Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMouse");
            component.Configure(InputGuidePanelSlot.Device, InputGuidePanelAnchor.Center, mouseLayout);
            component.Build(root);
            component.SetAnchor(anchor);
            component.Build(root);

            Assert.That(rebuiltPanel.ClassListContains(InputGuidePanelLayout.LeftClass), Is.EqualTo(left));
            Assert.That(rebuiltPanel.ClassListContains(InputGuidePanelLayout.HorizontalCenterClass),
                Is.EqualTo(horizontalCenter));
            Assert.That(rebuiltPanel.ClassListContains(InputGuidePanelLayout.RightClass), Is.EqualTo(right));
            Assert.That(rebuiltPanel.ClassListContains(InputGuidePanelLayout.TopClass), Is.EqualTo(top));
            Assert.That(rebuiltPanel.ClassListContains(InputGuidePanelLayout.MiddleClass), Is.EqualTo(middle));
            Assert.That(rebuiltPanel.ClassListContains(InputGuidePanelLayout.BottomClass), Is.EqualTo(bottom));
        }

        [Test]
        public void ToggleHintVisibility_OverridesPresetDefault()
        {
            InputGuideOverlay.ApplyLayoutPreset(overlay, operationPanel, new InputGuideConfiguration(
                InputGuideDisplayMode.OperationsOnly,
                InputGuideLayoutPreset.CompactOperations,
                InputGuideToggleHintVisibility.Visible));
            Assert.That(hint.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            InputGuideOverlay.ApplyLayoutPreset(overlay, operationPanel, new InputGuideConfiguration(
                InputGuideDisplayMode.Both,
                InputGuideLayoutPreset.Standard,
                InputGuideToggleHintVisibility.Hidden));
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
                IInputGuideOverlay inputGuide = AddOverlayWithPanelLayouts(gameObject);
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
        public void RuntimePanelAnchorChangeAppliesToPrimaryPanelWithoutOverwritingAdditionalDevicePanel()
        {
            var deviceLayout = layoutObject.AddComponent<InputGuidePanelLayout>();
            var mousePanelLayout = layoutObject.AddComponent<InputGuidePanelLayout>();
            var collection = layoutObject.AddComponent<InputGuidePanelCollection>();
            var root = new VisualElement();
            var devicePanel = new VisualElement { name = "device-panel-host" };
            var mousePanel = new VisualElement { name = "mouse-panel-host" };
            root.Add(devicePanel);
            root.Add(mousePanel);
            deviceLayout.Configure(
                InputGuidePanelSlot.Device,
                InputGuidePanelAnchor.TopRight,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMouse"));
            mousePanelLayout.Configure(
                InputGuidePanelSlot.Device,
                InputGuidePanelAnchor.MiddleRight,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMousePanel"),
                "mouse-panel-host");
            collection.Configure(deviceLayout, mousePanelLayout);
            deviceLayout.Build(root);
            mousePanelLayout.Build(root);

            collection.SetAnchor(InputGuidePanelSlot.Device, InputGuidePanelAnchor.BottomLeft);

            AssertPanelHasBottomLeftAnchor(devicePanel);
            Assert.That(mousePanel.ClassListContains(InputGuidePanelLayout.RightClass), Is.True);
            Assert.That(mousePanel.ClassListContains(InputGuidePanelLayout.MiddleClass), Is.True);
            Assert.That(mousePanel.ClassListContains(InputGuidePanelLayout.LeftClass), Is.False);
            Assert.That(mousePanel.ClassListContains(InputGuidePanelLayout.BottomClass), Is.False);
        }

        private static void AssertPanelHasBottomLeftAnchor(VisualElement panel)
        {
            Assert.That(panel.ClassListContains(InputGuidePanelLayout.LeftClass), Is.True);
            Assert.That(panel.ClassListContains(InputGuidePanelLayout.BottomClass), Is.True);
            Assert.That(panel.ClassListContains(InputGuidePanelLayout.RightClass), Is.False);
            Assert.That(panel.ClassListContains(InputGuidePanelLayout.TopClass), Is.False);
        }

        [Test]
        public void RuntimePanelLayoutOverrideCanBeSetAndCleared()
        {
            var gameObject = new GameObject("InputGuideRuntimeLayoutOverrideTest");
            try
            {
                gameObject.SetActive(false);
                gameObject.AddComponent<UIDocument>();
                var inputGuide = AddOverlayWithPanelLayouts(gameObject);
                var layout = Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMouse");

                inputGuide.SetPanelLayoutOverride(InputGuidePanelSlot.Device, layout);
                Assert.That(inputGuide.GetPanelLayoutOverride(InputGuidePanelSlot.Device), Is.SameAs(layout));

                inputGuide.SetPanelLayoutOverride(InputGuidePanelSlot.Device, null);
                Assert.That(inputGuide.GetPanelLayoutOverride(InputGuidePanelSlot.Device), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeDeviceLayoutOverrideCanBeSetAndCleared()
        {
            var gameObject = new GameObject("InputGuideDeviceLayoutOverrideTest");
            try
            {
                gameObject.SetActive(false);
                gameObject.AddComponent<UIDocument>();
                var inputGuide = AddOverlayWithPanelLayouts(gameObject);
                var layout = Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideKeyboard");

                inputGuide.SetDeviceLayoutOverride("keyboard", layout);
                Assert.That(inputGuide.GetDeviceLayoutOverride("keyboard"), Is.SameAs(layout));
                inputGuide.SetDeviceLayoutOverride("keyboard", null);
                Assert.That(inputGuide.GetDeviceLayoutOverride("keyboard"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DeviceLayouts_BuildIntoIndependentFullPanelLayers()
        {
            var root = new VisualElement();
            Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideOverlay").CloneTree(root);
            var devicePanel = layoutObject.AddComponent<InputGuidePanelLayout>();
            devicePanel.Configure(InputGuidePanelSlot.Device, InputGuidePanelAnchor.BottomLeft,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideDevicePanel"), "device-panel-host");
            var mousePanel = layoutObject.AddComponent<InputGuidePanelLayout>();
            mousePanel.Configure(InputGuidePanelSlot.Device, InputGuidePanelAnchor.BottomLeft,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMousePanel"), "mouse-panel-host");
            devicePanel.Build(root);
            mousePanel.Build(root);
            var keyboard = new InputGuideDeviceLayout();
            keyboard.Configure("keyboard", InputGuidePanelSlot.Device,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideKeyboard"),
                "primary", true, "Keyboard");
            var gamepad = new InputGuideDeviceLayout();
            gamepad.Configure("gamepad", InputGuidePanelSlot.Device,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideGamepad"),
                "primary", false, "Gamepad");
            var pointer = new InputGuideDeviceLayout();
            pointer.Configure("pointer", InputGuidePanelSlot.Device,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMouse"),
                string.Empty, true, "Mouse");
            pointer.ConfigureHostElementName("mouse-device-layouts-host");
            var collection = layoutObject.AddComponent<InputGuideDeviceLayoutCollection>();
            collection.Configure(keyboard, pointer, gamepad);

            collection.Build(root);

            var host = root.Q<VisualElement>("device-layouts-host");
            var keyboardLayer = root.Q<VisualElement>("input-guide-device-layout-keyboard");
            var mouseLayer = root.Q<VisualElement>("input-guide-device-layout-pointer");
            var gamepadLayer = root.Q<VisualElement>("input-guide-device-layout-gamepad");
            Assert.That(host.ClassListContains("input-device-layouts-host"), Is.True);
            Assert.That(host.ClassListContains("input-device-host"), Is.False);
            Assert.That(keyboardLayer.ClassListContains("input-device-host"), Is.True);
            Assert.That(mouseLayer.ClassListContains("input-device-host"), Is.False);
            Assert.That(mouseLayer.ClassListContains("input-device-auxiliary-host"), Is.True);
            Assert.That(mouseLayer.parent.name, Is.EqualTo("mouse-device-layouts-host"));
            Assert.That(gamepadLayer.ClassListContains("input-device-host"), Is.True);
            Assert.That(keyboardLayer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(mouseLayer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(gamepadLayer.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(keyboardLayer.Q<VisualElement>(className: "input-keyboard"), Is.Not.Null);
        }

        [Test]
        public void DeviceLayouts_MatchUsageAndShowEveryNonExclusiveMatch()
        {
            var device = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.SetDeviceUsage(device, CommonUsages.LeftHand);
                var root = new VisualElement();
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideDevicePanel").CloneTree(root);
                var generic = new InputGuideDeviceLayout();
                generic.Configure("xr-controller", InputGuidePanelSlot.Device,
                    Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideGamepad"),
                    string.Empty, false, "Gamepad");
                var left = new InputGuideDeviceLayout();
                left.Configure("xr-left", InputGuidePanelSlot.Device,
                    Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideGamepad"),
                    string.Empty, false, "Gamepad");
                left.ConfigureRequiredUsages(CommonUsages.LeftHand.ToString());
                var right = new InputGuideDeviceLayout();
                right.Configure("xr-right", InputGuidePanelSlot.Device,
                    Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideGamepad"),
                    string.Empty, false, "Gamepad");
                right.ConfigureRequiredUsages(CommonUsages.RightHand.ToString());
                var collection = layoutObject.AddComponent<InputGuideDeviceLayoutCollection>();
                collection.Configure(generic, left, right);
                collection.Build(root);

                var matched = collection.ShowForDevice(device);

                Assert.That(matched, Is.True);
                Assert.That(generic.IsVisible, Is.True);
                Assert.That(left.IsVisible, Is.True);
                Assert.That(right.IsVisible, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(device);
            }
        }

        [Test]
        public void InitialPresentationUsesConnectedGamepadWhenKeyboardIsUnavailable()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Assert.That(InputGuideOverlay.ResolvePresentationDevice(null, null, gamepad),
                    Is.SameAs(gamepad));
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
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
                IInputGuideOverlay inputGuide = AddOverlayWithPanelLayouts(gameObject);
                var mousePanel = System.Array.Find(
                    gameObject.GetComponents<InputGuidePanelLayout>(),
                    value => value.HostElementName == "mouse-panel-host");
                var initial = inputGuide.CaptureConfiguration();

                inputGuide.ApplyConfiguration(
                    InputGuideConfiguration.CompactOperations());
                gameObject.SetActive(true);
                Assert.That(inputGuide.DisplayMode,
                    Is.EqualTo(InputGuideDisplayMode.OperationsOnly));
                Assert.That(inputGuide.LayoutPreset, Is.EqualTo(InputGuideLayoutPreset.CompactOperations));
                Assert.That(inputGuide.GetPanelAnchor(InputGuidePanelSlot.Operations),
                    Is.EqualTo(InputGuidePanelAnchor.TopRight));
                Assert.That(mousePanel.Anchor, Is.EqualTo(InputGuidePanelAnchor.MiddleRight));

                inputGuide.ApplyConfiguration(initial);
                Assert.That(inputGuide.DisplayMode, Is.EqualTo(initial.DisplayMode));
                Assert.That(inputGuide.LayoutPreset, Is.EqualTo(initial.LayoutPreset));
                Assert.That(inputGuide.GetPanelAnchor(InputGuidePanelSlot.Device),
                    Is.EqualTo(initial.DevicePanelAnchor));
                Assert.That(inputGuide.GetPanelAnchor(InputGuidePanelSlot.Operations),
                    Is.EqualTo(initial.OperationsPanelAnchor));
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

        private static IInputGuideOverlay AddOverlayWithPanelLayouts(GameObject gameObject)
        {
            var deviceLayout = gameObject.AddComponent<InputGuidePanelLayout>();
            deviceLayout.Configure(
                InputGuidePanelSlot.Device,
                InputGuidePanelAnchor.BottomLeft,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideDevicePanel"));
            var operationsLayout = gameObject.AddComponent<InputGuidePanelLayout>();
            operationsLayout.Configure(
                InputGuidePanelSlot.Operations,
                InputGuidePanelAnchor.TopRight,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideOperationsPanel"));
            var mousePanelLayout = gameObject.AddComponent<InputGuidePanelLayout>();
            mousePanelLayout.Configure(
                InputGuidePanelSlot.Device,
                InputGuidePanelAnchor.MiddleRight,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMousePanel"),
                "mouse-panel-host");
            var collection = gameObject.AddComponent<InputGuidePanelCollection>();
            collection.Configure(deviceLayout, mousePanelLayout, operationsLayout);
            var keyboard = new InputGuideDeviceLayout();
            keyboard.Configure("keyboard", InputGuidePanelSlot.Device,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideKeyboard"),
                "primary", true, "Keyboard");
            var mouse = new InputGuideDeviceLayout();
            mouse.Configure("mouse", InputGuidePanelSlot.Device,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideMouse"),
                string.Empty, true, "Mouse");
            mouse.ConfigureHostElementName("mouse-device-layouts-host");
            var gamepad = new InputGuideDeviceLayout();
            gamepad.Configure("gamepad", InputGuidePanelSlot.Device,
                Resources.Load<VisualTreeAsset>("UI/InputGuide/InputGuideGamepad"),
                "primary", false, "Gamepad", "Joystick");
            var deviceLayouts = gameObject.AddComponent<InputGuideDeviceLayoutCollection>();
            deviceLayouts.Configure(keyboard, mouse, gamepad);
            var overlay = gameObject.AddComponent<InputGuideOverlay>();
            overlay.ConfigurePanelCollection(collection);
            overlay.ConfigureDeviceLayoutCollection(deviceLayouts);
            return overlay;
        }
    }
}
