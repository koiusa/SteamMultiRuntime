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
        [Tooltip("The complete InputActionAsset used by this profile. Configure actions and bindings in this asset.")]
        [SerializeField] private InputActionAsset inputActionAsset;

        [Tooltip("Optional exceptions only. Add an entry when a requested action path does not exist in the main asset.")]
        [SerializeField] private ActionOverride[] actionOverrides;

        public string Purpose => purpose;

        public InputAction FindAction(string actionPath)
        {
            var resolved = inputActionAsset?.FindAction(actionPath, throwIfNotFound: false);
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
