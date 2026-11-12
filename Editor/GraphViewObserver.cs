using System;
using System.Collections.Generic;
using Soup.Editor.Core;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Editor.Views;
using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Nodes;
using UnityEngine;

namespace Soup.Editor
{
    /// <summary>
    /// Keeps the GraphView in sync with the model.
    /// </summary>
    public sealed class GraphViewObserver : IDisposable
    {
        private readonly GraphEditorState m_state;
        private LoadedFlowGraph m_loadedFlowGraph;
        private readonly GraphView m_graphView;

        private readonly Dictionary<string, BaseNodeView> m_nodeViews = new();
        private readonly Dictionary<string, EdgeView> m_edgeViews = new();
        private readonly Dictionary<string, GroupView> m_groupViews = new();

        public event Action OnFlowGraphReloaded;

        public GraphViewObserver(GraphEditorState state, GraphView graphView)
        {
            m_state = state;
            m_graphView = graphView;
            m_state.OnGraphLoaded += HandleGraphLoaded;
            m_state.OnNavigationChanged += HandleNavigationChanged;
            m_state.OnSelectionChanged += ApplySelection;
        }

        public void Dispose()
        {
            SetLoadedFlowGraph(null);
            m_state.OnGraphLoaded -= HandleGraphLoaded;
            m_state.OnNavigationChanged -= HandleNavigationChanged;
            m_state.OnSelectionChanged -= ApplySelection;
        }

        public void SetLoadedFlowGraph(LoadedFlowGraph loadedFlowGraph)
        {
            if (m_loadedFlowGraph != null)
            {
                m_loadedFlowGraph.OnFlowGraphChanged -= HandleFlowGraphChanged;
                m_loadedFlowGraph.OnFlowGraphReloaded -= HandleFlowGraphReloaded;
            }

            if (loadedFlowGraph != m_loadedFlowGraph)
            {
                RemoveAllViews();
            }

            m_loadedFlowGraph = loadedFlowGraph;
            if (m_loadedFlowGraph != null)
            {
                m_loadedFlowGraph.OnFlowGraphChanged += HandleFlowGraphChanged;
                m_loadedFlowGraph.OnFlowGraphReloaded += HandleFlowGraphReloaded;
            }
        }

        private void RemoveAllViews()
        {
            foreach (EdgeView view in m_edgeViews.Values)
            {
                m_graphView.RemoveGraphElement(view);
            }

            foreach (BaseNodeView view in m_nodeViews.Values)
            {
                m_graphView.RemoveGraphElement(view);
            }

            foreach (GroupView view in m_groupViews.Values)
            {
                m_graphView.RemoveGraphElement(view);
            }

            m_edgeViews.Clear();
            m_nodeViews.Clear();
            m_groupViews.Clear();
        }

        private void HandleGraphLoaded()
        {
            Reconcile();
        }

        /// <summary>
        /// Starts the new collection from empty. Every collection's entry node has the same id, so a diff would otherwise
        /// keep the old entry view and rebind it.
        /// </summary>
        private void HandleNavigationChanged()
        {
            RemoveAllViews();
            Reconcile();
        }

        private void HandleFlowGraphReloaded()
        {
            // Undo can delete the subgraph node we're currently inside of. Pruning raises OnNavigationChanged, which reconciles.
            bool navigated = m_state.ValidateSubGraphPath(m_loadedFlowGraph);
            bool changed = navigated || Reconcile();

            // Only cancel any drags if reconcile made any real changes.
            if (changed)
            {
                m_graphView.CancelActiveDrag();
            }

            // A subgraph path prune already refreshed the UI through OnNavigationChanged.
            if (!navigated)
            {
                OnFlowGraphReloaded?.Invoke();
            }
        }

        private void HandleFlowGraphChanged(GraphChangeSet changeSet)
        {
            // A peer window can delete the subgraph node this window is inside of. Pruning raises OnNavigationChanged, which reconciles.
            if (changeSet.RemovedNodes.Count > 0 && m_state.ValidateSubGraphPath(m_loadedFlowGraph))
            {
                return;
            }

            if (changeSet.IsStructural)
            {
                Reconcile();
            }
            else
            {
                if (changeSet.MovedNodes.Count > 0)
                {
                    ApplyNodeMetaData(changeSet.MovedNodes);
                }

                // A data edit that changed outports arrives as structural, so these views keep their ports and edges. Node views
                // can show blackboard variable names, so a blackboard edit refreshes all of them.
                if (changeSet.BlackboardDirty)
                {
                    RefreshNodeViews(null);
                }
                else if (changeSet.ChangedNodes.Count > 0)
                {
                    RefreshNodeViews(changeSet.ChangedNodes);
                }

                // Group edits never touch nodes or edges, so they refresh group views alone rather than running the full reconcile.
                if (changeSet.GroupsDirty)
                {
                    ReconcileGroups();
                }
            }
        }

