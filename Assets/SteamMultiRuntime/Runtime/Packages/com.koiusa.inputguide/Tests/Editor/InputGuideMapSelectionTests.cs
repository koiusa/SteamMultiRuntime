using System.Collections.Generic;
using Koiusa.KeyConfig;
using Koiusa.InputGuide;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.InputGuide.Tests
{
    public sealed class InputGuideMapSelectionTests
    {
        private InputActionAsset asset;
        private readonly List<InputActionMap> result = new List<InputActionMap>();

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.AddActionMap("Global").AddAction("Pause");
            asset.AddActionMap("Calibration").AddAction("Calibrate");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(asset);

        [Test]
        public void All_SelectsEveryMapInAssetOrder()
        {
            InputGuideMapSelection.Select(asset, InputGuideMapFilter.All, null, null, result);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].name, Is.EqualTo("Global"));
            Assert.That(result[1].name, Is.EqualTo("Calibration"));
        }

        [Test]
        public void EnabledOnly_UsesDynamicMapState()
        {
            asset.FindActionMap("Calibration").Enable();

            InputGuideMapSelection.Select(asset, InputGuideMapFilter.EnabledOnly, null, null, result);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].name, Is.EqualTo("Calibration"));
        }

        [Test]
        public void Specified_UsesNamesAndFallsBackToLegacyName()
        {
            InputGuideMapSelection.Select(asset, InputGuideMapFilter.Specified,
                new[] { "Calibration", "Global" }, "Ignored", result);
            Assert.That(result.ConvertAll(map => map.name), Is.EqualTo(new[] { "Calibration", "Global" }));

            InputGuideMapSelection.Select(asset, InputGuideMapFilter.Specified,
                new string[0], "Global", result);
            Assert.That(result.ConvertAll(map => map.name), Is.EqualTo(new[] { "Global" }));
        }

        [Test]
        public void EmptyLegacySelection_SelectsAllForCompatibilityMigration()
        {
            InputGuideMapSelection.Select(asset, InputGuideMapFilter.Specified,
                new string[0], string.Empty, result);

            Assert.That(result, Has.Count.EqualTo(2));
        }
    }
}
