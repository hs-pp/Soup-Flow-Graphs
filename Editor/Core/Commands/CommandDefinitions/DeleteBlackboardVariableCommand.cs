using Soup.Editor.Core.DataRefs;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class DeleteBlackboardVariableCommand : IGraphCommand
    {
        private string m_variableId;

        public DeleteBlackboardVariableCommand(string variableId)
        {
            m_variableId = variableId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetVariable(m_variableId, out BlackboardVariableRef variable))
            {
                Debug.LogError($"[Soup] {nameof(DeleteBlackboardVariableCommand)}: no blackboard variable with the id '{m_variableId}'.");
                return;
            }

            blackboard.VariablesSP.DeleteArrayElementAtIndex(variable.Index);
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
