using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Applies NPC-only presentation policy when a character model is loaded.</summary>
    internal static class NpcCrowdModelPresentation
    {
        internal static void Configure(GameObject root)
        {
            if (root == null)
                return;

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
                animators[i].applyRootMotion = false;

            DisableNpcLateUpdateBehaviours(root);
            if (root.GetComponent<NpcCrowdModelLateUpdateGuard>() == null)
                root.AddComponent<NpcCrowdModelLateUpdateGuard>();
        }

        internal static void DisableNpcLateUpdateBehaviours(GameObject root)
        {
            if (root == null)
                return;
            Disable(root.GetComponentsInChildren<UnityChan.SDRandomWind>(true));
            Disable(root.GetComponentsInChildren<UnityChan.AutoBlinkforSD>(true));
            Disable(root.GetComponentsInChildren<UTJ.HighLeg>(true));
        }

        private static void Disable<T>(T[] behaviours) where T : Behaviour
        {
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] != null)
                    behaviours[i].enabled = false;
        }
    }

    internal sealed class NpcCrowdModelLateUpdateGuard : MonoBehaviour
    {
        private void Start()
        {
            NpcCrowdModelPresentation.DisableNpcLateUpdateBehaviours(gameObject);
            Destroy(this);
        }
    }
}
