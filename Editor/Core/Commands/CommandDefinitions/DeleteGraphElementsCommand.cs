using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using UnityEditor;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class DeleteGraphElementsCommand : IGraphCommand
    {
        private IReadOnlyList<string> m_nodeIds;
        private IReadOnlyList<string> m_groupIds;
        private IReadOnlyList<EdgeId> m_edges;

        public DeleteGraphElementsCommand(IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds = null, IReadOnlyList<EdgeId> edges = null)
        {
            m_nodeIds = nodeIds;
            if (m_nodeIds == null)
            {
                m_nodeIds = Array.Empty<string>();
            }

            m_groupIds = groupIds;
            if (m_groupIds == null)
            {
                m_groupIds = Array.Empty<string>();
            }

            m_edges = edges;
            if (m_edges == null)
            {
                m_edges = Array.Empty<EdgeId>();
            }
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            // Explicitly deleted edges.
            foreach (EdgeId edge in m_edges)
            {
                scope.TrySetOutportTarget(edge.FromNodeId, edge.OutportId, string.Empty);
            }

            HashSet<string> deletedNodeIds = DeleteNodes(scope, m_nodeIds);

            if (scope.TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                DeleteNodeMetaDatas(collectionMetaData, deletedNodeIds);
                DeleteGroups(scope, collectionMetaData, m_groupIds);
            }
        }

        private HashSet<string> DeleteNodes(LoadedFlowGraph.CommandScope scope, IEnumerable<string> nodeIds)
        {
            HashSet<string> deletedNodeIds = new();

            // Resolve which requested nodes exist.
            HashSet<string> requestedIds = new(nodeIds);
            List<int> indicesToDelete = new();
            foreach (NodeRef nodeRef in scope.Collection.GetNodes())
            {
                if (!nodeRef.IsMissingType && !nodeRef.IsEntryNode && requestedIds.Contains(nodeRef.Id) && deletedNodeIds.Add(nodeRef.Id))
                {
                    indicesToDelete.Add(nodeRef.Index);
                }
            }

            if (deletedNodeIds.Count == 0)
            {
                return deletedNodeIds;
            }

            // Scrub every edge pointing at a deleted node.
            foreach (NodeRef nodeRef in scope.Collection.GetNodes())
            {
                ScrubOutportTargets(scope, nodeRef, deletedNodeIds);
            }

            DeleteNodesAt(scope.Collection, indicesToDelete);
            scope.ChangeSet.RemovedNodes.UnionWith(deletedNodeIds);
            scope.ChangeSet.TopologyDirty = true;
            return deletedNodeIds;
        }

        private int DeleteNodesAt(NodeCollectionRef collection, IReadOnlyList<int> indices)
        {
            if (!collection.IsValid)
            {
                return 0;
            }

            List<int> ordered = new(indices);
            ordered.Sort((a, b) => b.CompareTo(a));

            int deleted = 0;
            foreach (int index in ordered)
            {
                collection.NodesSP.DeleteArrayElementAtIndex(index);
                deleted++;
            }

            return deleted;
        }

        private bool ScrubOutportTargets(LoadedFlowGraph.CommandScope scope, NodeRef nodeRef, HashSet<string> deletedNodeIds)
        {
            bool changed = false;
            foreach (OutportRef outport in nodeRef.EnumerateOutports())
            {
                SerializedProperty target = outport.ConnectedNodeIdSP;
                if (target != null && deletedNodeIds.Contains(target.stringValue))
                {
                    target.stringValue = string.Empty;
                    changed = true;
                }
            }

            if (changed)
            {
                scope.ChangeSet.TopologyDirty = true;
            }

            return changed;
        }

        private void DeleteNodeMetaDatas(NodeCollectionMetaDataRef collectionMetaData, HashSet<string> deletedNodeIds)
        {
            if (!collectionMetaData.IsValid || deletedNodeIds.Count == 0)
            {
                return;
            }

            for (int i = collectionMetaData.NodesSP.arraySize - 1; i >= 0; i--)
            {
                if (deletedNodeIds.Contains(new NodeMetaDataRef(collectionMetaData.NodesSP.GetArrayElementAtIndex(i), i).NodeId))
                {
                    collectionMetaData.NodesSP.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private int DeleteGroups(LoadedFlowGraph.CommandScope scope, NodeCollectionMetaDataRef collectionMetaData, IReadOnlyList<string> groupIds)
        {
            if (!collectionMetaData.IsValid || groupIds.Count == 0)
            {
                return 0;
            }

            HashSet<string> ids = new(groupIds);
            int deleted = 0;
            for (int i = collectionMetaData.GroupsSP.arraySize - 1; i >= 0; i--)
            {
                if (ids.Contains(new GroupMetaDataRef(collectionMetaData.GroupsSP.GetArrayElementAtIndex(i), i).GroupId))
                {
                    collectionMetaData.GroupsSP.DeleteArrayElementAtIndex(i);
                    deleted++;
                }
            }

            if (deleted > 0)
            {
                scope.ChangeSet.GroupsDirty = true;
            }

            return deleted;
        }
    }
}
