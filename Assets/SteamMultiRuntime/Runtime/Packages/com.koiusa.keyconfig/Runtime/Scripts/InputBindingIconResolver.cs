using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [CreateAssetMenu(fileName = "InputBindingIconResolver", menuName = "Koiusa/Keyconfig/Input Binding Icon Resolver", order = 100)]
    public class InputBindingIconResolver : ScriptableObject
    {
        [Serializable]
        public struct CustomIconBinding
        {
            public string deviceType;
            public string controlName;
            public Texture2D icon;
        }

        [SerializeField] private InputActionAssetResolver inputActionAssetResolver;
        [SerializeField] private List<CustomIconBinding> customBindings = new List<CustomIconBinding>();

        private const string KbmIconBasePath = "Icons/KeyboardAndMouse/Light/";
        private const string GamepadIconBasePath = "Icons/SteamGamepad/Light/";

        private static readonly Dictionary<string, string> KeyboardIconMap = new Dictionary<string, string>
        {
            { "a", "T_A_Key_Light" },
            { "b", "T_B_Key_Light" },
            { "c", "T_C_Key_Light" },
            { "d", "T_D_Key_Light" },
            { "e", "T_E_Key_Light" },
            { "f", "T_F_Key_Light" },
            { "g", "T_G_Key_Light" },
            { "h", "T_H_Key_Light" },
            { "i", "T_I_Key_Light" },
            { "j", "T_J_Key_Light" },
            { "k", "T_K_Key_Light" },
            { "l", "T_L_Key_Light" },
            { "m", "T_M_Key_Light" },
            { "n", "T_N_Key_Light" },
            { "o", "T_O_Key_Light" },
            { "p", "T_P_Key_Light" },
            { "q", "T_Q_Key_Light" },
            { "r", "T_R_Key_Light" },
            { "s", "T_S_Key_Light" },
            { "t", "T_T_Key_Light" },
            { "u", "T_U_Key_Light" },
            { "v", "T_V_Key_Light" },
            { "w", "T_W_Key_Light" },
            { "x", "T_X_Key_Light" },
            { "y", "T_Y_Key_Light" },
            { "z", "T_Z_Key_Light" },
            { "1", "T_1_Key_Light" },
            { "2", "T_2_Key_Light" },
            { "3", "T_3_Key_Light" },
            { "4", "T_4_Key_Light" },
            { "5", "T_5_Key_Light" },
            { "6", "T_6_Key_Light" },
            { "7", "T_7_Key_Light" },
            { "8", "T_8_Key_Light" },
            { "9", "T_9_Key_Light" },
            { "0", "T_0_Key_Light" },
            { "space", "T_Space_Key_Light" },
            { "enter", "T_Enter_Key_Light" },
            { "numpadenter", "T_Enter_Key_Light" },
            { "escape", "T_Esc_Key_Light" },
            { "tab", "T_Tab_Key_Light" },
            { "shift", "T_Shift_Key_Light" },
            { "leftshift", "T_Shift_Key_Light" },
            { "rightshift", "T_Shift_Key_Light" },
            { "ctrl", "T_Crtl_Key_Light" },
            { "leftctrl", "T_Crtl_Key_Light" },
            { "rightctrl", "T_Crtl_Key_Light" },
            { "alt", "T_Alt_Key_Light" },
            { "leftalt", "T_Alt_Key_Light" },
            { "rightalt", "T_Alt_Key_Light" },
            { "backspace", "T_BackSpace_Key_Light" },
            { "delete", "T_Del_Key_Light" },
            { "insert", "T_Ins_Key_Light" },
            { "home", "T_Home_Key_Light" },
            { "end", "T_End_Key_Light" },
            { "pageup", "T_PageUp_Key_Light" },
            { "pagedown", "T_PageDown_Key_Light" },
            { "uparrow", "T_Up_Key_Light" },
            { "downarrow", "T_Down_Key_Light" },
            { "leftarrow", "T_Left_Key_Light" },
            { "rightarrow", "T_Right_Key_Light" },
            { "f1", "T_F1_Key_Light" },
            { "f2", "T_F2_Key_Light" },
            { "f3", "T_F3_Key_Light" },
            { "f4", "T_F4_Key_Light" },
            { "f5", "T_F5_Key_Light" },
            { "f6", "T_F6_Key_Light" },
            { "f7", "T_F7_Key_Light" },
            { "f8", "T_F8_Key_Light" },
            { "f9", "T_F9_Key_Light" },
            { "f10", "T_F10_Key_Light" },
            { "f11", "T_F11_Key_Light" },
            { "f12", "T_F12_Key_Light" },
            { "capslock", "T_CapsLock_Key_Light" },
            { "numlock", "T_NumLock_Key_Light" },
            { "printscreen", "T_PrtScrn_Key_Light" },
            { "minus", "T_Minus_Key_Light" },
            { "plus", "T_Plus_Key_Light" },
            { "equals", "T_Plus_Key_Light" },
            { "semicolon", "T_Semicolon_Key_Light" },
            { "quote", "T_Quotation_Key_Light" },
            { "backquote", "T_Tilde_Key_Light" },
            { "tilde", "T_Tilde_Key_Light" },
            { "slash", "T_Slash_Key_Light" },
            { "asterisk", "T_Asterisk_Key_Light" },
            { "leftbracket", "T_Brackets_L_Key_Light" },
            { "rightbracket", "T_Brackets_R_Key_Light" },
        };

        private static readonly Dictionary<string, string> MouseIconMap = new Dictionary<string, string>
        {
            { "leftbutton", "T_Mouse_Left_Key_Light" },
            { "rightbutton", "T_Mouse_Right_Key_Light" },
            { "middlebutton", "T_Mouse_Middle_Key_Light" },
            { "scroll", "T_Mouse_Scroll_Key_Dark_Key_Light" },
            { "delta", "T_Mouse_XY_Key_Light" },
            { "position", "T_Mouse_XY_Key_Light" },
            { "x", "T_Mouse_X_Key_Light" },
            { "y", "T_Mouse_Y_Key_Light" },
        };

        private static readonly Dictionary<string, string> GamepadIconMap = new Dictionary<string, string>
        {
            { "buttonsouth", "T_Steam_A_Light" },
            { "buttoneast", "T_Steam_B_Light" },
            { "buttonwest", "T_Steam_X_Light" },
            { "buttonnorth", "T_Steam_Y_Light" },
            { "leftshoulder", "T_Steam_L1_Light" },
            { "rightshoulder", "T_Steam_R1_Light" },
            { "lefttrigger", "T_Steam_L2_Light" },
            { "righttrigger", "T_Steam_R2_Light" },
            { "leftstickbutton", "T_Steam_Left_Stick_Click_Light" },
            { "rightstickbutton", "T_Steam_Right_Stick_Click_Light" },
            { "leftstick", "T_Steam_L_2D_Light" },
            { "rightstick", "T_Steam_R_2D_Light" },
            { "dpad", "T_Steam_Dpad_Light" },
            { "up", "T_Steam_Dpad_Up_Light" },
            { "down", "T_Steam_Dpad_Down_Light" },
            { "left", "T_Steam_Dpad_Left_Light" },
            { "right", "T_Steam_Dpad_Right_Light" },
            { "start", "T_Steam_Options_Light" },
            { "select", "T_Steam_View_Light" },
            { "guide", "T_Steam_Guide_Light" },
        };

        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public IReadOnlyList<CustomIconBinding> CustomBindings => customBindings;

        public InputActionAssetResolver InputActionAssetResolver
        {
            get => inputActionAssetResolver;
            set => inputActionAssetResolver = value;
        }

        public Texture2D Resolve(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
            {
                return null;
            }

            if (cache.TryGetValue(bindingPath, out var cached))
            {
                return cached;
            }

            var texture = ResolveCustomIcon(bindingPath) ?? LoadIcon(bindingPath);
            cache[bindingPath] = texture;
            return texture;
        }

        public void SetCustomBindingIcon(string deviceType, string controlName, Texture2D icon)
        {
            var normalizedDeviceType = NormalizeToken(deviceType);
            var normalizedControlName = NormalizeToken(controlName);
            if (string.IsNullOrEmpty(normalizedDeviceType) || string.IsNullOrEmpty(normalizedControlName))
            {
                return;
            }

            for (var i = 0; i < customBindings.Count; i++)
            {
                if (!string.Equals(NormalizeToken(customBindings[i].deviceType), normalizedDeviceType, StringComparison.Ordinal) ||
                    !string.Equals(NormalizeToken(customBindings[i].controlName), normalizedControlName, StringComparison.Ordinal))
                {
                    continue;
                }

                var binding = customBindings[i];
                binding.icon = icon;
                customBindings[i] = binding;
                cache.Clear();
                return;
            }

            customBindings.Add(new CustomIconBinding
            {
                deviceType = deviceType,
                controlName = controlName,
                icon = icon
            });
            cache.Clear();
        }

        public InputActionAsset ResolveInputActionAsset()
        {
            return inputActionAssetResolver != null ? inputActionAssetResolver.Resolve() : null;
        }

        private Texture2D ResolveCustomIcon(string bindingPath)
        {
            var deviceType = NormalizeToken(ExtractDeviceType(bindingPath));
            var controlName = NormalizeToken(ExtractControlName(bindingPath));
            if (string.IsNullOrEmpty(deviceType) || string.IsNullOrEmpty(controlName))
            {
                return null;
            }

            for (var i = 0; i < customBindings.Count; i++)
            {
                var binding = customBindings[i];
                if (!string.Equals(NormalizeToken(binding.deviceType), deviceType, StringComparison.Ordinal) ||
                    !string.Equals(NormalizeToken(binding.controlName), controlName, StringComparison.Ordinal))
                {
                    continue;
                }

                return binding.icon;
            }

            return null;
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static Texture2D LoadIcon(string bindingPath)
        {
            var deviceType = ExtractDeviceType(bindingPath);
            var control = ExtractControlName(bindingPath);

            if (string.IsNullOrEmpty(control))
            {
                return null;
            }

            var lowerControl = control.ToLowerInvariant();

            if (IsKeyboard(deviceType))
            {
                if (KeyboardIconMap.TryGetValue(lowerControl, out var kbName))
                {
                    return Resources.Load<Texture2D>(KbmIconBasePath + kbName);
                }

                var kbByName = Resources.Load<Texture2D>(KbmIconBasePath + "T_" + control + "_Key_Light");
                if (kbByName != null)
                {
                    return kbByName;
                }
            }
            else if (IsMouse(deviceType))
            {
                if (MouseIconMap.TryGetValue(lowerControl, out var mouseName))
                {
                    return Resources.Load<Texture2D>(KbmIconBasePath + mouseName);
                }
            }
            else if (IsGamepad(deviceType))
            {
                if (GamepadIconMap.TryGetValue(lowerControl, out var gpName))
                {
                    return Resources.Load<Texture2D>(GamepadIconBasePath + gpName);
                }

                var gpByName = Resources.Load<Texture2D>(GamepadIconBasePath + "T_Steam_" + control + "_Light");
                if (gpByName != null)
                {
                    return gpByName;
                }
            }

            return null;
        }

        private static string ExtractDeviceType(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
            {
                return string.Empty;
            }

            var start = bindingPath.IndexOf('<');
            if (start < 0)
            {
                return string.Empty;
            }

            var end = bindingPath.IndexOf('>', start + 1);
            if (end <= start + 1)
            {
                return string.Empty;
            }

            return bindingPath.Substring(start + 1, end - start - 1);
        }

        private static string ExtractControlName(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
            {
                return string.Empty;
            }

            var slashIndex = bindingPath.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex >= bindingPath.Length - 1)
            {
                return string.Empty;
            }

            return bindingPath.Substring(slashIndex + 1);
        }

        private static bool IsKeyboard(string deviceType)
        {
            if (string.IsNullOrEmpty(deviceType))
            {
                return false;
            }

            return deviceType.ToLowerInvariant().Contains("keyboard");
        }

        private static bool IsMouse(string deviceType)
        {
            if (string.IsNullOrEmpty(deviceType))
            {
                return false;
            }

            return deviceType.ToLowerInvariant().Contains("mouse");
        }

        private static bool IsGamepad(string deviceType)
        {
            if (string.IsNullOrEmpty(deviceType))
            {
                return false;
            }

            var lower = deviceType.ToLowerInvariant();
            return lower.Contains("gamepad") || lower.Contains("steam") || lower.Contains("joystick") || lower.Contains("controller");
        }
    }
}
