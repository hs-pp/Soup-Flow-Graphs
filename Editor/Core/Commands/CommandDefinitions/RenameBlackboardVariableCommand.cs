using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class RenameBlackboardVariableCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_variableId;
        private string m_name;

        public RenameBlackboardVariableCommand(string variableId, string name)
        {
            m_variableId = variableId;
            m_name = name?.Trim();
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (string.IsNullOrEmpty(m_name))
            {
                return;
            }

            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetVariable(m_variableId, out BlackboardVariableRef variable))
            {
                Debug.LogError($"[Soup] {nameof(RenameBlackboardVariableCommand)}: no blackboard variable with the id '{m_variableId}'.");
                return;
            }

            SerializedProperty nameSP = variable.NameSP;
            if (nameSP == null || nameSP.stringValue == m_name)
            {
                return;
            }

            nameSP.stringValue = m_name;
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
