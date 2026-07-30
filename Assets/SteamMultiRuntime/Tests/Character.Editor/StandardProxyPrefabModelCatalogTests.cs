using Koiusa.SteamMultiRuntime.Character;
using Koiusa.SteamMultiRuntime.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Tests
{
    public sealed class StandardProxyPrefabModelCatalogTests
    {
        private const string ModelCatalogGuid = "53fb10e1957573c44be834f0809a3752";

        private static readonly string[] LocalProxyPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalPlayer.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalNPC.prefab"
        };

        private static readonly string[] NetworkProxyPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkNPC.prefab"
        };

        [Test]
        public void LocalProxyPrefabs_ReferenceStandardCharacterModelCatalog()
        {
            var expectedCatalog = LoadExpectedCatalog();

            foreach (var prefabPath in LocalProxyPaths)
            {
                var prefab = LoadPrefab(prefabPath);
                var modelSync = prefab.GetComponentInChildren<LocalPlayerModelSync>(true);

                Assert.That(modelSync, Is.Not.Null, $"LocalPlayerModelSync is missing: {prefabPath}");
                Assert.That(modelSync.ModelIdList, Is.SameAs(expectedCatalog),
                    $"CharacterModelIdList is not assigned: {prefabPath}");
            }
        }

        [Test]
        public void NetworkProxyPrefabs_ReferenceStandardCharacterModelCatalog()
        {
            var expectedCatalog = LoadExpectedCatalog();

            foreach (var prefabPath in NetworkProxyPaths)
            {
                var prefab = LoadPrefab(prefabPath);
                var modelSync = prefab.GetComponentInChildren<NetworkPlayerModelSync>(true);

                Assert.That(modelSync, Is.Not.Null, $"NetworkPlayerModelSync is missing: {prefabPath}");
                Assert.That(modelSync.ModelIdList, Is.SameAs(expectedCatalog),
                    $"CharacterModelIdList is not assigned: {prefabPath}");
            }
        }

        private static CharacterModelIdList LoadExpectedCatalog()
        {
            var catalogPath = AssetDatabase.GUIDToAssetPath(ModelCatalogGuid);
            Assert.That(catalogPath, Is.Not.Empty, "Standard CharacterModelIdList GUID could not be resolved.");
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterModelIdList>(catalogPath);
            Assert.That(catalog, Is.Not.Null, $"CharacterModelIdList could not be loaded: {catalogPath}");
            return catalog;
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Standard proxy prefab could not be loaded: {prefabPath}");
            return prefab;
        }
    }
}
