using Koiusa.Input;
using Koiusa.SteamMultiRuntime.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime.Character.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterSelectShortcutController : MonoBehaviour
    {
        [SerializeField] private PlayerModelProfileBase userProfile;
        [SerializeField] private InputActionsConfig inputActionsConfig;

        private InputAction modifierAction;
        private InputActionBinding directionBinding;
        private InputActionLease modifierLease;

        private void Awake()
        {
            if (userProfile == null) userProfile = GetComponent<PlayerModelProfileBase>();
        }

        private void OnEnable()
        {
            modifierAction = inputActionsConfig?.FindAction("Adventure/CharacterSelectModifier");
            var directionAction = inputActionsConfig?.FindAction("Adventure/CharacterSelectDirection");
            modifierLease = InputActionLease.Acquire(modifierAction);
            directionBinding = InputActionBinding.Bind(directionAction, OnDirectionPerformed);
        }

        private void OnDisable()
        {
            directionBinding?.Dispose();
            directionBinding = null;
            modifierLease?.Dispose();
            modifierLease = null;
            modifierAction = null;
        }

        private void OnDirectionPerformed(InputAction.CallbackContext context)
        {
            if (modifierAction == null || !modifierAction.IsPressed() || userProfile == null) return;

            var modelIds = userProfile.ModelIdList?.modelIds;
            if (modelIds == null || modelIds.Length == 0) return;

            var direction = context.ReadValue<float>();
            if (Mathf.Abs(direction) < 0.5f) return;

            var current = Mathf.Clamp(userProfile.SelectedModelIndex, 0, modelIds.Length - 1);
            var offset = direction > 0f ? 1 : -1;
            var next = (current + offset + modelIds.Length) % modelIds.Length;
            userProfile.SetSelectedModel(next);
            userProfile.ApplySelectedModel();
        }
    }
}
