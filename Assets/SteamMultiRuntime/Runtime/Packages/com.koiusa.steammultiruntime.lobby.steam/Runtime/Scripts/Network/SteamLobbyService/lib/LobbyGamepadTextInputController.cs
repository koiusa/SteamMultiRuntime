using Steamworks;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class LobbyGamepadTextInputController : System.IDisposable
    {
        private TextField gamepadTextInputField;
        private bool isGamepadTextInputOpen;

        public LobbyGamepadTextInputController(params TextField[] fields)
        {
            foreach (var field in fields)
                Register(field);
        }

        private void Register(TextField field)
        {
            field?.RegisterCallback<FocusInEvent>(_ => OpenGamepadTextInput(field));
        }

        private void OpenGamepadTextInput(TextField field)
        {
            if (field == null || isGamepadTextInputOpen || !SteamClient.IsValid ||
                Gamepad.current == null || !Gamepad.current.wasUpdatedThisFrame)
            {
                return;
            }

            var characterLimit = field.maxLength > 0 ? field.maxLength : 128;
            SteamUtils.OnGamepadTextInputDismissed += OnGamepadTextInputDismissed;
            if (!SteamUtils.ShowGamepadTextInput(
                    GamepadTextInputMode.Normal,
                    GamepadTextInputLineMode.SingleLine,
                    string.IsNullOrWhiteSpace(field.label) ? "文字を入力" : field.label,
                    characterLimit,
                    field.value ?? string.Empty))
            {
                SteamUtils.OnGamepadTextInputDismissed -= OnGamepadTextInputDismissed;
                return;
            }

            gamepadTextInputField = field;
            isGamepadTextInputOpen = true;
        }

        private void OnGamepadTextInputDismissed(bool submitted)
        {
            SteamUtils.OnGamepadTextInputDismissed -= OnGamepadTextInputDismissed;

            var field = gamepadTextInputField;
            gamepadTextInputField = null;
            isGamepadTextInputOpen = false;

            if (submitted && field != null)
            {
                field.value = SteamUtils.GetEnteredGamepadText() ?? string.Empty;
                field.Focus();
            }
        }

        public void Dispose()
        {
            SteamUtils.OnGamepadTextInputDismissed -= OnGamepadTextInputDismissed;
            gamepadTextInputField = null;
            isGamepadTextInputOpen = false;
        }
    }
}


