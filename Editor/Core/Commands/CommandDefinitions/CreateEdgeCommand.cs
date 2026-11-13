using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class CreateEdgeCommand : IGraphCommand
    {
        private string m_fromNodeId;
        private string m_outportId;
        private string m_toNodeId;

        public CreateEdgeCommand(string fromNodeId, string outportId, string toNodeId)
        {
            m_fromNodeId = fromNodeId;
            m_outportId = outportId;
            m_toNodeId = toNodeId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (m_fromNodeId == m_toNodeId)
            {
                Debug.LogWarning($"[Soup] {nameof(CreateEdgeCommand)}: a node cannot connect to itself.");
                return;
            }

            if (!scope.Collection.ContainsNode(m_toNodeId))
            {
                Debug.LogError($"[Soup] {nameof(CreateEdgeCommand)}: target node '{m_toNodeId}' not found.");
                return;
            }

            scope.TrySetOutportTarget(m_fromNodeId, m_outportId, m_toNodeId);
        }
    }
}