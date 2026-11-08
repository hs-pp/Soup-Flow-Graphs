using System;
using System.Collections.Generic;
using Soup.Editor.Core;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.State;
using Soup.Editor.Views.Breadcrumbs;
using Soup.Editor.Views.Extraneous;
using Soup.Editor.Views.Inspector;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public class SoupEditorView : VisualElement, IDisposable
    {
        private const string UXML_PATH = "Soup/SoupEditorView";
        private const string SPLITVIEW_TAG = "splitview";
        private const string LEFT_PANEL_TAG = "main-panel-left";
        private const string RIGHT_PANEL_TAG = "main-panel-right";
        private const string TOOLBAR_TAG = "toolbar";
        
        private TwoPaneSplitView m_splitView;
        private VisualElement m_leftPanel;
        private VisualElement m_rightPanel;
        private Toolbar m_toolbar;
        private ToolbarToggle m_snapToGridToggle;
        private ToolbarToggle m_miniMapToggle;

        private GraphViewObserver m_observer;
        private GraphView m_graphView;
        private InspectorView m_inspector;
        private BreadcrumbsView m_breadcrumbsView;
        
        private LoadedFlowGraph m_loadedFlowGraph;
        private GraphEditorState m_state;
        
        public SoupEditorView(GraphEditorState state)
        {
            m_state = state;
            CreateLayout();
            m_splitView.fixedPaneInitialDimension = m_state.InspectorState.InspectorSize;

            m_state.OnGraphLoaded += HandleGraphLoaded;
            m_state.OnNavigationChanged += HandleNavigationChanged;

            m_graphView = new GraphView();
            m_graphView.style.flexGrow = 1;
            m_leftPanel.Add(m_graphView);
            m_leftPanel.Add(new PanelEdgeShadowElement(PanelEdgeShadowElement.Edge.Top));
            m_leftPanel.Add(new PanelEdgeShadowElement(PanelEdgeShadowElement.Edge.Right));
            m_leftPanel.Add(new PanelEdgeShadowElement(PanelEdgeShadowElement.Edge.Bottom));
            m_leftPanel.Add(new PanelEdgeShadowElement(PanelEdgeShadowElement.Edge.Left));
            m_graphView.OnCommandRequested += HandleCommandRequested;
            m_graphView.OnCommandBatchRequested += HandleCommandBatchRequested;
            m_graphView.OnViewTransformChanged += _ => SaveViewTransformState();
            m_graphView.MiniMapVisible = m_state.MiniMapIsVisible;
            m_graphView.OnMiniMapVisibilityChanged += HandleMiniMapVisibilityChanged;
            m_miniMapToggle.SetValueWithoutNotify(m_graphView.MiniMapVisible);
            m_miniMapToggle.RegisterValueChangedCallback(HandleMiniMapToggleChanged);
            m_graphView.SnapToGrid = m_state.SnapToGridIsEnabled;
            m_graphView.OnSnapToGridChanged += HandleSnapToGridChanged;
            m_snapToGridToggle.SetValueWithoutNotify(m_graphView.SnapToGrid);
            m_snapToGridToggle.RegisterValueChangedCallback(HandleSnapToGridToggleChanged);

            m_observer = new GraphViewObserver(m_state, m_graphView);
            m_observer.OnFlowGraphReloaded += m_inspector.Refresh;

            m_inspector.SetState(m_state);
            m_inspector.OnCommandRequested += HandleCommandRequested;
            m_rightPanel.RegisterCallback<GeometryChangedEvent>(evt => m_state.InspectorState.InspectorSize = evt.newRect.width);
            
            if (!string.IsNullOrEmpty(m_state.AssetGuid))
            {
                LoadGraph(m_state.AssetGuid);
            }
        }
        
        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_splitView = this.Q<TwoPaneSplitView>();
            m_leftPanel = this.Q<VisualElement>(LEFT_PANEL_TAG);
            m_rightPanel = this.Q<VisualElement>(RIGHT_PANEL_TAG);
            m_toolbar =  this.Q<Toolbar>(TOOLBAR_TAG);
            
            m_inspector = new InspectorView();
            m_rightPanel.Add(m_inspector);

            m_breadcrumbsView = new BreadcrumbsView(HandleBreadcrumbPressed);
            m_toolbar.Add(m_breadcrumbsView);

            m_toolbar.Add(new ToolbarSpacer { flex = true });
            m_snapToGridToggle = new ToolbarToggle { text = "Snap to Grid", tooltip = "Snap nodes and groups to the grid" };
            m_toolbar.Add(m_snapToGridToggle);
            m_miniMapToggle = new ToolbarToggle { text = "Show MiniMap", tooltip = GraphShortcuts.WithHint("Show MiniMap", GraphShortcuts.TOGGLE_MINIMAP) };
            m_toolbar.Add(m_miniMapToggle);
        }
        
        public void Dispose()
        {
            if (m_observer == null)
            {
                return;
            }

            SaveViewTransformState();

            m_state.OnGraphLoaded -= HandleGraphLoaded;
            m_state.OnNavigationChanged -= HandleNavigationChanged;
            m_inspector.OnCommandRequested -= HandleCommandRequested;
            m_inspector.Dispose();

            m_observer.OnFlowGraphReloaded -= m_inspector.Refresh;
            m_observer.Dispose();
            m_observer = null;

            m_miniMapToggle.UnregisterValueChangedCallback(HandleMiniMapToggleChanged);
            m_graphView.OnMiniMapVisibilityChanged -= HandleMiniMapVisibilityChanged;
            m_snapToGridToggle.UnregisterValueChangedCallback(HandleSnapToGridToggleChanged);
            m_graphView.OnSnapToGridChanged -= HandleSnapToGridChanged;
            m_leftPanel.Remove(m_graphView);
            m_graphView = null;

            if (m_loadedFlowGraph != null)
            {
                SoupEditorCore.ReleaseLoadedGraph(m_loadedFlowGraph.AssetGuid, this);
            }
            m_loadedFlowGraph = null;
        }
        
        public void LoadGraph(string assetGuid)
        {
            SetLoadedFlowGraph(SoupEditorCore.RequestLoadedGraph(assetGuid, this));
        }

        internal void UnloadGraph()
        {
            SetLoadedFlowGraph(null);
        }

        private void SetLoadedFlowGraph(LoadedFlowGraph loadedFlowGraph)
        {
            LoadedFlowGraph previous = m_loadedFlowGraph;
            m_loadedFlowGraph = loadedFlowGraph;

            m_graphView.GraphType = loadedFlowGraph != null && loadedFlowGraph.IsGraphAlive ? loadedFlowGraph.GraphAsset.GetType() : null;
            m_observer.SetLoadedFlowGraph(loadedFlowGraph);
            m_inspector.SetLoadedFlowGraph(loadedFlowGraph);
            m_state.Load(loadedFlowGraph);
            LoadViewTransformState();

            if (previous != null && previous != loadedFlowGraph)
            {
                SoupEditorCore.ReleaseLoadedGraph(previous.AssetGuid, this);
            }
        }

        private void HandleCommandRequested(ICommand command)
        {
            if (m_loadedFlowGraph == null)
            {
                return;
            }

            switch (command)
            {
                case IWindowCommand windowCommand:
                    windowCommand.ExecuteCommand(m_state, m_loadedFlowGraph);
                    break;
                case IGraphCommand graphCommand:
                    ApplyEditResult(m_loadedFlowGraph.Execute(graphCommand, m_state.SubGraphPath));
                    break;
            }
        }

        private void HandleCommandBatchRequested(IReadOnlyList<IGraphCommand> commands, string undoName)
        {
            if (m_loadedFlowGraph == null)
            {
                return;
            }

            ApplyEditResult(m_loadedFlowGraph.ExecuteBatch(commands, m_state.SubGraphPath, undoName));
        }

        private void ApplyEditResult(GraphChangeSet result)
        {
            if (result == null || (result.AddedNodes.Count == 0 && result.AddedGroups.Count == 0))
            {
                return;
            }

            List<string> added = new(result.AddedNodes);
            added.AddRange(result.AddedGroups);
            m_state.ApplySelection(GraphSelectionMode.Replace, added);
        }

        private void HandleGraphLoaded()
        {
            RefreshBreadcrumbs();
        }

        private void HandleNavigationChanged()
        {
            LoadViewTransformState();
            RefreshBreadcrumbs();
        }

        private void RefreshBreadcrumbs()
        {
            m_breadcrumbsView.SetPath(m_state.GetSubGraphPathTitles(m_loadedFlowGraph));
        }

        private void HandleBreadcrumbPressed(int depth)
        {
            HandleCommandRequested(new NavigateToSubGraphPathCommand(depth));
        }

        // The M shortcut and the canvas/minimap context menus change visibility on the GraphView, so the toggle follows it.
        private void HandleMiniMapVisibilityChanged(bool visible)
        {
            m_state.MiniMapIsVisible = visible;
            m_miniMapToggle.SetValueWithoutNotify(visible);
        }

        private void HandleMiniMapToggleChanged(ChangeEvent<bool> evt)
        {
            m_graphView.MiniMapVisible = evt.newValue;
        }

        // The canvas context menu also flips snapping on the GraphView, so the toggle follows it.
        private void HandleSnapToGridChanged(bool enabled)
        {
            m_state.SnapToGridIsEnabled = enabled;
            m_snapToGridToggle.SetValueWithoutNotify(enabled);
        }

        private void HandleSnapToGridToggleChanged(ChangeEvent<bool> evt)
        {
            m_graphView.SnapToGrid = evt.newValue;
        }
        
        private void RefreshTitle()
        {
            // if (m_state is { IsLoaded: true })
            // {
            //     string dirtyMarker = EditorUtility.IsDirty(m_state.GraphAsset) ? "*" : string.Empty;
            //     titleContent = new GUIContent(m_state.GraphAsset.name + dirtyMarker);
            // }
            // else
            // {
            //     titleContent = new GUIContent("Soup");
            // }
        }

        private void LoadViewTransformState()
        {
            if (m_graphView == null)
            {
                return;
            }

            if (m_state.TryGetCurrentViewTransformState(out Vector2 position, out float scale))
            {
                m_graphView.UpdateViewTransform(position, new Vector3(scale, scale, 1f));
            }
            else
            {
                m_graphView.ResetViewTransform();
            }
        }

        private void SaveViewTransformState()
        {
            if (m_graphView == null)
            {
                return;
            }

            m_state.SaveViewTransformState(m_graphView.GetViewTransformPosition(), m_graphView.CurrentScale);
        }
    }
}
