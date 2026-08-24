using Koiusa.KeyConfig.Editor;
using NUnit.Framework;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class DeviceDiagnosticsSettingsTests
    {
        [Test]
        public void EnablingDiagnosticsPreservesExistingSymbolsWithoutDuplicates()
        {
            var result = DeviceDiagnosticsSettings.SetSymbolEnabled(
                "FEATURE_A; KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS;FEATURE_B",
                DeviceDiagnosticsSettings.DefineSymbol,
                true);

            Assert.That(result, Is.EqualTo(
                "FEATURE_A;FEATURE_B;KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS"));
        }

        [Test]
        public void DisablingDiagnosticsRemovesOnlyDiagnosticsSymbol()
        {
            var result = DeviceDiagnosticsSettings.SetSymbolEnabled(
                "FEATURE_A;KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS;FEATURE_B",
                DeviceDiagnosticsSettings.DefineSymbol,
                false);

            Assert.That(result, Is.EqualTo("FEATURE_A;FEATURE_B"));
            Assert.That(DeviceDiagnosticsSettings.ContainsSymbol(
                result,
                DeviceDiagnosticsSettings.DefineSymbol), Is.False);
        }
    }
}
