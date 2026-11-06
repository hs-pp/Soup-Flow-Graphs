using System;
using System.Collections.Generic;
using Soup.Editor.Core;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.Views.Inspector;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.State
{
    [Serializable]
    public sealed class GraphEditorState
    {
        [Serializable]
        private struct ViewTransformState
        {
            public string ViewTransformKey;
            public Vector2 Position;
            public float Scale;
        }
        
        // View transforms of Node Collections
        [SerializeField]
        private List<ViewTransformState> m_viewTransformStates = new();
        [NonSerialized]
        private string m_currentViewTransformKey;
        
        [SerializeField]
        private string m_assetGuid;
        public string AssetGuid => m_assetGuid;
        // Subgraphs
        [SerializeField]
        private List<string> m_subGraphPath = new(); // SubGraphNode ids, from the root collection down to the current one.
        public IReadOnlyList<string> SubGraphPath => m_subGraphPath;
        
        [SerializeField]
        private GraphSelectionState m_graphSelection = new();
        public GraphSelectionState GraphSelection => m_graphSelection;

        [SerializeField]
        private InspectorState m_inspectorState = new();
        public InspectorState InspectorState => m_inspectorState;

        [SerializeField]
        private bool m_miniMapIsVisible = true;
        public bool MiniMapIsVisible
        {
            get => m_miniMapIsVisible;
            set => m_miniMapIsVisible = value;
        }

        [SerializeField]
        private bool m_snapToGridIsEnabled = true;
        public bool SnapToGridIsEnabled
        {
            get => m_snapToGridIsEnabled;
            set => m_snapToGridIsEnabled = value;
        }
        
        public event Action OnGraphLoaded;
        public event Action OnNavigationChanged;
        public event Action OnSelectionChanged;

        public GraphEditorState() { }

        public GraphEditorState(string assetGuid)
        {
            m_assetGuid = assetGuid;
        }
        
        public void Load(LoadedFlowGraph loadedFlowGraph)
        {
            string assetGuid = loadedFlowGraph?.AssetGuid;
            bool isSameAsset = !string.IsNullOrEmpty(assetGuid) && assetGuid == m_assetGuid;
            m_assetGuid = assetGuid;

            if (!isSameAsset)
            {
                m_subGraphPath.Clear();
                m_viewTransformStates.Clear();
                m_graphSelection.Clear(); // No OnSelectionChanged: OnGraphLoaded below makes the observer reconcile, which reapplies selection.
            }

            PruneSubGraphPath(loadedFlowGraph);
            
            m_currentViewTransformKey = ComputeViewKey(); // This needs to happen after pruning the subgraph path.

            OnGraphLoaded?.Invoke();
        }

        public bool ApplySelection(GraphSelectionMode mode, IReadOnlyList<string> ids)
        {
            if (!m_graphSelection.Apply(mode, ids))
            {
                return false;
            }

            OnSelectionChanged?.Invoke();
            return true;
        }
        
        #region ViewTransformState
        private string ComputeViewKey()
        {
            if (string.IsNullOrEmpty(m_assetGuid))
            {
                return null;
            }

            return m_subGraphPath.Count == 0 ? m_assetGuid : m_assetGuid + ":" + string.Join("/", m_subGraphPath);
        }
        
        public void SaveViewTransformState(Vector2 position, float scale)
        {
            if (string.IsNullOrEmpty(m_currentViewTransformKey))
            {
                return;
            }

            ViewTransformState entry = new()
            {
                ViewTransformKey = m_currentViewTransformKey,
                Position = position,
                Scale = scale,
            };

            int index = IndexOfCurrentViewTransformState();
            if (index < 0)
            {
                m_viewTransformStates.Add(entry);
            }
            else
            {
                m_viewTransformStates[index] = entry;
            }
        }

        public bool TryGetCurrentViewTransformState(out Vector2 position, out float scale)
        {
            position = Vector2.zero;
            scale = 1f;

            int index = IndexOfCurrentViewTransformState();
            if (index < 0)
            {
                return false;
            }

            position = m_viewTransformStates[index].Position;
            scale = m_viewTransformStates[index].Scale;
            return true;
        }

        private int IndexOfCurrentViewTransformState()
        {
            if (string.IsNullOrEmpty(m_currentViewTransformKey))
            {
                return -1;
            }

            for (int i = 0; i < m_viewTransformStates.Count; i++)
            {
                if (m_viewTransformStates[i].ViewTransformKey == m_currentViewTransformKey)
                {
                    return i;
                }
            }

            return -1;
        }
        #endregion

        #region SubGraph path navigation

        public bool TryGetCurrentCollection(LoadedFlowGraph loadedFlowGraph, out NodeCollectionRef collection)
        {
            if (loadedFlowGraph == null)
            {
                collection = default;
                return false;
            }

            return loadedFlowGraph.TryGetCollection(m_subGraphPath, out collection);
        }

        public bool EnterSubGraph(string subGraphNodeId, LoadedFlowGraph loadedFlowGraph)
        {
            if (!TryGetCurrentCollection(loadedFlowGraph, out NodeCollectionRef collection)
                || !collection.TryGetNode(subGraphNodeId, out NodeRef nodeRef)
                || !nodeRef.TryGetSubGraphCollection(out _))
            {
                return false;
            }

            m_subGraphPath.Add(subGraphNodeId);
            AfterNavigation();
            return true;
        }

        // depth 0 is the root collection.
        public void NavigateToDepth(int depth)
        {
            depth = Mathf.Clamp(depth, 0, m_subGraphPath.Count);
            if (depth == m_subGraphPath.Count)
            {
                return;
            }

            m_subGraphPath.RemoveRange(depth, m_subGraphPath.Count - depth);
            AfterNavigation();
        }
        
        public bool ValidateSubGraphPath(LoadedFlowGraph loadedFlowGraph)
        {
            if (!PruneSubGraphPath(loadedFlowGraph))
            {
                return false;
            }

            AfterNavigation();
            return true;
        }
        
        private bool PruneSubGraphPath(LoadedFlowGraph loadedFlowGraph)
        {
            if (loadedFlowGraph == null)
            {
                return false;
            }

            if (loadedFlowGraph.TryGetCollection(m_subGraphPath, out _, out int resolvedDepth))
            {
                return false;
            }

            int removeCount = m_subGraphPath.Count - resolvedDepth;
            if (removeCount <= 0)
            {
                return false;
            }

            m_subGraphPath.RemoveRange(resolvedDepth, removeCount);
            return true;
        }

        public List<string> GetSubGraphPathTitles(LoadedFlowGraph loadedFlowGraph)
        {
            List<string> titles = new();
            if (loadedFlowGraph == null || !loadedFlowGraph.TryGetRootCollection(out NodeCollectionRef collection))
            {
                return titles;
            }
            
            titles.Add(loadedFlowGraph.GraphAsset.name);
            
            foreach (string id in m_subGraphPath)
            {
                if (!collection.TryGetNode(id, out NodeRef nodeRef)
                    || !nodeRef.TryGetSubGraphCollection(out NodeCollectionRef next))
                {
                    break;
                }

                Type nodeType = nodeRef.GetNodeType();
                titles.Add(nodeType != null ? EditorTypesFactory.GetNodeDisplayName(nodeType) : ObjectNames.NicifyVariableName(nameof(SubGraphPath)));
                collection = next;
            }

            return titles;
        }

        private void AfterNavigation()
        {
            m_graphSelection.Clear();
            m_currentViewTransformKey = ComputeViewKey();

            OnNavigationChanged?.Invoke();
        }
        #endregion
    }
}
