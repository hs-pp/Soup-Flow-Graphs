using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class CopyGraphElementsCommand : IWindowCommand
    {
        private IReadOnlyList<string> m_nodeIds;
        private IReadOnlyList<string> m_groupIds;

        public CopyGraphElementsCommand(IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds = null)
        {
            m_nodeIds = nodeIds;
            if (m_nodeIds == null)
            {
                m_nodeIds = new List<string>();
            }

            m_groupIds = groupIds;
            if (m_groupIds == null)
            {
                m_groupIds = new List<string>();
            }
        }

        public void ExecuteCommand(GraphEditorState state, LoadedFlowGraph loadedFlowGraph)
        {
            if (!state.TryGetCurrentCollection(loadedFlowGraph, out NodeCollectionRef collection))
            {
                return;
            }

            loadedFlowGraph.TryGetCollectionMetaData(state.SubGraphPath, out NodeCollectionMetaDataRef collectionMetaData);
            GraphClipboard.Copy(collection, collectionMetaData, m_nodeIds, m_groupIds);
        }
    }
}
