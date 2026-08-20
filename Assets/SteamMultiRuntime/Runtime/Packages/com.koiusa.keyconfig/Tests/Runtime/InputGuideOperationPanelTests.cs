using Koiusa.Keyconfig.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig.Tests
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
    }
}
