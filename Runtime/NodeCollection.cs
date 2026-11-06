using System;
using System.Collections.Generic;
using Soup.Runtime.BuiltInNodes;
using UnityEngine;

namespace Soup.Runtime
{
    [Serializable]
    public class NodeCollection : ISerializationCallbackReceiver
    {
        [SerializeReference]
        private List<ANode> m_nodes = new();

        [SerializeReference] // This could be a SerializeField but making it a reference makes accessing it consistent with the other nodes.
        private EntryNode m_entryNode = new();
        public EntryNode EntryNode => m_entryNode;

        [NonSerialized]
        private Dictionary<string, ANode> m_nodeCacheById;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // Any deserialize (an editor Apply, undo, reimport) can add, remove or replace nodes, so the lookup is rebuilt on next use.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_nodeCacheById = null;
        }

        public ANode GetNodeById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            
            if (m_nodeCacheById == null)
            {
                BuildNodeCache();
            }

            m_nodeCacheById.TryGetValue(id, out ANode node);
            return node;
        }

        private void BuildNodeCache()
        {
            if (m_nodeCacheById != null) // early out if we already built cache.
            {
                return;
            }

            m_nodeCacheById = new Dictionary<string, ANode>(m_nodes.Count + 1);

            // The entry node is not in m_nodes but is still addressable by id.
            if (!string.IsNullOrEmpty(m_entryNode.Id))
            {
                m_nodeCacheById.Add(m_entryNode.Id, m_entryNode);
            }

            foreach (ANode node in m_nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(node.Id) && !m_nodeCacheById.TryAdd(node.Id, node))
                {
                    Debug.LogError($"Duplicate node id '{node.Id}' found.");
                }
            }
        }
        
#if UNITY_EDITOR
        internal IReadOnlyList<ANode> Nodes => m_nodes;
        internal const string NodesFieldName = nameof(m_nodes);
        internal const string EntryNodeFieldName = nameof(m_entryNode);
#endif
    }
}
