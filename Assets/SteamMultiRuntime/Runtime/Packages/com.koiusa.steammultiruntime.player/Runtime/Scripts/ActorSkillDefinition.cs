using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(
        fileName = "ActorSkillDefinition",
        menuName = "SteamMultiRuntime/Player/Skill Definition")]
    public sealed class ActorSkillDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        private void OnValidate()
        {
            id = id?.Trim();
        }
    }
}
