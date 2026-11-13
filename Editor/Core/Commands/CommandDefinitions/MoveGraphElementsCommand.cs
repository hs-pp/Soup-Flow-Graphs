using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class MoveGraphElementsCommand : IGraphCommand
    {
        private IReadOnlyDictionary<string, Vector2> m_nodePositions;
        private IReadOnlyDictionary<string, Vector2> m_groupPositions;

        public MoveGraphElementsCommand(IReadOnlyDictionary<string, Vector2> nodePositions, IReadOnlyDictionary<string, Vector2> groupPositions = null)
        {
            m_nodePositions = nodePositions ?? new Dictionary<string, Vector2>();
            m_groupPositions = groupPositions ?? new Dictionary<string, Vector2>();
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            Dictionary<string, NodeRef> movedNodes = new(m_nodePositions.Count);
            foreach (NodeRef nodeRef in scope.Collection.GetNodes())
            {
                if (!nodeRef.IsMissingType && m_nodePositions.ContainsKey(nodeRef.Id))
                {
                    movedNodes.TryAdd(nodeRef.Id, nodeRef);
                }
            }

            // Failed writes still report as moved, and groups as dirty, so the observer snaps the dragged views back to the model.
            scope.ChangeSet.GroupsDirty |= m_groupPositions.Count > 0;

            if (!scope.TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                scope.ChangeSet.MovedNodes.UnionWith(movedNodes.Keys);
                return;
            }

            // First pass: create any missing node meta data.
            foreach (NodeRef nodeRef in movedNodes.Values)
            {
                scope.TryCreateNodeMetaData(collectionMetaData, nodeRef.Id, nodeRef.GetNodeType());
            }

            Dictionary<string, NodeMetaDataRef> movedMetaDatas = new(movedNodes.Count);
            foreach (NodeMetaDataRef nodeMetaData in collectionMetaData.GetNodeMetaDatas())
            {
                if (!nodeMetaData.IsMissingType && movedNodes.ContainsKey(nodeMetaData.NodeId))
                {
                    movedMetaDatas.TryAdd(nodeMetaData.NodeId, nodeMetaData);
                }
            }

            // Second pass: update node positions.
            List<string> unwritten = new();
            foreach (string nodeId in movedNodes.Keys)
            {
                if (!movedMetaDatas.TryGetValue(nodeId, out NodeMetaDataRef nodeMetaData)
                    || !CommandUtils.SetNodePosition(scope, nodeMetaData, m_nodePositions[nodeId]))
                {
                    unwritten.Add(nodeId);
                }
            }

            // Third pass: update group positions.
            foreach (KeyValuePair<string, Vector2> pair in m_groupPositions)
            {
                if (collectionMetaData.TryGetGroupMetaDataRef(pair.Key, out GroupMetaDataRef group))
                {
                    SetGroupPosition(scope, group, pair.Value);
                }
            }

            if (unwritten.Count > 0)
            {
                Debug.LogError($"[Soup] {unwritten.Count} node position(s) were not saved: their meta data could not be resolved.");
                scope.ChangeSet.MovedNodes.UnionWith(unwritten);
            }
        }

        private static bool SetGroupPosition(LoadedFlowGraph.CommandScope scope, GroupMetaDataRef group, Vector2 position)
        {
            SerializedProperty rectSP = group.RectSP;
            if (rectSP == null)
            {
                return false;
            }

            Rect rect = rectSP.rectValue;
            rect.position = position;
            rectSP.rectValue = rect;
            scope.ChangeSet.GroupsDirty = true;
            return true;
        }
    }

}
