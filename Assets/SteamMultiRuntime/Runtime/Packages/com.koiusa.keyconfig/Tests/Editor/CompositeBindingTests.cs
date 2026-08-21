using Koiusa.KeyConfig;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
            Assert.That(entries[0].ModifierCount, Is.EqualTo(1));
            Assert.That(entries[0].IsPartOfComposite, Is.False);
            Assert.That(entries[0].DisplayName, Is.EqualTo("Ctrl+R"));
            Assert.That(entries[0].BindingPaths, Is.EqualTo(new[]
            {
                "<Keyboard>/leftCtrl",
                "<Keyboard>/r"
            }));
            Assert.That(entries[0].IsRebindable, Is.True);
        }

        [Test]
        public void Entries_KeepWasdAndArrowMovementCompositesOnSeparateRows()
        {
            var move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            var entries = new InputBindingService(asset).GetBindingEntries();

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].BindingPaths, Is.EqualTo(new[]
            {
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d"
            }));
            Assert.That(entries[1].BindingPaths, Is.EqualTo(new[]
            {
                "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow", "<Keyboard>/rightArrow"
            }));
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
        public void ModifierChange_WithMultipleBindings_ChangesSelectedBindingWithoutReorderingRows()
        {
            var action = map.AddAction("Reload", InputActionType.Button);
            action.AddBinding("<Keyboard>/r");
            action.AddBinding("<Keyboard>/f");
            var selectedBindingId = action.bindings[0].id;
            var untouchedBindingId = action.bindings[1].id;
            var service = new InputBindingService(asset);
            action.ApplyBindingOverride(1, "<Keyboard>/g");

            Assert.That(service.AddModifier(action, 0), Is.True);

            var entries = service.GetBindingEntries();
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].BindingId, Is.EqualTo(selectedBindingId));
            Assert.That(entries[0].DisplayName, Is.EqualTo("Ctrl+R"));
            Assert.That(entries[1].BindingId, Is.EqualTo(untouchedBindingId));
            Assert.That(entries[1].DisplayName, Is.EqualTo("G"));

            Assert.That(service.RemoveModifier(action, entries[0].BindingIndex), Is.True);
            entries = service.GetBindingEntries();
            Assert.That(entries[0].BindingId, Is.EqualTo(selectedBindingId));
            Assert.That(entries[0].DisplayName, Is.EqualTo("R"));
            Assert.That(entries[1].BindingId, Is.EqualTo(untouchedBindingId));
            Assert.That(entries[1].DisplayName, Is.EqualTo("G"));
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
            Assert.That(two.BindingPaths, Is.EqualTo(new[]
            {
                "<Keyboard>/leftCtrl",
                "<Keyboard>/leftShift",
                "<Keyboard>/r"
            }));
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

        [Test]
        public void ModifierButtons_AreUnavailableWhenBindingDeviceIsDisconnected()
        {
            var action = AddModifiedAction("Reload", "<Gamepad>/leftShoulder", "<Gamepad>/buttonWest");
            var entry = new KeyConfigBinding(new InputBindingService(asset).GetBindingEntries()[0]);

            Assert.That(KeyConfigBindingRowFactory.CanChangeModifier(entry, true, false), Is.False);
            Assert.That(KeyConfigBindingRowFactory.CanChangeModifier(entry, false, false), Is.False);
            Assert.That(KeyConfigBindingRowFactory.CanChangeModifier(entry, false, true), Is.True);
        }

        [Test]
        public void DeviceAvailability_UsesConnectedLayoutInsteadOfExactControlResolution()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Assert.That(InputControlActivity.HasConnectedDevice("<Gamepad>/nonStandardButton"), Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void AliasSuppression_ExcludesEveryControlChangedInObservedStateEvent()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var suppression = new RebindAliasSuppression();
            var buttonProbe = map.AddAction("ButtonProbe", InputActionType.Button);
            buttonProbe.AddBinding("<Gamepad>/buttonSouth");
            var triggerProbe = map.AddAction("TriggerProbe", InputActionType.Button);
            triggerProbe.AddBinding("<Gamepad>/leftTrigger");
            var buttonCompleted = false;
            var triggerCompleted = false;
            InputActionRebindingExtensions.RebindingOperation buttonOperation = null;
            InputActionRebindingExtensions.RebindingOperation triggerOperation = null;
            try
            {
                suppression.BeginPartObservation();
                InputSystem.QueueStateEvent(gamepad, new GamepadState
                {
                    leftTrigger = 1f,
                    leftStick = new UnityEngine.Vector2(0.8f, 0f)
                }.WithButton(GamepadButton.South));
                InputSystem.Update();
                suppression.EndPartObservation();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();

                buttonOperation = buttonProbe.PerformInteractiveRebinding(0)
                    .OnComplete(_ => buttonCompleted = true);
                suppression.ApplyExclusions(buttonOperation);
                buttonOperation.Start();
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                buttonOperation.Dispose();
                buttonOperation = null;

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();
                triggerOperation = triggerProbe.PerformInteractiveRebinding(0)
                    .OnComplete(_ => triggerCompleted = true);
                suppression.ApplyExclusions(triggerOperation);
                triggerOperation.Start();
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1f });
                InputSystem.Update();

                Assert.That(buttonCompleted, Is.False);
                Assert.That(triggerCompleted, Is.False);
            }
            finally
            {
                buttonOperation?.Dispose();
                triggerOperation?.Dispose();
                suppression.Dispose();
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void AliasSuppression_IgnoresTextEventsBeforeChangedControlEnumeration()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var suppression = new RebindAliasSuppression();
            try
            {
                suppression.BeginPartObservation();
                Assert.DoesNotThrow(() =>
                {
                    InputSystem.QueueTextEvent(keyboard, 'a');
                    InputSystem.Update();
                });
            }
            finally
            {
                suppression.Dispose();
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void AliasSuppression_CollectsChangedControlFromDeltaStateEvent()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var suppression = new RebindAliasSuppression();
            var probe = map.AddAction("DeltaProbe", InputActionType.Button);
            probe.AddBinding("<Gamepad>/leftTrigger");
            var completed = false;
            InputActionRebindingExtensions.RebindingOperation operation = null;
            try
            {
                suppression.BeginPartObservation();
                InputSystem.QueueDeltaStateEvent(gamepad.leftTrigger, 1f);
                InputSystem.Update();
                suppression.EndPartObservation();
                InputSystem.QueueDeltaStateEvent(gamepad.leftTrigger, 0f);
                InputSystem.Update();

                operation = probe.PerformInteractiveRebinding(0).OnComplete(_ => completed = true);
                suppression.ApplyExclusions(operation);
                operation.Start();
                InputSystem.QueueDeltaStateEvent(gamepad.leftTrigger, 1f);
                InputSystem.Update();

                Assert.That(completed, Is.False);
            }
            finally
            {
                operation?.Dispose();
                suppression.Dispose();
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void SequentialRebind_GamepadCompositeCompletesEveryPart()
        {
            var action = AddModifiedAction("Reload", "<Gamepad>/leftShoulder", "<Gamepad>/buttonWest");
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var controller = new InputRebindController(new InputBindingService(asset));
            var completed = false;
            controller.RebindCompleted += _ => completed = true;
            try
            {
                Assert.That(controller.StartRebind(action.id, 0), Is.True);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                InputSystem.Update();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
                InputSystem.Update();

                Assert.That(completed, Is.True);
                Assert.That(controller.IsBusy, Is.False);
                Assert.That(action.bindings[1].effectivePath, Is.EqualTo("<Gamepad>/rightShoulder"));
                Assert.That(action.bindings[2].effectivePath, Is.EqualTo("<Gamepad>/buttonEast"));
            }
            finally
            {
                controller.Dispose();
                InputSystem.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void SequentialRebind_EscapeRestoresWholeComposite()
        {
            var action = AddModifiedAction("Reload", "<Keyboard>/leftCtrl", "<Keyboard>/r");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var controller = new InputRebindController(new InputBindingService(asset));
            var canceled = false;
            controller.RebindCanceled += () => canceled = true;
            try
            {
                Assert.That(controller.StartRebind(action.id, 0), Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                InputSystem.Update();
                Assert.That(action.bindings[1].effectivePath, Is.EqualTo("<Keyboard>/leftShift"));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.That(canceled, Is.True);
                Assert.That(controller.IsBusy, Is.False);
                Assert.That(action.bindings[1].overridePath, Is.Null);
                Assert.That(action.bindings[2].overridePath, Is.Null);
                Assert.That(action.bindings[1].effectivePath, Is.EqualTo("<Keyboard>/leftCtrl"));
                Assert.That(action.bindings[2].effectivePath, Is.EqualTo("<Keyboard>/r"));
            }
            finally
            {
                controller.Dispose();
                InputSystem.RemoveDevice(keyboard);
            }
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
