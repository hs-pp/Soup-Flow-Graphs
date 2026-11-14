using System;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Views.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Edges
{
    public class EdgeView : GraphElement
    {
        public override int Layer => -1;
        
        public string FromNodeId { get; private set; }
        public string OutportId { get; private set; }
        public string ToNodeId { get; private set; }
        public bool HasModelIdentity => FromNodeId != null; // Actually has serialized representation.
        public override string Id => HasModelIdentity ? new EdgeId(FromNodeId, OutportId).Key : null;
        
        private PortView m_inputPort;
        public PortView InputPort => m_inputPort;
        private Vector2 m_inputPortPosition;
        private Vector2? m_inputPositionOverride;
        public Vector2 To => GetEndpointPosition(m_inputPositionOverride, InputPort, m_inputPortPosition);
        public Orientation InputOrientation => InputPort?.Orientation ?? OutputPort?.Orientation ?? Orientation.Horizontal;

        private PortView m_outputPort;
        public PortView OutputPort => m_outputPort;
        private Vector2 m_outputPortPosition;
        private Vector2? m_outputPositionOverride;
        public Vector2 From => GetEndpointPosition(m_outputPositionOverride, OutputPort, m_outputPortPosition);
        public Orientation OutputOrientation => OutputPort?.Orientation ?? InputPort?.Orientation ?? Orientation.Horizontal;

        public bool IsRealEdge() => InputPort != null && OutputPort != null; // Both ends are attached to ports.
        public bool IsCandidateEdge() => m_inputPositionOverride.HasValue || m_outputPositionOverride.HasValue;
        
        private readonly IEdgeRenderStrategy m_renderer;

        public override bool Selected
        {
            get => base.Selected;
            set
            {
                if (base.Selected == value)
                {
                    return;
                }

                base.Selected = value;
                m_renderer.SetSelected(this, base.Selected);
            }
        }
        
        public EdgeView()
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
            UpdateCachedInputPortPosition();
            UpdateCachedOutputPortPosition();
            Graph.OnViewTransformChanged += OnViewTransformChanged;
            OnEdgeChanged();
        }

        protected override void OnRemovedFromGraphView()
        {
            base.OnRemovedFromGraphView();
            Graph.OnViewTransformChanged -= OnViewTransformChanged;
            Disconnect();
            UnsetPositionOverrides();
        }

        public void Disconnect()
        {
            ChangePort(ref m_inputPort, null);
            ChangePort(ref m_outputPort, null);
        }
        
        public void BindToModel(PortView outputPort, PortView inputPort)
        {
            FromNodeId = outputPort.NodeId;
            OutportId = outputPort.OutportId;
            ToNodeId = inputPort.NodeId;
            SetPort(outputPort);
            SetPort(inputPort);
        }
        
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateCachedInputPortPosition();
            UpdateCachedOutputPortPosition();
            OnEdgeChanged();
        }
        
        private void OnEdgeChanged()
        {
            m_renderer.Invalidate();
            m_renderer.UpdateLayout(this);
            MarkDirtyRepaint();
            Graph?.NotifyContentChanged();
        }

        private void OnViewTransformChanged(GraphElementContainer contentContainer)
        {
            MarkDirtyRepaint();
        }
        
        public override Vector2 GetPosition()
        {
            return m_inputPositionOverride ?? m_outputPositionOverride ?? Vector2.zero;
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

            OnEdgeChanged();
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
        
        public void UnsetPositionOverrides()
        {
            m_inputPositionOverride = null;
            m_outputPositionOverride = null;
            m_inputPort?.UpdateConnectedState();
            m_outputPort?.UpdateConnectedState();
            OnEdgeChanged();
        }

        public override void ApplyDeltaToPosition(Vector2 delta)
        {
            SetPosition(GetPosition() + delta);
        }

        public override Vector2 GetCenter()
        {
            return m_inputPositionOverride ?? m_outputPositionOverride ?? base.GetCenter();
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
            SetElementPosition(bounds.position);
            style.width = bounds.width;
            style.height = bounds.height;
        }

        [EventInterest(typeof(DragOfferEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);
            if (evt.target != this || evt.eventTypeId != DragOfferEvent.TypeId())
            {
                return;
            }

            DragOfferEvent dragOffer = (DragOfferEvent)evt;
            if (Graph != null && Graph.IsViewDrag(dragOffer))
            {
                Graph.OnDragOffer(dragOffer, true);
            }
            else if ((MouseButton)dragOffer.button == MouseButton.LeftMouse && dragOffer.modifiers.IsNone() && IsMovable())
            {
                dragOffer.AcceptDrag(this);
                dragOffer.SetDragThreshold(10);
            }
        }

        public void SetPort(PortView port)
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

            OnEdgeChanged();
        }

        private void TrackPort(PortView port)
        {
            port.OnPositionChange += OnPortPositionChanged;

            for (VisualElement current = port.hierarchy.parent; current != null && current is not GraphViewLayer; current = current.hierarchy.parent)
            {
                current.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }

            OnPortPositionChanged(new PositionData { Element = port });
        }

        private void UntrackPort(PortView port)
        {
            port.OnPositionChange -= OnPortPositionChanged;

            for (VisualElement current = port.hierarchy.parent; current != null && current is not GraphViewLayer; current = current.hierarchy.parent)
            {
                current.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }

        private void OnPortPositionChanged(PositionData changeData)
        {
            if (changeData.Element == m_inputPort)
            {
                UpdateCachedInputPortPosition();
            }
            else if (changeData.Element == m_outputPort)
            {
                UpdateCachedOutputPortPosition();
            }

            if (Graph != null)
            {
                OnEdgeChanged();
            }
        }

        private void UpdateCachedInputPortPosition()
        {
            if (Graph == null || InputPort == null)
            {
                return;
            }

            m_inputPortPosition = Graph.ContentContainer.WorldToLocal(InputPort.GetGlobalCenter());
        }

        private void UpdateCachedOutputPortPosition()
        {
            if (Graph == null || OutputPort == null)
            {
                return;
            }

            m_outputPortPosition = Graph.ContentContainer.WorldToLocal(OutputPort.GetGlobalCenter());
        }

        private Vector2 GetEndpointPosition(Vector2? dragOverride, PortView port, Vector2 portPosition)
        {
            if (dragOverride.HasValue)
            {
                return dragOverride.Value;
            }

            if (port == null || Graph == null)
            {
                return Vector2.zero;
            }

            return portPosition;
        }

        public override void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            evt.menu.AppendAction(GraphShortcuts.WithHint("Delete Connection", GraphShortcuts.DeleteSelectionHintId), _ =>
                graphView.RequestCommand(new DeleteGraphElementsCommand(null, edges: new[] { new EdgeId(FromNodeId, OutportId) })));
        }
    }
}
