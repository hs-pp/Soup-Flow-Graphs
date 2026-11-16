using System;
using System.Collections.Generic;
using System.Globalization;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Runtime;
using Soup.Runtime.Blackboards;
using Soup.Runtime.BuiltInNodes;
using UnityEditor;
using UnityEngine.Serialization;

namespace Soup.Editor.Core
{
    /// <summary>
    /// External edits are writes made outside a command: inspector bindings, IMGUI drawers, Unity's own Inspector.
    /// </summary>
    public partial class LoadedFlowGraph
    {
        private enum ExternalEditType
        {
            None,
            Node,
            MetaData,
            Groups,
            Blackboard,
            Structure,
        }

        private const string MANAGED_REFERENCES_PREFIX = "managedReferences[";
        private const string GRAPH_ROOT_PREFIX = AFlowGraph.ROOT_COLLECTION_FIELD_NAME + ".";
        private const string GRAPH_BLACKBOARD_PREFIX = AFlowGraph.BLACKBOARD_FIELD_NAME + ".";
        private const string BLACKBOARD_VALUE_PREFIX = ABlackboardVariable.VALUE_FIELD_NAME + ".";
        private const string GRAPH_SUBGRAPH_PREFIX = SubGraphNode.SUBGRAPH_FIELD_NAME + ".";
        private const string META_DATA_ROOT_PREFIX = FlowGraphMetaData.ROOT_COLLECTION_FIELD_NAME + ".";
        private const string META_DATA_SUBGRAPH_PREFIX = SubGraphNodeMetaData.SUBGRAPH_FIELD_NAME + ".";
        private const string META_DATA_GROUPS_PREFIX = NodeCollectionMetaData.GROUPS_FIELD_NAME + ".";

        private readonly HashSet<string> m_externalNodeEdits = new();
        private readonly HashSet<string> m_externalMetaDataEdits = new();
        private bool m_externalGroupsEdited;
        private bool m_externalBlackboardEdited;
        private bool m_externalStructureEdited;

        /** Node id by reference id for references nested inside a node.*/
        private Dictionary<long, string> m_nestedReferenceOwners;

        private UndoPropertyModification[] HandlePostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (m_isExecutingCommands)
            {
                return modifications;
            }

            m_nestedReferenceOwners = null;

            foreach (UndoPropertyModification modification in modifications)
            {
                PropertyModification property = modification.currentValue;
                if (property == null)
                {
                    property = modification.previousValue;
                }

                if (property == null)
                {
                    continue;
                }

                if (IsGraphAlive && property.target == GraphAsset)
                {
                    RecordExternalEdit(ClassifyGraphEdit(property.propertyPath, out string nodeId), nodeId);
                }
                else if (IsMetaDataAlive && property.target == m_metaData)
                {
                    RecordExternalEdit(ClassifyMetaDataEdit(property.propertyPath, out string nodeId), nodeId);
                }
            }

            return modifications;
        }

        private ExternalEditType ClassifyGraphEdit(string path, out string nodeId)
        {
            nodeId = null;
            if (!TrySplitManagedReference(path, out long referenceId, out string localPath))
            {
                // Outside any node, only the root collection's own lists can change
                if (path.StartsWith(GRAPH_ROOT_PREFIX, StringComparison.Ordinal))
                {
                    return ExternalEditType.Structure;
                }

                // Or the blackboard's variable list
                return path.StartsWith(GRAPH_BLACKBOARD_PREFIX, StringComparison.Ordinal) ? ExternalEditType.Blackboard : ExternalEditType.None;
            }

            if (localPath.Length == 0)
            {
                return ExternalEditType.Structure; // The reference itself changed
            }

            switch (ManagedReferenceUtility.GetManagedReference(GraphAsset, referenceId))
            {
                case SubGraphNode when localPath.StartsWith(GRAPH_SUBGRAPH_PREFIX, StringComparison.Ordinal):
                    return ExternalEditType.Structure; // The subgraph's own lists
                case ANode when localPath == ANode.ID_FIELD_NAME:
                    return ExternalEditType.Structure; // Views are keyed by id so only a rebuild finds the node under its new one.
                case ANode node:
                    nodeId = node.Id;
                    return ExternalEditType.Node;
                case ABlackboardVariable when localPath == ABlackboardVariable.VALUE_FIELD_NAME || localPath.StartsWith(BLACKBOARD_VALUE_PREFIX, StringComparison.Ordinal):
                    return ExternalEditType.None;
                case ABlackboardVariable:
                    return ExternalEditType.Blackboard; // Its name or id, which node views show and look up.
                default:
                    if (TryGetNestedReferenceOwner(referenceId, out nodeId))
                    {
                        return ExternalEditType.Node;
                    }

                    return ExternalEditType.Structure;
            }
        }

        private bool TryGetNestedReferenceOwner(long referenceId, out string nodeId)
        {
            if (m_nestedReferenceOwners == null)
            {
                m_nestedReferenceOwners = new Dictionary<long, string>();

                using SerializedObject serializedGraph = new(GraphAsset);
                CollectNestedReferenceOwners(new NodeCollectionRef(serializedGraph.FindProperty(AFlowGraph.ROOT_COLLECTION_FIELD_NAME)), m_nestedReferenceOwners);
            }

            return m_nestedReferenceOwners.TryGetValue(referenceId, out nodeId);
        }

        private static void CollectNestedReferenceOwners(NodeCollectionRef collection, Dictionary<long, string> owners)
        {
            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeRef.IsMissingType)
                {
                    continue;
                }

