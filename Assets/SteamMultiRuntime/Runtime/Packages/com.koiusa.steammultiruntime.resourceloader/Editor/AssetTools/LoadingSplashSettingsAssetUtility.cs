using Koiusa.SteamMultiRuntime;
using UnityEditor;
using UnityEngine;

public static class LoadingSplashSettingsAssetUtility
{
    private const string DefaultAssetPath = "Assets/SteamMultiRuntime/Runtime/Resources/UI/LoadingSplash/LoadingSplashSettings.asset";

    [MenuItem("Tools/SteamMultiRuntime/Create Loading Splash Settings Asset")]
    public static void CreateOrSelectAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<LoadingSplashSettings>(DefaultAssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var asset = ScriptableObject.CreateInstance<LoadingSplashSettings>();
        AssetDatabase.CreateAsset(asset, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
