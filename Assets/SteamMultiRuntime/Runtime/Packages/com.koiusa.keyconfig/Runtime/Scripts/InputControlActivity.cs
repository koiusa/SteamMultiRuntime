using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig
{
    /// <summary>Shared input activity evaluation for UI visualizers.</summary>
    internal static class InputControlActivity
    {
        public const float DefaultActuationThreshold = 0.15f;

#if KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeDeviceDiagnostics()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Gamepad && device is not Joystick)
            {
                return;
            }

            Debug.Log(
                $"[InputDevice] change={change}, name={device.displayName}, layout={device.layout}, " +
                $"id={device.deviceId}, added={device.added}, enabled={device.enabled}");
        }
#endif

        public static InputControl Resolve(string bindingPath)
        {
            return string.IsNullOrWhiteSpace(bindingPath)
                ? null
                : InputSystem.FindControl(bindingPath);
        }

        public static bool IsActive(InputControl control, float threshold = DefaultActuationThreshold)
        {
            if (!IsUsable(control) || IsAbsolutePointerPosition(control))
            {
                return false;
            }

            try
            {
                return control.EvaluateMagnitude() >= threshold;
            }
            catch (InvalidOperationException)
            {
                // The device can be removed between the added check and the state read.
                return false;
            }
        }

        public static float EvaluateMagnitude(InputControl control)
        {
            if (!IsUsable(control))
            {
                return 0f;
            }

            try
            {
                return control.EvaluateMagnitude();
            }
            catch (InvalidOperationException)
            {
                return 0f;
            }
        }

        public static bool IsUsable(InputControl control)
        {
            return control != null && control.device != null && control.device.added;
        }

        public static bool HasConnectedDevice(string bindingPath)
        {
            if (string.IsNullOrWhiteSpace(bindingPath)) return false;
            var requiredLayout = InputControlPath.TryGetDeviceLayout(bindingPath);
            if (string.IsNullOrWhiteSpace(requiredLayout)) return IsUsable(Resolve(bindingPath));

            var devices = InputSystem.devices;
            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (device == null || !device.added) continue;
                if (requiredLayout == "*" || InputSystem.IsFirstLayoutBasedOnSecond(device.layout, requiredLayout))
                    return true;
            }
            return false;
        }

        private static bool IsAbsolutePointerPosition(InputControl control)
        {
            return control?.device is Pointer
                && string.Equals(control.name, "position", StringComparison.OrdinalIgnoreCase);
        }

        public static InputControl FindActive(string bindingPath, InputControl cachedControl = null)
        {
            if (IsActive(cachedControl))
            {
                return cachedControl;
            }

            if (string.IsNullOrWhiteSpace(bindingPath))
            {
                return null;
            }

            var matchingControls = InputSystem.FindControls(bindingPath);
            try
            {
                for (var i = 0; i < matchingControls.Count; i++)
                {
                    var control = matchingControls[i];
                    if (IsActive(control))
                    {
                        return control;
                    }
                }
            }
            finally
            {
                matchingControls.Dispose();
            }

            return null;
        }
    }
}
