using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(WireSwingTraversalFeature))]
    [CanEditMultipleObjects]
    public sealed class WireSwingTraversalFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty aimTransform;
        private SerializedProperty wireOrigin;
        private SerializedProperty maximumRange;
        private SerializedProperty grappleLayers;
        private SerializedProperty triggerInteraction;
        private SerializedProperty minimumRopeLength;
        private SerializedProperty ropeSlack;
        private SerializedProperty pullAcceleration;
        private SerializedProperty swingAcceleration;
        private SerializedProperty reelSpeed;
        private SerializedProperty releaseBoost;
        private SerializedProperty radialVelocityDamping;
        private SerializedProperty lineRenderer;
        private SerializedProperty wireMaterial;
        private SerializedProperty wireWidth;
        private SerializedProperty wireColor;

        private void OnEnable()
        {
            aimTransform = serializedObject.FindProperty("aimTransform");
            wireOrigin = serializedObject.FindProperty("wireOrigin");
            maximumRange = serializedObject.FindProperty("maximumRange");
            grappleLayers = serializedObject.FindProperty("grappleLayers");
            triggerInteraction = serializedObject.FindProperty("triggerInteraction");
            minimumRopeLength = serializedObject.FindProperty("minimumRopeLength");
            ropeSlack = serializedObject.FindProperty("ropeSlack");
            pullAcceleration = serializedObject.FindProperty("pullAcceleration");
            swingAcceleration = serializedObject.FindProperty("swingAcceleration");
            reelSpeed = serializedObject.FindProperty("reelSpeed");
            releaseBoost = serializedObject.FindProperty("releaseBoost");
            radialVelocityDamping = serializedObject.FindProperty("radialVelocityDamping");
            lineRenderer = serializedObject.FindProperty("lineRenderer");
            wireMaterial = serializedObject.FindProperty("wireMaterial");
            wireWidth = serializedObject.FindProperty("wireWidth");
            wireColor = serializedObject.FindProperty("wireColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "PlayerMotorと連携するRigidbodyベースのワイヤースイングです。\n" +
                "Grappleを押している間はカメラ正面へ射出し、Jumpで加速離脱します。",
                MessageType.Info);

            DrawAimSection();
            DrawSwingSection();
            DrawRenderingSection();
            DrawValidation();
            DrawRuntimeState();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAimSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Aiming", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(aimTransform);
            EditorGUILayout.PropertyField(wireOrigin);
            EditorGUILayout.PropertyField(maximumRange);
            EditorGUILayout.PropertyField(grappleLayers);
            EditorGUILayout.PropertyField(triggerInteraction);

            if (!serializedObject.isEditingMultipleObjects
                && aimTransform.objectReferenceValue == null
                && Camera.main != null
                && GUILayout.Button("Assign Main Camera As Aim Transform"))
            {
                aimTransform.objectReferenceValue = Camera.main.transform;
            }
        }

        private void DrawSwingSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Swing Physics", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(minimumRopeLength);
            EditorGUILayout.PropertyField(ropeSlack);
            EditorGUILayout.PropertyField(pullAcceleration);
            EditorGUILayout.PropertyField(swingAcceleration);
            EditorGUILayout.PropertyField(reelSpeed);
            EditorGUILayout.PropertyField(releaseBoost);
            EditorGUILayout.PropertyField(radialVelocityDamping);
        }

        private void DrawRenderingSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(lineRenderer);
            EditorGUILayout.PropertyField(wireMaterial, new GUIContent("Wire Material", "省略時はURP/HDRP/Built-inを判定して対応するUnlit Materialを共有生成します。"));
            EditorGUILayout.PropertyField(wireWidth);
            EditorGUILayout.PropertyField(wireColor);

            if (!serializedObject.isEditingMultipleObjects && lineRenderer.objectReferenceValue == null)
            {
                var feature = (WireSwingTraversalFeature)target;
                var existing = feature.GetComponent<LineRenderer>();
                var label = existing != null ? "Use Attached LineRenderer" : "Add LineRenderer";
                if (GUILayout.Button(label))
                {
                    var renderer = existing != null ? existing : Undo.AddComponent<LineRenderer>(feature.gameObject);
                    lineRenderer.objectReferenceValue = renderer;
                    EditorUtility.SetDirty(feature);
                    MarkSceneDirty(feature.gameObject);
                }
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.Space();
            if (aimTransform.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Aim Transform未設定時はMain Camera、見つからない場合はプレイヤー正面を使用します。", MessageType.Info);
            }

            var feature = (WireSwingTraversalFeature)target;
            if (feature.GetComponent<PlayerMotor>() == null)
            {
                EditorGUILayout.HelpBox("PlayerMotorが同じGameObjectにありません。単独動作は可能ですがMotor連携は無効です。", MessageType.Warning);
            }

            if (grappleLayers.intValue == 0)
            {
                EditorGUILayout.HelpBox("Grapple Layersが空のため、接続可能なオブジェクトがありません。", MessageType.Error);
            }

            if (wireMaterial.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Wire Material未設定時は現在のRender Pipelineに対応するUnlit Shaderを自動選択します。ビルドでShader Strippingする場合はMaterialの明示指定を推奨します。",
                    MessageType.Info);
            }
        }

        private void DrawRuntimeState()
        {
            if (!Application.isPlaying || serializedObject.isEditingMultipleObjects)
            {
                return;
            }

            var feature = (WireSwingTraversalFeature)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Attached", feature.IsAttached);
                EditorGUILayout.Vector3Field("Anchor Point", feature.AnchorPoint);
                EditorGUILayout.FloatField("Rope Length", feature.RopeLength);
            }

            if (feature.IsAttached && GUILayout.Button("Detach"))
            {
                feature.Detach();
            }
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
                Handles.Label(feature.AnchorPoint, $"Wire {feature.RopeLength:F1}m");
            }
        }

        [MenuItem("Tools/SteamMultiRuntime/Setup Wire Swing On Selected Player", priority = 120)]
        private static void SetupSelectedPlayer()
        {
            var gameObject = Selection.activeGameObject;
            var feature = gameObject.GetComponent<WireSwingTraversalFeature>();
            if (feature == null)
            {
                feature = Undo.AddComponent<WireSwingTraversalFeature>(gameObject);
            }

            Selection.activeObject = feature;
            EditorGUIUtility.PingObject(feature);
            MarkSceneDirty(gameObject);
        }

        [MenuItem("Tools/SteamMultiRuntime/Setup Wire Swing On Selected Player", true)]
        private static bool ValidateSetupSelectedPlayer()
        {
            return Selection.activeGameObject != null
                && Selection.activeGameObject.GetComponent<PlayerMotor>() != null;
        }

        private static void MarkSceneDirty(GameObject gameObject)
        {
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }
}
