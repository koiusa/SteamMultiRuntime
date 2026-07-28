using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    public sealed class FacepunchTransportAppIdSettingsWindow : EditorWindow
    {
        private string appIdText = string.Empty;
        private string message = string.Empty;
        private MessageType messageType = MessageType.Info;

        [MenuItem("Tools/SteamMultiRuntime/Modify Project Files/Facepunch AppID")]
        private static void Open()
        {
            var window = GetWindow<FacepunchTransportAppIdSettingsWindow>("Facepunch AppID");
            window.minSize = new Vector2(420f, 180f);
            window.RefreshFromFile();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshFromFile();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Local AppID File", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(FacepunchTransportAppIdEditorUtility.GetLocalAppIdFullPath());
            }

            EditorGUILayout.Space();
            appIdText = EditorGUILayout.TextField("Steam AppID", appIdText);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    Save();
                }

                if (GUILayout.Button("Reload"))
                {
                    RefreshFromFile();
                }

                if (GUILayout.Button("Delete"))
                {
                    FacepunchTransportAppIdEditorUtility.DeleteLocalAppId();
                    appIdText = string.Empty;
                    message = "Local AppID file deleted.";
                    messageType = MessageType.Warning;
                }
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(message, messageType);
            }
        }

        private void Save()
        {
            if (!uint.TryParse(appIdText, out var appId) || appId == 0)
            {
                message = "Steam AppID must be a positive integer.";
                messageType = MessageType.Error;
                return;
            }

            FacepunchTransportAppIdEditorUtility.SaveLocalAppId(appId);
            message = "Local AppID file saved.";
            messageType = MessageType.Info;
        }

        private void RefreshFromFile()
        {
            if (FacepunchTransportAppIdEditorUtility.TryReadLocalAppId(out var appId))
            {
                appIdText = appId.ToString();
                message = "Loaded from local AppID file.";
                messageType = MessageType.Info;
                return;
            }

            appIdText = string.Empty;
            message = "Local AppID file not found.";
            messageType = MessageType.Warning;
        }
    }
}
