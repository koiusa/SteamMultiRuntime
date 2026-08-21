using Koiusa.Keyconfig.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class CompositeBindingTests
    {
        private InputActionAsset asset;
        private InputActionMap map;

        [SetUp]
        public void SetUp()
        {
            asset = new InputActionAsset();
            map = new InputActionMap("Gameplay");
            asset.AddActionMap(map);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(asset);

        [Test]
        public void Entries_CollapseModifierCompositeIntoOneRow()
        {
            AddModifiedAction("Reload", "<Keyboard>/leftCtrl", "<Keyboard>/r");
            var entries = new InputBindingService(asset).GetBindingEntries();

            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].IsComposite, Is.True);
            Assert.That(entries[0].IsPartOfComposite, Is.False);
            Assert.That(entries[0].DisplayName, Is.EqualTo("Ctrl+R"));
            Assert.That(entries[0].IsRebindable, Is.True);
        }

        [Test]
        public void Conflict_UsesWholeCombinationAndNormalizesModifierSide()
        {
            var reload = AddModifiedAction("Reload", "<Keyboard>/leftCtrl", "<Keyboard>/r");
            AddModifiedAction("Find", "<Keyboard>/rightCtrl", "<Keyboard>/f");
            var duplicate = AddModifiedAction("OtherReload", "<Keyboard>/rightCtrl", "<Keyboard>/r");
            var plain = map.AddAction("Plain", binding: "<Keyboard>/r");
            var service = new InputBindingService(asset);

            Assert.That(service.TryFindConflictingBinding(reload, 0, null, out var conflict, out _), Is.True);
            Assert.That(conflict, Is.SameAs(duplicate));
            Assert.That(conflict, Is.Not.SameAs(plain));
        }

        [Test]
        public void ResetAndJsonRoundTrip_IncludeEveryCompositePart()
        {
            var action = AddModifiedAction("Reload", "<Keyboard>/leftCtrl", "<Keyboard>/r");
            var service = new InputBindingService(asset);
            action.ApplyBindingOverride(1, "<Keyboard>/leftShift");
            action.ApplyBindingOverride(2, "<Keyboard>/f");
            var json = service.CaptureOverrides();

            service.ResetBinding(action, 0);
            Assert.That(action.bindings[1].overridePath, Is.Null);
            Assert.That(action.bindings[2].overridePath, Is.Null);
            service.RestoreOverrides(json);
            Assert.That(service.GetBindingDisplayString(action, 0), Is.EqualTo("Shift+F"));
        }

        [Test]
        public void SingleBinding_RemainsOneRebindableEntry()
        {
            var action = map.AddAction("Jump", binding: "<Keyboard>/space");
            var service = new InputBindingService(asset);
            var entries = service.GetBindingEntries();

            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].IsComposite, Is.False);
            Assert.That(service.GetBindingDisplayString(action, 0), Is.EqualTo("Space"));
        }

        [Test]
        public void AddModifier_ConvertsSingleBindingAndResetRestoresOriginalShape()
        {
            var action = map.AddAction("Reload", binding: "<Keyboard>/r");
            var service = new InputBindingService(asset);

            Assert.That(service.AddModifier(action, 0), Is.True);
            var entry = service.GetBindingEntries()[0];
            Assert.That(entry.IsComposite, Is.True);
            Assert.That(entry.DisplayName, Is.EqualTo("Ctrl+R"));

            service.ResetBinding(action, entry.BindingIndex);
            entry = service.GetBindingEntries()[0];
            Assert.That(entry.IsComposite, Is.False);
            Assert.That(entry.DisplayName, Is.EqualTo("R"));
        }

        [Test]
        public void RemoveModifier_RemovesModifierAndPreservesButton()
        {
            var action = AddModifiedAction("Reload", "<Keyboard>/leftCtrl", "<Keyboard>/r");
            var service = new InputBindingService(asset);

            Assert.That(service.RemoveModifier(action, 0), Is.True);
            var entry = service.GetBindingEntries()[0];
            Assert.That(entry.IsComposite, Is.False);
            Assert.That(entry.DisplayName, Is.EqualTo("R"));
        }

        [Test]
        public void StructuralState_RoundTripsAlongsideOverrides()
        {
            var action = map.AddAction("Reload", binding: "<Keyboard>/r");
            var originalAssetJson = asset.ToJson();
            var service = new InputBindingService(asset);
            service.AddModifier(action, 0);
            var transformed = service.GetBindingEntries()[0];
            action.ApplyBindingOverride(transformed.BindingIndex + 1, "<Keyboard>/leftShift");
            action.ApplyBindingOverride(transformed.BindingIndex + 2, "<Keyboard>/f");
            var state = service.CaptureOverrides();

            var restartedAsset = InputActionAsset.FromJson(originalAssetJson);
            var restartedService = new InputBindingService(restartedAsset);
            restartedService.RestoreOverrides(state);
            var restored = restartedService.GetBindingEntries()[0];
            Assert.That(restored.IsComposite, Is.True);
            Assert.That(restored.DisplayName, Is.EqualTo("Shift+F"));
            UnityEngine.Object.DestroyImmediate(restartedAsset);
        }

        [Test]
        public void AddAndRemoveModifier_SupportsTwoModifierComposite()
        {
            var action = map.AddAction("Reload", binding: "<Keyboard>/r");
            var service = new InputBindingService(asset);

            Assert.That(service.AddModifier(action, 0), Is.True);
            var one = service.GetBindingEntries()[0];
            Assert.That(service.AddModifier(action, one.BindingIndex), Is.True);
            var two = service.GetBindingEntries()[0];
            Assert.That(two.DisplayName, Is.EqualTo("Ctrl+Shift+R"));
            Assert.That(service.AddModifier(action, two.BindingIndex), Is.False);

            Assert.That(service.RemoveModifier(action, two.BindingIndex), Is.True);
            var backToOne = service.GetBindingEntries()[0];
            Assert.That(backToOne.DisplayName, Is.EqualTo("Ctrl+R"));
        }

        [Test]
        public void SequentialRebind_ExcludesControlsAlreadyChosenForEarlierParts()
        {
            var action = AddModifiedAction("Reload", "<Keyboard>/leftCtrl", "<Keyboard>/r");
            action.ApplyBindingOverride(1, "<Keyboard>/leftShift");

            var excluded = InputRebindController.GetPreviouslyReboundControlPaths(
                action,
                new[] { 1, 2 },
                1);

            Assert.That(excluded, Is.EqualTo(new[] { "<Keyboard>/leftShift" }));
        }

        private InputAction AddModifiedAction(string name, string modifier, string button)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", modifier)
                .With("Button", button);
            return action;
        }
    }
}
