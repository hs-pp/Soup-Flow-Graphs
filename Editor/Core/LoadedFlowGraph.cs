using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Runtime;
using Soup.Runtime.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core
{
    public partial class LoadedFlowGraph
    {
        private string m_assetGuid;
        public string AssetGuid => m_assetGuid;

        private AFlowGraph m_graphAsset;
        public AFlowGraph GraphAsset => m_graphAsset;
        
        private SerializedObject m_serializedGraph;
        private FlowGraphMetaData m_metaData;
        private SerializedObject m_serializedMetaData;
        
        private GraphChangeSet m_workingChangeSet = new();
        public event Action<GraphChangeSet> OnFlowGraphChanged;
        public event Action OnReloaded;

        private bool m_isNotifyingGraphChanged;
        private bool m_isExecutingCommands; // While set, every undoable write to this graph is a command's own, not an external edit.

        public bool IsGraphAlive => m_graphAsset != null;
        public bool IsMetaDataAlive => m_metaData != null;

        public static bool TryLoad(string assetGuid, out LoadedFlowGraph loadedFlowGraph)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuid);
            AFlowGraph graph = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AFlowGraph>(path);
            if (graph == null)
            {
                loadedFlowGraph = null;
                return false;
            }
            loadedFlowGraph = new LoadedFlowGraph(assetGuid, graph, path);
            return true;
        }
        
        private LoadedFlowGraph(string assetGuid, AFlowGraph graph, string assetPath)
        {
            m_assetGuid = assetGuid;
            m_graphAsset = graph;
            m_serializedGraph = new SerializedObject(graph);

            m_metaData = GetMetaDataSubAsset(assetPath);
            m_serializedMetaData = m_metaData != null ? new SerializedObject(m_metaData) : null;
            
            ValidateOnLoad();

            Undo.undoRedoPerformed += HandleUndoRedoPerformed;
            Undo.postprocessModifications += HandlePostprocessModifications;
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
            Undo.postprocessModifications -= HandlePostprocessModifications;
            EditorApplication.delayCall -= ProcessExternalEdits;
        }

        private void ValidateOnLoad()
        {
            if (!TryGetRootCollection(out NodeCollectionRef rootCollection))
            {
                return;
            }

            FlowGraphValidator.EdgeValidationReport edgeReport = FlowGraphValidator.ValidateAllEdges(rootCollection);
            if (edgeReport.Changed)
            {
                ApplyGraphChangesWithoutUndo();
            }

            FlowGraphValidator.LogReport(edgeReport);

            if (TryGetRootCollectionMetaData(out NodeCollectionMetaDataRef rootCollectionMetaData))
            {
                FlowGraphValidator.MetaDataValidationReport metaDataReport = FlowGraphValidator.ValidateFlowGraphMetaData(rootCollectionMetaData, rootCollection);
                if (metaDataReport.Changed)
                {
                    ApplyMetaDataChangesWithoutUndo();
                }

                FlowGraphValidator.LogReport(metaDataReport);
            }
        }

        #region Commands
        public GraphChangeSet Execute(IGraphCommand command, IReadOnlyList<string> subGraphPath)
        {
            if (command == null)
            {
                return null;
            }

            return ExecuteCommands(command.IsUndoable ? command.GetUndoName() : null, new[] { command }, subGraphPath);
        }

        public GraphChangeSet ExecuteBatch(IReadOnlyList<IGraphCommand> commands, IReadOnlyList<string> subGraphPath, string undoName = "FlowGraph Edit")
        {
            return ExecuteCommands(undoName, commands, subGraphPath);
        }

        private GraphChangeSet ExecuteCommands(string undoName, IReadOnlyList<IGraphCommand> commands, IReadOnlyList<string> subGraphPath)
        {
            if (commands == null || commands.Count == 0)
            {
                return null;
            }
            
            if (m_isNotifyingGraphChanged) // Avoid any callbacks trying to make another change. Unlikely but good protection.
            {
                Debug.LogError("[Soup] LoadedFlowGraph: An execute was started from inside an OnFlowGraphChanged subscriber. Subscribers must not modify the graph.");
                return null;
            }

            // Unity holds back array resizes and reference swaps until its next undo flush. One a binding left pending is still an
            // external edit, so it has to come out before this command's writes start being ignored.
            Undo.FlushUndoRecordObjects();

            bool succeeded = false;
            m_isExecutingCommands = true;
            try
            {
                RefreshSerializedObjects();

                // Begin edit
                int undoGroup = -1;
                if (undoName != null)
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName(undoName);
                    undoGroup = Undo.GetCurrentGroup();
                }

                // Apply commands
                try
                {
                    foreach (IGraphCommand command in commands)
                    {
                        if (command == null)
                        {
                            continue;
                        }

                        command.ExecuteCommand(new CommandScope(this, subGraphPath)); // Always give a new CommandScope.
                    }

                    succeeded = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    // The edit may have applied writes it never recorded so every window rebuilds and try to recover.
                    m_workingChangeSet.TopologyDirty = true;
                    succeeded = false;
                }

                if (succeeded)
                {
                    ApplyGraphChanges();
                    ApplyMetaDataChanges();
                }
                else
                {
                    DiscardPendingChanges();
                }

                // This command's own held-back writes come out while they're still ignored. Failed commands too: they apply partway through.
                Undo.FlushUndoRecordObjects();

                if (undoGroup >= 0)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }
            }
            finally
            {
                m_isExecutingCommands = false; // Left set, it would silently drop every external edit to this graph.
            }

            GraphChangeSet result = succeeded ? m_workingChangeSet.Copy() : null; // Copy before the broadcast clears it.
            NotifyIfDirty();
            return result;
        }
        #endregion

        #region SerializedObject plumbing
        private void RefreshSerializedObjects()
        {
            if (IsGraphAlive)
            {
                m_serializedGraph.UpdateIfRequiredOrScript();
            }

            if (IsMetaDataAlive)
            {
                m_serializedMetaData.UpdateIfRequiredOrScript();
            }
        }

        private void DiscardPendingChanges()
        {
            if (IsGraphAlive)
            {
                m_serializedGraph.Update();
            }

            if (IsMetaDataAlive)
            {
                m_serializedMetaData.Update();
            }
        }

        private void NotifyIfDirty()
        {
            m_isNotifyingGraphChanged = true;
            if (m_workingChangeSet.HasChanges)
            {
                try
                {
                    OnFlowGraphChanged?.Invoke(m_workingChangeSet);
                } catch(Exception e) { Debug.LogException(e); }
            }
            m_workingChangeSet.Clear();
            m_isNotifyingGraphChanged = false;
        }

        private void HandleUndoRedoPerformed()
        {
            ClearExternalEdits(); // An undo flushes held-back writes just before it runs, and the reload covers them.
            RefreshSerializedObjects();
            m_workingChangeSet.Clear();
            OnReloaded?.Invoke();
        }

        private bool ApplyGraphChanges() => IsGraphAlive && m_serializedGraph.ApplyModifiedProperties();
        private bool ApplyGraphChangesWithoutUndo() => IsGraphAlive && m_serializedGraph.ApplyModifiedPropertiesWithoutUndo();
        private bool ApplyMetaDataChanges() => IsMetaDataAlive && m_serializedMetaData.ApplyModifiedProperties();
        private bool ApplyMetaDataChangesWithoutUndo() => IsMetaDataAlive && m_serializedMetaData.ApplyModifiedPropertiesWithoutUndo();
        #endregion

        #region NodeCollections
        public bool TryGetRootCollection(out NodeCollectionRef collection)
        {
            collection = IsGraphAlive ? new NodeCollectionRef(m_serializedGraph.FindProperty(AFlowGraph.ROOT_COLLECTION_FIELD_NAME)) : default;
            return collection.IsValid;
        }

        public bool TryGetCollection(IReadOnlyList<string> subGraphPath, out NodeCollectionRef collection) => TryGetCollection(subGraphPath, out collection, out _);
        public bool TryGetCollection(IReadOnlyList<string> subGraphPath, out NodeCollectionRef collection, out int resolvedDepth)
        {
            resolvedDepth = 0;
            if (!TryGetRootCollection(out collection))
            {
                return false;
            }

            if (subGraphPath == null)
            {
                return true;
            }

            NodeCollectionRef current = collection;
            for (int depth = 0; depth < subGraphPath.Count; depth++)
            {
                if (!current.TryGetNode(subGraphPath[depth], out NodeRef nodeRef) || !nodeRef.TryGetSubGraphCollection(out NodeCollectionRef next))
                {
                    collection = current;
                    return false;
                }

                current = next;
                resolvedDepth = depth + 1;
            }

            collection = current;
            return true;
        }
        #endregion

        #region GraphProperties
        public bool TryGetGraphProperties(out SerializedProperty graphProperties)
        {
            graphProperties = IsGraphAlive ? m_serializedGraph.FindProperty(AFlowGraph.GRAPH_PROPERTIES_FIELD_NAME) : null;
            return graphProperties != null;
        }
        #endregion

        #region Blackboard
        public bool TryGetBlackboard(out BlackboardRef blackboard)
        {
            blackboard = IsGraphAlive ? BlackboardRef.Find(m_serializedGraph) : default;
            return blackboard.IsValid;
        }
        #endregion

        #region MetaData
        private static FlowGraphMetaData GetMetaDataSubAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is FlowGraphMetaData metaData)
                {
                    return metaData;
                }
            }

            return null;
        }

        private bool EnsureMetaDataSubAssetExists()
        {
            if (IsMetaDataAlive)
            {
                return true;
            }

            if (!IsGraphAlive)
            {
                return false;
            }

            // AddObjectToAsset needs a real asset to attach to. Nothing can be created without one.
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m_graphAsset)))
            {
                return false;
            }

            m_metaData = ScriptableObject.CreateInstance<FlowGraphMetaData>();
            m_metaData.name = nameof(FlowGraphMetaData);
            m_metaData.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild; // May not be necessary as long as there's no references to it but just in case.

            AssetDatabase.AddObjectToAsset(m_metaData, m_graphAsset);
            EditorUtility.SetDirty(m_graphAsset);

            m_serializedMetaData = new SerializedObject(m_metaData);
            return true;
        }
        #endregion

        #region NodeCollectionMetaData
        private bool TryGetRootCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
        {
            collectionMetaData = IsMetaDataAlive ? new NodeCollectionMetaDataRef(m_serializedMetaData.FindProperty(FlowGraphMetaData.RootCollectionFieldName)) : default;
            return collectionMetaData.IsValid;
        }

        public bool TryGetCollectionMetaData(IReadOnlyList<string> subGraphPath, out NodeCollectionMetaDataRef collectionMetaData)
        {
            if (!TryGetRootCollectionMetaData(out collectionMetaData))
            {
                return false;
            }

            if (subGraphPath == null)
            {
                return true;
            }

            foreach (string subGraphNodeId in subGraphPath)
            {
                if (!collectionMetaData.TryGetNodeMetaDataRef(subGraphNodeId, out NodeMetaDataRef nodeMetaData) || !nodeMetaData.TryGetSubGraphCollectionMetaData(out collectionMetaData))
                {
                    collectionMetaData = default;
                    return false;
                }
            }

            return true;
        }
        #endregion
    }
}