                string subGraphPath = nodeRef.NodeSP.propertyPath + "." + SubGraphNode.SUBGRAPH_FIELD_NAME;
                SerializedProperty iterator = nodeRef.NodeSP.Copy();
                SerializedProperty endProperty = nodeRef.NodeSP.GetEndProperty();
                bool enterChildren = true;
                while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
                {
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        owners.TryAdd(iterator.managedReferenceId, nodeRef.Id);
                    }

                    enterChildren = iterator.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference
                                    && iterator.propertyPath != subGraphPath;
                }

                if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph))
                {
                    CollectNestedReferenceOwners(subGraph, owners);
                }
            }
        }

        private ExternalEditType ClassifyMetaDataEdit(string path, out string nodeId)
        {
            nodeId = null;
            if (!TrySplitManagedReference(path, out long referenceId, out string localPath))
            {
                return path.StartsWith(META_DATA_ROOT_PREFIX, StringComparison.Ordinal)
                    ? ClassifyCollectionMetaDataEdit(path.Substring(META_DATA_ROOT_PREFIX.Length))
                    : ExternalEditType.None;
            }

            if (localPath.Length == 0)
            {
                return ExternalEditType.Structure; // The reference itself changed.
            }

            switch (ManagedReferenceUtility.GetManagedReference(m_metaData, referenceId))
            {
                case SubGraphNodeMetaData when localPath.StartsWith(META_DATA_SUBGRAPH_PREFIX, StringComparison.Ordinal):
                    return ClassifyCollectionMetaDataEdit(localPath.Substring(META_DATA_SUBGRAPH_PREFIX.Length));
                case NodeMetaData when localPath == NodeMetaData.NODE_ID_FIELD_NAME:
                    return ExternalEditType.Structure; // Re-keyed to another node or to none.
                case NodeMetaData nodeMetaData:
                    nodeId = nodeMetaData.NodeId;
                    return ExternalEditType.MetaData;
                default:
                    return ExternalEditType.Structure; // Swapped out.
            }
        }

        private static ExternalEditType ClassifyCollectionMetaDataEdit(string collectionPath)
        {
            return collectionPath.StartsWith(META_DATA_GROUPS_PREFIX, StringComparison.Ordinal) ? ExternalEditType.Groups : ExternalEditType.Structure;
        }

        private static bool TrySplitManagedReference(string path, out long referenceId, out string localPath)
        {
            referenceId = 0;
            localPath = null;
            if (!path.StartsWith(MANAGED_REFERENCES_PREFIX, StringComparison.Ordinal))
            {
                return false;
            }

            int closeIndex = path.IndexOf(']', MANAGED_REFERENCES_PREFIX.Length);
            if (closeIndex < 0 || !long.TryParse(path.Substring(MANAGED_REFERENCES_PREFIX.Length, closeIndex - MANAGED_REFERENCES_PREFIX.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out referenceId))
            {
                return false;
            }

            localPath = closeIndex + 2 <= path.Length ? path.Substring(closeIndex + 2) : string.Empty; // Skips "].".
            return true;
        }

        private void RecordExternalEdit(ExternalEditType type, string nodeId)
        {
            switch (type)
            {
                case ExternalEditType.None:
                    return;
                case ExternalEditType.Node when !string.IsNullOrEmpty(nodeId):
                    m_externalNodeEdits.Add(nodeId);
                    break;
                case ExternalEditType.MetaData when !string.IsNullOrEmpty(nodeId):
                    m_externalMetaDataEdits.Add(nodeId);
                    break;
                case ExternalEditType.Groups:
                    m_externalGroupsEdited = true;
                    break;
                case ExternalEditType.Blackboard:
                    m_externalBlackboardEdited = true;
                    break;
                default:
                    m_externalStructureEdited = true;
                    break;
            }

            EditorApplication.delayCall -= ProcessExternalEdits;
            EditorApplication.delayCall += ProcessExternalEdits;
        }

        private void ProcessExternalEdits()
        {
            bool structureEdited = m_externalStructureEdited;
            bool groupsEdited = m_externalGroupsEdited;
            bool blackboardEdited = m_externalBlackboardEdited;
            List<string> nodeEdits = new(m_externalNodeEdits);
            List<string> metaDataEdits = new(m_externalMetaDataEdits);
            ClearExternalEdits();

            if (!IsGraphAlive)
            {
                return;
            }

            if (structureEdited)
            {
                Reload(validate: true);
                return;
            }

            if (nodeEdits.Count > 0 || metaDataEdits.Count > 0 || groupsEdited || blackboardEdited)
            {
                ExecuteCommands(new[] { new ApplyExternalEditsCommand(nodeEdits, metaDataEdits, groupsEdited, blackboardEdited) }, null, null);
            }
        }

        private void Reload(bool validate)
        {
            RefreshSerializedObjects();
            if (validate)
            {
                ValidateAll();
            }

            m_workingChangeSet.Clear();
            OnFlowGraphReloaded?.Invoke();
        }

        internal void HandleReimported()
        {
            if (!IsGraphAlive)
            {
                return;
            }

            m_serializedGraph.Update();

            m_metaData = GetMetaDataSubAsset(AssetDatabase.GetAssetPath(GraphAsset));
            m_serializedMetaData = m_metaData != null ? new SerializedObject(m_metaData) : null;

            RecordExternalEdit(ExternalEditType.Structure, null);
        }

        private void ClearExternalEdits()
        {
            m_externalNodeEdits.Clear();
            m_externalMetaDataEdits.Clear();
            m_externalGroupsEdited = false;
            m_externalBlackboardEdited = false;
            m_externalStructureEdited = false;
        }
    }
}
