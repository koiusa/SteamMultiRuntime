using System;
using System.IO;
using UnityEngine;

namespace Koiusa.Keyconfig
{
    public sealed class InputBindingOverridesRepository
    {
        private const string DirectoryName = "InputBindings";
        private const string FileExtension = ".json";

        public bool TryLoad(string userId, out string overridesJson)
        {
            overridesJson = string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var filePath = BuildFilePath(userId);
            if (!File.Exists(filePath))
            {
                return false;
            }

            overridesJson = File.ReadAllText(filePath);
            return !string.IsNullOrWhiteSpace(overridesJson);
        }

        public void Save(string userId, string overridesJson)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            var filePath = BuildFilePath(userId);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, overridesJson ?? string.Empty);
        }

        public void Delete(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var filePath = BuildFilePath(userId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static string BuildFilePath(string userId)
        {
            var safeUserId = SanitizeFileName(userId);
            var directoryPath = Path.Combine(Application.persistentDataPath, DirectoryName);
            return Path.Combine(directoryPath, $"{safeUserId}{FileExtension}");
        }

        private static string SanitizeFileName(string raw)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = raw.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                for (var j = 0; j < invalidChars.Length; j++)
                {
                    if (chars[i] == invalidChars[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }
    }
}
