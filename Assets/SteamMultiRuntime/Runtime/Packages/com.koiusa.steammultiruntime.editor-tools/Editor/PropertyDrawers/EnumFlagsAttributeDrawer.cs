using System;
using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Editor
{
    [CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
    public sealed class EnumFlagsAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            var enumType = fieldInfo.FieldType;
            var enumValue = (Enum)Enum.ToObject(enumType, property.intValue);
            var newValue = EditorGUI.EnumFlagsField(position, label, enumValue);
            property.intValue = Convert.ToInt32(newValue);
            EditorGUI.EndProperty();
        }
    }
}
