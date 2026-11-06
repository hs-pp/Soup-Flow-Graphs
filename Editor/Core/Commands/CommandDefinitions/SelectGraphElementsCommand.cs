using System;
using System.Collections.Generic;
using Soup.Editor.State;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class SelectGraphElementsCommand : IWindowCommand
    {
        private IReadOnlyList<string> m_ids;
        private GraphSelectionMode m_selectionMode;

        public SelectGraphElementsCommand(IReadOnlyList<string> ids, GraphSelectionMode mode)
        {
            m_ids = ids ?? Array.Empty<string>();
            m_selectionMode = mode;
        }

        public void ExecuteCommand(GraphEditorState state, LoadedFlowGraph loadedFlowGraph)
        {
            state.ApplySelection(m_selectionMode, m_ids);
        }
    }
}