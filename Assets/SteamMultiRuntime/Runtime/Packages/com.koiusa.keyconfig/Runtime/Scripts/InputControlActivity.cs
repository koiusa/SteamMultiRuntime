using System;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    /// <summary>Shared input activity evaluation for UI visualizers.</summary>
    public static class InputControlActivity
    {
        public const float DefaultActuationThreshold = 0.15f;

        public static InputControl Resolve(string bindingPath)
        {
            return string.IsNullOrWhiteSpace(bindingPath)
                ? null
                : InputSystem.FindControl(bindingPath);
        }

        public static bool IsActive(InputControl control, float threshold = DefaultActuationThreshold)
        {
            if (!IsUsable(control))
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
