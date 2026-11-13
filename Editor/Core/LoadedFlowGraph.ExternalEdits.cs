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

// Claude generated.
namespace Soup.Editor.Core
{
    /// <summary>
    /// External edits are writes made outside a command: inspector bindings, IMGUI drawers, Unity's own Inspector. Unity reports
    /// every undoable write through Undo.postprocessModifications, once per write however many windows show the graph. Each one is
    /// reduced to node ids and flags as it arrives, while its managed reference still resolves, and handled on the next editor tick,
    /// since the callback runs in the middle of the write.
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
        private const string METADATA_ROOT_PREFIX = FlowGraphMetaData.RootCollectionFieldName + ".";
        private const string METADATA_SUBGRAPH_PREFIX = SubGraphNodeMetaData.SubGraphFieldName + ".";
        private const string METADATA_GROUPS_PREFIX = NodeCollectionMetaData.GroupsFieldName + ".";

        private readonly HashSet<string> m_externalNodeEdits = new();
        private readonly HashSet<string> m_externalMetaDataEdits = new();
        private bool m_externalGroupsEdited;
        private bool m_externalBlackboardEdited;
        private bool m_externalStructureEdited;
        private bool m_isExternalEditProcessingScheduled;
        private Dictionary<long, string> m_nestedReferenceOwners; // Node id by reference id, built when one batch first needs it.

        private UndoPropertyModification[] HandlePostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (m_isExecutingCommands)
            {
                return modifications;
            }

            m_nestedReferenceOwners = null; // Each batch can follow writes that added or removed references.
            foreach (UndoPropertyModification modification in modifications)
            {
                PropertyModification property = modification.currentValue ?? modification.previousValue;
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

            return modifications; // A filter hook: anything not returned is dropped from the undo record.
        }

        private ExternalEditType ClassifyGraphEdit(string path, out string nodeId)
        {
            nodeId = null;
            if (!TrySplitManagedReference(path, out long referenceId, out string localPath))
            {
                // Outside any node, only the root collection's own lists can change: a node added, removed or swapped.
                if (path.StartsWith(GRAPH_ROOT_PREFIX, StringComparison.Ordinal))
                {
                    return ExternalEditType.Structure;
                }

                // Or the blackboard's variable list: a variable added, removed, reordered or swapped.
                return path.StartsWith(GRAPH_BLACKBOARD_PREFIX, StringComparison.Ordinal) ? ExternalEditType.Blackboard : ExternalEditType.None;
            }

            if (localPath.Length == 0)
            {
                return ExternalEditType.Structure; // The reference itself changed.
            }

            switch (ManagedReferenceUtility.GetManagedReference(GraphAsset, referenceId))
            {
                case SubGraphNode when localPath.StartsWith(GRAPH_SUBGRAPH_PREFIX, StringComparison.Ordinal):
                    return ExternalEditType.Structure; // The subgraph's own lists. Its nodes report under their own references.
                case ANode when localPath == ANode.ID_FIELD_NAME:
                    return ExternalEditType.Structure; // Views are keyed by id, so only a rebuild finds the node under its new one.
                case ANode node:
                    nodeId = node.Id;
                    return ExternalEditType.Node;
                case ABlackboardVariable when localPath == ABlackboardVariable.VALUE_FIELD_NAME || localPath.StartsWith(BLACKBOARD_VALUE_PREFIX, StringComparison.Ordinal):
                    return ExternalEditType.None; // Default values are plain bindings that nothing on the canvas reads.
                case ABlackboardVariable:
                    return ExternalEditType.Blackboard; // Its name or id, which node views show and look up.
                default:
                    // Graph properties and blackboard values are plain bindings that nothing on the canvas reads, even through a
                    // reference nested inside them.
                    if (IsReferenceUnder(AFlowGraph.GRAPH_PROPERTIES_FIELD_NAME, referenceId) || IsReferenceUnder(AFlowGraph.BLACKBOARD_FIELD_NAME, referenceId))
                    {
                        return ExternalEditType.None;
                    }

                    // A reference nested inside a node, like a strategy a node holds, is an edit to that node.
                    if (TryGetNestedReferenceOwner(referenceId, out nodeId))
                    {
                        return ExternalEditType.Node;
                    }

                    return ExternalEditType.Structure; // Swapped out, or nested where nothing leads back to its owner.
            }
        }

        private bool TryGetNestedReferenceOwner(long referenceId, out string nodeId)
        {
            if (m_nestedReferenceOwners == null)
            {
                m_nestedReferenceOwners = new Dictionary<long, string>();

                // A fresh SerializedObject: this runs in the middle of a write the document's own may not have read back yet.
                using SerializedObject serializedGraph = new(GraphAsset);
                CollectNestedReferenceOwners(new NodeCollectionRef(serializedGraph.FindProperty(AFlowGraph.ROOT_COLLECTION_FIELD_NAME)), m_nestedReferenceOwners);
            }

            return m_nestedReferenceOwners.TryGetValue(referenceId, out nodeId) && !string.IsNullOrEmpty(nodeId);
        }

        private static void CollectNestedReferenceOwners(NodeCollectionRef collection, Dictionary<long, string> owners)
        {
            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeRef.IsMissingType)
                {
                    continue;
                }

                // The nodes of a subgraph own what they hold, so the walk skips the subgraph here and recurses into it below.
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

