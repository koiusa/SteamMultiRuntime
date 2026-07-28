using Koiusa.SteamMultiRuntime.Character;
using UnityEditor;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomEditor(typeof(CharacterModelIdList))]
    public class CharacterModelIdListEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
