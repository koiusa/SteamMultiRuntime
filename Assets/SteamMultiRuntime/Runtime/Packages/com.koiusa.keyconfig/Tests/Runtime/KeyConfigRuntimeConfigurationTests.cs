using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class KeyConfigRuntimeConfigurationTests
    {
        [Test]
        public void Settings_AcceptsRuntimeInputActionAsset()
        {
            var settings = ScriptableObject.CreateInstance<KeyConfigSettings>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                settings.SetInputActionAsset(asset);

                Assert.That(settings.Resolve(), Is.SameAs(asset));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Configure_AfterAwake_CanBeRepeatedAndKeepsExistingSettersUsable()
        {
            var gameObject = new GameObject("Runtime Key Config");
            var settings = ScriptableObject.CreateInstance<KeyConfigSettings>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                settings.SetInputActionAsset(asset);
                var panel = gameObject.AddComponent<KeyConfigPanel>();

                Assert.DoesNotThrow(() => panel.Configure(settings, null, null, null));
                Assert.DoesNotThrow(() => panel.Configure(settings, null, null, null, "Gamepad"));
                panel.SetPersistence(() => string.Empty, _ => { });
                panel.SetBindingGroup("Keyboard&Mouse");

                Assert.That(panel.BindingGroup, Is.EqualTo("Keyboard&Mouse"));
                panel.Deactivate();
                Assert.DoesNotThrow(panel.Open);
                Assert.That(panel.IsVisible, Is.True);
                Assert.DoesNotThrow(panel.Toggle);
                Assert.That(panel.IsVisible, Is.False);
            }
            finally
            {
                Koiusa.UI.Core.UiMenuNavigator.CloseAll();
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
