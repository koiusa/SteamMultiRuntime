using UnityEditor;

namespace Koiusa.App.Editor
{
    [InitializeOnLoad]
    internal static class GameQuitterEditorBridge
    {
        static GameQuitterEditorBridge()
        {
            GameQuitter.QuitRequested -= StopPlayMode;
            GameQuitter.QuitRequested += StopPlayMode;
        }

        private static void StopPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
