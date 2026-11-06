using Soup.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [CustomPropertyDrawer(typeof(AGraphProperties), true)]
    public class GraphPropertiesPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Unbound: the PropertyField hosting this drawer binds what it returns.
            VisualElement root = new VisualElement();
            foreach (SerializedProperty child in SerializedPropertyUtils.EnumerateVisibleChildren(property))
            {
                root.Add(new PropertyField(child));
            }

            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            foreach (SerializedProperty child in SerializedPropertyUtils.EnumerateVisibleChildren(property))
            {
                position.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(position, child, true);
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 0f;
            foreach (SerializedProperty child in SerializedPropertyUtils.EnumerateVisibleChildren(property))
            {
                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }
    }
}
