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
            return control != null && control.EvaluateMagnitude() >= threshold;
        }
    }
}
