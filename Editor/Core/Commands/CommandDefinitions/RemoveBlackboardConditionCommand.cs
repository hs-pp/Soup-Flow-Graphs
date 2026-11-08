using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Removes one condition from a Conditional node, with its outport and the edge on it.
    /// </summary>
    public sealed class RemoveBlackboardConditionCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_nodeId;
        private string m_entryId;

        public RemoveBlackboardConditionCommand(string nodeId, string entryId)
        {
            m_nodeId = nodeId;
            m_entryId = entryId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.Collection.TryGetNode(m_nodeId, out NodeRef nodeRef) || nodeRef.GetNodeType() != typeof(BlackboardConditionalNode))
            {
                Debug.LogError($"[Soup] {nameof(RemoveBlackboardConditionCommand)}: no Conditional node '{m_nodeId}' in the current collection.");
                return;
            }

            SerializedProperty entriesSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ENTRIES_FIELD_NAME);
            int index = FindEntry(entriesSP, m_entryId);
            if (index < 0)
            {
                Debug.LogError($"[Soup] {nameof(RemoveBlackboardConditionCommand)}: the node has no condition '{m_entryId}'.");
                return;
            }

            entriesSP.DeleteArrayElementAtIndex(index);
            scope.ChangeSet.ChangedNodes.Add(m_nodeId);
            scope.RepairOutports(nodeRef);
        }

        private static int FindEntry(SerializedProperty entriesSP, string entryId)
        {
            for (int i = 0; i < entriesSP.arraySize; i++)
            {
                if (entriesSP.GetArrayElementAtIndex(i).FindPropertyRelative(BlackboardConditionalEntry.OUTPORT_ID_FIELD_NAME).stringValue == entryId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
