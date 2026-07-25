using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(WireSwingTraversalFeature))]
    [CanEditMultipleObjects]
    public sealed class WireSwingTraversalFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty targeting;
        private SerializedProperty visual;
        private SerializedProperty minimumRopeLength;
        private SerializedProperty ropeSlack;
        private SerializedProperty pullAcceleration;
        private SerializedProperty swingAcceleration;
        private SerializedProperty maximumInputSwingSpeed;
        private SerializedProperty reelSpeed;
        private SerializedProperty jumpReelDistance;
        private SerializedProperty radialVelocityDamping;

        private void OnEnable()
        {
            targeting = serializedObject.FindProperty("targeting");
            visual = serializedObject.FindProperty("visual");
            minimumRopeLength = serializedObject.FindProperty("minimumRopeLength");
            ropeSlack = serializedObject.FindProperty("ropeSlack");
            pullAcceleration = serializedObject.FindProperty("pullAcceleration");
            swingAcceleration = serializedObject.FindProperty("swingAcceleration");
            maximumInputSwingSpeed = serializedObject.FindProperty("maximumInputSwingSpeed");
            reelSpeed = serializedObject.FindProperty("reelSpeed");
            jumpReelDistance = serializedObject.FindProperty("jumpReelDistance");
            radialVelocityDamping = serializedObject.FindProperty("radialVelocityDamping");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "スイング物理を担当します。照準・接続判定は Wire Grapple Targeting、始点・描画は Wire Line Visual で設定します。",
                MessageType.Info);

            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targeting);
            EditorGUILayout.PropertyField(visual);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Swing Physics", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(minimumRopeLength);
            EditorGUILayout.PropertyField(ropeSlack);
            EditorGUILayout.PropertyField(pullAcceleration);
            EditorGUILayout.PropertyField(swingAcceleration);
            EditorGUILayout.PropertyField(maximumInputSwingSpeed, new GUIContent("Maximum Input Swing Speed", "移動入力による加速を停止する接線方向速度です。"));
            EditorGUILayout.PropertyField(reelSpeed);
            EditorGUILayout.PropertyField(jumpReelDistance, new GUIContent("Jump Reel Distance", "接続中にJumpを押すたび短くする距離です。"));
            EditorGUILayout.PropertyField(radialVelocityDamping);

            DrawValidation();
            DrawRuntimeState();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawValidation()
        {
            if (targeting.objectReferenceValue == null || visual.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("分割コンポーネントの参照が不足しています。下の修復ボタンで設定できます。", MessageType.Error);
                if (!serializedObject.isEditingMultipleObjects && GUILayout.Button("Add / Assign Wire Components"))
                {
                    var feature = (WireSwingTraversalFeature)target;
                    AssignComponents(feature.gameObject, feature, serializedObject);
                }
            }

            var current = (WireSwingTraversalFeature)target;
            if (current.GetComponent<PlayerMotor>() == null)
            {
                EditorGUILayout.HelpBox("PlayerMotorが同じGameObjectにないためMotor連携は無効です。", MessageType.Warning);
            }
        }

        private void DrawRuntimeState()
        {
            if (!Application.isPlaying || serializedObject.isEditingMultipleObjects) return;
            var feature = (WireSwingTraversalFeature)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Attached", feature.IsAttached);
                EditorGUILayout.Vector3Field("Anchor Point", feature.AnchorPoint);
                EditorGUILayout.FloatField("Rope Length", feature.RopeLength);
            }
            if (feature.IsAttached && GUILayout.Button("Detach")) feature.Detach();
        }

        private void OnSceneGUI()
        {
            var feature = (WireSwingTraversalFeature)target;
            var aim = feature.AimTransform;
            var origin = aim != null ? aim.position : feature.transform.position;
            var forward = aim != null ? aim.forward : feature.transform.forward;
            Handles.color = new Color(0.2f, 0.75f, 1f, 0.65f);
            Handles.DrawWireDisc(origin, forward, feature.MaximumRange);
            Handles.DrawLine(origin, origin + forward * feature.MaximumRange, 2f);
            if (Application.isPlaying && feature.IsAttached)
            {
                Handles.color = Color.cyan;
                Handles.DrawAAPolyLine(4f, feature.transform.position, feature.AnchorPoint);
                Handles.SphereHandleCap(0, feature.AnchorPoint, Quaternion.identity, 0.25f, EventType.Repaint);
            }
        }

        [MenuItem("Tools/SteamMultiRuntime/Setup Wire Swing On Selected Player", priority = 120)]
        private static void SetupSelectedPlayer()
        {
            var gameObject = Selection.activeGameObject;
            var feature = gameObject.GetComponent<WireSwingTraversalFeature>() ?? Undo.AddComponent<WireSwingTraversalFeature>(gameObject);
            AssignComponents(gameObject, feature, new SerializedObject(feature));
            Selection.activeObject = feature;
            EditorGUIUtility.PingObject(feature);
            MarkSceneDirty(gameObject);
        }

        [MenuItem("Tools/SteamMultiRuntime/Setup Wire Swing On Selected Player", true)]
        private static bool ValidateSetupSelectedPlayer() => Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<PlayerMotor>() != null;

        private static void AssignComponents(GameObject gameObject, WireSwingTraversalFeature feature, SerializedObject featureObject)
        {
            var targetingComponent = gameObject.GetComponent<WireGrappleTargetingFeature>() ?? Undo.AddComponent<WireGrappleTargetingFeature>(gameObject);
            var visualComponent = gameObject.GetComponent<WireLineVisualFeature>() ?? Undo.AddComponent<WireLineVisualFeature>(gameObject);
            featureObject.Update();
            featureObject.FindProperty("targeting").objectReferenceValue = targetingComponent;
            featureObject.FindProperty("visual").objectReferenceValue = visualComponent;
            featureObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(feature);
            MarkSceneDirty(gameObject);
        }

        private static void MarkSceneDirty(GameObject gameObject)
        {
            if (!Application.isPlaying && gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    [CustomEditor(typeof(WireGrappleTargetingFeature))]
    public sealed class WireGrappleTargetingFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("マウスポインタ等から渡されたRayに対し、接続先を検索します。", MessageType.Info);
            DrawDefaultInspector();
            var layers = serializedObject.FindProperty("grappleLayers");
            if (layers != null && layers.intValue == 0) EditorGUILayout.HelpBox("Grapple Layersが空です。", MessageType.Error);
        }
    }

    [CustomEditor(typeof(WireLineVisualFeature))]
    public sealed class WireLineVisualFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("ワイヤーの始点とLineRenderer表示だけを担当します。Wire Origin未設定時はRigidbodyの重心を使います。", MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
