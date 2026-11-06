using Soup.Editor.Core.DataRefs;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// Keys that point at the element are left as they are: their nodes show it as missing, and undoing the delete brings it back.
    /// </summary>
    public sealed class DeleteBlackboardElementCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_elementId;

        public DeleteBlackboardElementCommand(string elementId)
        {
            m_elementId = elementId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetElement(m_elementId, out BlackboardElementRef element))
            {
                Debug.LogError($"[Soup] {nameof(DeleteBlackboardElementCommand)}: no blackboard element has the id '{m_elementId}'.");
                return;
            }

            blackboard.ElementsSP.DeleteArrayElementAtIndex(element.Index);
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
