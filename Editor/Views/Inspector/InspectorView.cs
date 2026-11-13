using System;
using System.Collections.Generic;
using Soup.Editor.Core;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [Serializable]
    public class InspectorState
    {
        public float InspectorSize = 400; // Default value
    }
    
    public class InspectorView : VisualElement
    {
        private const string UXML_PATH = "Soup/InspectorView";
        private const string NODE_INSPECTOR_VIEW_TAG = "node-inspector-view";
        private const string MULTI_NODE_INSPECTOR_VIEW_TAG = "multi-node-inspector-view";
        private const string GRAPH_INSPECTOR_VIEW_TAG = "graph-inspector-view";
        private const string GROUP_INSPECTOR_VIEW_TAG = "group-inspector-view";
        
        private NodeInspectorView m_nodeInspectorView;
        private MultiNodeInspectorView m_multiNodeInspectorView;
        private GraphInspectorView m_graphInspectorView;
        private GroupInspectorView m_groupInspectorView;

        private GraphEditorState m_graphState;
        private LoadedFlowGraph m_loadedFlowGraph;

        public event Action<ICommand> OnCommandRequested;

        public InspectorView()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);
            
            m_nodeInspectorView = this.Q<NodeInspectorView>(NODE_INSPECTOR_VIEW_TAG);
            m_multiNodeInspectorView = this.Q<MultiNodeInspectorView>(MULTI_NODE_INSPECTOR_VIEW_TAG);
            m_graphInspectorView = this.Q<GraphInspectorView>(GRAPH_INSPECTOR_VIEW_TAG);
            m_groupInspectorView = this.Q<GroupInspectorView>(GROUP_INSPECTOR_VIEW_TAG);

            m_nodeInspectorView.OnCommandRequested += command => OnCommandRequested?.Invoke(command);
            m_graphInspectorView.OnCommandRequested += command => OnCommandRequested?.Invoke(command);
        }

        public void SetState(GraphEditorState state)
        {
            if (m_graphState != null)
            {
                m_graphState.OnGraphLoaded -= Refresh;
                m_graphState.OnNavigationChanged -= Refresh;
                m_graphState.OnSelectionChanged -= Refresh;
            }

            m_graphState = state;
            if (m_graphState != null)
            {
                m_graphState.OnGraphLoaded += Refresh;
                m_graphState.OnNavigationChanged += Refresh;
                m_graphState.OnSelectionChanged += Refresh;
            }

            Refresh();
        }

        public void SetLoadedFlowGraph(LoadedFlowGraph loadedFlowGraph)
        {
            if (m_loadedFlowGraph != null)
            {
                m_loadedFlowGraph.OnFlowGraphChanged -= HandleFlowGraphChanged;
            }
            
            m_loadedFlowGraph = loadedFlowGraph;
            if (m_loadedFlowGraph != null)
            {
                m_loadedFlowGraph.OnFlowGraphChanged += HandleFlowGraphChanged;
            }
        }

        private void HandleFlowGraphChanged(GraphChangeSet changeSet)
        {
            Refresh();
        }

        public void Dispose()
        {
            SetLoadedFlowGraph(null);
            SetState(null);
        }

        public void Refresh()
        {
            HideAllViews();
            
            if (m_graphState == null || m_loadedFlowGraph == null || !m_graphState.TryGetCurrentCollection(m_loadedFlowGraph, out NodeCollectionRef collection))
            {
                m_groupInspectorView.SetGroup(default);
                m_nodeInspectorView.SetNode(default, default);
                m_graphInspectorView.SetGraph(null);
                return;
            }

            List<NodeRef> selectedNodes = new();
            if (m_graphState.GraphSelection.SelectedIds.Count > 0)
            {
                Dictionary<string, NodeRef> nodesById = collection.GetNodesById();
                foreach (string id in m_graphState.GraphSelection.SelectedIds)
                {
                    if (id != null && nodesById.TryGetValue(id, out NodeRef nodeRef))
                    {
                        selectedNodes.Add(nodeRef);
                    }
                }
            }

            m_loadedFlowGraph.TryGetCollectionMetaData(m_graphState.SubGraphPath, out NodeCollectionMetaDataRef collectionMetaData);

            GroupMetaDataRef group = default;
            bool hasLoneGroup = selectedNodes.Count == 0 && TryGetSingleSelectedGroup(collectionMetaData, out group);
            if (hasLoneGroup)
            {
                m_groupInspectorView.SetGroup(group);
                m_groupInspectorView.style.display = DisplayStyle.Flex;
                return;
            }

            if (selectedNodes.Count == 1)
            {
                NodeRef nodeRef = selectedNodes[0];
                collectionMetaData.TryGetNode(nodeRef.Id, out NodeMetaDataRef nodeMetaData);
                m_nodeInspectorView.SetNode(nodeRef, nodeMetaData);
                m_nodeInspectorView.style.display = DisplayStyle.Flex;
                return;
            }

            m_nodeInspectorView.SetNode(default, default);

            if (selectedNodes.Count > 1)
            {
                m_multiNodeInspectorView.SetNodeCount(selectedNodes.Count);
                m_multiNodeInspectorView.style.display = DisplayStyle.Flex;
                return;
            }
            
            m_graphInspectorView.SetGraph(m_loadedFlowGraph);
            m_graphInspectorView.style.display = DisplayStyle.Flex;
        }

        private void HideAllViews()
        {
            m_nodeInspectorView.style.display = DisplayStyle.None;
            m_multiNodeInspectorView.style.display = DisplayStyle.None;
            m_graphInspectorView.style.display = DisplayStyle.None;
            m_groupInspectorView.style.display = DisplayStyle.None;
        }

        private bool TryGetSingleSelectedGroup(NodeCollectionMetaDataRef collectionMetaData, out GroupMetaDataRef group)
        {
            group = default;
            bool found = false;
            foreach (string id in m_graphState.GraphSelection.SelectedIds)
            {
                if (!collectionMetaData.TryGetGroup(id, out GroupMetaDataRef candidate))
                {
                    continue;
                }

                if (found)
                {
                    group = default;
                    return false;
                }

                group = candidate;
                found = true;
            }

            return found;
        }
    }
}
