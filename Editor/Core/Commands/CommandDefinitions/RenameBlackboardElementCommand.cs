using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    /// <summary>
    /// An empty name keeps the old one. Names don't have to be unique.
    /// </summary>
    public sealed class RenameBlackboardElementCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private string m_elementId;
        private string m_name;

        public RenameBlackboardElementCommand(string elementId, string name)
        {
            m_elementId = elementId;
            m_name = name?.Trim();
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (string.IsNullOrEmpty(m_name))
            {
                return;
            }

            if (!scope.TryGetBlackboard(out BlackboardRef blackboard) || !blackboard.TryGetElement(m_elementId, out BlackboardElementRef element))
            {
                Debug.LogError($"[Soup] {nameof(RenameBlackboardElementCommand)}: no blackboard element has the id '{m_elementId}'.");
                return;
            }

            SerializedProperty nameSP = element.NameSP;
            if (nameSP == null || nameSP.stringValue == m_name) // No change: don't dirty the asset.
            {
                return;
            }

            nameSP.stringValue = m_name;
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
