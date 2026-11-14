using System.Collections.Generic;
using Soup.Editor.State;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public readonly struct NodeCollectionMetaDataRef
    {
        public readonly SerializedProperty CollectionSP;
        public readonly SerializedProperty NodesSP;
        public readonly SerializedProperty GroupsSP;
        
        public bool IsValid => CollectionSP != null && NodesSP != null && GroupsSP != null;

        public NodeCollectionMetaDataRef(SerializedProperty collectionSP)
        {
            CollectionSP = collectionSP;
            NodesSP = collectionSP?.FindPropertyRelative(NodeCollectionMetaData.NODES_FIELD_NAME);
            GroupsSP = collectionSP?.FindPropertyRelative(NodeCollectionMetaData.GROUPS_FIELD_NAME);
        }
        
        public IEnumerable<NodeMetaDataRef> GetNodes()
        {
            if (!IsValid)
            {
                yield break;
            }

            for (int i = 0; i < NodesSP.arraySize; i++)
            {
                SerializedProperty metaDataSP = NodesSP.GetArrayElementAtIndex(i);
                yield return new NodeMetaDataRef(metaDataSP, i);
            }
        }

        public bool TryGetNode(string nodeId, out NodeMetaDataRef nodeMetaData)
        {
            nodeMetaData = new();
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            foreach (NodeMetaDataRef entry in GetNodes())
            {
                if (entry.NodeId == nodeId)
                {
                    nodeMetaData = entry;
                    return true;
                }
            }

            return false;
        }

        public Dictionary<string, NodeMetaDataRef> GetNodesById()
        {
            Dictionary<string, NodeMetaDataRef> nodeMetaDatas = new();
            foreach (NodeMetaDataRef entry in GetNodes())
            {
                if (!entry.IsMissingType)
                {
                    nodeMetaDatas.TryAdd(entry.NodeId, entry);
                }
            }

            return nodeMetaDatas;
        }

        public IEnumerable<GroupMetaDataRef> GetGroups()
        {
            if (!IsValid)
            {
                yield break;
            }

            for (int i = 0; i < GroupsSP.arraySize; i++)
            {
                SerializedProperty groupSP = GroupsSP.GetArrayElementAtIndex(i);
                yield return new GroupMetaDataRef(groupSP, i);
            }
        }

        public bool TryGetGroup(string groupId, out GroupMetaDataRef groupMetaData)
        {
            groupMetaData = new();
            if (string.IsNullOrEmpty(groupId))
            {
                return false;
            }

            foreach (GroupMetaDataRef group in GetGroups())
            {
                if (group.GroupId == groupId)
                {
                    groupMetaData = group;
                    return true;
                }
            }

            return false;
        }
    }
}