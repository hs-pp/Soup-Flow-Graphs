using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Points a Setter or Conditional node at another blackboard variable, or at none. What the node holds follows the variable's
    /// value type: kept when the type is unchanged, otherwise the Setter gets a new setter for the type and the Conditional loses its
    /// conditions, along with their outports and edges.
    /// </summary>
    public sealed class SetBlackboardNodeVariableCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_nodeId;
        private string m_variableId;

        public SetBlackboardNodeVariableCommand(string nodeId, string variableId)
        {
            m_nodeId = nodeId;
            m_variableId = variableId ?? string.Empty;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.Collection.TryGetNode(m_nodeId, out NodeRef nodeRef))
            {
                Debug.LogError($"[Soup] {nameof(SetBlackboardNodeVariableCommand)}: node '{m_nodeId}' not found in the current collection.");
                return;
            }

            Type valueType = null;
            if (m_variableId.Length > 0)
            {
                if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetVariable(m_variableId, out BlackboardVariableRef variable))
                {
                    Debug.LogError($"[Soup] {nameof(SetBlackboardNodeVariableCommand)}: no blackboard variable has the id '{m_variableId}'.");
                    return;
                }

                valueType = variable.GetValueType();
            }

            // Decided before anything is written: the strategies read here are the live ones, which only change on the final apply.
            Type nodeType = nodeRef.GetNodeType();
            if (nodeType == typeof(BlackboardSetterNode))
            {
                SerializedProperty setterSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.SETTER_FIELD_NAME);
                bool keepSetter = (setterSP.managedReferenceValue as ABlackboardSetter)?.ValueType == valueType;

                new BlackboardKeyRef(nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.VARIABLE_FIELD_NAME)).VariableIdSP.stringValue = m_variableId;
                if (!keepSetter)
                {
                    setterSP.managedReferenceValue = EditorTypesFactory.CreateBlackboardSetter(valueType);
                }
            }
            else if (nodeType == typeof(BlackboardConditionalNode))
            {
                SerializedProperty entriesSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ENTRIES_FIELD_NAME);
                bool keepEntries = AllConditionsWorkOn(entriesSP, valueType);

                new BlackboardKeyRef(nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.VARIABLE_FIELD_NAME)).VariableIdSP.stringValue = m_variableId;
                if (!keepEntries)
                {
                    entriesSP.ClearArray();
                }
            }
            else
            {
                Debug.LogError($"[Soup] {nameof(SetBlackboardNodeVariableCommand)}: '{nodeType?.Name}' is not a blackboard Setter or Conditional node.");
                return;
            }

            scope.ChangeSet.ChangedNodes.Add(m_nodeId);
            scope.RepairOutports(nodeRef); // A Conditional that lost its conditions lost their outports.
        }

        private static bool AllConditionsWorkOn(SerializedProperty entriesSP, Type valueType)
        {
            for (int i = 0; i < entriesSP.arraySize; i++)
            {
                SerializedProperty conditionalSP = entriesSP.GetArrayElementAtIndex(i).FindPropertyRelative(BlackboardConditionalEntry.CONDITIONAL_FIELD_NAME);
                if ((conditionalSP?.managedReferenceValue as ABlackboardConditional)?.ValueType != valueType)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
