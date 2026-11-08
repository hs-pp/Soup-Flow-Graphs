using Soup.Editor.Core.DataRefs;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Keys that point at the variable are left as they are: their nodes show it as missing, and undoing the delete brings it back.
    /// </summary>
    public sealed class DeleteBlackboardVariableCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_variableId;

        public DeleteBlackboardVariableCommand(string variableId)
        {
            m_variableId = variableId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetVariable(m_variableId, out BlackboardVariableRef variable))
            {
                Debug.LogError($"[Soup] {nameof(DeleteBlackboardVariableCommand)}: no blackboard variable has the id '{m_variableId}'.");
                return;
            }

            blackboard.VariablesSP.DeleteArrayElementAtIndex(variable.Index);
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
