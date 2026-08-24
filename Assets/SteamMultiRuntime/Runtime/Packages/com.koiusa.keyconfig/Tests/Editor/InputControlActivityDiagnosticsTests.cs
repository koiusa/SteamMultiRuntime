using System.Reflection;
using NUnit.Framework;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class InputControlActivityDiagnosticsTests
    {
        [Test]
        public void DeviceDiagnosticsAreNotPartOfRuntimeAssembly()
        {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
            var initializer = typeof(InputControlActivity).GetMethod("InitializeDeviceDiagnostics", Flags);
            var callback = typeof(InputControlActivity).GetMethod("OnDeviceChange", Flags);

            Assert.That(initializer, Is.Null,
                "Runtime builds must not subscribe diagnostic logging to InputSystem.onDeviceChange.");
            Assert.That(callback, Is.Null,
                "Runtime builds must not contain the device diagnostics callback.");
        }
    }
}
