using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
    public sealed class MinMaxRangePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var range = (MinMaxRangeAttribute)attribute;
            var value = property.vector2Value;
            var min = value.x;
            var max = value.y;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var labelRect = new Rect(position.x, position.y, position.width, lineHeight);
            EditorGUI.LabelField(labelRect, label);

            var sliderRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, range.Min, range.Max);

            var fieldY = sliderRect.y + lineHeight + spacing;
            var half = (position.width - 4f) * 0.5f;
            var minRect = new Rect(position.x, fieldY, half, lineHeight);
            var maxRect = new Rect(position.x + half + 4f, fieldY, half, lineHeight);

            min = EditorGUI.FloatField(minRect, "Min", min);
            max = EditorGUI.FloatField(maxRect, "Max", max);

            min = Mathf.Clamp(min, range.Min, range.Max);
            max = Mathf.Clamp(max, range.Min, range.Max);
            if (max < min)
            {
                var t = min;
                min = max;
                max = t;
            }

            property.vector2Value = new Vector2(min, max);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            return lineHeight * 3f + spacing * 2f;
        }
    }
}
