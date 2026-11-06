using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(LogNode))]
    public class LogNodeView : BaseHorizontalNodeView
    {
        private Label m_logLabel;
        
        protected override void OnAddExtensionsContents(VisualElement extensionsContainer) 
        {
            m_logLabel = new Label();
            extensionsContainer.Add(m_logLabel);
        }

        protected override void OnPropertyBound(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            base.OnPropertyBound(nodeRef, metaDataRef);
            m_logLabel.BindProperty(nodeRef.NodeSP.FindPropertyRelative(LogNode.MessageFieldName));
        }
    }
}
