using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomPropertyDrawer(typeof(UnitAttribute))]
    public class UnitPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var unitAttribute = attribute as UnitAttribute;
            
            // 単位情報を表示テキストに追加
            string displayLabel = label.text;
            if (!string.IsNullOrEmpty(unitAttribute.Unit))
            {
                displayLabel += $" ({unitAttribute.Unit})";
            }

            // ツールチップに説明を追加
            string tooltip = label.tooltip;
            if (!string.IsNullOrEmpty(unitAttribute.Description))
            {
                tooltip = string.IsNullOrEmpty(tooltip) 
                    ? unitAttribute.Description 
                    : $"{tooltip}\n{unitAttribute.Description}";
            }

            var newLabel = new GUIContent(displayLabel, tooltip);

            // プロパティを表示
            EditorGUI.PropertyField(position, property, newLabel);
        }
    }
}
