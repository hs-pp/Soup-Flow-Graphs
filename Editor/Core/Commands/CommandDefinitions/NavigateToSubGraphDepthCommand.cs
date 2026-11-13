using Soup.Editor.State;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class NavigateToSubGraphDepthCommand : IWindowCommand
    {
        private int m_depth;

        public NavigateToSubGraphDepthCommand(int depth)
        {
            m_depth = depth;
        }

        public void ExecuteCommand(GraphEditorState state, LoadedFlowGraph loadedFlowGraph)
        {
            state.NavigateToDepth(m_depth);
        }
    }
}