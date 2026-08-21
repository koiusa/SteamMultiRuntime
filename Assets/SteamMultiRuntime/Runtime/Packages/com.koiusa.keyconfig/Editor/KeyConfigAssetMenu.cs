using System.IO;
using UnityEditor;
using UnityEngine;
using Koiusa.KeyConfig;
using Koiusa.Input.Icons;

namespace Koiusa.KeyConfig.Editor
{
    public static class KeyConfigAssetMenu
    {
        [MenuItem("Tools/KeyConfig/Create Input Action Asset Resolver")]
        private static void CreateInputActionsConfig()
        {
            CreateAssetInSelectedFolder<KeyConfigSettings>("KeyConfigSettings.asset");
        }

        [MenuItem("Tools/KeyConfig/Create Input Binding Icon Resolver")]
        private static void CreateKeyConfigIconSet()
        {
            CreateAssetInSelectedFolder<KeyConfigIconSet>("KeyConfigIconSet.asset");
        }

        private static void CreateAssetInSelectedFolder<T>(string fileName) where T : ScriptableObject
        {
            var selectedPath = "Assets";
            var activeObjectPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrWhiteSpace(activeObjectPath))
            {
                selectedPath = AssetDatabase.IsValidFolder(activeObjectPath)
                    ? activeObjectPath
                    : Path.GetDirectoryName(activeObjectPath)?.Replace('\\', '/') ?? "Assets";
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(selectedPath, fileName).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
