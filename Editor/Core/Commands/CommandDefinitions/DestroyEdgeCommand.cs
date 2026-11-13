namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class DestroyEdgeCommand : IGraphCommand
    {
        private EdgeId m_edge;

        public DestroyEdgeCommand(EdgeId edge)
        {
            m_edge = edge;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            CommandUtils.TrySetOutportTarget(scope, m_edge.FromNodeId, m_edge.OutportId, string.Empty);
        }
    }
}