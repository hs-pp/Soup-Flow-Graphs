using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Runtime.BuiltInNodes;
using UnityEngine;

namespace Soup.Editor.Core
{
    public partial class LoadedFlowGraph
    {
        /// <summary>
        /// The object that every graph command gets. It provides everything it needs to successfully execute the command.
        /// </summary>
        public readonly struct CommandScope
        {
            private readonly LoadedFlowGraph m_loadedFlowGraph;
            private readonly IReadOnlyList<string> m_subGraphPath;
            public readonly NodeCollectionRef Collection;
            public GraphChangeSet ChangeSet => m_loadedFlowGraph.m_workingChangeSet;

            /** The edited graph asset's type. Every collection in the asset, subgraphs included, belongs to it. */
            public Type GraphType => m_loadedFlowGraph.GraphAsset.GetType();

            public CommandScope(LoadedFlowGraph loadedFlowGraph, IReadOnlyList<string> subGraphPath)
            {
                m_loadedFlowGraph = loadedFlowGraph;
                m_subGraphPath = subGraphPath;
                if (!loadedFlowGraph.TryGetCollection(subGraphPath, out NodeCollectionRef collection))
                {
                    string path = subGraphPath != null ? string.Join("/", subGraphPath) : string.Empty;
                    throw new InvalidOperationException($"[Soup] CommandScope: subgraph path '{path}' could not be resolved.");
                }
                
                Collection = collection;
            }

            public bool TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
            {
                return m_loadedFlowGraph.TryGetCollectionMetaData(m_subGraphPath, out collectionMetaData);
            }

            /// <summary>
            /// The graph's blackboard. There's one per graph, whichever subgraph the command runs in.
            /// </summary>
            public bool TryGetBlackboard(out BlackboardRef blackboard)
            {
                return m_loadedFlowGraph.TryGetBlackboard(out blackboard);
            }

            #region Nodes
            public bool RepairOutports(NodeRef nodeRef)
            {
                m_loadedFlowGraph.ApplyGraphChanges();
                return RecordRepair(FlowGraphValidator.ValidateNodeTreeEdges(nodeRef));
            }

            public bool RepairOutports(IReadOnlyCollection<string> nodeIds)
            {
                m_loadedFlowGraph.ApplyGraphChanges();

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

            #region NodeCollectionMetaData
            public bool TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
            {
                collectionMetaData = default;
                if (!m_loadedFlowGraph.EnsureMetaDataSubAssetExists())
                {
                    return false;
                }

                if (!m_loadedFlowGraph.TryGetRootCollectionMetaData(out collectionMetaData))
                {
                    return false;
                }

                if (m_subGraphPath == null)
                {
                    return true;
                }

                // Every node on a subgraph path is a SubGraphNode, which is sealed, so its meta data type is known without reading the node.
                foreach (string subGraphNodeId in m_subGraphPath)
                {
                    if (!TryGetOrCreateNodeMetaData(collectionMetaData, subGraphNodeId, typeof(SubGraphNode), out NodeMetaDataRef nodeMetaData) || !nodeMetaData.TryGetSubGraphCollectionMetaData(out collectionMetaData))
                    {
                        Debug.LogError($"[Soup] CommandScope: meta data for subgraph '{subGraphNodeId}' could not be resolved, so positions in this subgraph won't be saved.");
                        collectionMetaData = default;
                        return false;
                    }
                }

                return true;
            }
            #endregion

            #region NodeMetaData
            public bool TryGetOrCreateNodeMetaData(NodeCollectionMetaDataRef collectionMetaData, string nodeId, Type nodeType, out NodeMetaDataRef nodeMetaData)
            {
                // A new entry reads back through the same handles without an apply, so its writes stay pending until the run applies.
                TryCreateNodeMetaData(collectionMetaData, nodeId, nodeType);
                return collectionMetaData.TryGetNodeMetaDataRef(nodeId, out nodeMetaData);
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