        /// <summary>
        /// Full diff pass holy moly.
        /// </summary>
        private bool Reconcile()
        {
            m_state.TryGetCurrentCollection(m_loadedFlowGraph, out NodeCollectionRef currentCollection);
            Dictionary<string, NodeMetaDataRef> metaDatas = BuildMetaDataLookup();

            Dictionary<string, NodeRef> modelNodes = new();
            foreach (NodeRef nodeRef in currentCollection.GetNodes())
            {
                if (!nodeRef.IsMissingType)
                {
                    modelNodes[nodeRef.Id] = nodeRef;
                }
            }

            Dictionary<string, (string fromId, string outportId, string toId)> modelEdges = new();
            foreach (KeyValuePair<string, NodeRef> node in modelNodes)
            {
                foreach (OutportRef outport in node.Value.EnumerateOutports())
                {
                    string toId = outport.ConnectedNodeId;
                    if (string.IsNullOrEmpty(outport.Id) || string.IsNullOrEmpty(toId) || !modelNodes.ContainsKey(toId))
                    {
                        continue;
                    }

                    modelEdges[new EdgeId(node.Key, outport.Id).Key] = (node.Key, outport.Id, toId);
                }
            }

            HashSet<string> staleNodeIds = new();
            foreach (KeyValuePair<string, BaseNodeView> pair in m_nodeViews)
            {
                if (!modelNodes.TryGetValue(pair.Key, out NodeRef nodeRef) || !pair.Value.OutportsMatch(nodeRef))
                {
                    staleNodeIds.Add(pair.Key);
                }
            }

            List<string> staleEdgeKeys = new();
            foreach (KeyValuePair<string, EdgeView> pair in m_edgeViews)
            {
                if (!modelEdges.TryGetValue(pair.Key, out var edge)
                    || edge.toId != pair.Value.ToNodeId
                    || staleNodeIds.Contains(pair.Value.FromNodeId)
                    || staleNodeIds.Contains(pair.Value.ToNodeId))
                {
                    staleEdgeKeys.Add(pair.Key);
                }
            }

            bool changed = staleNodeIds.Count > 0 || staleEdgeKeys.Count > 0;

            foreach (string key in staleEdgeKeys)
            {
                m_graphView.RemoveGraphElement(m_edgeViews[key]);
                m_edgeViews.Remove(key);
            }

            // Remove the stale/rebuilt node views.
            foreach (string id in staleNodeIds)
            {
                m_graphView.RemoveGraphElement(m_nodeViews[id]);
                m_nodeViews.Remove(id);
            }

            // Create node views for new (or rebuilt) model nodes then hand every surviving view its current model and property.
            foreach (KeyValuePair<string, NodeRef> pair in modelNodes)
            {
                metaDatas.TryGetValue(pair.Key, out NodeMetaDataRef metaData); // Invalid when the node has no entry yet.

                if (!m_nodeViews.TryGetValue(pair.Key, out BaseNodeView view))
                {
                    Type nodeType = pair.Value.GetNodeType();
                    view = EditorTypesFactory.CreateNodeView(nodeType);
                    view.InitializeView(pair.Value, metaData, nodeType);
                    m_nodeViews[pair.Key] = view;
                    m_graphView.AddGraphElement(view);
                    changed = true;
                    continue;
                }

                Vector2 previousPosition = view.GetPosition();
                view.UpdateFromModel(pair.Value, metaData);
                changed |= view.GetPosition() != previousPosition;
            }

            // Create edge views for new model edges.
            foreach (KeyValuePair<string, (string fromId, string outportId, string toId)> pair in modelEdges)
            {
                if (m_edgeViews.ContainsKey(pair.Key))
                {
                    continue;
                }

                BaseNodeView fromView = m_nodeViews[pair.Value.fromId];
                BaseNodeView toView = m_nodeViews[pair.Value.toId];
                PortView outputPort = fromView.GetOutputPort(pair.Value.outportId);
                PortView inputPort = toView.InputPort;
                if (outputPort == null || inputPort == null)
                {
                    Debug.LogError($"[Soup] GraphViewObserver: Cannot build edge view '{pair.Key}': missing port.");
                    continue;
                }

                EdgeView edgeView = new();
                edgeView.BindToModel(outputPort, inputPort);
                m_edgeViews[pair.Key] = edgeView;
                m_graphView.AddGraphElement(edgeView);
                changed = true;
            }

            changed |= ReconcileGroups();

            // Validate selection
            HashSet<string> validIds = new(modelNodes.Keys);
            validIds.UnionWith(modelEdges.Keys);
            validIds.UnionWith(m_groupViews.Keys);
            m_state.GraphSelection.RemoveInvalid(validIds);
            ApplySelection();

            return changed;
        }

