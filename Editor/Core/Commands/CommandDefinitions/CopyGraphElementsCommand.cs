using System;
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
            m_nodeIds = nodeIds ?? Array.Empty<string>();
            m_groupIds = groupIds ?? Array.Empty<string>();
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
