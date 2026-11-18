using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(SubGraphNode))]
    [CustomNodeName("SubGraph")]
    public class SubGraphNodeView : BaseHorizontalNodeView
    {
        private const string ICON_CLASS = "subGraphNodeIcon";
        private const string NAME_CLASS = "subGraphNodeName";

        private Label m_nameLabel;

        protected override void OnAddExtensionsContents()
        {
            m_nameLabel = new Label { pickingMode = PickingMode.Ignore };
            m_nameLabel.AddToClassList(NAME_CLASS);
            ExtensionContainer.Add(m_nameLabel);
            
            VisualElement icon = new() { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(ICON_CLASS);
            ExtensionContainer.Add(icon);
        }

        protected override void OnNodeDataChanged(NodeRef nodeRef)
        {
            base.OnNodeDataChanged(nodeRef);

            string subGraphName = nodeRef.NodeSP?.FindPropertyRelative(SubGraphNode.NAME_FIELD_NAME)?.stringValue;
            m_nameLabel.text = subGraphName;
            m_nameLabel.style.display = string.IsNullOrEmpty(subGraphName) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        protected override void AddManipulators()
        {
            base.AddManipulators();
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }
        
        private void OnMouseDown(MouseDownEvent e)
        {
            if (Graph == null)
            {
                return;
            }
            
            if (e.button == 0 && e.clickCount == 2)
            {
                Graph.RequestCommand(new EnterSubGraphCommand(Id));
                e.StopPropagation();
            }
        }
    }
}