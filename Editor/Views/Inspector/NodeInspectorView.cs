using System;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.DataRefs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [UxmlElement]
    public partial class NodeInspectorView : VisualElement
    {
        private const string UXML_PATH = "Soup/NodeInspectorView";
        private const string NODE_HEADER_TAG = "node-header";
        private const string INSPECTOR_CONTAINER_TAG = "inspector-container";

        private NodeInspectorHeader m_nodeHeader;
        private VisualElement m_inspectorContainer;
        private ANodeInspector m_nodeInspector;

        public event Action<ICommand> OnCommandRequested;

        public NodeInspectorView()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_nodeHeader =  this.Q<NodeInspectorHeader>(NODE_HEADER_TAG);
            m_inspectorContainer = this.Q(INSPECTOR_CONTAINER_TAG);
        }

        public void SetNode(NodeRef nodeRef, NodeMetaDataRef nodeMetaData)
        {
            if (!nodeRef.IsValid)
            {
                ClearView();
                return;
            }
            
            // Header
            m_nodeHeader.SetNode(nodeRef, nodeMetaData);

            // Inspector
            Type nodeType = nodeRef.GetNodeType();
            if (m_nodeInspector != null) // If not null, see if we can reuse.
            {
                if (!EditorTypesFactory.IsCorrectNodeInspectorType(nodeType, m_nodeInspector.GetType()))
                {
                    m_nodeInspector.RemoveFromHierarchy();
                    m_nodeInspector = null;
                }
            }
            if (m_nodeInspector == null) // Fill it if null.
            {
                m_nodeInspector = EditorTypesFactory.CreateNodeInspector(nodeType);
                m_nodeInspector.SetOnCommandRequestedHandler(command => OnCommandRequested?.Invoke(command));
                m_inspectorContainer.Add(m_nodeInspector);
            }
            m_nodeInspector.SetNode(nodeRef, nodeMetaData);
        }

        private void ClearView()
        {
            m_nodeInspector?.SetNode(default, default);
        }
    }
}
