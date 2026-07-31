using UnityEngine;
using UnityEngine.VFX;

namespace Koiusa.SteamMultiRuntime
{
    public sealed class ActorSkillEffectVisual : MonoBehaviour
    {
        private const string AttackVfxPath = "VFX/Skills/SkillAttackBurst";
        private const string DashVfxPath = "VFX/Skills/SkillDashTrail";
        private const string HealVfxPath = "VFX/Skills/SkillHealBurst";

        private float duration;
        private float stopEmissionAt = float.PositiveInfinity;
        private float elapsed;
        private VisualEffect visualEffect;
        private bool emissionStopped;

        internal static void Create(Transform owner, PlayerSkillSlot slot)
        {
            var asset = Resources.Load<VisualEffectAsset>(GetResourcePath(slot));
            if (asset == null)
            {
                Debug.LogWarning($"Skill VFX Graph is missing for {slot}.", owner);
                return;
            }

            var effectObject = new GameObject($"{slot}SkillVFX", typeof(VisualEffect), typeof(ActorSkillEffectVisual));
            effectObject.transform.SetParent(FindPresentationRoot(owner), false);
            var lifetime = effectObject.GetComponent<ActorSkillEffectVisual>();
            lifetime.Configure(slot);
            var effect = effectObject.GetComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            lifetime.visualEffect = effect;
            effect.Play();
        }

        private static Transform FindPresentationRoot(Transform owner)
        {
            for (var i = 0; i < owner.childCount; i++)
            {
                var child = owner.GetChild(i);
                if (child.name == "Presentation") return child;
            }
            return owner;
        }

        private static string GetResourcePath(PlayerSkillSlot slot)
        {
            return slot switch
            {
                PlayerSkillSlot.Attack => AttackVfxPath,
                PlayerSkillSlot.Dash => DashVfxPath,
                PlayerSkillSlot.Heal => HealVfxPath,
                _ => string.Empty
            };
        }

        private void Configure(PlayerSkillSlot slot)
        {
            switch (slot)
            {
                case PlayerSkillSlot.Attack:
                    transform.localPosition = new Vector3(0f, 0.75f, 0.9f);
                    transform.localScale = new Vector3(1.2f, 0.8f, 0.45f);
                    duration = 1.5f;
                    break;
                case PlayerSkillSlot.Dash:
                    transform.localPosition = new Vector3(0f, 0.55f, -0.65f);
                    transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    transform.localScale = new Vector3(0.6f, 0.6f, 1.4f);
                    stopEmissionAt = 0.2f;
                    duration = 0.42f;
                    break;
                default:
                    transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    transform.localScale = Vector3.one;
                    duration = 1.8f;
                    break;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!emissionStopped && elapsed >= stopEmissionAt)
            {
                emissionStopped = true;
                visualEffect?.Stop();
            }
            if (elapsed >= duration) Destroy(gameObject);
        }
    }
}
