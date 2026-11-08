using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class AddBlackboardVariableCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private Type m_variableType;

        public AddBlackboardVariableCommand(Type variableType)
        {
            m_variableType = variableType;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!EditorTypesFactory.IsBlackboardVariableType(m_variableType))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardVariableCommand)}: '{m_variableType}' is not a registered blackboard variable type.");
                return;
            }

            if (!scope.TryGetBlackboard(out BlackboardRef blackboard))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardVariableCommand)}: the graph's blackboard could not be resolved.");
                return;
            }

            ABlackboardVariable variable = (ABlackboardVariable)Activator.CreateInstance(m_variableType);
            variable.SetId(Guid.NewGuid().ToString("N"));
            variable.SetName($"New {EditorTypesFactory.GetBlackboardVariableDisplayName(m_variableType)}");

            int index = blackboard.VariablesSP.arraySize;
            blackboard.VariablesSP.arraySize++;
            blackboard.VariablesSP.GetArrayElementAtIndex(index).managedReferenceValue = variable;
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
