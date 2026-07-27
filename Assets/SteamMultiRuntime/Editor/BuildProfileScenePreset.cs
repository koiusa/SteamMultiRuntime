using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CreateAssetMenu(
        fileName = "BuildProfileScenePreset",
        menuName = "SteamMultiRuntime/Build Profile Scene Preset")]
    public sealed class BuildProfileScenePreset : ScriptableObject
    {
        [Serializable]
        public sealed class SceneEntry
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private string guid;
            [SerializeField] private string path;

            public bool Enabled => enabled;
            public string Guid => guid;
            public string Path => path;

            public SceneEntry(string guid, string path, bool enabled)
            {
                this.guid = guid;
                this.path = path;
                this.enabled = enabled;
            }
        }

        [SerializeField] private List<SceneEntry> scenes = new List<SceneEntry>();

        public IReadOnlyList<SceneEntry> Scenes => scenes;

        public void SetScenes(IEnumerable<SceneEntry> entries)
        {
            scenes.Clear();
            scenes.AddRange(entries);
        }
    }
}
