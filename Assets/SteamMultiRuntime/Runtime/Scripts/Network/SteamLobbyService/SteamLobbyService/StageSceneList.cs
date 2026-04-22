using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    [CreateAssetMenu(fileName = "StageSceneList", menuName = "SteamMultiRuntime/Stage Scene List", order = 101)]
    public class StageSceneList : ScriptableObject
    {
        public string[] sceneNames;
    }
}
