using System;
using System.Collections.Generic;
using System.Linq;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.State;
using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Extraneous;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Manipulators;
using Soup.Editor.Views.Nodes;
using Soup.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public class GraphViewLayer : VisualElement
    {
        public GraphViewLayer() => pickingMode = PickingMode.Ignore;
    }
    
    internal struct PendingPortConnection
    {
        public string NodeId { get; }
        public string OutportId { get; }
        public bool AnchoredIsOutput { get; }

        public PendingPortConnection(string nodeId, string outportId, bool anchoredIsOutput)
        {
            NodeId = nodeId;
            OutportId = outportId;
            AnchoredIsOutput = anchoredIsOutput;
        }
    }
    
    public class GraphView : VisualElement
    {
        public const float SNAP_STEP = 10f;

        private const string STYLESHEET_PATH = "Soup/GraphView";
        private const string STYLECLASS_NAME = "graphView";
        private const float GROUP_PADDING = 30f;
        private const float FRAME_BORDER = 30f;

        // GraphView
        public GraphElementContainer ContentContainer { get; }
        private Dictionary<int, GraphViewLayer> m_layers = new();
        private Vector2 m_mousePosition;

        public Type GraphType { get; set; }

        // Commands
        public event Action<ICommand> OnCommandRequested;
        public event Action<IReadOnlyList<IGraphCommand>, string> OnCommandBatchRequested;
        
        // View Transform
        public event Action OnViewTransformChanged;

        // Content
        public event Action OnContentChanged;

        // MiniMap
        private MiniMapElement m_miniMapElement;
        public event Action<bool> OnMiniMapVisibilityChanged;

        // Snapping
        private bool m_snapToGrid = true;
        public event Action<bool> OnSnapToGridChanged;

        // Zoom
        private ZoomManipulator m_zoomer = new();

        // Drag and Drop
        private DragAndDropManipulator m_dragAndDrop;
        private ElementDragManipulator m_elementDrags;
        private bool m_draggingView;
        private bool m_draggingMarquee;
        
        // Marquee (Selection box)
        private MarqueeElement m_marqueeElement = new();

        public GraphView()
        {
            // Setup visuals
            StyleSheet styleSheet = Resources.Load<StyleSheet>(STYLESHEET_PATH);
            styleSheets.Add(styleSheet);
            AddToClassList(STYLECLASS_NAME);
            usageHints = UsageHints.MaskContainer;

            GridBackgroundElement gridBackgroundElement = new(this);
            hierarchy.Add(gridBackgroundElement);

            ContentContainer = new GraphElementContainer();
            gridBackgroundElement.Add(ContentContainer);

            m_miniMapElement = new MiniMapElement(this);
            hierarchy.Add(m_miniMapElement);

            // Manipulators
            this.AddManipulator(m_zoomer);
            m_dragAndDrop = new DragAndDropManipulator();
            this.AddManipulator(m_dragAndDrop);
            m_elementDrags = new ElementDragManipulator(this);
            ContentContainer.AddManipulator(m_elementDrags);
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            focusable = true; // Shortcuts only reach the graph while it holds focus.
            GraphShortcutContext.EnsureRegistered();
        }
        
        #region GraphViewLayers
        private VisualElement GetViewLayer(int index)
        {
            if (!m_layers.TryGetValue(index, out GraphViewLayer layer))
            {
                layer = new() { name = $"Layer {index}" };
                ContentContainer.Insert(m_layers.Keys.Count(key => key < index), layer);
                m_layers[index] = layer;
            }

            return layer;
        }

        internal void SortViewLayer(int index, Comparison<VisualElement> comparison)
        {
            if (m_layers.TryGetValue(index, out GraphViewLayer layer))
            {
                layer.Sort(comparison);
            }
        }
        #endregion
        
        #region Commands
        public void RequestCommand(ICommand command)
        {
            OnCommandRequested?.Invoke(command);
        }

        public void RequestCommandBatch(string undoName, params IGraphCommand[] commands)
        {
            OnCommandBatchRequested?.Invoke(commands, undoName);
        }
        
        internal static List<string> CollectIds<T>(IEnumerable<T> elements, Func<T, bool> include) where T : GraphElement
        {
            List<string> ids = new();
            foreach (T element in elements)
            {
                if (element.Id != null && include(element))
                {
                    ids.Add(element.Id);
                }
            }

            return ids;
        }

        private bool CollectCopiableSelection(out List<string> nodeIds, out List<string> groupIds)
        {
            nodeIds = CollectIds(ContentContainer.NodesSelected, node => node.IsCopiable());
            groupIds = CollectIds(ContentContainer.GroupsSelected, group => group.IsCopiable());
            return nodeIds.Count > 0 || groupIds.Count > 0;
        }

        private bool CollectDeletableSelection(out List<string> nodeIds, out List<EdgeId> edges, out List<string> groupIds)
        {
            nodeIds = CollectIds(ContentContainer.NodesSelected, node => node.IsDeletable());
            groupIds = CollectIds(ContentContainer.GroupsSelected, group => group.IsDeletable());
            edges = new List<EdgeId>();
            foreach (EdgeView edge in ContentContainer.EdgesSelected)
            {
                if (edge.HasModelIdentity && edge.IsDeletable())
                {
                    edges.Add(new EdgeId(edge.FromNodeId, edge.OutportId));
                }
            }

            return nodeIds.Count > 0 || edges.Count > 0 || groupIds.Count > 0;
        }

        private void SelectAll()
        {
            RequestCommand(new SelectGraphElementsCommand(CollectIds(ContentContainer.ElementsAll, element => element.IsSelectable()), GraphSelectionMode.Replace));
        }

        private void CopySelection()
        {
            if (CollectCopiableSelection(out List<string> nodeIds, out List<string> groupIds))
            {
                RequestCommand(new CopyGraphElementsCommand(nodeIds, groupIds));
            }
        }

        private void CutSelection()
        {
            if (!CollectCopiableSelection(out List<string> nodeIds, out List<string> groupIds))
            {
                return;
            }

            RequestCommand(new CopyGraphElementsCommand(nodeIds, groupIds)); // This is a window command.
            RequestCommandBatch("Cut", // Batch so we can override the undo name.
                new DeleteGraphElementsCommand(
                    CollectIds(ContentContainer.NodesSelected, node => node.IsCopiable() && node.IsDeletable()),
                    CollectIds(ContentContainer.GroupsSelected, group => group.IsCopiable() && group.IsDeletable())));
        }

        private void PasteFromClipboard(Vector2 panelPosition)
        {
            float snapStep = SnapToGrid ? SNAP_STEP : 0f;
            RequestCommand(new PasteFromClipboardCommand(ContentContainer.WorldToLocal(panelPosition), snapStep));
        }

        private void DuplicateSelection()
        {
            if (CollectCopiableSelection(out List<string> nodeIds, out List<string> groupIds))
            {
                RequestCommand(new DuplicateGraphElementsCommand(nodeIds, groupIds));
            }
        }

        internal void DeleteSelection()
        {
            if (CollectDeletableSelection(out List<string> nodeIds, out List<EdgeId> edges, out List<string> groupIds))
            {
                RequestCommand(new DeleteGraphElementsCommand(nodeIds, groupIds, edges));
            }
        }

        private void CreateGroup(Vector2 panelPosition)
        {
            if (!GroupSelection())
            {
                RequestCommand(new CreateGroupCommand(new Rect(SnapPosition(ContentContainer.WorldToLocal(panelPosition)), GroupMetaData.DEFAULT_SIZE)));
            }
        }

        internal bool GroupSelection()
        {
            if (!TryGetBounds(element => element.Selected && element is (BaseNodeView or GroupView), out Rect bounds))
            {
                return false;
            }

            RequestCommand(new CreateGroupCommand(RectUtils.Inflate(bounds, GROUP_PADDING, GROUP_PADDING + GroupView.TITLE_HEIGHT, GROUP_PADDING, GROUP_PADDING)));
            return true;
        }

        private bool TryGetBounds(Func<GraphElement, bool> include, out Rect bounds)
        {
            bounds = default;
            bool found = false;
            foreach (GraphElement element in ContentContainer.ElementsAll)
            {
                if (!include(element))
                {
                    continue;
                }

                Rect rect = element.CanvasRect;
                if (float.IsNaN(rect.width) || float.IsNaN(rect.height))
                {
                    continue;
                }

                bounds = found ? RectUtils.Encompass(bounds, rect) : rect;
                found = true;
            }

            return found;
        }
        
        private void CreateNode(Type nodeType, Vector2 panelPosition, PendingPortConnection? pending)
        {
            Vector2 graphPosition = SnapPosition(ContentContainer.WorldToLocal(panelPosition));

            string newNodeId = Guid.NewGuid().ToString("N");
            CreateNodeCommand createNode = new(nodeType, graphPosition, newNodeId);

            CreateEdgeCommand connectNode = null;
            if (pending.HasValue)
            {
                PendingPortConnection connection = pending.Value;
                if (connection.AnchoredIsOutput)
                {
                    connectNode = new CreateEdgeCommand(connection.NodeId, connection.OutportId, newNodeId);
                }
                else
                {
                    List<OutportDefinition> definitions = NodeUtils.GetOutportDefinitions(nodeType);
                    if (definitions != null && definitions.Count > 0)
                    {
                        connectNode = new CreateEdgeCommand(newNodeId, definitions[0].Id, connection.NodeId);
                    }
                }
            }

            if (connectNode == null)
            {
                RequestCommand(createNode);
            }
            else
            {
                RequestCommandBatch("Create and Connect Node", createNode, connectNode);
            }
        }
        #endregion

        #region View Transform
        public void SetViewTransform(Vector2 position, float scale)
        {
            // Make sure we dont accidentally override this change later.
            UnregisterCallback<GeometryChangedEvent>(OnFirstLayoutResetViewTransform);

            if (ViewPosition == position && ViewScale == scale)
            {
                return;
            }

            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(scale))
            {
                return;
            }

            ContentContainer.style.translate = new Translate(position.x, position.y);
            ContentContainer.style.scale = new Vector3(scale, scale, 1f);

            OnViewTransformChanged?.Invoke();
        }

        public Vector2 ViewPosition
        {
            get
            {
                Translate translate = ContentContainer.style.translate.value;
                return new Vector2(translate.x.value, translate.y.value);
            }
        }

        public float ViewScale => ContentContainer.style.scale.value.value.x;

        public void ResetViewTransform()
        {
            Vector2 size = layout.size;
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x <= 0f || size.y <= 0f)
            {
                RegisterCallback<GeometryChangedEvent>(OnFirstLayoutResetViewTransform);
                return;
            }

            SetViewTransform(size * 0.3f, 1f);
        }

        private void OnFirstLayoutResetViewTransform(GeometryChangedEvent evt)
        {
            UnregisterCallback<GeometryChangedEvent>(OnFirstLayoutResetViewTransform);
            ResetViewTransform();
        }
        #endregion

        #region Zoom
        public float MinScale => m_zoomer.MinScale;
        public float MaxScale => m_zoomer.MaxScale;
        public float ScaleStep => m_zoomer.ScaleStep;
        public float ReferenceScale => m_zoomer.ReferenceScale;
        #endregion

        #region Snapping
        public bool SnapToGrid
        {
            get => m_snapToGrid;
            set
            {
                if (m_snapToGrid == value)
                {
                    return;
                }

                m_snapToGrid = value;
                OnSnapToGridChanged?.Invoke(value);
            }
        }

        public Vector2 SnapPosition(Vector2 position)
        {
            if (!SnapToGrid)
            {
                return position;
            }

            return new Vector2(Mathf.Round(position.x / SNAP_STEP) * SNAP_STEP, Mathf.Round(position.y / SNAP_STEP) * SNAP_STEP);
        }
        #endregion

        #region MiniMap
        public bool MiniMapVisible
        {
            get => m_miniMapElement.style.display != DisplayStyle.None;
            set
            {
                if (MiniMapVisible == value)
                {
                    return;
                }

                m_miniMapElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                OnMiniMapVisibilityChanged?.Invoke(value);
            }
        }
        #endregion

        #region Event Handlers
        [EventInterest(typeof(MouseMoveEvent), typeof(DragOfferEvent), typeof(DragEvent), typeof(DragEndEvent), typeof(DragCancelEvent), typeof(ValidateCommandEvent), typeof(ExecuteCommandEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            long typeId = evt.eventTypeId;
            if (typeId == MouseMoveEvent.TypeId())
            {
                m_mousePosition = ((MouseMoveEvent)evt).mousePosition;
            }
            else if (typeId == ValidateCommandEvent.TypeId())
            {
                if (AcceptsEditCommand(evt) && CanExecuteEditCommand(((ValidateCommandEvent)evt).commandName))
                {
                    MarkEditCommandHandled(evt);
                }
            }
            else if (typeId == ExecuteCommandEvent.TypeId())
            {
                if (AcceptsEditCommand(evt) && ExecuteEditCommand(((ExecuteCommandEvent)evt).commandName))
                {
                    MarkEditCommandHandled(evt);
                }
            }
            else if (typeId == DragOfferEvent.TypeId())
            {
                OnDragOffer((DragOfferEvent)evt);
            }
            else if (typeId == DragEvent.TypeId())
            {
                OnDrag((DragEvent)evt);
            }
            else if (typeId == DragEndEvent.TypeId())
            {
                OnDragEnd((DragEndEvent)evt);
            }
            else if (typeId == DragCancelEvent.TypeId())
            {
                OnDragCancel((DragCancelEvent)evt);
            }
        }

        public void OnDragOffer(DragOfferEvent e)
        {
            if (IsViewDrag(e))
            {
                e.AcceptDrag(this);
                e.StopImmediatePropagation();
                m_draggingView = true;

                Focus();
            }
            else if (IsMarqueeDrag(e))
            {
                e.AcceptDrag(this);
                e.StopImmediatePropagation();
                m_draggingMarquee = true;

                if (GetMarqueeSelectionMode(e.modifiers) == GraphSelectionMode.Replace)
                {
                    RequestCommand(new SelectGraphElementsCommand(null, GraphSelectionMode.Replace));
                }

                // Create marquee
                Add(m_marqueeElement);
                Vector2 position = this.WorldToLocal(e.mousePosition);
                m_marqueeElement.SetCoordinates(position, position);
                
                Focus();
            }
        }

        private void OnDrag(DragEvent e)
        {
            if (m_draggingMarquee)
            {
                e.StopImmediatePropagation();
                m_marqueeElement.SetEndPoint(this.WorldToLocal(e.mousePosition));
            }
            else if (m_draggingView)
            {
                e.StopImmediatePropagation();
                SetViewTransform(ViewPosition + e.mouseDelta, ViewScale);
            }
        }

        private void OnDragEnd(DragEndEvent e)
        {
            if (m_draggingMarquee)
            {
                // Remove marquee
                e.StopImmediatePropagation();
                m_draggingMarquee = false;
                m_marqueeElement.RemoveFromHierarchy();

                // Ignore if the rectangle is infinitely small
                Rect selectionRect = m_marqueeElement.SelectionRect;
                if (selectionRect.size == Vector2.zero)
                {
                    return;
                }

                // Select elements that overlap the marquee
                List<string> overlappingIds = new();
                foreach (GraphElement element in ContentContainer.ElementsAll)
                {
                    if (element.Id == null || !element.IsSelectable())
                    {
                        continue;
                    }

                    Rect localSelRect = this.ChangeCoordinatesTo(element, selectionRect);
                    if (element.Overlaps(localSelRect))
                    {
                        overlappingIds.Add(element.Id);
                    }
                }

                RequestCommand(new SelectGraphElementsCommand(overlappingIds, GetMarqueeSelectionMode(e.modifiers)));
            }
            else if (m_draggingView)
            {
                e.StopImmediatePropagation();
                m_draggingView = false;
            }
        }

        private void OnDragCancel(DragCancelEvent e)
        {
            if (m_draggingMarquee)
            {
                e.StopImmediatePropagation();
                m_marqueeElement.RemoveFromHierarchy();
                m_draggingMarquee = false;
            }
            else if (m_draggingView)
            {
                e.StopImmediatePropagation();
                m_draggingView = false;
            }
        }
        
        public void CancelActiveDrag()
        {
            m_elementDrags.KeepPositionsOnCancel();
            m_dragAndDrop.CancelActiveDrag();
        }

        private bool IsMarqueeDrag(DragOfferEvent e)
        {
            return (MouseButton)e.button == MouseButton.LeftMouse
                && (e.modifiers.IsNone() || e.modifiers.IsExclusiveShift() || e.modifiers.IsExclusiveActionKey());
        }

        public bool IsViewDrag(DragOfferEvent e)
        {
            return e.modifiers.HasFlag(EventModifiers.Alt)
                || ((MouseButton)e.button == MouseButton.MiddleMouse && e.modifiers.IsNone());
        }


        private static GraphSelectionMode GetMarqueeSelectionMode(EventModifiers modifiers)
        {
            bool additive = modifiers.IsShift();
            bool subtractive = modifiers.IsActionKey();
            if (additive == subtractive)
            {
                return GraphSelectionMode.Replace;
            }

            return additive ? GraphSelectionMode.Add : GraphSelectionMode.Remove;
        }
        #endregion

        #region Edit Commands
        private const string COPY_COMMAND = "Copy";
        private const string CUT_COMMAND = "Cut";
        private const string PASTE_COMMAND = "Paste";
        private const string DUPLICATE_COMMAND = "Duplicate";
        private const string SELECT_ALL_COMMAND = "SelectAll";
        private const string DELETE_COMMAND = "Delete";
        private const string SOFT_DELETE_COMMAND = "SoftDelete";
        private const string FRAME_SELECTED_COMMAND = "FrameSelected";

        internal bool IsMouseCaptured => panel?.GetCapturingElement(PointerId.mousePointerId) != null;

        private bool AcceptsEditCommand(EventBase evt) => evt.target == this && !IsMouseCaptured;

        private bool CanExecuteEditCommand(string commandName)
        {
            switch (commandName)
            {
                case COPY_COMMAND:
                case CUT_COMMAND:
                case DUPLICATE_COMMAND:
                    return CollectCopiableSelection(out _, out _);
                case PASTE_COMMAND:
                    return GraphClipboard.HasClipboardContent;
                case DELETE_COMMAND:
                case SOFT_DELETE_COMMAND:
                    return CollectDeletableSelection(out _, out _, out _);
                case SELECT_ALL_COMMAND:
                case FRAME_SELECTED_COMMAND:
                    return true;
                default:
                    return false;
            }
        }

        private bool ExecuteEditCommand(string commandName)
        {
            switch (commandName)
            {
                case COPY_COMMAND:
                    CopySelection();
                    return true;
                case CUT_COMMAND:
                    CutSelection();
                    return true;
                case PASTE_COMMAND:
                    PasteFromClipboard(m_mousePosition);
                    return true;
                case DUPLICATE_COMMAND:
                    DuplicateSelection();
                    return true;
                case SELECT_ALL_COMMAND:
                    SelectAll();
                    return true;
                case DELETE_COMMAND:
                case SOFT_DELETE_COMMAND:
                    DeleteSelection();
                    return true;
                case FRAME_SELECTED_COMMAND:
                    Frame();
                    return true;
                default:
                    return false;
            }
        }

        private static void MarkEditCommandHandled(EventBase evt)
        {
            evt.StopPropagation();
            evt.imguiEvent?.Use(); // The Edit menu reads the IMGUI event.
        }
        #endregion
        
        #region Framing
        public void Frame()
        {
            if (!TryGetBounds(element => element.Selected, out Rect bounds) && !TryGetBounds(_ => true, out bounds))
            {
                bounds = ContentContainer.layout;
            }

            float zoom = Mathf.Min((layout.width - 2 * FRAME_BORDER) / bounds.width, (layout.height - 2 * FRAME_BORDER) / bounds.height);
            zoom = Mathf.Clamp(zoom, MinScale, MaxScale);
            SetViewTransform(layout.size / 2 - bounds.center * zoom, zoom);
        }
        #endregion

        #region Context Menus
        private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            VisualElement picked = evt.triggerEvent?.target as VisualElement;
            if (picked == null)
            {
                picked = evt.target as VisualElement;
            }

            GraphElement element = picked as GraphElement;
            if (element == null && picked != null)
            {
                element = picked.GetFirstAncestorOfType<GraphElement>();
            }

            if (element != null)
            {
                element.BuildContextMenu(evt, this);
            }
            else
            {
                BuildCanvasMenu(evt);
            }
        }

        private void BuildCanvasMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 panelPosition = evt.mousePosition;
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(panelPosition);

            evt.menu.AppendAction(GraphShortcuts.WithHint("Create Node", GraphShortcuts.CREATE_NODE), _ => OpenNodeSearch(screenPosition, panelPosition, null));
            evt.menu.AppendAction(GraphShortcuts.WithHint("Create Group", GraphShortcuts.GROUP_SELECTION), _ => CreateGroup(panelPosition));
            evt.menu.AppendAction(GraphShortcuts.WithHint("Paste", GraphShortcuts.EDIT_PASTE), _ => PasteFromClipboard(panelPosition), GraphClipboard.HasClipboardContent ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction(GraphShortcuts.WithHint("Frame", GraphShortcuts.EDIT_FRAME), _ => Frame());
            evt.menu.AppendAction(GraphShortcuts.WithHint("Show MiniMap", GraphShortcuts.TOGGLE_MINIMAP), _ => MiniMapVisible = !MiniMapVisible, MiniMapVisible ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Snap to Grid", _ => SnapToGrid = !SnapToGrid, SnapToGrid ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        #endregion
        
        #region GraphElements
        public void AddGraphElement(GraphElement graphElement)
        {
            graphElement.Graph = this;
            GetViewLayer(graphElement.Layer).Add(graphElement);
            NotifyContentChanged();
        }

        public void RemoveGraphElement(GraphElement graphElement)
        {
            graphElement.RemoveFromHierarchy();
            graphElement.Graph = null;
            NotifyContentChanged();
        }

        internal void NotifyContentChanged() => OnContentChanged?.Invoke();
        #endregion

        #region Node Search
        internal void OpenNodeSearchAtMouse() => OpenNodeSearch(m_mousePosition);

        internal void OpenNodeSearch(Vector2 panelPosition, PendingPortConnection? pending = null)
        {
            OpenNodeSearch(GUIUtility.GUIToScreenPoint(panelPosition), panelPosition, pending);
        }

        private void OpenNodeSearch(Vector2 screenPosition, Vector2 panelPosition, PendingPortConnection? pending)
        {
            NodeSearchWindow.Open(screenPosition, GraphType, nodeType => CreateNode(nodeType, panelPosition, pending));
        }
        #endregion
    }
}
