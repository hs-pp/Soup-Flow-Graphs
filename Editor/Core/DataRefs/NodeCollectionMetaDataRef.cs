using System.Collections.Generic;
using Soup.Editor.State;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public struct NodeCollectionMetaDataRef
    {
        public readonly string Path;
        public readonly SerializedProperty CollectionSP;
        public readonly SerializedProperty NodesSP;
        public readonly SerializedProperty GroupsSP;

        public NodeCollectionMetaDataRef(SerializedProperty collectionSP)
        {
            CollectionSP = collectionSP;
            Path = collectionSP?.propertyPath;
            NodesSP = collectionSP?.FindPropertyRelative(NodeCollectionMetaData.NodesFieldName);
            GroupsSP = collectionSP?.FindPropertyRelative(NodeCollectionMetaData.GroupsFieldName);
        }

        public bool IsValid => CollectionSP != null && NodesSP != null && GroupsSP != null;

        public IEnumerable<NodeMetaDataRef> GetNodeMetaDatas()
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

        public bool TryGetNodeMetaDataRef(string nodeId, out NodeMetaDataRef nodeMetaData)
        {
            nodeMetaData = new();
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            foreach (NodeMetaDataRef entry in GetNodeMetaDatas())
            {
                if (entry.NodeId == nodeId)
                {
                    nodeMetaData = entry;
                    return true;
                }
            }

            return false;
        }

        public Dictionary<string, NodeMetaDataRef> GetNodeMetaDatasById()
        {
            Dictionary<string, NodeMetaDataRef> nodeMetaDatas = new();
            foreach (NodeMetaDataRef entry in GetNodeMetaDatas())
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

        public bool TryGetGroupMetaDataRef(string groupId, out GroupMetaDataRef groupMetaData)
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