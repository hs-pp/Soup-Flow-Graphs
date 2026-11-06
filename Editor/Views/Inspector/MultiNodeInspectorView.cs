using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [UxmlElement]
    public partial class MultiNodeInspectorView : VisualElement
    {
        private Label m_label;
        public MultiNodeInspectorView()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            m_label = new Label("Multiple Nodes Selected!");
            Add(m_label);
        }

        public void SetNodeCount(int count)
        {
            m_label.text = $"{count} Nodes Selected!";
        }
    }
}