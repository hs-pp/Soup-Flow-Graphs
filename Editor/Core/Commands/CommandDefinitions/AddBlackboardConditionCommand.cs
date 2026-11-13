using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class AddBlackboardConditionCommand : IGraphCommand
    {
        private string m_nodeId;

        public AddBlackboardConditionCommand(string nodeId)
        {
            m_nodeId = nodeId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.Collection.TryGetNode(m_nodeId, out NodeRef nodeRef) || nodeRef.GetNodeType() != typeof(BlackboardConditionalNode))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardConditionCommand)}: no Conditional node '{m_nodeId}' in the current collection.");
                return;
            }

            string variableId = new BlackboardKeyRef(nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.VARIABLE_FIELD_NAME)).VariableId;
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetVariable(variableId, out BlackboardVariableRef variable))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardConditionCommand)}: the node has no variable to add a condition for.");
                return;
            }

            Type valueType = variable.GetValueType();
            ABlackboardConditional conditional = EditorTypesFactory.CreateBlackboardConditional(valueType);
            if (conditional == null)
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardConditionCommand)}: there is no conditional for {valueType?.Name}.");
                return;
            }

            SerializedProperty entriesSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ENTRIES_FIELD_NAME);
            int index = entriesSP.arraySize;
            entriesSP.arraySize++;
            SerializedProperty entrySP = entriesSP.GetArrayElementAtIndex(index);
            entrySP.FindPropertyRelative(BlackboardConditionalEntry.OUTPORT_ID_FIELD_NAME).stringValue = Guid.NewGuid().ToString("N");

            // A new array element starts as a copy of the last one, reference included, so it gets an instance of its own.
            entrySP.FindPropertyRelative(BlackboardConditionalEntry.CONDITIONAL_FIELD_NAME).managedReferenceValue = conditional;

            scope.ChangeSet.ChangedNodes.Add(m_nodeId);
            scope.RepairOutports(nodeRef);
        }
    }
}
