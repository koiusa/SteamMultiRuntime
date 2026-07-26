using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal abstract class NpcNavMeshModuleEditorBase : UnityEditor.Editor
    {
        protected static bool IsEnabled(SerializedProperty property) =>
            property.hasMultipleDifferentValues || property.boolValue;

        protected static void Draw(SerializedProperty parent, string name)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name));
        }

        protected void DrawInspector(System.Action drawProperties)
        {
            serializedObject.Update();
            drawProperties();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(NpcNavMeshAvoidanceModule))]
    [CanEditMultipleObjects]
    internal sealed class NpcNavMeshAvoidanceModuleEditor : NpcNavMeshModuleEditorBase
    {
        public override void OnInspectorGUI()
        {
            DrawInspector(() =>
            {
                var mode = serializedObject.FindProperty("mode");
                EditorGUILayout.PropertyField(mode);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("updateInterval"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("holdLastValueBetweenUpdates"));

                var showBothModes = mode.hasMultipleDifferentValues;
                var selectedMode = (NpcNavMeshAvoidanceModule.AvoidanceMode)mode.enumValueIndex;
                if (showBothModes || selectedMode == NpcNavMeshAvoidanceModule.AvoidanceMode.Boid)
                    DrawBoidSettings();
                if (showBothModes || selectedMode == NpcNavMeshAvoidanceModule.AvoidanceMode.Rvo)
                    DrawRvoSettings();
            });
        }

        private void DrawBoidSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Boid Separation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boidSeparationRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boidGoalWeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boidSeparationWeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boidSeparationExponent"));
            var useFilter = serializedObject.FindProperty("boidUseForwardNeighborFilter");
            EditorGUILayout.PropertyField(useFilter);
            if (IsEnabled(useFilter))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("boidNeighborForwardDotMin"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boidMaxNeighbors"));
        }

        private void DrawRvoSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RVO-style Local Avoidance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoNeighborRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoTimeHorizon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoGoalWeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoAvoidanceWeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoMinApproachSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoMaxNeighbors"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoSideBias"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoSideSwitchThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoSideHoldTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rvoPrimaryNeighborCount"));
        }
    }

    [CustomEditor(typeof(NpcNavMeshMovementModule))]
    [CanEditMultipleObjects]
    internal sealed class NpcNavMeshMovementModuleEditor : NpcNavMeshModuleEditorBase
    {
        public override void OnInspectorGUI()
        {
            DrawInspector(() =>
            {
                var path = serializedObject.FindProperty("path");
                var randomMoveEnabled = path.FindPropertyRelative("randomMoveEnabled");
                EditorGUILayout.PropertyField(randomMoveEnabled);
                if (!IsEnabled(randomMoveEnabled))
                    return;

                DrawPathSettings(path);
                DrawStuckSettings(serializedObject.FindProperty("stuck"));
                DrawReturnSettings(serializedObject.FindProperty("returnToCenter"));
            });
        }

        private static void DrawPathSettings(SerializedProperty path)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
            Draw(path, "navMeshSearchRadius");
            Draw(path, "radius");
            Draw(path, "minDistance");
            Draw(path, "initialDelayMax");
            Draw(path, "reachedBuffer");
            Draw(path, "repathCooldown");
            Draw(path, "noPathRetryCooldown");
            Draw(path, "maxAttempts");
            Draw(path, "maxConsecutiveFailures");
            Draw(path, "centerBiasWeight");

            var useWait = path.FindPropertyRelative("useWaitBeforeNextDestination");
            EditorGUILayout.PropertyField(useWait);
            if (!IsEnabled(useWait))
                return;
            Draw(path, "waitChance");
            Draw(path, "waitDurationMin");
            Draw(path, "waitDurationMax");
        }

        private static void DrawStuckSettings(SerializedProperty stuck)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stuck Recovery", EditorStyles.boldLabel);
            var repathWhenStuck = stuck.FindPropertyRelative("repathWhenStuck");
            EditorGUILayout.PropertyField(repathWhenStuck);
            if (!IsEnabled(repathWhenStuck))
                return;
            Draw(stuck, "speedThreshold");
            Draw(stuck, "timeout");
            Draw(stuck, "remainingDistanceEpsilon");
            Draw(stuck, "noProgressTimeout");
            Draw(stuck, "movementEpsilon");
            Draw(stuck, "noMovementTimeout");
            Draw(stuck, "minDesiredSpeedForMovementCheck");
        }

        private static void DrawReturnSettings(SerializedProperty settings)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Return To Center", EditorStyles.boldLabel);
            Draw(settings, "maxDistance");
            Draw(settings, "exitRatio");
            Draw(settings, "targetRadius");
        }
    }

    [CustomEditor(typeof(NpcNavMeshSpeedModule))]
    [CanEditMultipleObjects]
    internal sealed class NpcNavMeshSpeedModuleEditor : NpcNavMeshModuleEditorBase
    {
        public override void OnInspectorGUI()
        {
            DrawInspector(() =>
            {
                var scale = serializedObject.FindProperty("scale");
                EditorGUILayout.PropertyField(scale.FindPropertyRelative("range"));

                var returnToCenter = serializedObject.FindProperty("returnToCenter");
                var useBoost = returnToCenter.FindPropertyRelative("useBoost");
                EditorGUILayout.PropertyField(useBoost);
                if (IsEnabled(useBoost))
                    EditorGUILayout.PropertyField(returnToCenter.FindPropertyRelative("scale"));
            });
        }
    }

    [CustomEditor(typeof(NpcNavMeshJumpModule))]
    [CanEditMultipleObjects]
    internal sealed class NpcNavMeshJumpModuleEditor : NpcNavMeshModuleEditorBase
    {
        public override void OnInspectorGUI()
        {
            DrawInspector(() =>
            {
                var enabled = serializedObject.FindProperty("randomJumpEnabled");
                EditorGUILayout.PropertyField(enabled);
                if (!IsEnabled(enabled))
                    return;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpChancePerSecond"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpCooldownMin"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpCooldownMax"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minHorizontalSpeedToJump"));
            });
        }
    }
}
