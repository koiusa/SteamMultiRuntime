using System;
using UnityEngine;

namespace Koiusa.System.Core
{
    public sealed class GameQuitter : MonoBehaviour
    {
        public static event Action QuitRequested;

        public void RequestQuit()
        {
            QuitRequested?.Invoke();
            Application.Quit();
        }
    }
}
