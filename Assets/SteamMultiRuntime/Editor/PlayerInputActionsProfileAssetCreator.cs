using System.IO;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class PlayerInputActionsProfileAssetCreator
    {
        [MenuItem("Tools/SteamMultiRuntime/Create Player Input Actions Profile Asset")]
        private static void CreateProfileAsset()
        {
            const string folderPath = "Assets/SteamMultiRuntime/Runtime/Configs/Input";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, "PlayerInputActionsProfile.asset"));
            var asset = ScriptableObject.CreateInstance<PlayerInputActionsProfile>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
