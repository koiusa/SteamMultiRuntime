using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal abstract class TraversalSettingsPropertyDrawer : PropertyDrawer
    {
        protected abstract string[] VisibleProperties { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var lineStep = lineHeight + EditorGUIUtility.standardVerticalSpacing;
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, lineHeight),
                property.isExpanded,
                label,
                true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                var y = position.y + lineStep;
                foreach (var propertyName in VisibleProperties)
                {
                    var child = property.FindPropertyRelative(propertyName);
                    if (child != null)
                    {
                        EditorGUI.PropertyField(
                            new Rect(position.x, y, position.width, lineHeight),
                            child,
                            includeChildren: true);
                    }
                    y += lineStep;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineCount = property.isExpanded ? VisibleProperties.Length + 1 : 1;
            return lineCount * EditorGUIUtility.singleLineHeight
                + (lineCount - 1) * EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(WallRunTraversalSettings))]
    internal sealed class WallRunTraversalSettingsPropertyDrawer : TraversalSettingsPropertyDrawer
    {
        private static readonly string[] Properties =
        {
            "WallRunSpeed",
            "WallRunAcceleration",
            "EnterMinimumAlongWallSpeed",
            "MaintainMinimumAlongWallSpeed",
            "EnterMinimumAlongWallRatio",
            "MaintainMinimumAlongWallRatio",
            "VerticalMotionMode",
            "WallRunGravityMultiplier",
            "WallRunMaxFallSpeed",
            "HeightHoldAcceleration",
            "ArcInitialUpSpeed",
            "ArcGravityMultiplier",
        };

        protected override string[] VisibleProperties => Properties;
    }

    [CustomPropertyDrawer(typeof(WallJumpTraversalSettings))]
    internal sealed class WallJumpTraversalSettingsPropertyDrawer : TraversalSettingsPropertyDrawer
    {
        private static readonly string[] Properties =
        {
            "WallJumpTrajectoryMode",
            "WallJumpUpForce",
            "WallJumpAwayForce",
            "TriangleKickForwardForce",
        };

        protected override string[] VisibleProperties => Properties;
    }

    [CustomPropertyDrawer(typeof(WallSlideTraversalSettings))]
    internal sealed class WallSlideTraversalSettingsPropertyDrawer : TraversalSettingsPropertyDrawer
    {
        private static readonly string[] Properties =
        {
            "WallSlideGravityMultiplier",
            "WallSlideMaxFallSpeed",
            "WallSlideMinDownSpeed",
            "AllowWallSlideLateralMovement",
        };

        protected override string[] VisibleProperties => Properties;
    }

    [CustomPropertyDrawer(typeof(LadderTraversalSettings))]
    internal sealed class LadderTraversalSettingsPropertyDrawer : TraversalSettingsPropertyDrawer
    {
        private static readonly string[] Properties =
        {
            "ClimbSpeed",
            "ClimbAcceleration",
            "ExitTopBoostSpeed",
            "FacingRotationSpeed",
            "LateralMoveSpeed",
            "LateralMoveAcceleration",
        };

        protected override string[] VisibleProperties => Properties;
    }
}
