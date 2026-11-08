using System;
using Soup.Editor.Core.DataRefs;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class MoveBlackboardVariableCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_variableId;
        private int m_offset;

        // An offset of -1 moves the variable up one place, +1 down one.
        public MoveBlackboardVariableCommand(string variableId, int offset)
        {
            m_variableId = variableId;
            m_offset = offset;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetVariable(m_variableId, out BlackboardVariableRef variable))
            {
                Debug.LogError($"[Soup] {nameof(MoveBlackboardVariableCommand)}: no blackboard variable has the id '{m_variableId}'.");
                return;
            }

            // Variables whose class is missing aren't shown, so they don't count as places to move past.
            int target = variable.Index + m_offset;
            while (m_offset != 0 && target >= 0 && target < blackboard.VariablesSP.arraySize
                   && new BlackboardVariableRef(blackboard.VariablesSP.GetArrayElementAtIndex(target), target).IsMissingType)
            {
                target += Math.Sign(m_offset);
            }

            if (m_offset == 0 || target < 0 || target >= blackboard.VariablesSP.arraySize)
            {
                return;
            }

            blackboard.VariablesSP.MoveArrayElement(variable.Index, target);
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
