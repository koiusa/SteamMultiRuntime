using Koiusa.SteamMultiRuntime;
using UnityEditor;
using UnityEngine;

public static class SteamLobbyLoadingSplashSettingsAssetUtility
{
    private const string DefaultAssetPath = "Assets/SteamMultiRuntime/Runtime/Resources/UI/LoadingSplash/SteamLobbyLoadingSplashSettings.asset";

    [MenuItem("Tools/SteamMultiRuntime/Create Loading Splash Settings Asset")]
    public static void CreateOrSelectAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SteamLobbyLoadingSplashSettings>(DefaultAssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var asset = ScriptableObject.CreateInstance<SteamLobbyLoadingSplashSettings>();
        AssetDatabase.CreateAsset(asset, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
