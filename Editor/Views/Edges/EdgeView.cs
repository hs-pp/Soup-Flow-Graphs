using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Views.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Edges
{
    public sealed class EdgeView : GraphElement
    {
        private const float DRAG_THRESHOLD = 10f;

        public override int Layer => -1;

        private readonly IEdgeRenderStrategy m_renderer;
        private PortView m_inputPort;
        private PortView m_outputPort;
        private Vector2 m_inputPortPosition;
        private Vector2 m_outputPortPosition;
        private Vector2? m_inputPositionOverride;
        private Vector2? m_outputPositionOverride;

        public string FromNodeId { get; }
        public string OutportId { get; }
        public string ToNodeId { get; }
        public bool HasModelIdentity => FromNodeId != null;

        public PortView InputPort => m_inputPort;
        public PortView OutputPort => m_outputPort;
        public Vector2 InputPosition => GetEndpointPosition(m_inputPositionOverride, m_inputPort, m_inputPortPosition);
        public Vector2 OutputPosition => GetEndpointPosition(m_outputPositionOverride, m_outputPort, m_outputPortPosition);
        public Orientation InputOrientation => GetOrientation(m_inputPort, m_outputPort);
        public Orientation OutputOrientation => GetOrientation(m_outputPort, m_inputPort);
        public bool IsDragged => m_inputPositionOverride.HasValue || m_outputPositionOverride.HasValue;

        protected override void OnSelectedChanged() => m_renderer.SetSelected(this, Selected);

        public EdgeView(PortView outputPort, PortView inputPort) : this()
        {
            FromNodeId = outputPort.NodeId;
            OutportId = outputPort.OutportId;
            ToNodeId = inputPort.NodeId;
            Id = new EdgeId(FromNodeId, OutportId).Key;
            SetPort(outputPort);
            SetPort(inputPort);
        }

        public EdgeView(PortView port) : this()
        {
            SetPort(port);
        }

        private EdgeView()
        {
            m_renderer = new SplineEdgeRenderStrategy();
            ClearClassList();
            AddToClassList("edge");
            Capabilities |= Capabilities.Selectable | Capabilities.Deletable | Capabilities.Movable;
            generateVisualContent = context => m_renderer.Render(this, context);
        }

        protected override void OnAddedToGraphView()
        {
            base.OnAddedToGraphView();
            Graph.OnViewTransformChanged += OnViewTransformChanged;
            Refresh();
        }

        protected override void OnRemovedFromGraphView()
        {
            base.OnRemovedFromGraphView();
            Graph.OnViewTransformChanged -= OnViewTransformChanged;
            Disconnect();
        }

        private void OnViewTransformChanged(GraphElementContainer contentContainer)
        {
            MarkDirtyRepaint();
        }

        private void SetPort(PortView port)
        {
            if (port.Direction == Direction.Input)
            {
                ChangePort(ref m_inputPort, port);
            }
            else
            {
                ChangePort(ref m_outputPort, port);
            }
        }

        private void Disconnect()
        {
            ChangePort(ref m_inputPort, null);
            ChangePort(ref m_outputPort, null);
        }

        private void ChangePort(ref PortView currentPort, PortView newPort)
        {
            if (currentPort == newPort)
            {
                return;
            }

            if (currentPort != null)
            {
                UntrackPort(currentPort);
                currentPort.Disconnect(this);
            }

            currentPort = newPort;
            if (currentPort != null)
            {
                TrackPort(currentPort);
                currentPort.Connect(this);
            }

            Refresh();
        }

        private void TrackPort(PortView port)
        {
            port.OnPositionChanged += OnPortPositionChanged;

            for (VisualElement current = port.hierarchy.parent; current != null && current is not GraphViewLayer; current = current.hierarchy.parent)
            {
                current.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }

        private void UntrackPort(PortView port)
        {
            port.OnPositionChanged -= OnPortPositionChanged;

            for (VisualElement current = port.hierarchy.parent; current != null && current is not GraphViewLayer; current = current.hierarchy.parent)
            {
                current.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }

        private void OnPortPositionChanged(VisualElement port)
        {
            Refresh();   
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            Refresh();   
        }

        private void Refresh()
        {
            UpdateCachedPortPositions();
            m_renderer.Invalidate();
            m_renderer.UpdateLayout(this);
            MarkDirtyRepaint();
            Graph?.NotifyContentChanged();
        }

        private void UpdateCachedPortPositions()
        {
            if (Graph == null)
            {
                return;
            }

            if (m_inputPort != null)
            {
                m_inputPortPosition = Graph.ContentContainer.WorldToLocal(m_inputPort.GetGlobalCenter());
            }

            if (m_outputPort != null)
            {
                m_outputPortPosition = Graph.ContentContainer.WorldToLocal(m_outputPort.GetGlobalCenter());
            }
        }

        private Vector2 GetEndpointPosition(Vector2? positionOverride, PortView port, Vector2 portPosition)
        {
            if (positionOverride.HasValue)
            {
                return positionOverride.Value;
            }

            if (port == null || Graph == null)
            {
                return Vector2.zero;
            }

            return portPosition;
        }

        private static Orientation GetOrientation(PortView port, PortView otherPort)
        {
            if (port != null)
            {
                return port.Orientation;
            }

            if (otherPort != null)
            {
                return otherPort.Orientation;
            }

            return Orientation.Horizontal;
        }

        public override Vector2 GetPosition()
        {
            if (m_inputPositionOverride.HasValue)
            {
                return m_inputPositionOverride.Value;
            }

            if (m_outputPositionOverride.HasValue)
            {
                return m_outputPositionOverride.Value;
            }

            return Vector2.zero;
        }

        public override void SetPosition(Vector2 newPosition)
        {
            if (m_inputPositionOverride.HasValue)
            {
                SetInputPositionOverride(newPosition);
            }
            else if (m_outputPositionOverride.HasValue)
            {
                SetOutputPositionOverride(newPosition);
            }
        }

        public override void ApplyDeltaToPosition(Vector2 delta)
        {
            SetPosition(GetPosition() + delta);
        }

        public override Vector2 GetCenter()
        {
            if (m_inputPositionOverride.HasValue)
            {
                return m_inputPositionOverride.Value;
            }

            if (m_outputPositionOverride.HasValue)
            {
                return m_outputPositionOverride.Value;
            }

            return base.GetCenter();
        }

        public void SetInputPositionOverride(Vector2 position) => SetPositionOverride(position, ref m_inputPositionOverride, m_inputPort);
        public void SetOutputPositionOverride(Vector2 position) => SetPositionOverride(position, ref m_outputPositionOverride, m_outputPort);

        private void SetPositionOverride(Vector2 position, ref Vector2? positionOverride, PortView port)
        {
            bool wasOverridden = positionOverride.HasValue;
            positionOverride = position;
            if (!wasOverridden)
            {
                port?.UpdateConnectedState();
            }

            Refresh();
        }

        public void ClearPositionOverrides()
        {
            m_inputPositionOverride = null;
            m_outputPositionOverride = null;
            m_inputPort?.UpdateConnectedState();
            m_outputPort?.UpdateConnectedState();
            Refresh();
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            return m_renderer.ContainsPoint(this, localPoint);
        }

        public override bool Overlaps(Rect rect)
        {
            return m_renderer.Overlaps(this, rect);
        }

        public void SetRenderBounds(Rect bounds)
        {
            SetPositionWithoutNotify(bounds.position);
            style.width = bounds.width;
            style.height = bounds.height;
        }

        [EventInterest(typeof(DragOfferEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt.target == this && evt.eventTypeId == DragOfferEvent.TypeId())
            {
                DragOfferEvent dragOffer = (DragOfferEvent)evt;
                OfferSelectionDrag(dragOffer);
                if (dragOffer.GetDraggedElement() == this)
                {
                    dragOffer.SetDragThreshold(DRAG_THRESHOLD);
                }
            }
        }

        public override void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            evt.menu.AppendAction(GraphShortcuts.WithHint("Delete Connection", GraphShortcuts.DeleteSelectionHintId), _ =>
                graphView.RequestCommand(new DeleteGraphElementsCommand(null, edges: new[] { new EdgeId(FromNodeId, OutportId) })),
                IsDeletable() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }
    }
}
