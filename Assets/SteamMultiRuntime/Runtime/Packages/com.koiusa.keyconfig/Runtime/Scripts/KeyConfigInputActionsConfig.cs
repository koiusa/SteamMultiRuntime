using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "KeyConfigInputActionsConfig", menuName = "Koiusa/Keyconfig/Input Actions Config", order = 101)]
    public class KeyConfigInputActionsConfig : ScriptableObject
    {
        [Header("Input Actions")]
        [Tooltip("The InputActionAsset used by key configuration and input guides.")]
        [InspectorName("Input Action Asset")]
        [SerializeField] private InputActionAsset inputActionAsset;

        [Header("Rebinding")]
        [Tooltip("Action Maps shown for input monitoring but excluded from rebind and reset operations.")]
        [SerializeField] private string[] nonRebindableActionMaps = Array.Empty<string>();

        [Header("UI Navigation")]
        [SerializeField] private string submitActionPath = "UI/Submit";
        [SerializeField] private string previousSectionActionPath = "UI/PreviousSection";
        [SerializeField] private string nextSectionActionPath = "UI/NextSection";

        public IReadOnlyList<string> NonRebindableActionMaps => nonRebindableActionMaps;
        public string SubmitActionPath => submitActionPath;
        public string PreviousSectionActionPath => previousSectionActionPath;
        public string NextSectionActionPath => nextSectionActionPath;

        public InputActionAsset Resolve()
        {
            return inputActionAsset;
        }
    }
}
