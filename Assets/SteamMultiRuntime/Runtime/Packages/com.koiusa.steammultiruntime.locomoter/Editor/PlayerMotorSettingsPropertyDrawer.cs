using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomPropertyDrawer(typeof(PlayerMotorSettings))]
    public class PlayerMotorSettingsPropertyDrawer : PropertyDrawer
    {
        private string GetFoldoutKey(SerializedProperty property, string foldoutName)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}_{foldoutName}";
        }

        private bool GetFoldout(SerializedProperty property, string foldoutName, bool defaultValue)
        {
            return EditorPrefs.GetBool(GetFoldoutKey(property, foldoutName), defaultValue);
        }

        private void SetFoldout(SerializedProperty property, string foldoutName, bool value)
        {
            EditorPrefs.SetBool(GetFoldoutKey(property, foldoutName), value);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            var currentY = position.y;
            var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var resetButtonRect = new Rect(position.x, currentY, position.width, lineHeight);
            if (GUI.Button(resetButtonRect, "Reset to Default", EditorStyles.miniButton))
            {
                var defaultSettings = PlayerMotorSettings.CreateDefault();
                ApplySettingsToProperty(property, defaultSettings);
                property.serializedObject.ApplyModifiedProperties();
            }
            currentY += lineHeight;

            var movementFoldout = GetFoldout(property, "Movement", true);
            movementFoldout = EditorGUI.Foldout(new Rect(position.x, currentY, position.width, lineHeight), movementFoldout, "Movement", true);
            SetFoldout(property, "Movement", movementFoldout);
            currentY += lineHeight;

            if (movementFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "MoveSpeed", "Maximum movement speed");
                DrawField(ref currentY, position, property, "GroundAcceleration", "Acceleration on ground");
                DrawField(ref currentY, position, property, "AirAcceleration", "Acceleration in air");
                DrawField(ref currentY, position, property, "RotationSpeed", "Rotation speed (degrees/sec)");
                EditorGUI.indentLevel--;
            }

            var strafeFoldout = GetFoldout(property, "Strafe", false);
            strafeFoldout = EditorGUI.Foldout(new Rect(position.x, currentY, position.width, lineHeight), strafeFoldout, "Strafe Movement", true);
            SetFoldout(property, "Strafe", strafeFoldout);
            currentY += lineHeight;

            if (strafeFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "StrafeMoveSpeedMultiplier", "Speed multiplier while strafing");
                DrawField(ref currentY, position, property, "StrafeAccelerationMultiplier", "Acceleration multiplier while strafing");
                DrawField(ref currentY, position, property, "StrafeRotationSpeed", "Rotation speed while strafing (0 = no rotation)");
                DrawField(ref currentY, position, property, "BackwardSpeedMultiplier", "Speed multiplier when moving backward");
                EditorGUI.indentLevel--;
            }

            var jumpFoldout = GetFoldout(property, "Jump", true);
            jumpFoldout = EditorGUI.Foldout(new Rect(position.x, currentY, position.width, lineHeight), jumpFoldout, "Jump", true);
            SetFoldout(property, "Jump", jumpFoldout);
            currentY += lineHeight;

            if (jumpFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "JumpForce", "Jump force");
                DrawField(ref currentY, position, property, "FallMultiplier", "Gravity multiplier while falling");
                EditorGUI.indentLevel--;
            }

            var groundFoldout = GetFoldout(property, "Ground", false);
            groundFoldout = EditorGUI.Foldout(new Rect(position.x, currentY, position.width, lineHeight), groundFoldout, "Ground", true);
            SetFoldout(property, "Ground", groundFoldout);
            currentY += lineHeight;

            if (groundFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "GroundLayer", "Layers considered as ground");
                DrawField(ref currentY, position, property, "EnableStepAssist", "Enable small step assist");
                DrawField(ref currentY, position, property, "StepAssistMaxHeight", "Maximum step height to climb automatically");
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private void ApplySettingsToProperty(SerializedProperty property, PlayerMotorSettings settings)
        {
            property.FindPropertyRelative("MoveSpeed").floatValue = settings.MoveSpeed;
            property.FindPropertyRelative("GroundAcceleration").floatValue = settings.GroundAcceleration;
            property.FindPropertyRelative("AirAcceleration").floatValue = settings.AirAcceleration;
            property.FindPropertyRelative("RotationSpeed").floatValue = settings.RotationSpeed;
            property.FindPropertyRelative("JumpForce").floatValue = settings.JumpForce;
            property.FindPropertyRelative("FallMultiplier").floatValue = settings.FallMultiplier;
            property.FindPropertyRelative("JumpDetachDuration").floatValue = settings.JumpDetachDuration;
            property.FindPropertyRelative("GroundLayer").intValue = settings.GroundLayer;
            property.FindPropertyRelative("MinGroundNormalDot").floatValue = settings.MinGroundNormalDot;
            property.FindPropertyRelative("GroundedGraceTime").floatValue = settings.GroundedGraceTime;
            property.FindPropertyRelative("NearbyGroundDistance").floatValue = settings.NearbyGroundDistance;
            property.FindPropertyRelative("StrafeMoveSpeedMultiplier").floatValue = settings.StrafeMoveSpeedMultiplier;
            property.FindPropertyRelative("StrafeAccelerationMultiplier").floatValue = settings.StrafeAccelerationMultiplier;
            property.FindPropertyRelative("StrafeRotationSpeed").floatValue = settings.StrafeRotationSpeed;
            property.FindPropertyRelative("BackwardSpeedMultiplier").floatValue = settings.BackwardSpeedMultiplier;
            property.FindPropertyRelative("EnableStepAssist").boolValue = settings.EnableStepAssist;
            property.FindPropertyRelative("StepAssistMaxHeight").floatValue = settings.StepAssistMaxHeight;
            property.FindPropertyRelative("StepAssistCheckDistance").floatValue = settings.StepAssistCheckDistance;
            property.FindPropertyRelative("StepAssistMinMoveSpeed").floatValue = settings.StepAssistMinMoveSpeed;
            property.FindPropertyRelative("StepAssistObstacleUpDot").floatValue = settings.StepAssistObstacleUpDot;
        }

        private void DrawField(ref float currentY, Rect position, SerializedProperty property, string fieldName, string tooltip)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var field = property.FindPropertyRelative(fieldName);
            if (field != null)
            {
                EditorGUI.PropertyField(new Rect(position.x, currentY, position.width, lineHeight), field, new GUIContent(field.displayName, tooltip));
            }
            currentY += lineHeight;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var height = lineHeight * 5;

            var movementFoldout = GetFoldout(property, "Movement", true);
            var strafeFoldout = GetFoldout(property, "Strafe", false);
            var jumpFoldout = GetFoldout(property, "Jump", true);
            var groundFoldout = GetFoldout(property, "Ground", false);

            if (movementFoldout) height += lineHeight * 4;
            if (strafeFoldout) height += lineHeight * 4;
            if (jumpFoldout) height += lineHeight * 2;
            if (groundFoldout) height += lineHeight * 3;

            return height;
        }
    }
}
