using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.State
{
    public sealed class ClipboardPayload
    {
        public readonly List<ANode> Nodes;
        public readonly List<NodeMetaData> NodeMetaDatas;
        public readonly List<GroupMetaData> Groups;

        internal ClipboardPayload(List<ANode> nodes, List<NodeMetaData> nodeMetaDatas, List<GroupMetaData> groups)
        {
            Nodes = nodes;
            NodeMetaDatas = nodeMetaDatas;
            Groups = groups;
        }
    }

    /// <summary>
    /// Copies nodes and groups as JSON through the system copy buffer.
    /// </summary>
    public static class GraphClipboard
    {
        // Jsonification has to happen through a temp scriptableobject to allow serialization as SerializeReferences.
        private class Payload : ScriptableObject
        {
            [SerializeReference]
            public List<ANode> Nodes = new();

            [SerializeReference]
            public List<NodeMetaData> NodeMetaDatas = new();

            public List<GroupMetaData> Groups = new();
        }

        public static bool HasClipboardContent => EditorGUIUtility.systemCopyBuffer != null && EditorGUIUtility.systemCopyBuffer.StartsWith(HEADER);
        private const string HEADER = "SoupClipboard:";

        public static void Copy(NodeCollectionRef collection, NodeCollectionMetaDataRef collectionMetaData, IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds)
        {
            string json = ToJson(collection, collectionMetaData, nodeIds, groupIds);
            if (json != null)
            {
                EditorGUIUtility.systemCopyBuffer = HEADER + json;
            }
        }

        public static ClipboardPayload Paste()
        {
            if (!HasClipboardContent)
            {
                return null;
            }

            return FromJson(EditorGUIUtility.systemCopyBuffer.Substring(HEADER.Length));
        }

        public static ClipboardPayload Clone(NodeCollectionRef collection, NodeCollectionMetaDataRef collectionMetaData, IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds)
        {
            string json = ToJson(collection, collectionMetaData, nodeIds, groupIds); // deep serialized copy
            return json != null ? FromJson(json) : null;
        }

        private static string ToJson(NodeCollectionRef collection, NodeCollectionMetaDataRef collectionMetaData, IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds)
        {
            Payload payload = ScriptableObject.CreateInstance<Payload>();
            try
            {
                if (nodeIds != null)
                {
                    Dictionary<string, NodeRef> nodesById = collection.GetNodesById();
                    Dictionary<string, NodeMetaDataRef> nodeMetaDatasById = collectionMetaData.GetNodeMetaDatasById();
                    foreach (string nodeId in nodeIds)
                    {
                        if (nodeId == null || !nodesById.TryGetValue(nodeId, out NodeRef nodeRef) || nodeRef.IsEntryNode)
                        {
                            continue;
                        }

                        ANode node = nodeRef.GetNodeInstance();
                        if (node == null)
                        {
                            continue;
                        }

                        payload.Nodes.Add(node);

                        if (nodeMetaDatasById.TryGetValue(nodeId, out NodeMetaDataRef nodeMetaData)
                            && nodeMetaData.GetNodeMetaDataInstance() is NodeMetaData metaData)
                        {
                            payload.NodeMetaDatas.Add(metaData);
                        }
                    }
                }

                if (groupIds != null)
                {
                    foreach (string groupId in groupIds)
                    {
                        if (collectionMetaData.TryGetGroupMetaDataRef(groupId, out GroupMetaDataRef group) 
                            && group.GroupSP.boxedValue is GroupMetaData groupMetaData)
                        {
                            payload.Groups.Add(groupMetaData);
                        }
                    }
                }

                return payload.Nodes.Count > 0 || payload.Groups.Count > 0 ? EditorJsonUtility.ToJson(payload) : null;
            }
            finally
            {
                Object.DestroyImmediate(payload);
            }
        }

        private static ClipboardPayload FromJson(string json)
        {
            Payload payload = ScriptableObject.CreateInstance<Payload>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(json, payload);
                return payload.Nodes.Count > 0 || payload.Groups.Count > 0
                    ? new ClipboardPayload(new List<ANode>(payload.Nodes), 
                        new List<NodeMetaData>(payload.NodeMetaDatas), 
                        new List<GroupMetaData>(payload.Groups))
                    : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                Object.DestroyImmediate(payload);
            }
        }
    }
}
