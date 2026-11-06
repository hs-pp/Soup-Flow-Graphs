using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class CreateGroupCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private Rect m_rect;

        public CreateGroupCommand(Rect rect)
        {
            m_rect = rect;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!scope.TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                Debug.LogError($"[Soup] {nameof(CreateGroupCommand)}: the current collection's meta data could not be resolved, so the group was not created.");
                return;
            }

            CommandUtils.AddGroup(scope, collectionMetaData, GroupMetaData.DEFAULT_TITLE, m_rect, GroupMetaData.DEFAULT_COLOR);
        }
    }
}
