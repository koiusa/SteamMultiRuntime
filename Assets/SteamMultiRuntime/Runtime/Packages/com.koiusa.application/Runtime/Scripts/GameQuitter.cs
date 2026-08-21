using System;
using UnityEngine;

namespace Koiusa.App
{
    public sealed class GameQuitter : MonoBehaviour
    {
        private bool quitRequested;

        public static event Action QuitRequested;
        public bool IsQuitRequested => quitRequested;

        public void RequestQuit()
        {
            if (quitRequested)
            {
                return;
            }

            quitRequested = true;
            QuitRequested?.Invoke();
            Application.Quit();
        }
    }
}
