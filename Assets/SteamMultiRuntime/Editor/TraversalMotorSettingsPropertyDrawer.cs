using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomPropertyDrawer(typeof(TraversalMotorSettings))]
    public class TraversalMotorSettingsPropertyDrawer : PropertyDrawer
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

            // Reset button
            var resetButtonRect = new Rect(position.x, currentY, position.width, lineHeight);
            if (GUI.Button(resetButtonRect, "Reset to Default", EditorStyles.miniButton))
            {
                var defaultSettings = TraversalMotorSettings.CreateDefault();
                ApplySettingsToProperty(property, defaultSettings);
                property.serializedObject.ApplyModifiedProperties();
            }
            currentY += lineHeight;

            // Wall Run
            var wallRunFoldout = GetFoldout(property, "WallRun", true);
            wallRunFoldout = EditorGUI.Foldout(
                new Rect(position.x, currentY, position.width, lineHeight),
                wallRunFoldout, "Wall Run", true);
            SetFoldout(property, "WallRun", wallRunFoldout);
            currentY += lineHeight;

            if (wallRunFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "WallRunSpeed", "Speed while wall running");
                DrawField(ref currentY, position, property, "WallRunAcceleration", "Acceleration on wall");
                DrawField(ref currentY, position, property, "WallRunGravityMultiplier", "Gravity multiplier on wall");
                DrawField(ref currentY, position, property, "WallRunMaxFallSpeed", "Maximum fall speed on wall");
                DrawField(ref currentY, position, property, "WallRunMaxUpwardStartSpeed", "Max upward speed to start wall run");
                EditorGUI.indentLevel--;
            }

            // Wall Jump
            var wallJumpFoldout = GetFoldout(property, "WallJump", true);
            wallJumpFoldout = EditorGUI.Foldout(
                new Rect(position.x, currentY, position.width, lineHeight),
                wallJumpFoldout, "Wall Jump", true);
            SetFoldout(property, "WallJump", wallJumpFoldout);
            currentY += lineHeight;

            if (wallJumpFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "WallJumpUpForce", "Upward force for wall jump");
                DrawField(ref currentY, position, property, "WallJumpAwayForce", "Away from wall force");
                DrawField(ref currentY, position, property, "TriangleKickForwardForce", "Triangle kick forward force");
                DrawField(ref currentY, position, property, "WallJumpTrajectoryMode", "Wall jump trajectory");
                EditorGUI.indentLevel--;
            }

            // Wall Slide
            var wallSlideFoldout = GetFoldout(property, "WallSlide", true);
            wallSlideFoldout = EditorGUI.Foldout(
                new Rect(position.x, currentY, position.width, lineHeight),
                wallSlideFoldout, "Wall Slide", true);
            SetFoldout(property, "WallSlide", wallSlideFoldout);
            currentY += lineHeight;

            if (wallSlideFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "WallSlideGravityMultiplier", "Gravity multiplier while sliding");
                DrawField(ref currentY, position, property, "WallSlideMaxFallSpeed", "Maximum fall speed while sliding");
                DrawField(ref currentY, position, property, "WallSlideMinDownSpeed", "Minimum downward speed to maintain slide");
                DrawField(ref currentY, position, property, "WallSlideExitMoveOppositeNormalDot", "Exit slide when pressing INTO wall (higher = less sensitive)");
                EditorGUI.indentLevel--;
            }

            // Contact Detection
            var contactDetectionFoldout = GetFoldout(property, "ContactDetection", false);
            contactDetectionFoldout = EditorGUI.Foldout(
                new Rect(position.x, currentY, position.width, lineHeight),
                contactDetectionFoldout, "Contact Detection", true);
            SetFoldout(property, "ContactDetection", contactDetectionFoldout);
            currentY += lineHeight;

            if (contactDetectionFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "WallRunStartContactFrames", "Frames to establish wall run contact");
                DrawField(ref currentY, position, property, "WallSlideStartContactFrames", "Frames to establish wall slide contact");
                EditorGUI.indentLevel--;
            }

            // Input
            var inputFoldout = GetFoldout(property, "Input", false);
            inputFoldout = EditorGUI.Foldout(
                new Rect(position.x, currentY, position.width, lineHeight),
                inputFoldout, "Input", true);
            SetFoldout(property, "Input", inputFoldout);
            currentY += lineHeight;

            if (inputFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "WallRunMinInputDot", "Minimum input direction dot to run");
                DrawField(ref currentY, position, property, "WallRunMinAlongWallSpeed", "Minimum speed along wall");
                DrawField(ref currentY, position, property, "WallMaxUpDot", "Maximum upward direction for wall detection");
                EditorGUI.indentLevel--;
            }

            // Other
            var otherFoldout = GetFoldout(property, "Other", false);
            otherFoldout = EditorGUI.Foldout(
                new Rect(position.x, currentY, position.width, lineHeight),
                otherFoldout, "Other", true);
            SetFoldout(property, "Other", otherFoldout);
            currentY += lineHeight;

            if (otherFoldout)
            {
                EditorGUI.indentLevel++;
                DrawField(ref currentY, position, property, "SameWallKickLockDuration", "Lock duration for same wall kicks");
                DrawField(ref currentY, position, property, "SameWallNormalDotThreshold", "Threshold for same wall detection");
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private void ApplySettingsToProperty(SerializedProperty property, TraversalMotorSettings settings)
        {
            property.FindPropertyRelative("WallRunSpeed").floatValue = settings.WallRunSpeed;
            property.FindPropertyRelative("WallRunAcceleration").floatValue = settings.WallRunAcceleration;
            property.FindPropertyRelative("WallRunGravityMultiplier").floatValue = settings.WallRunGravityMultiplier;
            property.FindPropertyRelative("WallRunMaxFallSpeed").floatValue = settings.WallRunMaxFallSpeed;
            property.FindPropertyRelative("WallRunMinInputDot").floatValue = settings.WallRunMinInputDot;
            property.FindPropertyRelative("WallRunMinAlongWallSpeed").floatValue = settings.WallRunMinAlongWallSpeed;
            property.FindPropertyRelative("WallRunMaxUpwardStartSpeed").floatValue = settings.WallRunMaxUpwardStartSpeed;
            property.FindPropertyRelative("WallMaxUpDot").floatValue = settings.WallMaxUpDot;
            property.FindPropertyRelative("WallJumpUpForce").floatValue = settings.WallJumpUpForce;
            property.FindPropertyRelative("WallJumpAwayForce").floatValue = settings.WallJumpAwayForce;
            property.FindPropertyRelative("TriangleKickForwardForce").floatValue = settings.TriangleKickForwardForce;
            property.FindPropertyRelative("WallRunStartContactFrames").intValue = settings.WallRunStartContactFrames;
            property.FindPropertyRelative("WallSlideGravityMultiplier").floatValue = settings.WallSlideGravityMultiplier;
            property.FindPropertyRelative("WallSlideMaxFallSpeed").floatValue = settings.WallSlideMaxFallSpeed;
            property.FindPropertyRelative("WallJumpTrajectoryMode").enumValueIndex = (int)settings.WallJumpTrajectoryMode;
            property.FindPropertyRelative("SameWallKickLockDuration").floatValue = settings.SameWallKickLockDuration;
            property.FindPropertyRelative("SameWallNormalDotThreshold").floatValue = settings.SameWallNormalDotThreshold;
            property.FindPropertyRelative("WallSlideMinDownSpeed").floatValue = settings.WallSlideMinDownSpeed;
            property.FindPropertyRelative("WallSlideStartContactFrames").intValue = settings.WallSlideStartContactFrames;
            property.FindPropertyRelative("WallSlideExitMoveOppositeNormalDot").floatValue = settings.WallSlideExitMoveOppositeNormalDot;
        }

        private void DrawField(ref float currentY, Rect position, SerializedProperty property, string fieldName, string tooltip)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var field = property.FindPropertyRelative(fieldName);
            if (field != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, currentY, position.width, lineHeight),
                    field,
                    new GUIContent(field.displayName, tooltip));
            }
            currentY += lineHeight;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var height = lineHeight * 7; // Foldout headers + reset button

            var wallRunFoldout = GetFoldout(property, "WallRun", true);
            var wallJumpFoldout = GetFoldout(property, "WallJump", true);
            var wallSlideFoldout = GetFoldout(property, "WallSlide", true);
            var contactDetectionFoldout = GetFoldout(property, "ContactDetection", false);
            var inputFoldout = GetFoldout(property, "Input", false);
            var otherFoldout = GetFoldout(property, "Other", false);

            if (wallRunFoldout) height += lineHeight * 5;
            if (wallJumpFoldout) height += lineHeight * 4;
            if (wallSlideFoldout) height += lineHeight * 4;
            if (contactDetectionFoldout) height += lineHeight * 2;
            if (inputFoldout) height += lineHeight * 3;
            if (otherFoldout) height += lineHeight * 2;

            return height;
        }
    }
}
