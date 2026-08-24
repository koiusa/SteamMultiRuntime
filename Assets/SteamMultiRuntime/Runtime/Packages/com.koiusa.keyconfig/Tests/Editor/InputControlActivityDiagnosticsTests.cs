using System.Reflection;
using NUnit.Framework;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class InputControlActivityDiagnosticsTests
    {
        [Test]
        public void DeviceDiagnosticsMatchExplicitScriptingDefine()
        {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
            var initializer = typeof(InputControlActivity).GetMethod("InitializeDeviceDiagnostics", Flags);
            var callback = typeof(InputControlActivity).GetMethod("OnDeviceChange", Flags);

#if KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS
            Assert.That(initializer, Is.Not.Null);
            Assert.That(callback, Is.Not.Null);
#else
            Assert.That(initializer, Is.Null,
                "Default builds must not contain a runtime initializer that subscribes to InputSystem.onDeviceChange.");
            Assert.That(callback, Is.Null,
                "Default builds must not contain the device diagnostics callback.");
#endif
        }
    }
}
