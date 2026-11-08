using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Reorders a Conditional node's conditions, which is also the order they are tested in. Edges follow their outports.
    /// </summary>
    public sealed class MoveBlackboardConditionCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_nodeId;
        private string m_entryId;
        private int m_offset;

        // An offset of -1 moves the condition up one place, +1 down one.
        public MoveBlackboardConditionCommand(string nodeId, string entryId, int offset)
        {
            m_nodeId = nodeId;
            m_entryId = entryId;
            m_offset = offset;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.Collection.TryGetNode(m_nodeId, out NodeRef nodeRef) || nodeRef.GetNodeType() != typeof(BlackboardConditionalNode))
            {
                Debug.LogError($"[Soup] {nameof(MoveBlackboardConditionCommand)}: no Conditional node '{m_nodeId}' in the current collection.");
                return;
            }

            SerializedProperty entriesSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ENTRIES_FIELD_NAME);
            int index = FindEntry(entriesSP, m_entryId);
            if (index < 0)
            {
                Debug.LogError($"[Soup] {nameof(MoveBlackboardConditionCommand)}: the node has no condition '{m_entryId}'.");
                return;
            }

            int target = index + m_offset;
            if (m_offset == 0 || target < 0 || target >= entriesSP.arraySize)
            {
                return;
            }

            entriesSP.MoveArrayElement(index, target);
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
