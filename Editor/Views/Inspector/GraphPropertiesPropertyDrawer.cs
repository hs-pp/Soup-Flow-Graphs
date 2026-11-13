using Soup.Runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [CustomPropertyDrawer(typeof(AGraphProperties), true)]
    public class GraphPropertiesPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new();
            SerializedPropertyUtils.AddVisibleChildFields(root, property);
            return root;
        }
    }
}
