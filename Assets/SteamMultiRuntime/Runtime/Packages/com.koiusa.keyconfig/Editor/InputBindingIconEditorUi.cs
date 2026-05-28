using System;
using System.Collections.Generic;

namespace Koiusa.Keyconfig.Editor
{
    internal static class InputBindingIconEditorUi
    {
        public static string[] BuildMapTabs<T>(IReadOnlyList<T> rows, Func<T, string> mapNameSelector)
        {
            var tabs = new List<string> { "All" };
            var mapSet = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                var mapName = mapNameSelector(rows[i]);
                if (string.IsNullOrWhiteSpace(mapName) || !mapSet.Add(mapName))
                {
                    continue;
                }

                tabs.Add(mapName);
            }

            return tabs.ToArray();
        }

        public static string BuildCategory(string deviceType)
        {
            if (string.IsNullOrWhiteSpace(deviceType))
            {
                return "Other";
            }

            var lower = deviceType.Trim().ToLowerInvariant();
            if (lower.Contains("keyboard"))
            {
                return "Keyboard";
            }

            if (lower.Contains("mouse"))
            {
                return "Mouse";
            }

            if (lower.Contains("gamepad") || lower.Contains("controller") || lower.Contains("joystick") || lower.Contains("steam"))
            {
                return "Gamepad";
            }

            return "Other";
        }

        public static string ExtractDeviceType(string bindingPath)
        {
            if (string.IsNullOrWhiteSpace(bindingPath))
            {
                return string.Empty;
            }

            var start = bindingPath.IndexOf('<');
            if (start < 0)
            {
                return string.Empty;
            }

            var end = bindingPath.IndexOf('>', start + 1);
            if (end <= start + 1)
            {
                return string.Empty;
            }

            return bindingPath.Substring(start + 1, end - start - 1);
        }

        public static string ExtractControlName(string bindingPath)
        {
            if (string.IsNullOrWhiteSpace(bindingPath))
            {
                return string.Empty;
            }

            var slashIndex = bindingPath.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex >= bindingPath.Length - 1)
            {
                return string.Empty;
            }

            return bindingPath.Substring(slashIndex + 1);
        }

        public static string BuildKey(string deviceType, string controlName)
        {
            var normalizedDevice = string.IsNullOrWhiteSpace(deviceType) ? string.Empty : deviceType.Trim().ToLowerInvariant();
            var normalizedControl = string.IsNullOrWhiteSpace(controlName) ? string.Empty : controlName.Trim().ToLowerInvariant();
            return normalizedDevice + "/" + normalizedControl;
        }
    }
}
