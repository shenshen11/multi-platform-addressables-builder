using UnityEditor;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    [CustomPropertyDrawer(typeof(MpabPlatformConfig))]
    public sealed class MpabPlatformConfigDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // foldout + PlatformId + DisplayName + BuildByDefault + SwitchMode + AddressablesProfileName
            float height = step * 6;

            var switchMode = (MpabPlatformSwitchMode)property.FindPropertyRelative("SwitchMode").enumValueIndex;
            if (switchMode == MpabPlatformSwitchMode.UnityBuildTarget)
                height += step * 2; // BuildTargetName + BuildTargetGroupName
            else if (switchMode == MpabPlatformSwitchMode.CustomHandler)
                height += step;     // CustomSwitchHandlerTypeName

            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("IncludedLabels"), true);

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineH = EditorGUIUtility.singleLineHeight;
            float step = lineH + EditorGUIUtility.standardVerticalSpacing;
            var rect = new Rect(position.x, position.y, position.width, lineH);

            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                rect.y += step;

                DrawField(ref rect, step, property, "PlatformId");
                DrawField(ref rect, step, property, "DisplayName");
                DrawField(ref rect, step, property, "BuildByDefault");
                DrawField(ref rect, step, property, "SwitchMode");

                var switchMode = (MpabPlatformSwitchMode)property.FindPropertyRelative("SwitchMode").enumValueIndex;
                if (switchMode == MpabPlatformSwitchMode.UnityBuildTarget)
                {
                    DrawField(ref rect, step, property, "BuildTargetName");
                    DrawField(ref rect, step, property, "BuildTargetGroupName");
                }
                else if (switchMode == MpabPlatformSwitchMode.CustomHandler)
                {
                    DrawField(ref rect, step, property, "CustomSwitchHandlerTypeName");
                }

                DrawField(ref rect, step, property, "AddressablesProfileName");

                var includedLabels = property.FindPropertyRelative("IncludedLabels");
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUI.GetPropertyHeight(includedLabels, true)),
                    includedLabels, true);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static void DrawField(ref Rect rect, float step, SerializedProperty parent, string name)
        {
            EditorGUI.PropertyField(rect, parent.FindPropertyRelative(name));
            rect.y += step;
        }
    }
}