        private bool IsReferenceUnder(string rootPropertyPath, long referenceId)
        {
            SerializedProperty root = IsGraphAlive ? m_serializedGraph.FindProperty(rootPropertyPath) : null;
            if (root == null)
            {
                return false;
            }

            SerializedProperty iterator = root.Copy();
            SerializedProperty endProperty = root.GetEndProperty();
            bool enterChildren = true;
            while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.propertyType == SerializedPropertyType.ManagedReference && iterator.managedReferenceId == referenceId)
                {
                    return true;
                }

                enterChildren = iterator.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference;
            }

            return false;
        }

        private ExternalEditType ClassifyMetaDataEdit(string path, out string nodeId)
        {
            nodeId = null;
            if (!TrySplitManagedReference(path, out long referenceId, out string localPath))
            {
                return path.StartsWith(METADATA_ROOT_PREFIX, StringComparison.Ordinal)
                    ? ClassifyCollectionMetaDataEdit(path.Substring(METADATA_ROOT_PREFIX.Length))
                    : ExternalEditType.None;
            }

            if (localPath.Length == 0)
            {
                return ExternalEditType.Structure; // The reference itself changed.
            }

            switch (ManagedReferenceUtility.GetManagedReference(m_metaData, referenceId))
            {
                case SubGraphNodeMetaData when localPath.StartsWith(METADATA_SUBGRAPH_PREFIX, StringComparison.Ordinal):
                    return ClassifyCollectionMetaDataEdit(localPath.Substring(METADATA_SUBGRAPH_PREFIX.Length));
                case NodeMetaData when localPath == NodeMetaData.NodeIdFieldName:
                    return ExternalEditType.Structure; // Re-keyed to another node, or to none.
                case NodeMetaData nodeMetaData:
                    nodeId = nodeMetaData.NodeId;
                    return ExternalEditType.MetaData;
                default:
                    return ExternalEditType.Structure; // Swapped out.
            }
        }

        // Groups are plain fields, so their edits arrive by path rather than by reference. Anything else is the node list itself.
        private static ExternalEditType ClassifyCollectionMetaDataEdit(string collectionPath)
        {
            return collectionPath.StartsWith(METADATA_GROUPS_PREFIX, StringComparison.Ordinal) ? ExternalEditType.Groups : ExternalEditType.Structure;
        }

        private static bool TrySplitManagedReference(string path, out long referenceId, out string localPath)
        {
            referenceId = 0;
            localPath = null;
            if (!path.StartsWith(MANAGED_REFERENCES_PREFIX, StringComparison.Ordinal))
            {
                return false;
            }

            int close = path.IndexOf(']', MANAGED_REFERENCES_PREFIX.Length);
            if (close < 0 || !long.TryParse(path.Substring(MANAGED_REFERENCES_PREFIX.Length, close - MANAGED_REFERENCES_PREFIX.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out referenceId))
            {
                return false;
            }

            localPath = close + 2 <= path.Length ? path.Substring(close + 2) : string.Empty; // Skips "].".
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
                default: // Structural, or a node with no id to report it by.
                    m_externalStructureEdited = true;
                    break;
            }

            if (!m_isExternalEditProcessingScheduled)
            {
                m_isExternalEditProcessingScheduled = true;
                EditorApplication.delayCall += ProcessExternalEdits;
            }
        }

        private void ProcessExternalEdits()
        {
            m_isExternalEditProcessingScheduled = false;

            bool structureEdited = m_externalStructureEdited;
            bool groupsEdited = m_externalGroupsEdited;
            bool blackboardEdited = m_externalBlackboardEdited;
            List<string> nodeEdits = new(m_externalNodeEdits);
            List<string> metaDataEdits = new(m_externalMetaDataEdits);
            ClearExternalEdits(); // Before anything runs: the command's opening flush can record new ones, which schedule their own pass.

            if (!IsGraphAlive)
            {
                return;
            }

            if (structureEdited)
            {
                // Like an undo, this can leave anything stale, the subgraph a window is inside of included. Validation repairs
                // whatever the other edits would have.
                RefreshSerializedObjects();
                ValidateAll();
                m_workingChangeSet.Clear();
                OnFlowGraphReloaded?.Invoke();
                return;
            }

            if (nodeEdits.Count > 0 || metaDataEdits.Count > 0 || groupsEdited || blackboardEdited)
            {
                // No undo name, so its writes join the current undo step instead of opening their own.
                ExecuteCommands(new[] { new ApplyExternalEditsCommand(nodeEdits, metaDataEdits, groupsEdited, blackboardEdited) }, null, null);
            }
        }

        /// <summary>
        /// A reimport rewrote the asset from disk, which no undo record reports, so it goes through the structural path.
        /// </summary>
        internal void HandleReimported()
        {
            if (!IsGraphAlive)
            {
                return;
            }

            // Forced rather than left to RefreshSerializedObjects, which only updates when it sees the object as modified.
            m_serializedGraph.Update();

            // A reimport can add or replace the meta data sub-asset. Holding on to the old one would let the next command create a second.
            FlowGraphMetaData metaData = GetMetaDataSubAsset(AssetDatabase.GetAssetPath(GraphAsset));
            if (metaData != m_metaData)
            {
                m_metaData = metaData;
                m_serializedMetaData = metaData != null ? new SerializedObject(metaData) : null;
            }
            else if (IsMetaDataAlive)
            {
                m_serializedMetaData.Update();
            }

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
