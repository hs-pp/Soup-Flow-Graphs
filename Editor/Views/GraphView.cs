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
using Soup.Editor.Views.Manipulators.Gestures;
using Soup.Editor.Views.Nodes;
using Soup.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public static class GraphViewConstants
    {
        public const int FrameBorder = 30;
        public const int PanAreaWidth = 100;
        public const int PanSpeed = 4;
        public const int PanInterval = 10;
        private const float MinSpeedFactor = 0.5f;
        private const float MaxSpeedFactor = 2.5f;
        public const float MaxPanSpeed = MaxSpeedFactor * PanSpeed;
        public const float PanAreaWidthAndMinSpeedFactor = PanAreaWidth + MinSpeedFactor;
        public const float DefaultSnapStep = 10f;
    }
    
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
        private const string STYLESHEET_PATH = "Soup/GraphView";
        private const string STYLECLASS_NAME = "graphView";
        private const float GROUP_PADDING = 30f;
        
        // GraphView
        public GraphElementContainer ContentContainer { get; }
        private Dictionary<int, GraphViewLayer> m_layers = new();
        private Vector2 m_mousePosition;
        
        private GridBackgroundElement m_gridBackgroundElement = new();

        /** The loaded graph asset's type. The node search only offers node types it allows. */
        public Type GraphType { get; set; }

        // Commands
        public event Action<ICommand> OnCommandRequested;
        public event Action<IReadOnlyList<IGraphCommand>, string> OnCommandBatchRequested;
        
        // View Transform
        public delegate void ViewTransformChanged(GraphElementContainer contentContainer);
        public event ViewTransformChanged OnViewTransformChanged;

        // Content
        public event Action OnContentChanged;

        // MiniMap
        private MiniMapElement m_miniMapElement;
        public event Action<bool> OnMiniMapVisibilityChanged;

        // Snapping
        private bool m_snapToGrid = true;
        public event Action<bool> OnSnapToGridChanged;

        // Pan
        private readonly IVisualElementScheduledItem m_panSchedule;
        private bool m_panning;
        private Vector2 m_panOriginDiff;
        
        // Zoom
        private ZoomManipulator m_zoomer = new();
        public float CurrentScale => GetViewTransformScale().x;
        
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
            usageHints = UsageHints.MaskContainer; // Should equate to RenderHints.ClipWithScissors

            hierarchy.Add(m_gridBackgroundElement);
            
            ContentContainer = new GraphElementContainer();
            m_gridBackgroundElement.Add(ContentContainer);

            m_miniMapElement = new MiniMapElement(this);
            hierarchy.Add(m_miniMapElement);

            // Manipulators
            m_panSchedule = schedule
                .Execute(Pan)
                .Every(GraphViewConstants.PanInterval)
                .StartingIn(GraphViewConstants.PanInterval);
            m_panSchedule.Pause();
            this.AddManipulator(m_zoomer);
            m_dragAndDrop = new DragAndDropManipulator(this);
            this.AddManipulator(m_dragAndDrop);
            m_elementDrags = new ElementDragManipulator(this);
            ContentContainer.AddManipulator(m_elementDrags);
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            focusable = true; // Shortcuts and Edit-menu commands only reach the graph while it holds focus.
            GraphShortcutContext.EnsureRegistered();
        }
        
        #region GraphViewLayers
        public void AddToViewLayer(GraphElement element)
        {
            GetViewLayer(element.Layer).Add(element);
        }
        
        private VisualElement GetViewLayer(int index)
        {
            if (!m_layers.TryGetValue(index, out GraphViewLayer layer))
            {
                layer = new() { name = $"Layer {index}" };
                m_layers[index] = layer;

                int indexOfLayer = m_layers.OrderBy(t => t.Key).Select(t => t.Value).ToList().IndexOf(layer);
                ContentContainer.Insert(indexOfLayer, layer);
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
        
        private static List<string> CollectIds<T>(IEnumerable<T> elements, Func<T, bool> include) where T : GraphElement
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
                    CollectIds(ContentContainer.NodesSelected, node => node.IsCopiable() && node.IsDeletable()), null,
                    CollectIds(ContentContainer.GroupsSelected, group => group.IsCopiable() && group.IsDeletable())));
        }

        private void PasteFromClipboard(Vector2 panelPosition)
        {
            RequestCommand(new PasteFromClipboardCommand(ContentContainer.WorldToLocal(panelPosition)));
        }

        private void DuplicateSelection()
        {
            if (CollectCopiableSelection(out List<string> nodeIds, out List<string> groupIds))
            {
                RequestCommand(new DuplicateGraphElementsCommand(nodeIds, groupIds));
            }
        }

        internal void RequestDeleteSelection()
        {
            if (CollectDeletableSelection(out List<string> nodeIds, out List<EdgeId> edges, out List<string> groupIds))
            {
                RequestCommand(new DeleteGraphElementsCommand(nodeIds, edges, groupIds));
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
            if (!TryGetSelectionBounds(out Rect bounds))
            {
                return false;
            }

            RequestCommand(new CreateGroupCommand(RectUtils.Inflate(bounds, GROUP_PADDING, GROUP_PADDING + GroupView.TITLE_HEIGHT, GROUP_PADDING, GROUP_PADDING)));
            return true;
        }

        private bool TryGetSelectionBounds(out Rect bounds)
        {
            bounds = default;
            bool found = false;
            foreach (GraphElement element in ContentContainer.ElementsAll)
            {
                if (!element.Selected || element is not (BaseNodeView or GroupView))
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
            List<IGraphCommand> commands = new()
            {
                new CreateNodeCommand(nodeType, graphPosition, newNodeId),
            };

            if (pending.HasValue)
            {
                if (pending.Value.AnchoredIsOutput)
                {
                    commands.Add(new CreateEdgeCommand(pending.Value.NodeId, pending.Value.OutportId, newNodeId));
                }
                else
                {
                    List<OutportDefinition> definitions = NodeUtils.GetOutportDefinitions(nodeType);
                    if (definitions != null && definitions.Count > 0)
                    {
                        commands.Add(new CreateEdgeCommand(newNodeId, definitions[0].Id, pending.Value.NodeId));
                    }
                }
            }

            if (commands.Count > 1)
            {
                RequestCommandBatch("Create and Connect Node", commands.ToArray());
            }
            else
            {
                RequestCommand(commands[0]);
            }
        }
        #endregion

        #region View Transform
        private void UpdateViewTransform(Vector3 newPosition) => UpdateViewTransform(newPosition, GetViewTransformScale());
        public void UpdateViewTransform(Vector3 newPosition, Vector3 newScale)
        {
            // Make sure we dont accidentally override this change later.
            UnregisterCallback<GeometryChangedEvent>(OnFirstLayoutResetViewTransform);

            // Early out if Position and Scale didnt change.
            if (GetViewTransformPosition() == newPosition && GetViewTransformScale() == newScale)
            {
                return;
            }

            float validateFloat = newPosition.x + newPosition.y + newPosition.z + newScale.x + newScale.y + newScale.z;
            if (float.IsInfinity(validateFloat) || float.IsNaN(validateFloat))
            {
                return;
            }

            ContentContainer.style.translate = newPosition;
            ContentContainer.style.scale = newScale;

            OnViewTransformChanged?.Invoke(ContentContainer);
        }

        public Vector3 GetViewTransformPosition()
        {
            Translate translate = ContentContainer.style.translate.value;
            return new Vector3(translate.x.value, translate.y.value, translate.z);
        }

        public Vector3 GetViewTransformScale()
        {
            return ContentContainer.style.scale.value.value;
        }

        public void ResetViewTransform()
        {
            Vector2 size = layout.size;
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x <= 0f || size.y <= 0f)
            {
                // Layout hasn't resolved yet. Center once the first geometry pass lands.
                RegisterCallback<GeometryChangedEvent>(OnFirstLayoutResetViewTransform);
                return;
            }

            UpdateViewTransform(new Vector2(size.x * 0.3f, size.y * 0.3f), Vector3.one);
        }

        private void OnFirstLayoutResetViewTransform(GeometryChangedEvent evt)
        {
            UnregisterCallback<GeometryChangedEvent>(OnFirstLayoutResetViewTransform);
            ResetViewTransform();
        }
        #endregion

        #region Pan
        /// <summary>
        /// Auto-pans while the element drag nears the edges, until <see cref="StopPan"/>.
        /// </summary>
        internal void StartPan()
        {
            m_panOriginDiff = Vector2.zero;
            m_panning = true;
            m_panSchedule.Resume();
        }

        internal void StopPan(bool resetView)
        {
            if (!m_panning)
            {
                return;
            }

            m_panSchedule.Pause();
            m_panning = false;
            if (resetView)
            {
                UpdateViewTransform((Vector2)GetViewTransformPosition() + m_panOriginDiff);
            }
        }

        private Vector2 GetEffectivePanSpeed(Vector2 mousePos)
        {
            Vector2 effectiveSpeed = Vector2.zero;

            if (mousePos.x <= GraphViewConstants.PanAreaWidth)
            {
                effectiveSpeed.x = -((GraphViewConstants.PanAreaWidth - mousePos.x) / GraphViewConstants.PanAreaWidthAndMinSpeedFactor) * GraphViewConstants.PanSpeed;
            }
            else if (mousePos.x >= m_gridBackgroundElement.layout.width - GraphViewConstants.PanAreaWidth)
            {
                effectiveSpeed.x = (mousePos.x - (m_gridBackgroundElement.layout.width - GraphViewConstants.PanAreaWidth)) / GraphViewConstants.PanAreaWidthAndMinSpeedFactor * GraphViewConstants.PanSpeed;
            }

            if (mousePos.y <= GraphViewConstants.PanAreaWidth)
            {
                effectiveSpeed.y = -((GraphViewConstants.PanAreaWidth - mousePos.y) / GraphViewConstants.PanAreaWidthAndMinSpeedFactor) * GraphViewConstants.PanSpeed;
            }
            else if (mousePos.y >= m_gridBackgroundElement.layout.height - GraphViewConstants.PanAreaWidth)
            {
                effectiveSpeed.y = (mousePos.y - (m_gridBackgroundElement.layout.height - GraphViewConstants.PanAreaWidth)) / GraphViewConstants.PanAreaWidthAndMinSpeedFactor * GraphViewConstants.PanSpeed;
            }

            return Vector2.ClampMagnitude(effectiveSpeed, GraphViewConstants.MaxPanSpeed);
        }

        private void Pan()
        {
            // The pan zones are measured from the grid's own edges, not the window's.
            Vector2 elementPosition = m_gridBackgroundElement.WorldToLocal(m_elementDrags.PanAnchor);
            Vector2 speed = GetEffectivePanSpeed(elementPosition);
            if (Vector2.zero == speed)
            {
                return;
            }

            m_panOriginDiff += speed;

            UpdateViewTransform((Vector2)GetViewTransformPosition() - speed);
            Vector2 localSpeed = speed / CurrentScale;
            m_elementDrags.ApplyPanDelta(localSpeed);
        }
        #endregion

        #region Zoom
        public float MinScale
        {
            get => m_zoomer.MinScale;
            set
            {
                m_zoomer.MinScale = Math.Min(value, ZoomManipulator.DefaultMinScale);
                ValidateTransform();
            }
        }

        public float MaxScale
        {
            get => m_zoomer.MaxScale;
            set
            {
                m_zoomer.MaxScale = Math.Max(value, ZoomManipulator.DefaultMaxScale);
                ValidateTransform();
            }
        }

        public float ScaleStep
        {
            get => m_zoomer.ScaleStep;
            set
            {
                m_zoomer.ScaleStep = Math.Min(value, (MaxScale - MinScale) / 2);
                ValidateTransform();
            }
        }

        public float ReferenceScale
        {
            get => m_zoomer.ReferenceScale;
            set
            {
                m_zoomer.ReferenceScale = Math.Clamp(value, MinScale, MaxScale);
                ValidateTransform();
            }
        }

        private void ValidateTransform()
        {
            if (ContentContainer == null)
            {
                return;
            }

            Vector3 transformScale = GetViewTransformScale();

            transformScale.x = Mathf.Clamp(transformScale.x, MinScale, MaxScale);
            transformScale.y = Mathf.Clamp(transformScale.y, MinScale, MaxScale);

            UpdateViewTransform(GetViewTransformPosition(), transformScale);
        }
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

            const float step = GraphViewConstants.DefaultSnapStep;
            return new Vector2(Mathf.Round(position.x / step) * step, Mathf.Round(position.y / step) * step);
        }
        #endregion

        #region Event Handlers
        [EventInterest(typeof(MouseMoveEvent), typeof(DragOfferEvent), typeof(DragEvent), typeof(DragEndEvent), typeof(DragCancelEvent), typeof(DropEnterEvent), typeof(DropEvent), typeof(DropExitEvent), typeof(ValidateCommandEvent), typeof(ExecuteCommandEvent))]
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
            else if (typeId == DropEnterEvent.TypeId())
            {
                OnDropEnter((DropEnterEvent)evt);
            }
            else if (typeId == DropEvent.TypeId())
            {
                OnDrop((DropEvent)evt);
            }
            else if (typeId == DropExitEvent.TypeId())
            {
                OnDropExit((DropExitEvent)evt);
            }
        }

        public void OnDragOffer(DragOfferEvent e, bool forceViewDrag = false)
        {
            if (forceViewDrag || IsViewDrag(e))
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

                bool additive = e.modifiers.IsShift();
                bool subtractive = e.modifiers.IsActionKey();
                bool exclusive = !(additive ^ subtractive);
                if (exclusive)
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
                UpdateViewTransform(GetViewTransformPosition() + (Vector3)e.mouseDelta);
            }
        }

        private void OnDragEnd(DragEndEvent e)
        {
            if (m_draggingMarquee)
            {
                // Remove marquee
                e.StopImmediatePropagation();
                m_marqueeElement.RemoveFromHierarchy();

                // Ignore if the rectangle is infinitely small
                Rect selectionRect = m_marqueeElement.SelectionRect;
                if (selectionRect.size == Vector2.zero)
                {
                    m_draggingMarquee = false;
                    return;
                }

                // Select elements that overlap the marquee
                bool additive = e.modifiers.IsShift();
                bool subtractive = e.modifiers.IsActionKey();
                bool exclusive = !(additive ^ subtractive);

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

                GraphSelectionMode mode = exclusive ? GraphSelectionMode.Replace : additive ? GraphSelectionMode.Add : GraphSelectionMode.Remove;
                RequestCommand(new SelectGraphElementsCommand(overlappingIds, mode));

                m_draggingMarquee = false;
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
                UpdateViewTransform(GetViewTransformPosition() + (Vector3)e.mouseDelta);
                m_draggingView = false;
            }
        }
        
        /// <summary>
        /// Only to be called from outside the gesture (ex. undo/redo), after the observer has reconciled.
        /// We want to hard cancel the drag because it's no longer operating in the correct world.
        /// </summary>
        public void CancelActiveDrag()
        {
            m_elementDrags.KeepPositionsOnCancel();
            m_dragAndDrop.CancelActiveDrag();
        }

        private void OnDropEnter(DropEnterEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture)
            {
                e.StopImmediatePropagation();
            }
        }

        private void OnDrop(DropEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture edgeDrag)
            {
                e.StopImmediatePropagation();

                bool candidateHookFired = false;

                for (int i = edgeDrag.Edges.Count - 1; i >= 0; i--)
                {
                    EdgeView edge = edgeDrag.Edges[i];

                    // A real edge dropped in space is disconnected from the model
                    if (edge.HasModelIdentity)
                    {
                        RequestCommand(new DestroyEdgeCommand(new EdgeId(edge.FromNodeId, edge.OutportId)));
                        edgeDrag.Take(edge);
                    }
                    else
                    {
                        PendingPortConnection? pending = null;
                        PortView anchoredPort = edgeDrag.GetAnchoredPort(edge);
                        if (anchoredPort != null)
                        {
                            pending = new PendingPortConnection(
                                anchoredPort.NodeId,
                                anchoredPort.OutportId,
                                anchoredPort.Direction == Direction.Output);
                        }

                        edgeDrag.Take(edge);

                        if (!candidateHookFired)
                        {
                            candidateHookFired = true;
                            OpenNodeSearch(e.mousePosition, pending);
                        }
                    }
                }
            }
        }

        private void OnDropExit(DropExitEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture)
            {
                e.StopImmediatePropagation();
            }
        }

        private bool IsMarqueeDrag<T>(DragAndDropEvent<T> e) where T : DragAndDropEvent<T>, new()
        {
            if ((MouseButton)e.button != MouseButton.LeftMouse)
            {
                return false;
            }

            if (e.modifiers.IsNone() || e.modifiers.IsExclusiveShift() || e.modifiers.IsExclusiveActionKey())
            {
                return true;
            }
            
            return false;
        }

        public bool IsViewDrag<T>(DragAndDropEvent<T> e) where T : DragAndDropEvent<T>, new()
        {
            if (e.modifiers.HasFlag(EventModifiers.Alt))
            {
                return true;
            }

            if ((MouseButton)e.button != MouseButton.MiddleMouse)
            {
                return false;
            }

            if (!e.modifiers.IsNone())
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Edit Commands
        // Unity's Edit menu and its shortcuts reach the focused element as command events. F is Unity's "Frame Selected in
        // Window under Cursor", which sends FrameSelected. Validate decides whether the menu item is enabled. Soup-only
        // hotkeys live in GraphShortcuts.
        private const string COPY_COMMAND = "Copy";
        private const string CUT_COMMAND = "Cut";
        private const string PASTE_COMMAND = "Paste";
        private const string DUPLICATE_COMMAND = "Duplicate";
        private const string SELECT_ALL_COMMAND = "SelectAll";
        private const string DELETE_COMMAND = "Delete";
        private const string SOFT_DELETE_COMMAND = "SoftDelete";
        private const string FRAME_SELECTED_COMMAND = "FrameSelected";

        internal bool IsMouseCaptured => panel?.GetCapturingElement(PointerId.mousePointerId) != null;

        // Targeting the graph itself keeps a focused text field's Copy/Paste/Select All its own.
        private bool AcceptsEditCommand(EventBase evt) => evt.target == this && !IsMouseCaptured;

        private bool CanExecuteEditCommand(string commandName) => commandName switch
        {
            COPY_COMMAND or CUT_COMMAND or DUPLICATE_COMMAND => CollectCopiableSelection(out _, out _),
            PASTE_COMMAND => GraphClipboard.HasClipboardContent,
            DELETE_COMMAND or SOFT_DELETE_COMMAND => CollectDeletableSelection(out _, out _, out _),
            SELECT_ALL_COMMAND or FRAME_SELECTED_COMMAND => true,
            _ => false,
        };

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
                    RequestDeleteSelection();
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
            if (focusController == null || focusController.focusedElement == null || !(focusController.focusedElement is IBindable)) // Is focused element null or not bindable?
            {
                // Construct rect for selected and unselected elements
                Rect rectToFitSelected = ContentContainer.layout;
                Rect rectToFitUnselected = rectToFitSelected;
                bool reachedFirstSelected = false;
                bool reachedFirstUnselected = false;
                foreach (GraphElement ge in ContentContainer.ElementsAll)
                {
                    if (ge.Selected)
                    {
                        if (!reachedFirstSelected)
                        {
                            rectToFitSelected = ge.ChangeCoordinatesTo(ContentContainer, ge.Rect());
                            reachedFirstSelected = true;
                        }
                        else
                        {
                            rectToFitSelected = RectUtils.Encompass(rectToFitSelected,
                                ge.ChangeCoordinatesTo(ContentContainer, ge.Rect()));
                        }
                    }
                    else if (!reachedFirstSelected) // Don't bother if we already have at least one selected item
                    {
                        if (!reachedFirstUnselected)
                        {
                            rectToFitUnselected = ge.ChangeCoordinatesTo(ContentContainer, ge.Rect());
                            reachedFirstUnselected = true;
                        }
                        else
                        {
                            rectToFitUnselected = RectUtils.Encompass(rectToFitUnselected,
                                ge.ChangeCoordinatesTo(ContentContainer, ge.Rect()));
                        }
                    }
                }

                // Use selection only if possible, otherwise unselected. Failing both, use original content container rect
                Vector3 frameScaling;
                Vector3 frameTranslation;
                if (reachedFirstSelected)
                {
                    CalculateFrameTransform(rectToFitSelected, layout, GraphViewConstants.FrameBorder, out frameTranslation,
                        out frameScaling);
                }
                else if (reachedFirstUnselected)
                {
                    CalculateFrameTransform(rectToFitUnselected, layout, GraphViewConstants.FrameBorder, out frameTranslation,
                        out frameScaling);
                }
                else
                {
                    // Note: rectToFitSelected will just be the container rect
                    CalculateFrameTransform(rectToFitSelected, layout, GraphViewConstants.FrameBorder, out frameTranslation,
                        out frameScaling);
                }

                // Update transform
                UpdateViewTransform(frameTranslation, frameScaling);
            }
        }

        private float ZoomRequiredToFrameRect(Rect rectToFit, Rect clientRect, int border)
        {
            Rect screenRect = new() { xMin = border, xMax = clientRect.width - border, yMin = border, yMax = clientRect.height - border };
            Rect identity = GUIUtility.ScreenToGUIRect(screenRect);
            return Math.Min(identity.width / rectToFit.width, identity.height / rectToFit.height);
        }

        private void CalculateFrameTransform(Rect rectToFit, Rect clientRect, int border, out Vector3 frameTranslation, out Vector3 frameScaling)
        {
            float zoomLevel = ZoomRequiredToFrameRect(rectToFit, clientRect, border);

            zoomLevel = Mathf.Clamp(zoomLevel, MinScale, MaxScale);

            Matrix4x4 transformMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new(zoomLevel, zoomLevel, 1.0f));
            Vector2 edge = new(clientRect.width, clientRect.height);
            Vector2 origin = new(0, 0);

            Rect r = new() { min = origin, max = edge };

            Vector3 parentScale = new(transformMatrix.GetColumn(0).magnitude, transformMatrix.GetColumn(1).magnitude, transformMatrix.GetColumn(2).magnitude);
            Vector2 offset = r.center - rectToFit.center * parentScale.x;

            frameTranslation = new(offset.x, offset.y, 0.0f);
            frameScaling = parentScale;
        }
        #endregion

        #region Context Menus
        private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            VisualElement picked = evt.triggerEvent?.target as VisualElement ?? evt.target as VisualElement;
            GraphElement element = picked as GraphElement ?? picked?.GetFirstAncestorOfType<GraphElement>();
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

            evt.menu.AppendAction(GraphShortcuts.WithHint("Paste", GraphShortcuts.EDIT_PASTE), _ =>
                PasteFromClipboard(panelPosition), GraphClipboard.HasClipboardContent ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            
            evt.menu.AppendSeparator();
            
            evt.menu.AppendAction("Ping FlowGraph", _ =>
            {
                // if (m_state is { IsLoaded: true })
                // {
                //     EditorGUIUtility.PingObject(m_state.GraphAsset);
                // }
            });
            
            evt.menu.AppendAction(GraphShortcuts.WithHint("Frame", GraphShortcuts.EDIT_FRAME), _ => { Frame(); });

            evt.menu.AppendAction(GraphShortcuts.WithHint("Show MiniMap", GraphShortcuts.TOGGLE_MINIMAP), _ => MiniMapVisible = !MiniMapVisible, MiniMapVisible ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Snap to Grid", _ => SnapToGrid = !SnapToGrid, SnapToGrid ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        #endregion
        
        #region GraphElements
        public void AddGraphElement(GraphElement graphElement)
        {
            graphElement.Graph = this;
            AddToViewLayer(graphElement);
            NotifyContentChanged();
        }

        public void RemoveGraphElement(GraphElement graphElement)
        {
            graphElement.RemoveFromHierarchy();
            graphElement.Graph = null;
            NotifyContentChanged();
        }

        internal void NotifyContentChanged() => OnContentChanged?.Invoke();

        public void IlluminateCompatiblePorts(PortView port)
        {
            foreach (PortView otherPort in ContentContainer.Ports)
            {
                otherPort.Highlight = port.CanConnectTo(otherPort);
            }
        }

        public void IlluminateAllPorts()
        {
            foreach (PortView otherPort in ContentContainer.Ports)
            {
                otherPort.Highlight = true;
            }
        }
        
        internal void OpenNodeSearchAtMouse() => OpenNodeSearch(m_mousePosition);

        private void OpenNodeSearch(Vector2 panelPosition, PendingPortConnection? pending = null)
        {
            OpenNodeSearch(GUIUtility.GUIToScreenPoint(panelPosition), panelPosition, pending);
        }
        private void OpenNodeSearch(Vector2 screenPosition, Vector2 panelPosition, PendingPortConnection? pending)
        {
            NodeSearchWindow.Open(screenPosition, GraphType, nodeType => CreateNode(nodeType, panelPosition, pending));
        }
        #endregion

        #region Helpers
        public bool MiniMapVisible
        {
            get
            {
                if (m_miniMapElement == null)
                {
                    return false;
                }
                return m_miniMapElement.style.display == DisplayStyle.Flex;
            }
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
        
        public List<GraphElement> GetGroupContents(GroupView group)
        {
            return ContentContainer.GetGroupContents(group);
        }
        #endregion
    }
}
