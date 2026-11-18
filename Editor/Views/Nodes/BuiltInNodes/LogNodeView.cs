using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeName("DebugLog")]
    [CustomNodeView(typeof(LogNode))]
    public class LogNodeView : BaseHorizontalNodeView
    {
        private Label m_logLabel;
        
        protected override void OnAddExtensionsContents() 
        {
            m_logLabel = new Label();
            ExtensionContainer.Add(m_logLabel);
        }

        protected override void OnPropertyBound(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            base.OnPropertyBound(nodeRef, metaDataRef);
            m_logLabel.BindProperty(nodeRef.NodeSP.FindPropertyRelative(LogNode.MESSAGE_FIELD_NAME));
        }
    }
}
