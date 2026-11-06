using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class AddBlackboardElementCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private Type m_elementType;

        public AddBlackboardElementCommand(Type elementType)
        {
            m_elementType = elementType;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!EditorTypesFactory.IsBlackboardElementType(m_elementType))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardElementCommand)}: '{m_elementType}' is not a registered blackboard element type.");
                return;
            }

            if (!scope.TryGetBlackboard(out BlackboardRef blackboard))
            {
                Debug.LogError($"[Soup] {nameof(AddBlackboardElementCommand)}: the graph's blackboard could not be resolved.");
                return;
            }

            ABlackboardElement element = (ABlackboardElement)Activator.CreateInstance(m_elementType);
            element.SetId(Guid.NewGuid().ToString("N"));
            element.SetName($"New {EditorTypesFactory.GetBlackboardElementDisplayName(m_elementType)}");

            int index = blackboard.ElementsSP.arraySize;
            blackboard.ElementsSP.arraySize++;
            blackboard.ElementsSP.GetArrayElementAtIndex(index).managedReferenceValue = element;
            scope.ChangeSet.BlackboardDirty = true;
        }
    }
}
