using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class ApplyExternalEditsCommand : IGraphCommand
    {
        private readonly IReadOnlyCollection<string> m_editedNodeIds;
        private readonly IReadOnlyCollection<string> m_editedMetaDataNodeIds;
        private readonly bool m_groupsEdited;
        private readonly bool m_blackboardEdited;

        public ApplyExternalEditsCommand(IReadOnlyCollection<string> editedNodeIds, IReadOnlyCollection<string> editedMetaDataNodeIds, bool groupsEdited, bool blackboardEdited)
        {
            m_editedNodeIds = editedNodeIds;
            m_editedMetaDataNodeIds = editedMetaDataNodeIds;
            m_groupsEdited = groupsEdited;
            m_blackboardEdited = blackboardEdited;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (m_editedNodeIds.Count > 0)
            {
                // External edits can land in any subgraph, so this runs at the root and searches the whole tree.
                List<NodeRef> editedNodes = new(m_editedNodeIds.Count);
                CollectNodes(scope.Collection, new HashSet<string>(m_editedNodeIds), editedNodes);
                foreach (NodeRef nodeRef in editedNodes)
                {
                    scope.RepairOutports(nodeRef);
                }

                scope.ChangeSet.ChangedNodes.UnionWith(m_editedNodeIds);
            }

            scope.ChangeSet.MovedNodes.UnionWith(m_editedMetaDataNodeIds);
            scope.ChangeSet.GroupsDirty |= m_groupsEdited;
            scope.ChangeSet.BlackboardDirty |= m_blackboardEdited;
        }

        private static void CollectNodes(NodeCollectionRef collection, HashSet<string> nodeIds, List<NodeRef> found)
        {
            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeIds.Contains(nodeRef.Id))
                {
                    found.Add(nodeRef);
                }

                if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph))
                {
                    CollectNodes(subGraph, nodeIds, found);
                }
            }
        }
    }
}
