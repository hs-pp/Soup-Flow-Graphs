using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Appends a condition for the node's element type to a Conditional node, which gives it a new outport before Else.
    /// </summary>
    public sealed class AddBlackboardConditionCommand : IGraphCommand
    {
        public bool IsUndoable => true;

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

            string elementId = new BlackboardKeyRef(nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ElementFieldName)).ElementId;
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetElement(elementId, out BlackboardElementRef element))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardConditionCommand)}: the node has no variable to add a condition for.");
                return;
            }

            Type valueType = element.GetValueType();
            ABlackboardConditional conditional = EditorTypesFactory.CreateBlackboardConditional(valueType);
            if (conditional == null)
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardConditionCommand)}: there is no conditional for {valueType?.Name ?? "an unknown type"}.");
                return;
            }

            SerializedProperty entriesSP = nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.EntriesFieldName);
            int index = entriesSP.arraySize;
            entriesSP.arraySize++;
            SerializedProperty entrySP = entriesSP.GetArrayElementAtIndex(index);
            entrySP.FindPropertyRelative(BlackboardConditionalEntry.IdFieldName).stringValue = Guid.NewGuid().ToString("N");

            // A new array element starts as a copy of the last one, reference included, so it gets an instance of its own.
            entrySP.FindPropertyRelative(BlackboardConditionalEntry.ConditionalFieldName).managedReferenceValue = conditional;

            scope.ChangeSet.ChangedNodes.Add(m_nodeId);
            scope.RepairOutports(nodeRef);
        }
    }
}
