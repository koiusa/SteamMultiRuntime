using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    [CreateAssetMenu(fileName = "InputActionAssetProfile", menuName = "Koiusa/Input/Input Action Asset Profile")]
    public sealed class InputActionAssetProfile : ScriptableObject
    {
        [Serializable]
        private struct ActionOverride
        {
            [SerializeField] private string actionPath;
            [SerializeField] private InputActionReference action;

            public string ActionPath => actionPath;
            public InputAction Action => action?.action;
        }

        [Header("Purpose")]
        [TextArea, SerializeField] private string purpose;

        [Header("Input Actions")]
        [Tooltip("Reference any action from the InputActionAsset used by this profile.")]
        [SerializeField] private InputActionReference assetSource;

        [Tooltip("Compatibility overrides for actions that do not exist at the requested path in the main asset.")]
        [SerializeField] private ActionOverride[] actionOverrides;

        public string Purpose => purpose;

        public InputAction FindAction(string actionPath)
        {
            var resolved = InputActionResolver.Resolve(assetSource, actionPath);
            if (resolved != null)
            {
                return resolved;
            }

            if (actionOverrides == null)
            {
                return null;
            }

            foreach (var entry in actionOverrides)
            {
                if (string.Equals(entry.ActionPath, actionPath, StringComparison.Ordinal))
                {
                    return entry.Action;
                }
            }

            return null;
        }
    }
}
