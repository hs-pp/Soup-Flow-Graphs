using Soup.Editor.State;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class EnterSubGraphCommand : IWindowCommand
    {
        private string m_nodeId;

        public EnterSubGraphCommand(string nodeId)
        {
            m_nodeId = nodeId;
        }

        public void ExecuteCommand(GraphEditorState state, LoadedFlowGraph loadedFlowGraph)
        {
            state.EnterSubGraph(m_nodeId, loadedFlowGraph);
        }
    }
}