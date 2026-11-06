using System;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime;
using UnityEngine;

namespace Soup.Editor.Core.Commands.CommandDefinitions
{
    public sealed class CreateNodeCommand : IGraphCommand
    {
        public bool IsUndoable => true;

        private Type m_nodeType;
        private Vector2 m_position;
        private string m_presetNodeId;

        public CreateNodeCommand(Type nodeType, Vector2 position, string presetNodeId = null)
        {
            m_nodeType = nodeType;
            m_position = position;
            m_presetNodeId = presetNodeId;
        }

        public void ExecuteCommand(LoadedFlowGraph.CommandScope scope)
        {
            if (!NodeUtils.IsConcreteNodeType(m_nodeType))
            {
                Debug.LogError($"[Soup] {nameof(CreateNodeCommand)}: '{m_nodeType}' is not a concrete ANode type.");
                return;
            }

            if (!string.IsNullOrEmpty(m_presetNodeId) && scope.Collection.ContainsNode(m_presetNodeId))
            {
                Debug.LogError($"[Soup] {nameof(CreateNodeCommand)}: preset id '{m_presetNodeId}' already exists.");
                return;
            }

            ANode node = (ANode)Activator.CreateInstance(m_nodeType);
            node.SetId(string.IsNullOrEmpty(m_presetNodeId) ? Guid.NewGuid().ToString("N") : m_presetNodeId);

            NodeRef nodeRef = CommandUtils.AddNode(scope, node);
            if (!nodeRef.IsValid)
            {
                Debug.LogError($"[Soup] {nameof(CreateNodeCommand)}: the current collection could not be resolved, so '{m_nodeType.Name}' was not created.");
                return;
            }

            scope.RepairOutports(nodeRef);

            if (!scope.TryGetOrCreateCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
                || !scope.TryGetOrCreateNodeMetaDataRef(collectionMetaData, node.Id, m_nodeType, out NodeMetaDataRef nodeMetaData)
                || !CommandUtils.SetNodePosition(scope, nodeMetaData, m_position))
            {
                Debug.LogError($"[Soup] '{m_nodeType.Name}' was created but its position could not be saved; it will appear at the origin.");
            }
        }
    }
}