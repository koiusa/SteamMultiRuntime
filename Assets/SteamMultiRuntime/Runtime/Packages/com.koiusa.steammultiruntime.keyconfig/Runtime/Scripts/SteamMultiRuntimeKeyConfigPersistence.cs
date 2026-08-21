using System;
using System.IO;
using Koiusa.KeyConfig;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Keyconfig
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KeyConfigPanel))]
    public sealed class SteamMultiRuntimeKeyConfigPersistence : MonoBehaviour
    {
        [SerializeField] private string userId = "LocalUser";

        private void Awake()
        {
            GetComponent<KeyConfigPanel>().SetPersistence(Load, Save);
        }

        private string Load()
        {
            var path = BuildPath();
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private void Save(string json)
        {
            var path = BuildPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json ?? string.Empty);
        }

        private string BuildPath()
        {
            var safeId = userId;
            foreach (var invalid in Path.GetInvalidFileNameChars()) safeId = safeId.Replace(invalid, '_');
            return Path.Combine(Application.persistentDataPath, "InputBindings", safeId + ".json");
        }
    }
}
