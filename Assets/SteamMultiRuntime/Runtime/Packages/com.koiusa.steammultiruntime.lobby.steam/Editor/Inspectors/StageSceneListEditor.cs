using Koiusa.SteamMultiRuntime.Network;
using UnityEditor;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomEditor(typeof(StageSceneList))]
    public class StageSceneListEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
