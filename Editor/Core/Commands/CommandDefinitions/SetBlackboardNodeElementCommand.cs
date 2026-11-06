using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Points a Setter or Conditional node at another blackboard element, or at none. What the node holds follows the element's
    /// value type: kept when the type is unchanged, otherwise the Setter gets a new setter for the type and the Conditional loses its
    /// conditions, along with their outports and edges.
    /// </summary>
    public sealed class SetBlackboardNodeElementCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_nodeId;
        private string m_elementId;

        public SetBlackboardNodeElementCommand(string nodeId, string elementId)
        {
            m_nodeId = nodeId;
            m_elementId = elementId ?? string.Empty;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.Collection.TryGetNode(m_nodeId, out NodeRef nodeRef))
            {
                Debug.LogError($"[Soup] {nameof(SetBlackboardNodeElementCommand)}: node '{m_nodeId}' not found in the current collection.");
                return;
            }

            Type valueType = null;
            if (m_elementId.Length > 0)
            {
                if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetElement(m_elementId, out BlackboardElementRef element))
                {
                    Debug.LogError($"[Soup] {nameof(SetBlackboardNodeElementCommand)}: no blackboard element has the id '{m_elementId}'.");
                    return;
                }

                valueType = element.GetValueType();
            }

            // Decided before anything is written: the strategies read here are the live ones, which only change on the final apply.
            Type nodeType = nodeRef.GetNodeType();
            if (nodeType == typeof(BlackboardSetterNode))
            {
                SerializedProperty setterSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.SetterFieldName);
                bool keepSetter = (setterSP.managedReferenceValue as ABlackboardSetter)?.ValueType == valueType;

                new BlackboardKeyRef(nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.ElementFieldName)).ElementIdSP.stringValue = m_elementId;
                if (!keepSetter)
                {
                    setterSP.managedReferenceValue = EditorTypesFactory.CreateBlackboardSetter(valueType);
                }
            }
            else if (nodeType == typeof(BlackboardConditionalNode))
            {
                SerializedProperty entriesSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.EntriesFieldName);
                bool keepEntries = AllConditionsWorkOn(entriesSP, valueType);

                new BlackboardKeyRef(nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ElementFieldName)).ElementIdSP.stringValue = m_elementId;
                if (!keepEntries)
                {
                    entriesSP.ClearArray();
                }
            }
            else
            {
                Debug.LogError($"[Soup] {nameof(SetBlackboardNodeElementCommand)}: '{nodeType?.Name}' is not a blackboard Setter or Conditional node.");
                return;
            }

            scope.ChangeSet.ChangedNodes.Add(m_nodeId);
            scope.RepairOutports(nodeRef); // A Conditional that lost its conditions lost their outports.
        }

        private static bool AllConditionsWorkOn(SerializedProperty entriesSP, Type valueType)
        {
            for (int i = 0; i < entriesSP.arraySize; i++)
            {
                SerializedProperty conditionalSP = entriesSP.GetArrayElementAtIndex(i).FindPropertyRelative(BlackboardConditionalEntry.ConditionalFieldName);
                if ((conditionalSP?.managedReferenceValue as ABlackboardConditional)?.ValueType != valueType)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
