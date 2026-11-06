using System;
using Soup.Editor.Core.DataRefs;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class MoveBlackboardElementCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_elementId;
        private int m_offset;

        // An offset of -1 moves the element up one place, +1 down one.
        public MoveBlackboardElementCommand(string elementId, int offset)
        {
            m_elementId = elementId;
            m_offset = offset;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetElement(m_elementId, out BlackboardElementRef element))
            {
                Debug.LogError($"[Soup] {nameof(MoveBlackboardElementCommand)}: no blackboard element has the id '{m_elementId}'.");
                return;
            }

            // Elements whose class is missing aren't shown, so they don't count as places to move past.
            int target = element.Index + m_offset;
            while (m_offset != 0 && target >= 0 && target < blackboard.ElementsSP.arraySize
                   && new BlackboardElementRef(blackboard.ElementsSP.GetArrayElementAtIndex(target), target).IsMissingType)
            {
                target += Math.Sign(m_offset);
            }

            if (m_offset == 0 || target < 0 || target >= blackboard.ElementsSP.arraySize)
            {
                return;
            }

            blackboard.ElementsSP.MoveArrayElement(element.Index, target);
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