        private bool ReconcileGroups()
        {
            List<GroupMetaDataRef> modelGroups = new();
            HashSet<string> modelIds = new();
            if (m_loadedFlowGraph != null && m_loadedFlowGraph.TryGetCollectionMetaData(m_state.SubGraphPath, out NodeCollectionMetaDataRef collectionMetaData))
            {
                foreach (GroupMetaDataRef group in collectionMetaData.GetGroups())
                {
                    if (!string.IsNullOrEmpty(group.GroupId) && modelIds.Add(group.GroupId))
                    {
                        modelGroups.Add(group);
                    }
                }
            }

            bool changed = false;

            List<string> staleGroupIds = new();
            foreach (string id in m_groupViews.Keys)
            {
                if (!modelIds.Contains(id))
                {
                    staleGroupIds.Add(id);
                }
            }

            foreach (string id in staleGroupIds)
            {
                m_graphView.RemoveGraphElement(m_groupViews[id]);
                m_groupViews.Remove(id);
                changed = true;
            }

            m_state.GraphSelection.Apply(GraphSelectionMode.Remove, staleGroupIds);

            foreach (GroupMetaDataRef group in modelGroups)
            {
                if (!m_groupViews.TryGetValue(group.GroupId, out GroupView view))
                {
                    view = new GroupView(group.GroupId);
                    m_groupViews[group.GroupId] = view;
                    m_graphView.AddGraphElement(view);
                    changed = true;
                }

                changed |= view.UpdateFromModel(group);
            }

            // Only sizes and membership decide stacking and both show up as a change.
            if (changed)
            {
                m_graphView.SortViewLayer(GroupView.LAYER, (a, b) => GroupView.CompareStacking((GroupView)a, (GroupView)b));
            }

            return changed;
        }

        /// <summary>
        /// Null refreshes every node view.
        /// </summary>
        private void RefreshNodeViews(HashSet<string> nodeIds)
        {
            m_state.TryGetCurrentCollection(m_loadedFlowGraph, out NodeCollectionRef currentCollection);
            Dictionary<string, NodeMetaDataRef> metaDatas = BuildMetaDataLookup();
            foreach (NodeRef nodeRef in currentCollection.GetNodes())
            {
                if ((nodeIds == null || nodeIds.Contains(nodeRef.Id)) && m_nodeViews.TryGetValue(nodeRef.Id, out BaseNodeView view))
                {
                    metaDatas.TryGetValue(nodeRef.Id, out NodeMetaDataRef metaData);
                    view.UpdateFromModel(nodeRef, metaData);
                }
            }
        }

        private void ApplyNodeMetaData(HashSet<string> nodeIds)
        {
            Dictionary<string, NodeMetaDataRef> metaDatas = BuildMetaDataLookup();
            foreach (string id in nodeIds)
            {
                if (m_nodeViews.TryGetValue(id, out BaseNodeView view))
                {
                    metaDatas.TryGetValue(id, out NodeMetaDataRef metaData);
                    view.UpdateMetaData(metaData);
                }
            }
        }

        private Dictionary<string, NodeMetaDataRef> BuildMetaDataLookup()
        {
            Dictionary<string, NodeMetaDataRef> metaDatas = new();

            if (m_loadedFlowGraph == null || !m_loadedFlowGraph.TryGetCollectionMetaData(m_state.SubGraphPath, out NodeCollectionMetaDataRef collectionMetaData))
            {
                return metaDatas;
            }

            foreach (NodeMetaDataRef nodeMetaData in collectionMetaData.GetNodeMetaDatas())
            {
                if (!nodeMetaData.IsMissingType)
                {
                    metaDatas[nodeMetaData.NodeId] = nodeMetaData;
                }
            }

            return metaDatas;
        }

        private void ApplySelection()
        {
            HashSet<string> selected = new(m_state.GraphSelection.SelectedIds);

            foreach (BaseNodeView view in m_nodeViews.Values)
            {
                view.Selected = selected.Contains(view.Id);
            }

            foreach (EdgeView view in m_edgeViews.Values)
            {
                view.Selected = selected.Contains(view.Id);
            }

            foreach (GroupView view in m_groupViews.Values)
            {
                view.Selected = selected.Contains(view.Id);
            }
        }
    }
}
