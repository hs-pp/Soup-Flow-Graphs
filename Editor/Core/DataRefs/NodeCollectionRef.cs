using System.Collections.Generic;
using Soup.Runtime;
using Soup.Runtime.BuiltInNodes;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public struct NodeCollectionRef
    {
        public readonly string Path;
        public readonly SerializedProperty CollectionSP;
        public readonly SerializedProperty NodesSP;
        public readonly NodeRef EntryNode;

        public NodeCollectionRef(SerializedProperty collectionSP)
        {
            CollectionSP = collectionSP;
            Path = collectionSP?.propertyPath;
            NodesSP = collectionSP?.FindPropertyRelative(NodeCollection.NODES_FIELD_NAME);
            EntryNode = new NodeRef(collectionSP?.FindPropertyRelative(NodeCollection.ENTRY_NODE_FIELD_NAME), NodeRef.ENTRY_NODE_INDEX);
        }

        public bool IsValid => CollectionSP != null && NodesSP != null;

        public IEnumerable<NodeRef> GetNodes()
        {
            if (!IsValid)
            {
                yield break;
            }

            if (EntryNode.IsValid)
            {
                yield return EntryNode;
            }

            for (int i = 0; i < NodesSP.arraySize; i++)
            {
                SerializedProperty nodeSP = NodesSP.GetArrayElementAtIndex(i);
                yield return new NodeRef(nodeSP, i);
            }
        }

        public bool TryGetNode(string nodeId, out NodeRef nodeRef)
        {
            nodeRef = new();
            if (!IsValid || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            foreach (NodeRef node in GetNodes())
            {
                if (node.Id == nodeId)
                {
                    nodeRef = node;
                    return true;
                }
            }

            return false;
        }

        public bool ContainsNode(string nodeId)
        {
            return TryGetNode(nodeId, out _);
        }

        public Dictionary<string, NodeRef> GetNodesById()
        {
            Dictionary<string, NodeRef> nodes = new();
            foreach (NodeRef node in GetNodes())
            {
                if (!node.IsMissingType)
                {
                    nodes.TryAdd(node.Id, node);
                }
            }

            return nodes;
        }
    }
}