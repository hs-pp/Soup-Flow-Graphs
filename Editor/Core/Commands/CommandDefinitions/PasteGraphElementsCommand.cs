using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Runtime;
using Soup.Runtime.BuiltInNodes;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class PasteGraphElementsCommand : IGraphCommand
    {
        private static readonly Vector2 DUPLICATE_POSITION_OFFSET = new(30f, 30f);

        private ClipboardPayload m_payload;
        private Vector2? m_centerPosition;
        private float m_snapStep;

        public PasteGraphElementsCommand(ClipboardPayload payload, Vector2? centerPosition, float snapStep)
        {
            m_payload = payload;
            m_centerPosition = centerPosition;
            m_snapStep = snapStep;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            Type graphType = scope.GraphType;
            List<ANode> pastedNodes = new();
            List<string> disallowedNodes = new();
            foreach (ANode node in m_payload.Nodes)
            {
                if (node == null || node is EntryNode)
                {
                    continue;
                }

                if (TryFindDisallowedNodeType(node, graphType, out Type disallowedType))
                {
                    disallowedNodes.Add(disallowedType == node.GetType()
                        ? disallowedType.Name
                        : $"{node.GetType().Name} (holds a {disallowedType.Name})");
                    continue;
                }

                pastedNodes.Add(node);
            }

            if (disallowedNodes.Count > 0)
            {
                Debug.LogWarning($"[Soup] {disallowedNodes.Count} node(s) weren't pasted because a {graphType.Name} doesn't allow them: {string.Join(", ", disallowedNodes)}.");
            }

            List<GroupMetaData> pastedGroups = new();
            foreach (GroupMetaData group in m_payload.Groups)
            {
                if (group != null)
                {
                    pastedGroups.Add(group);
                }
            }

            if (pastedNodes.Count == 0 && pastedGroups.Count == 0)
            {
                return;
            }

            Dictionary<string, NodeMetaData> metaDataById = new();
            foreach (NodeMetaData entry in m_payload.NodeMetaDatas)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.NodeId))
                {
                    metaDataById[entry.NodeId] = entry;
                }
            }

            Dictionary<string, string> allCollectionsIdMap = new Dictionary<string, string>(); // needed for metadata.
            Dictionary<string, string> collectionIdMap = new Dictionary<string, string>(); // Just for the collection in question.
            List<string> newIds = new List<string>();
            List<NodeMetaData> pastedMetaDatas = new List<NodeMetaData>(pastedNodes.Count);

            foreach (ANode node in pastedNodes)
            {
                string newId = Guid.NewGuid().ToString("N");
                string oldId = node.Id;
                if (!string.IsNullOrEmpty(oldId))
                {
                    collectionIdMap[oldId] = newId;
                    allCollectionsIdMap[oldId] = newId;
                }

                node.SetId(newId);
                newIds.Add(newId);

                Type nodeType = node.GetType();
                NodeMetaData metaData = oldId != null && metaDataById.TryGetValue(oldId, out NodeMetaData found) ? found : null;
                if (metaData == null)
                {
                    metaData = EditorTypesFactory.CreateMetaDataInstance(nodeType, newId);
                }
                else if (!EditorTypesFactory.IsUsingCorrectMetaDataType(metaData, nodeType))
                {
                    metaData = EditorTypesFactory.ConvertMetaDataToCorrectType(metaData, nodeType, newId);
                }

                metaData.NodeId = newId;
                pastedMetaDatas.Add(metaData);
            }

            foreach (ANode node in pastedNodes)
            {
                RemapOutportTargets(node, collectionIdMap);

                if (node is SubGraphNode subGraph && subGraph.SubGraph != null)
                {
                    RemapCollectionIds(subGraph.SubGraph, allCollectionsIdMap);
                }
            }

            foreach (NodeMetaData entry in pastedMetaDatas)
            {
                if (entry is SubGraphNodeMetaData subGraphEntry)
                {
                    RemapMetaDataIds(subGraphEntry.SubGraph, allCollectionsIdMap);
                }
            }

            Vector2 delta = DUPLICATE_POSITION_OFFSET;
            if (m_centerPosition.HasValue && TryGetBounds(pastedMetaDatas, pastedGroups, out Rect bounds))
            {
                delta = m_centerPosition.Value - bounds.center;
                if (m_snapStep > 0f)
                {
                    Vector2 corner = bounds.min + delta;
                    delta += SnapToGrid(corner, m_snapStep) - corner;
                }
            }

            foreach (NodeMetaData entry in pastedMetaDatas)
            {
                entry.Position += delta;
            }

            if (pastedNodes.Count > 0)
            {
                foreach (ANode node in pastedNodes)
                {
                    scope.AddNode(node);
                }

                scope.RepairOutports(newIds);
            }

            if (scope.TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                foreach (NodeMetaData entry in pastedMetaDatas)
                {
                    AddNodeMetaData(collectionMetaData, entry);
                }

                foreach (GroupMetaData group in pastedGroups)
                {
                    scope.AddGroup(collectionMetaData, group.Title, new Rect(group.Rect.position + delta, group.Rect.size), group.Color);
                }
            }
            else
            {
                Debug.LogError($"[Soup] {nameof(PasteGraphElementsCommand)}: meta data could not be resolved, so pasted nodes will appear at the origin and pasted groups were dropped.");
            }
        }

        /// <summary>
        /// Finds the first node type in this node, or anywhere inside it if it's a subgraph node, that the graph type doesn't allow.
        /// </summary>
        private static bool TryFindDisallowedNodeType(ANode node, Type graphType, out Type disallowedType)
        {
            disallowedType = node.GetType();
            if (!NodeUtils.IsNodeTypeAllowed(disallowedType, graphType))
            {
                return true;
            }

            if (node is SubGraphNode subGraph && subGraph.SubGraph != null)
            {
                foreach (ANode child in subGraph.SubGraph.Nodes)
                {
                    if (child != null && TryFindDisallowedNodeType(child, graphType, out disallowedType))
                    {
                        return true;
                    }
                }
            }

            disallowedType = null;
            return false;
        }

        /// <summary>
        /// Nodes contribute their positions and groups both corners, so a group-only paste centers the frames themselves.
        /// </summary>
        private static bool TryGetBounds(List<NodeMetaData> nodes, List<GroupMetaData> groups, out Rect bounds)
        {
            bool found = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;

            void Include(Vector2 point)
            {
                min = found ? Vector2.Min(min, point) : point;
                max = found ? Vector2.Max(max, point) : point;
                found = true;
            }

            foreach (NodeMetaData entry in nodes)
            {
                Include(entry.Position);
            }

            foreach (GroupMetaData group in groups)
            {
                Include(group.Rect.min);
                Include(group.Rect.max);
            }

            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return found;
        }

        private static Vector2 SnapToGrid(Vector2 position, float step)
        {
            return new Vector2(Mathf.Round(position.x / step) * step, Mathf.Round(position.y / step) * step);
        }

        private void RemapOutportTargets(ANode node, Dictionary<string, string> collectionIdMap)
        {
            foreach (OutportEdge outport in node.Outports)
            {
                if (outport == null)
                {
                    continue;
                }

                outport.SetConnectedNodeId(!string.IsNullOrEmpty(outport.ConnectedNodeId) && collectionIdMap.TryGetValue(outport.ConnectedNodeId, out string mapped) ? mapped : string.Empty);
            }
        }

        private void RemapCollectionIds(NodeCollection collection, Dictionary<string, string> allCollectionsIdMap)
        {
            List<ANode> nodes = new(collection.Nodes.Count);
            foreach (ANode node in collection.Nodes)
            {
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            Dictionary<string, string> collectionIdMap = new();
            foreach (ANode node in nodes)
            {
                string newId = Guid.NewGuid().ToString("N");
                if (!string.IsNullOrEmpty(node.Id))
                {
                    collectionIdMap[node.Id] = newId;
                    allCollectionsIdMap[node.Id] = newId;
                }

                node.SetId(newId);
            }

            if (collection.EntryNode != null)
            {
                nodes.Add(collection.EntryNode);
            }

            foreach (ANode node in nodes)
            {
                RemapOutportTargets(node, collectionIdMap);

                if (node is SubGraphNode subGraph && subGraph.SubGraph != null)
                {
                    RemapCollectionIds(subGraph.SubGraph, allCollectionsIdMap);
                }
            }
        }

        private static void RemapMetaDataIds(NodeCollectionMetaData metaData, Dictionary<string, string> allCollectionsIdMap)
        {
            if (metaData == null)
            {
                return;
            }

            foreach (NodeMetaData entry in metaData.Nodes)
            {
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.NodeId) && allCollectionsIdMap.TryGetValue(entry.NodeId, out string mapped))
                {
                    entry.NodeId = mapped;
                }

                if (entry is SubGraphNodeMetaData subGraph)
                {
                    RemapMetaDataIds(subGraph.SubGraph, allCollectionsIdMap);
                }
            }
        }

        private static void AddNodeMetaData(NodeCollectionMetaDataRef collectionMetaData, NodeMetaData nodeMetaData)
        {
            int index = collectionMetaData.NodesSP.arraySize;
            collectionMetaData.NodesSP.arraySize++;
            SerializedProperty metaDataSP = collectionMetaData.NodesSP.GetArrayElementAtIndex(index);
            metaDataSP.managedReferenceValue = nodeMetaData;
        }
    }

    public sealed class PasteFromClipboardCommand : IGraphCommand
    {
        private Vector2? m_centerPosition;
        private float m_snapStep;

        public PasteFromClipboardCommand(Vector2? centerPosition, float snapStep)
        {
            m_centerPosition = centerPosition;
            m_snapStep = snapStep;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            ClipboardPayload payload = GraphClipboard.Paste();
            if (payload != null)
            {
                new PasteGraphElementsCommand(payload, m_centerPosition, m_snapStep).ExecuteCommand(scope);
            }
        }
    }

    public sealed class DuplicateGraphElementsCommand : IGraphCommand
    {
        private IReadOnlyList<string> m_nodeIds;
        private IReadOnlyList<string> m_groupIds;

        public DuplicateGraphElementsCommand(IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds = null)
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
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            scope.TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData);
            ClipboardPayload payload = GraphClipboard.Clone(scope.Collection, collectionMetaData, m_nodeIds, m_groupIds);
            if (payload != null)
            {
                new PasteGraphElementsCommand(payload, null, 0f).ExecuteCommand(scope);
            }
        }
    }
}
