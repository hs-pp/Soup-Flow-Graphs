using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using UnityEditor;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class DeleteGraphElementsCommand : IGraphCommand
    {
        private IReadOnlyList<string> m_nodeIds;
        private IReadOnlyList<EdgeId> m_edges;
        private IReadOnlyList<string> m_groupIds;

        public DeleteGraphElementsCommand(IReadOnlyList<string> nodeIds, IReadOnlyList<EdgeId> edges, IReadOnlyList<string> groupIds = null)
        {
            m_nodeIds = nodeIds ?? Array.Empty<string>();
            m_edges = edges ?? Array.Empty<EdgeId>();
            m_groupIds = groupIds ?? Array.Empty<string>();
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            // Explicitly deleted edges.
            foreach (EdgeId edge in m_edges)
            {
                CommandUtils.TrySetOutportTarget(scope, edge.FromNodeId, edge.OutportId, string.Empty);
            }

            HashSet<string> deletedIds = DeleteNodes(scope, m_nodeIds);

            if (scope.TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                DeleteNodeMetaDatas(collectionMetaData, deletedIds);
                DeleteGroups(scope, collectionMetaData, m_groupIds);
            }
        }

        private HashSet<string> DeleteNodes(LoadedFlowGraph.CommandScope scope, IEnumerable<string> nodeIds)
        {
            HashSet<string> deletedIds = new();
            if (nodeIds == null)
            {
                return deletedIds;
            }

            // Resolve which requested nodes exist.
            HashSet<string> requestedIds = new(nodeIds);
            List<int> indicesToDelete = new();
            foreach (NodeRef nodeRef in scope.Collection.GetNodes())
            {
                if (!nodeRef.IsMissingType && !nodeRef.IsEntryNode && requestedIds.Contains(nodeRef.Id) && deletedIds.Add(nodeRef.Id))
                {
                    indicesToDelete.Add(nodeRef.Index);
                }
            }

            if (deletedIds.Count == 0)
            {
                return deletedIds;
            }

            // Scrub every edge pointing at a deleted node.
            foreach (NodeRef nodeRef in scope.Collection.GetNodes())
            {
                ScrubOutportTargets(scope, nodeRef, deletedIds);
            }

            DeleteNodesAt(scope.Collection, indicesToDelete);
            scope.ChangeSet.RemovedNodes.UnionWith(deletedIds);
            scope.ChangeSet.TopologyDirty = true;
            return deletedIds;
        }

        private int DeleteNodesAt(NodeCollectionRef collection, IReadOnlyList<int> indices)
        {
            if (!collection.IsValid || indices == null || indices.Count == 0)
            {
                return 0;
            }

            List<int> ordered = new(indices);
            ordered.Sort((a, b) => b.CompareTo(a));

            int deleted = 0;
            foreach (int index in ordered)
            {
                if (index < 0 || index >= collection.NodesSP.arraySize)
                {
                    continue;
                }

                collection.NodesSP.DeleteArrayElementAtIndex(index);
                deleted++;
            }

            return deleted;
        }

        private bool ScrubOutportTargets(LoadedFlowGraph.CommandScope scope, NodeRef nodeRef, HashSet<string> removedNodeIds)
        {
            if (removedNodeIds == null || removedNodeIds.Count == 0)
            {
                return false;
            }

            bool changed = false;
            foreach (OutportRef outport in nodeRef.EnumerateOutports())
            {
                SerializedProperty target = outport.ConnectedNodeIdSP;
                if (target != null && removedNodeIds.Contains(target.stringValue))
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

        private void DeleteNodeMetaDatas(NodeCollectionMetaDataRef collectionMetaData, HashSet<string> nodeIds)
        {
            if (!collectionMetaData.IsValid || nodeIds == null || nodeIds.Count == 0)
            {
                return;
            }

            for (int i = collectionMetaData.NodesSP.arraySize - 1; i >= 0; i--)
            {
                if (nodeIds.Contains(new NodeMetaDataRef(collectionMetaData.NodesSP.GetArrayElementAtIndex(i), i).NodeId))
                {
                    collectionMetaData.NodesSP.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private int DeleteGroups(LoadedFlowGraph.CommandScope scope, NodeCollectionMetaDataRef collectionMetaData, IReadOnlyList<string> groupIds)
        {
            if (!collectionMetaData.IsValid || groupIds == null || groupIds.Count == 0)
            {
                return 0;
            }

            HashSet<string> ids = new(groupIds);
            int deleted = 0;
            for (int i = collectionMetaData.GroupsSP.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty groupSP = collectionMetaData.GroupsSP.GetArrayElementAtIndex(i);
                if (ids.Contains(groupSP.FindPropertyRelative(GroupMetaData.GroupIdFieldName)?.stringValue))
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