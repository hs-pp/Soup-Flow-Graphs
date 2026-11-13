using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class ResizeGroupCommand : IGraphCommand
    {
        private string m_groupId;
        private Vector2 m_size;

        public ResizeGroupCommand(string groupId, Vector2 size)
        {
            m_groupId = groupId;
            m_size = size;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (scope.TryGetCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData) && collectionMetaData.TryGetGroupMetaDataRef(m_groupId, out GroupMetaDataRef group))
            {
                SetGroupSize(scope, group, m_size);
            }
        }

        private static void SetGroupSize(LoadedFlowGraph.CommandScope scope, GroupMetaDataRef group, Vector2 size)
        {
            SerializedProperty rectSP = group.RectSP;
            if (rectSP == null)
            {
                return;
            }

            Rect rect = rectSP.rectValue;
            size = GroupMetaData.ClampSize(size);
            if (rect.size == size)
            {
                return;
            }

            rect.size = size;
            rectSP.rectValue = rect;
            scope.ChangeSet.GroupsDirty = true;
        }
    }
}
