using Koiusa.KeyConfig;
using Koiusa.InputGuide;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide.Tests
{
    public sealed class InputGuideOperationPanelTests
    {
        [Test]
        public void Build_CreatesLocalizedSectionForEveryMap()
        {
            var keyboardList = new VisualElement();
            var gamepadList = new VisualElement();
            var global = new InputActionMap("Global");
            global.AddAction("Pause").AddBinding("<Keyboard>/escape");
            var calibration = new InputActionMap("Calibration");
            calibration.AddAction("Calibrate").AddBinding("<Keyboard>/c");
            var panel = new InputGuideOperationPanel(keyboardList, gamepadList, _ => true);

            panel.Build(new[] { global, calibration });

            var titles = keyboardList.Query<Label>(className: "input-operation-section-title").ToList();
            Assert.That(titles, Has.Count.EqualTo(2));
            Assert.That(titles[0].text, Is.EqualTo(KeyConfigLocalization.Get("Global")));
            Assert.That(titles[1].text, Is.EqualTo(KeyConfigLocalization.Get("Calibration")));
            Assert.That(keyboardList.Query<Label>(className: "input-operation-action").ToList(), Has.Count.EqualTo(2));
        }

        [Test]
        public void Build_ShowsModifierCompositeAsOneOperation()
        {
            var keyboardList = new VisualElement();
            var gamepadList = new VisualElement();
            var map = new InputActionMap("Gameplay");
            var action = map.AddAction("Reload", InputActionType.Button);
            action.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/leftCtrl")
                .With("Button", "<Keyboard>/r");
            var panel = new InputGuideOperationPanel(keyboardList, gamepadList, _ => true);

            panel.Build(new[] { map });

            var actions = keyboardList.Query<Label>(className: "input-operation-action").ToList();
            var bindings = keyboardList.Query<Label>(className: "input-operation-binding").ToList();
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(bindings, Has.Count.EqualTo(1));
            Assert.That(bindings[0].text, Is.EqualTo("Ctrl+R"));
        }
    }
}
