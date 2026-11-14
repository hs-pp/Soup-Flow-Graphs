using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Manipulators;
using Soup.Editor.Views.Manipulators.Gestures;
using Soup.Editor.Views.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public class PortView : VisualElement
    {
        private const string UXML_PATH = "Soup/PortView";
        private const string ROOT_TAG = "root";
        private const string CONNECTOR_LABEL_TAG = "connector-label";
        private const string CONNECTOR_BOX_TAG = "connector-box";
        private const string CONNECTOR_CAP_TAG = "connector-cap";

        private const string PORT_VIEW_CLASS = "portView";
        private const string CONNECTED_CLASS = "portViewConnected";
        private const string DROP_TARGET_CLASS = "portViewDropTarget";
        private const string DIMMED_CLASS = "portViewDimmed";

        private static readonly CustomStyleProperty<float> s_portSizeProperty = new("--port-size");
        private static readonly CustomStyleProperty<float> s_portLineWidthProperty = new("--port-line-width");

        public override bool canGrabFocus => true;

        public string NodeId { get; }
        public string OutportId { get; }
        
        public Direction Direction { get; }
        public Orientation Orientation { get; }
        private PortCapacity Capacity { get; }
        public bool AllowMultiDrag => true;
        
        private HashSet<EdgeView> m_connections;
        public IEnumerable<EdgeView> Connections => m_connections;
        private BaseNodeView m_parentNodeView;
        public BaseNodeView ParentNodeView => m_parentNodeView;
        public event Action<VisualElement> OnPositionChanged;

        private VisualElement m_root;
        private VisualElement m_connectorBox;
        private VisualElement m_connectorBoxCap;
        private Label m_connectorText;
        private bool m_highlight = true;
        
        public string PortName
        {
            get => m_connectorText.text;
            set
            {
                m_connectorText.text = value;
                m_connectorText.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public bool Highlight
        {
            get => m_highlight;
            set
            {
                if (m_highlight == value)
                {
                    return;
                }

                m_highlight = value;
                EnableInClassList(DIMMED_CLASS, !m_highlight);
                m_connectorBox.SetEnabled(m_highlight);
            }
        }

        private float m_portSize = float.NaN;
        private float m_portLineWidth = float.NaN;
        
        public PortView(string nodeId, string outportId, Direction direction, PortCapacity capacity, Orientation orientation = Orientation.Horizontal)
        {
            NodeId = nodeId;
            OutportId = outportId;

            ClearClassList();
            CreateLayout();

            m_connections = new();

            Orientation = orientation;
            Direction = direction;
            Capacity = capacity;

            AddToClassList(PORT_VIEW_CLASS);
            AddToClassList($"{PORT_VIEW_CLASS}{Direction}{Orientation}");
            m_root.AddToClassList($"port{Direction}{Orientation}");
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_root = this.Q<VisualElement>(ROOT_TAG);
            m_connectorText = this.Q<Label>(CONNECTOR_LABEL_TAG);
            m_connectorBox = this.Q<VisualElement>(CONNECTOR_BOX_TAG);
            m_connectorBoxCap = this.Q<VisualElement>(CONNECTOR_CAP_TAG);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            if (!e.customStyle.TryGetValue(s_portSizeProperty, out float size) || !e.customStyle.TryGetValue(s_portLineWidthProperty, out float lineWidth))
            {
                return;
            }

            if (size.Equals(m_portSize) && lineWidth.Equals(m_portLineWidth))
            {
                return;
            }

            m_portSize = size;
            m_portLineWidth = lineWidth;

            // Both images are white so USS can colour each layer through its tint, per state.
            m_connectorBox.style.backgroundImage = new StyleBackground(GetPortChevronGraphics(size, lineWidth, filled: true));
            m_connectorBoxCap.style.backgroundImage = new StyleBackground(GetPortChevronGraphics(size, lineWidth, filled: false));
        }

        public void SetParent(BaseNodeView nodeView)
        {
            if (m_parentNodeView == nodeView)
            {
                return;
            }

            if (m_parentNodeView != null)
            {
                m_parentNodeView.OnPositionChanged -= OnParentPositionChanged;
            }

            if (nodeView == null)
            {
                m_parentNodeView = null;
            }
            else
            {
                m_parentNodeView = nodeView;
                m_parentNodeView.OnPositionChanged += OnParentPositionChanged;
            }
        }

        public void Connect(EdgeView edge)
        {
            if (edge == null)
            {
                return;
            }

            if (!m_connections.Contains(edge))
            {
                m_connections.Add(edge);
            }

            UpdateConnectedState();
        }

        public void Disconnect(EdgeView edge)
        {
            if (edge == null)
            {
                return;
            }

            m_connections.Remove(edge);
            UpdateConnectedState();
        }
        
        private bool IsConnected(bool ignoreCandidateEdges = true)
        {
            foreach (EdgeView edge in m_connections)
            {
                if (ignoreCandidateEdges && edge.IsCandidateEdge())
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool CanConnectToMore(bool ignoreCandidateEdges = true) => Capacity == PortCapacity.Multi || !IsConnected(ignoreCandidateEdges);
        private bool IsSameDirection(PortView other) => Direction == other.Direction;
        private bool IsOnSameNode(PortView other) => other.ParentNodeView == ParentNodeView;
        
        private bool IsConnectedTo(PortView other, bool ignoreCandidateEdges = true)
        {
            foreach (EdgeView e in m_connections)
            {
                if (ignoreCandidateEdges && e.IsCandidateEdge())
                {
                    continue;
                }

                if (Direction == Direction.Output)
                {
                    if (e.InputPort == other)
                    {
                        return true;
                    }
                }
                else
                {
                    if (e.OutputPort == other)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool CanConnectTo(PortView other, bool ignoreCandidateEdges = true)
        {
            return !IsSameDirection(other)
                   && !IsOnSameNode(other)
                   && CanConnectToMore(ignoreCandidateEdges)
                   && other.CanConnectToMore(ignoreCandidateEdges)
                   && !IsConnectedTo(other, ignoreCandidateEdges);
        }

        public Vector2 GetGlobalCenter() => m_connectorBox.LocalToWorld(GetCenter());
        public Vector2 GetCenter() => new Rect(Vector2.zero, m_connectorBox.layout.size).center;

        private void OnParentPositionChanged(VisualElement nodeView)
        {
            OnPositionChanged?.Invoke(this);
        }

        #region Style

        internal void UpdateConnectedState()
        {
            EnableInClassList(CONNECTED_CLASS, IsConnected());
        }

        #endregion

        [EventInterest(typeof(DragOfferEvent), typeof(DropEnterEvent), typeof(DropEvent), typeof(DropExitEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt.target != this)
            {
                return;
            }

            if (evt.eventTypeId == DragOfferEvent.TypeId()) OnDragOffer((DragOfferEvent)evt);
            else if (evt.eventTypeId == DropEnterEvent.TypeId()) OnDropEnter((DropEnterEvent)evt);
            else if (evt.eventTypeId == DropEvent.TypeId()) OnDrop((DropEvent)evt);
            else if (evt.eventTypeId == DropExitEvent.TypeId()) OnDropExit((DropExitEvent)evt);
        }

        private void OnDragOffer(DragOfferEvent e)
        {
            if (ParentNodeView != null && ParentNodeView.Graph != null && ParentNodeView.Graph.IsViewDrag(e))
            {
                ParentNodeView.Graph.OnDragOffer(e, true);
                return;
            }

            // Check if this is a port drag event
            if (!IsPortDrag(e) || !CanConnectToMore()) return;

            // The candidate edge is only created once the drag starts, so a click or an early cancel leaves nothing behind.
            e.AcceptDrag(this);
            e.SetDragThreshold(10);
        }

        /// <summary>
        /// Called by the container when a drag from this port starts. Null if the node left the graph before then.
        /// </summary>
        internal EdgeView CreateCandidateEdge()
        {
            GraphView graph = ParentNodeView?.Graph;
            if (graph == null)
            {
                return null;
            }

            EdgeView edge = new();
            edge.SetPort(this);
            graph.AddGraphElement(edge);
            return edge;
        }

        private void OnDropEnter(DropEnterEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture edgeDrag)
            {
                e.StopImmediatePropagation();

                if (edgeDrag.Edges.Count == 0)
                {
                    return;
                }

                // Grab dragged port
                PortView anchoredPort = edgeDrag.GetAnchoredPort(edgeDrag.Edges[0]);

                // Ignore drags from invalid ports
                if (!CanConnectTo(anchoredPort))
                {
                    return;
                }

                // But if it's compatible, light this port up
                AddToClassList(DROP_TARGET_CLASS);
            }
        }

        private void OnDrop(DropEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture edgeDrag)
            {
                e.StopImmediatePropagation();
                RemoveFromClassList(DROP_TARGET_CLASS);

                if (edgeDrag.Edges.Count == 0)
                {
                    return;
                }

                // Grab dragged port
                for (int i = edgeDrag.Edges.Count - 1; i >= 0; i--)
                {
                    ConnectToPortWithEdge(e, edgeDrag, edgeDrag.Edges[i]);
                }

                UpdateConnectedState();
            }
        }

        private void OnDropExit(DropExitEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture edgeDrag)
            {
                e.StopImmediatePropagation();
                RemoveFromClassList(DROP_TARGET_CLASS);

                if (edgeDrag.Edges.Count == 0)
                {
                    return;
                }

                UpdateConnectedState();
            }
        }

        private bool IsPortDrag<T>(DragAndDropEvent<T> e) where T : DragAndDropEvent<T>, new()
        {
            if ((MouseButton)e.button != MouseButton.LeftMouse)
            {
                return false;
            }

            if (!e.modifiers.IsNone())
            {
                return false;
            }

            return true;
        }

        private void ConnectToPortWithEdge(DropEvent e, EdgeDragGesture edgeDrag, EdgeView draggedEdge)
        {
            PortView anchoredPort = edgeDrag.GetAnchoredPort(draggedEdge);

            // Cancel invalid drags or already extant edges
            if (!CanConnectTo(anchoredPort))
            {
                // Real edges will be returned to their former state
                e.CancelDrag();
                return;
            }

            // Capture the ports being connected
            PortView inputPort;
            PortView outputPort;
            if (anchoredPort.Direction == Direction.Input)
            {
                inputPort = anchoredPort;
                outputPort = this;
            }
            else
            {
                inputPort = this;
                outputPort = anchoredPort;
            }

            // An output port must carry an outport id for the model to address it.
            if (outputPort.OutportId == null)
            {
                e.CancelDrag();
                return;
            }

            GraphView graph = ParentNodeView.Graph;
            CreateEdgeCommand connect = new (outputPort.NodeId, outputPort.OutportId, inputPort.NodeId);
            DestroyEdgeCommand destroyOldOutport = null;

            // An existing edge being re-routed
            if (draggedEdge.HasModelIdentity)
            {
                if (draggedEdge.InputPort == inputPort && draggedEdge.OutputPort == outputPort)
                {
                    e.CancelDrag();
                    return;
                }

                bool sameOutport = draggedEdge.FromNodeId == outputPort.NodeId && draggedEdge.OutportId == outputPort.OutportId;
                if (!sameOutport)
                {
                    destroyOldOutport = new DestroyEdgeCommand(new EdgeId(draggedEdge.FromNodeId, draggedEdge.OutportId));
                }
            }

            edgeDrag.Take(draggedEdge);
            if (destroyOldOutport != null)
            {
                graph.RequestCommandBatch("Reconnect Edge", destroyOldOutport, connect);
            }
            else
            {
                graph.RequestCommand(connect);
            }
        }

        // Home-plate chevron on a 14-unit grid: flat back, point toward the flow direction.
        private static readonly Vector2[] s_chevronPoints =
        {
            new(2, 2), new(8, 2), new(12, 7), new(8, 12), new(2, 12),
        };
        private const float CHEVRON_GRID = 14f;

        // Keyed by geometry so a USS override on one port cannot hand its chevron to every
        // other port through a single shared cache.
        private static readonly Dictionary<(float, float, bool), VectorImage> s_portChevronGraphics = new();
        private static VectorImage GetPortChevronGraphics(float size, float lineWidth, bool filled)
        {
            // The null check also covers a cached image destroyed by a domain reload.
            if (!s_portChevronGraphics.TryGetValue((size, lineWidth, filled), out VectorImage image) || image == null)
            {
                image = VectorImage.CreateInstance<VectorImage>();
                Painter2D painter = new Painter2D();
                float scale = size / CHEVRON_GRID;

                // SaveToVectorImage crops to the geometry's bounds, which differ between the fill and the
                // stroke. A transparent full-frame rect pins both images to the same frame so the layers align.
                painter.fillColor = Color.clear;
                painter.BeginPath();
                painter.MoveTo(Vector2.zero);
                painter.LineTo(new Vector2(size, 0));
                painter.LineTo(new Vector2(size, size));
                painter.LineTo(new Vector2(0, size));
                painter.ClosePath();
                painter.Fill();

                painter.BeginPath();
                painter.MoveTo(s_chevronPoints[0] * scale);
                for (int i = 1; i < s_chevronPoints.Length; i++)
                {
                    painter.LineTo(s_chevronPoints[i] * scale);
                }
                painter.ClosePath();

                if (filled)
                {
                    painter.fillColor = Color.white;
                    painter.Fill();
                }
                else
                {
                    painter.lineWidth = lineWidth;
                    painter.lineJoin = LineJoin.Miter;
                    painter.strokeColor = Color.white;
                    painter.Stroke();
                }

                painter.SaveToVectorImage(image);
                s_portChevronGraphics[(size, lineWidth, filled)] = image;
            }

            return image;
        }
        
        public override string ToString() => PortName;
    }
}
