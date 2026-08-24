using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Koiusa.KeyConfig.Editor
{
    internal static class DeviceDiagnosticsSettings
    {
        internal const string DefineSymbol = "KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS";
        internal const string MenuPath = "Tools/KeyConfig/Diagnostics/Device Diagnostics Settings";
        private const string SettingsPath = "Project/Koiusa/Keyconfig";

        [MenuItem(MenuPath)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Keyconfig",
                guiHandler = _ => DrawSettings(),
                keywords = new HashSet<string>(new[]
                {
                    "Keyconfig", "Input System", "Device", "Diagnostics", DefineSymbol
                })
            };
        }

        private static void DrawSettings()
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var enabled = ContainsSymbol(defines, DefineSymbol);

            EditorGUILayout.LabelField("Input Device Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gamepad / Joystick device-change logging is disabled by default. Enabling it adds " +
                $"{DefineSymbol} to the currently selected build target and triggers script recompilation. " +
                "Editor and Development Build settings do not enable diagnostics automatically.",
                MessageType.Info);
            EditorGUILayout.LabelField("Build Target", targetGroup.ToString());

            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.Toggle("Enable Device Diagnostics", enabled);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            PlayerSettings.SetScriptingDefineSymbols(
                namedTarget,
                SetSymbolEnabled(defines, DefineSymbol, enabled));
        }

        internal static bool ContainsSymbol(string defines, string symbol)
        {
            return SplitSymbols(defines).Contains(symbol, StringComparer.Ordinal);
        }

        internal static string SetSymbolEnabled(string defines, string symbol, bool enabled)
        {
            var symbols = SplitSymbols(defines)
                .Where(value => !string.Equals(value, symbol, StringComparison.Ordinal))
                .ToList();

            if (enabled)
            {
                symbols.Add(symbol);
            }

            return string.Join(";", symbols);
        }

        private static IEnumerable<string> SplitSymbols(string defines)
        {
            return (defines ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal);
        }
    }
}
