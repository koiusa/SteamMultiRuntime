using System;
using UnityEngine;

namespace Koiusa.System.Core
{
    [DisallowMultipleComponent]
    public sealed class ApplicationLifecycle : MonoBehaviour
    {
        public bool IsFocused { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsQuitting { get; private set; }

        public event Action<bool> FocusChanged;
        public event Action<bool> PauseChanged;
        public event Action Quitting;

        private void OnEnable()
        {
            IsFocused = Application.isFocused;
            IsPaused = false;
            IsQuitting = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (IsFocused == hasFocus)
            {
                return;
            }

            IsFocused = hasFocus;
            FocusChanged?.Invoke(hasFocus);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (IsPaused == pauseStatus)
            {
                return;
            }

            IsPaused = pauseStatus;
            PauseChanged?.Invoke(pauseStatus);
        }

        private void OnApplicationQuit()
        {
            if (IsQuitting)
            {
                return;
            }

            IsQuitting = true;
            Quitting?.Invoke();
        }
    }
}
