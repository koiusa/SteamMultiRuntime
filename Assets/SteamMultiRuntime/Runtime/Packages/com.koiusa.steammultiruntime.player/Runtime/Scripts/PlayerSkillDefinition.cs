using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CreateAssetMenu(
        fileName = "PlayerSkillDefinition",
        menuName = "SteamMultiRuntime/Player/Skill Definition")]
    public sealed class PlayerSkillDefinition : ScriptableObject
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
