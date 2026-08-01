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

            NpcCrowdSpringSimulation.RegisterModel(root);
            ConfigureCosmeticBehaviours(root.GetComponentsInChildren<MonoBehaviour>(true));
        }

        private static void ConfigureCosmeticBehaviours(MonoBehaviour[] behaviours)
        {
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                var typeName = behaviour.GetType().FullName;
                if (typeName == "UnityChan.SDRandomWind"
                    || typeName == "UTJ.HighLeg"
                    || typeName == "UnityChan.AutoBlinkforSD"
                    || typeName == "Koiusa.SteamMultiRuntime.UnityChan.FaceAnimationDriver")
                    behaviour.enabled = false;
            }
        }
    }
}
