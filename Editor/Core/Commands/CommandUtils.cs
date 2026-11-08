using System;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Runtime;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands
{
    /// <summary>
    /// Functions shared between Commands.
    /// </summary>
    public static class CommandUtils
    {
        #region Nodes
        public static NodeRef AddNode(LoadedFlowGraph.CommandScope scope, ANode node)
        {
            if (!scope.Collection.IsValid || node == null)
            {
                return default;
            }

            int index = scope.Collection.NodesSP.arraySize;
            scope.Collection.NodesSP.arraySize++;
            SerializedProperty nodeSP = scope.Collection.NodesSP.GetArrayElementAtIndex(index);
            nodeSP.managedReferenceValue = node;
            scope.ChangeSet.AddedNodes.Add(node.Id);
            return new NodeRef(nodeSP, index);
        }
        #endregion

        #region Outports
        public static bool TrySetOutportTarget(LoadedFlowGraph.CommandScope scope, string nodeId, string outportId, string targetNodeId)
        {
            if (!scope.Collection.TryGetNode(nodeId, out NodeRef nodeRef))
            {
                Debug.LogError($"[Soup] CommandUtils: Node '{nodeId}' not found in the current collection.");
                return false;
            }

            if (!TrySetOutportTarget(nodeRef, outportId, targetNodeId))
            {
                Debug.LogError($"[Soup] CommandUtils: Node '{nodeId}' has no outport with id '{outportId}'.");
                return false;
            }

            scope.ChangeSet.TopologyDirty = true;
            return true;
        }

        private static bool TrySetOutportTarget(NodeRef nodeRef, string outportId, string targetNodeId)
        {
            if (!nodeRef.TryGetOutport(outportId, out OutportRef outportRef))
            {
                return false;
            }

            SerializedProperty target = outportRef.ConnectedNodeIdSP;
            if (target == null)
            {
                return false;
            }

            target.stringValue = targetNodeId ?? string.Empty;
            return true;
        }
        #endregion

        #region MetaData
        public static bool SetNodePosition(LoadedFlowGraph.CommandScope scope, NodeMetaDataRef nodeMetaData, Vector2 position)
        {
            SerializedProperty positionSP = nodeMetaData.PositionSP;
            if (positionSP == null)
            {
                return false;
            }

            positionSP.vector2Value = position;
            scope.ChangeSet.MovedNodes.Add(nodeMetaData.NodeId);
            return true;
        }
        #endregion

        #region Groups
        public static string AddGroup(LoadedFlowGraph.CommandScope scope, NodeCollectionMetaDataRef collectionMetaData, string title, Rect rect, Color color)
        {
            if (!collectionMetaData.IsValid)
            {
                return null;
            }

            string groupId = Guid.NewGuid().ToString("N");
            int newIndex = collectionMetaData.GroupsSP.arraySize;
            collectionMetaData.GroupsSP.arraySize++;
            SerializedProperty entry = collectionMetaData.GroupsSP.GetArrayElementAtIndex(newIndex);
            entry.FindPropertyRelative(GroupMetaData.GroupIdFieldName).stringValue = groupId;
            entry.FindPropertyRelative(GroupMetaData.TitleFieldName).stringValue = title;
            entry.FindPropertyRelative(GroupMetaData.RectFieldName).rectValue = new Rect(rect.position, GroupMetaData.ClampSize(rect.size));
            entry.FindPropertyRelative(GroupMetaData.ColorFieldName).colorValue = color;
            scope.ChangeSet.GroupsDirty = true;
            scope.ChangeSet.AddedGroups.Add(groupId);
            return groupId;
        }
        #endregion
    }
}
