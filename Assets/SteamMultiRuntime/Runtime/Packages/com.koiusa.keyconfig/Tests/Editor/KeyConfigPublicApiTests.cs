using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class KeyConfigPublicApiTests
    {
        [Test]
        public void ImplementationServicesAreNotPublic()
        {
            Assert.That(typeof(InputBindingService).IsPublic, Is.False);
            Assert.That(typeof(InputRebindController).IsPublic, Is.False);
            Assert.That(typeof(KeyConfigView).IsPublic, Is.False);
        }

        [Test]
        public void PublicSurfaceMatchesApprovedTypes()
        {
            var approved = new HashSet<Type>
            {
                typeof(KeyConfigPanel), typeof(KeyConfigSettings), typeof(IKeyConfigLocalizer),
                typeof(KeyConfigLocalization), typeof(KeyConfigLanguage), typeof(BuiltInKeyConfigLocalizer),
                typeof(KeyConfigBindingId), typeof(KeyConfigBinding), typeof(KeyConfigRebindStatus),
                typeof(KeyConfigConflictResolution), typeof(KeyConfigRebindResult), typeof(KeyConfigConflict),
                typeof(KeyConfigController)
            };
            var actual = typeof(KeyConfigController).Assembly.GetTypes()
                .Where(type => type.IsPublic && !type.IsNested)
                .ToHashSet();
            Assert.That(actual, Is.EquivalentTo(approved));
        }

        [Test]
        public void ControllerAddressesBindingsByGuidAndExportsOverrides()
        {
            var asset = new InputActionAsset();
            var map = new InputActionMap("Player");
            var action = map.AddAction("Jump", InputActionType.Button);
            action.AddBinding("<Keyboard>/space", groups: "Keyboard");
            asset.AddActionMap(map);

            using var controller = new KeyConfigController(asset);
            var binding = controller.GetBindings().Single();

            Assert.That(binding.Id.ActionId, Is.EqualTo(action.id));
            Assert.That(binding.Id.BindingId, Is.EqualTo(action.bindings[0].id));
            Assert.That(controller.Reset(binding.Id), Is.True);
            Assert.That(controller.ExportOverrides(), Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
