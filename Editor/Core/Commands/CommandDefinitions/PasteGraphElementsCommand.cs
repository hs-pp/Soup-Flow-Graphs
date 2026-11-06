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
        public bool IsUndoable => true;

        private static readonly Vector2 DUPLICATE_POSITION_OFFSET = new(30f, 30f);

        private ClipboardPayload m_payload;
        private Vector2? m_centerPosition;

        public PasteGraphElementsCommand(ClipboardPayload payload, Vector2? centerPosition)
        {
            m_payload = payload;
            m_centerPosition = centerPosition;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (m_payload == null)
            {
                return;
            }

            if (!scope.Collection.IsValid)
            {
                Debug.LogError($"[Soup] {nameof(PasteGraphElementsCommand)}: the current collection could not be resolved, so nothing was pasted.");
                return;
            }

            List<ANode> pastedNodes = new();
            foreach (ANode node in m_payload.Nodes)
            {
                if (node != null && node is not EntryNode)
                {
                    pastedNodes.Add(node);
                }
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
            if (m_payload.NodeMetaDatas != null)
            {
                foreach (NodeMetaData entry in m_payload.NodeMetaDatas)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.NodeId))
                    {
                        metaDataById[entry.NodeId] = entry;
                    }
                }
            }

            Dictionary<string, string> allCollectionsIdMap = new Dictionary<string, string>(); // needed for metadata.
            Dictionary<string, string> collectionIdMap = new Dictionary<string, string>(); // Just for the collection in question.
            List<string> newIds = new List<string>();
            List<NodeMetaData> pastedMetaDatas = new List<NodeMetaData>(pastedNodes.Count);

            // Fresh IDs for the pasted nodes, edges between them are remapped, edges to anything else are dropped.
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
                if (!EditorTypesFactory.IsUsingCorrectMetaDataType(metaData, nodeType))
                {
                    metaData = EditorTypesFactory.ConvertMetaDataToCorrectType(metaData, nodeType, newId);
                }

                if (metaData == null)
                {
                    metaData = EditorTypesFactory.CreateMetaDataInstance(nodeType, newId);
                }

                metaData.NodeId = newId;
                pastedMetaDatas.Add(metaData);
            }

            foreach (ANode node in pastedNodes)
            {
                RemapOutportTargets(node, collectionIdMap);

                // Nested subgraph collections get fresh internal ids too
                if (node is SubGraphNode subGraph && subGraph.SubGraph != null)
                {
                    RemapCollectionIds(subGraph.SubGraph, allCollectionsIdMap);
                }
            }

            // Now that every level has contributed its pairs, rewrite the subtrees.
            foreach (NodeMetaData entry in pastedMetaDatas)
            {
                if (entry is SubGraphNodeMetaData subGraphEntry)
                {
                    RemapMetaDataIds(subGraphEntry.SubGraph, allCollectionsIdMap);
                }
            }

            // Pasting at a position centers everything there; otherwise (duplicate) it moves slightly over. Nodes and groups share one offset.
            Vector2 delta = m_centerPosition.HasValue && TryGetBounds(pastedMetaDatas, pastedGroups, out Rect bounds)
                ? m_centerPosition.Value - bounds.center : DUPLICATE_POSITION_OFFSET;

            foreach (NodeMetaData entry in pastedMetaDatas)
            {
                entry.Position += delta;
            }

            if (pastedNodes.Count > 0)
            {
                foreach (ANode node in pastedNodes)
                {
                    CommandUtils.AddNode(scope, node);
                }

                // The clipboard outlives code changes, so pasted outports can be stale.
                scope.RepairOutports(newIds);
            }

            if (scope.TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                // Inserted as-is rather than created fresh: a pasted subgraph node's meta data carries its whole nested layout.
                // Each entry was already retyped to match its node above.
                foreach (NodeMetaData entry in pastedMetaDatas)
                {
                    AddNodeMetaData(collectionMetaData, entry);
                }

                foreach (GroupMetaData group in pastedGroups)
                {
                    CommandUtils.AddGroup(scope, collectionMetaData, group.Title, new Rect(group.Rect.position + delta, group.Rect.size), group.Color);
                }
            }
            else
            {
                Debug.LogError($"[Soup] {nameof(PasteGraphElementsCommand)}: meta data could not be resolved, so pasted nodes will appear at the origin and pasted groups were dropped.");
            }
        }

        // Nodes contribute their positions and groups both corners, so a group-only paste centers the frames themselves.
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

        private void RemapOutportTargets(ANode node, Dictionary<string, string> collectionIdMap)
        {
            foreach (OutportEdge outport in node.Outports)
            {
                if (outport == null)
                {
                    continue;
                }

                outport.ConnectedNodeId = !string.IsNullOrEmpty(outport.ConnectedNodeId) && collectionIdMap.TryGetValue(outport.ConnectedNodeId, out string mapped) ? mapped : string.Empty;
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

            // Scoped to this collection on purpose.
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

            // Skip new id for the EntryNode.
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
            if (!collectionMetaData.IsValid || nodeMetaData == null)
            {
                return;
            }

            int index = collectionMetaData.NodesSP.arraySize;
            collectionMetaData.NodesSP.arraySize++;
            SerializedProperty metaDataSP = collectionMetaData.NodesSP.GetArrayElementAtIndex(index);
            metaDataSP.managedReferenceValue = nodeMetaData;
        }
    }

    public sealed class PasteFromClipboardCommand : IGraphCommand
    {
        public bool IsUndoable => true;
        public readonly Vector2? CenterPosition;

        public PasteFromClipboardCommand(Vector2? centerPosition)
        {
            CenterPosition = centerPosition;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            ClipboardPayload payload = GraphClipboard.Paste();
            if (payload != null)
            {
                new PasteGraphElementsCommand(payload, CenterPosition).ExecuteCommand(scope);
            }
        }
    }

    public sealed class DuplicateGraphElementsCommand : IGraphCommand
    {
        public bool IsUndoable => true;
        public readonly IReadOnlyList<string> NodeIds;
        public readonly IReadOnlyList<string> GroupIds;

        public DuplicateGraphElementsCommand(IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds = null)
        {
            NodeIds = nodeIds ?? Array.Empty<string>();
            GroupIds = groupIds ?? Array.Empty<string>();
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            // Ignoring the result: with no meta data, nodes still duplicate and simply land without positions.
            scope.TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData);
            ClipboardPayload payload = GraphClipboard.Clone(scope.Collection, collectionMetaData, NodeIds, GroupIds);
            if (payload != null)
            {
                new PasteGraphElementsCommand(payload, null).ExecuteCommand(scope);
            }
        }
    }
}
