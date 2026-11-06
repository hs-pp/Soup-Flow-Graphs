using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using UnityEngine;

namespace Soup.Editor.Core
{
    public partial class LoadedFlowGraph
    {
        public struct CommandScope
        {
            private readonly LoadedFlowGraph m_graph;
            private readonly IReadOnlyList<string> m_subGraphPath;
            public readonly NodeCollectionRef Collection;
            public GraphChangeSet ChangeSet => m_graph.m_workingChangeSet;

            public CommandScope(LoadedFlowGraph graph, IReadOnlyList<string> subGraphPath)
            {
                m_graph = graph;
                m_subGraphPath = subGraphPath;
                if (!graph.TryGetCollection(subGraphPath, out NodeCollectionRef collection))
                {
                    throw new InvalidOperationException(
                        $"[Soup] CommandScope: subgraph path '{string.Join("/", subGraphPath ?? Array.Empty<string>())}' could not be resolved.");
                }
                
                Collection = collection;
            }

            public bool TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
            {
                return m_graph.TryGetCollectionMetaData(m_subGraphPath, out collectionMetaData);
            }

            // One per graph, whichever subgraph the command runs in.
            public bool TryGetBlackboard(out BlackboardRef blackboard)
            {
                return m_graph.TryGetBlackboard(out blackboard);
            }

            #region Nodes
            public bool RepairOutports(NodeRef nodeRef)
            {
                m_graph.ApplyGraphChanges();
                return RecordRepair(FlowGraphValidator.ValidateNodeTreeEdges(nodeRef));
            }

            public bool RepairOutports(IReadOnlyCollection<string> nodeIds)
            {
                m_graph.ApplyGraphChanges();

                bool changed = false;
                Dictionary<string, NodeRef> nodesById = Collection.GetNodesById();
                foreach (string nodeId in nodeIds)
                {
                    if (nodesById.TryGetValue(nodeId, out NodeRef nodeRef))
                    {
                        changed |= FlowGraphValidator.ValidateNodeTreeEdges(nodeRef);
                    }
                }

                return RecordRepair(changed);
            }

            private bool RecordRepair(bool changed)
            {
                if (changed)
                {
                    ChangeSet.TopologyDirty = true;
                }

                return changed;
            }
            #endregion

            #region MetaData
            public NodeCollectionMetaDataRef ApplyAndReResolve(NodeCollectionMetaDataRef collectionMetaData)
            {
                if (!collectionMetaData.IsValid || !m_graph.IsMetaDataAlive)
                {
                    return default;
                }

                m_graph.ApplyMetaDataChanges();
                return new NodeCollectionMetaDataRef(m_graph.m_serializedMetaData.FindProperty(collectionMetaData.Path));
            }
            #endregion

            #region NodeCollectionMetaData
            public bool TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
            {
                collectionMetaData = default;
                if (!m_graph.EnsureMetaDataSubAssetExists())
                {
                    return false;
                }

                List<(string id, Type type)> subGraphNodes = new();
                if (!m_graph.TryGetRootCollection(out NodeCollectionRef collection))
                {
                    return false;
                }

                if (m_subGraphPath != null)
                {
                    foreach (string subGraphNodeId in m_subGraphPath)
                    {
                        if (!collection.TryGetNode(subGraphNodeId, out NodeRef nodeRef) || !nodeRef.TryGetSubGraphCollection(out NodeCollectionRef next))
                        {
                            Debug.LogError($"[Soup] meta data for subgraph '{subGraphNodeId}' could not be resolved!!! Node data in this subgraph will not be saved.");
                            return false;
                        }

                        subGraphNodes.Add((subGraphNodeId, nodeRef.GetNodeType()));
                        collection = next;
                    }
                }

                if (!m_graph.TryGetRootCollectionMetaData(out collectionMetaData))
                {
                    return false;
                }

                foreach ((string id, Type type) in subGraphNodes)
                {
                    if (!TryGetOrCreateNodeMetaDataRef(collectionMetaData, id, type, out NodeMetaDataRef nodeMetaData) || !nodeMetaData.TryGetSubGraphCollectionMetaData(out collectionMetaData))
                    {
                        Debug.LogError($"[Soup] meta data for subgraph '{id}' could not be resolved!!! Node data in this subgraph will not be saved.");
                        collectionMetaData = default;
                        return false;
                    }
                }

                return true;
            }
            #endregion

            #region NodeMetaData
            public bool TryGetOrCreateNodeMetaDataRef(NodeCollectionMetaDataRef collectionMetaData, string nodeId, Type nodeType, out NodeMetaDataRef nodeMetaData)
            {
                // Creating an entry invalidates element handles, so re-resolve the collection before looking it back up.
                return TryCreateNodeMetaData(collectionMetaData, nodeId, nodeType)
                    ? ApplyAndReResolve(collectionMetaData).TryGetNodeMetaDataRef(nodeId, out nodeMetaData)
                    : collectionMetaData.TryGetNodeMetaDataRef(nodeId, out nodeMetaData);
            }

            public bool TryCreateNodeMetaData(NodeCollectionMetaDataRef collectionMetaData, string nodeId, Type nodeType)
            {
                if (!collectionMetaData.IsValid || string.IsNullOrEmpty(nodeId))
                {
                    return false;
                }

                if (!collectionMetaData.TryGetNodeMetaDataRef(nodeId, out NodeMetaDataRef nodeMetaData))
                {
                    collectionMetaData.NodesSP.arraySize++;
                    collectionMetaData.NodesSP.GetArrayElementAtIndex(collectionMetaData.NodesSP.arraySize - 1).managedReferenceValue = EditorTypesFactory.CreateMetaDataInstance(nodeType, nodeId);
                    return true;
                }

                return FlowGraphValidator.ValidateNodeMetaDataType(nodeMetaData, nodeType);
            }
            #endregion
        }
    }
}
