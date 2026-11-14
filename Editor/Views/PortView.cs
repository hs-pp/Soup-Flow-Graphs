using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Manipulators;
using Soup.Editor.Views.Manipulators.Gestures;
using Soup.Editor.Views.Nodes;
using UnityEditor;
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

        private const float DRAG_THRESHOLD = 10f;
        private const float CHEVRON_GRID = 14f;

        private static readonly CustomStyleProperty<float> PORT_SIZE_PROPERTY = new("--port-size");
        private static readonly CustomStyleProperty<float> PORT_LINE_WIDTH_PROPERTY = new("--port-line-width");

        /** Home-plate chevron on a 14-unit grid: flat back, point toward the flow direction. */
        private static readonly Vector2[] CHEVRON_POINTS =
        {
            new(2, 2), new(8, 2), new(12, 7), new(8, 12), new(2, 12),
        };

        private static readonly Dictionary<(float, float, bool), VectorImage> CHEVRON_IMAGES = new();

        private readonly HashSet<EdgeView> m_connections = new();
        private BaseNodeView m_nodeView;
        private VisualElement m_root;
        private VisualElement m_connectorBox;
        private VisualElement m_connectorCap;
        private Label m_nameLabel;
        private bool m_dimmed;
        private float m_portSize = float.NaN;
        private float m_portLineWidth = float.NaN;

        public string NodeId { get; }
        public string OutportId { get; }
        public Direction Direction { get; }
        public Orientation Orientation { get; }
        public IEnumerable<EdgeView> Connections => m_connections;
        public BaseNodeView NodeView => m_nodeView;

        public string PortName
        {
            get => m_nameLabel.text;
            set
            {
                m_nameLabel.text = value;
                m_nameLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public bool Dimmed
        {
            get => m_dimmed;
            set
            {
                if (m_dimmed == value)
                {
                    return;
                }

                m_dimmed = value;
                EnableInClassList(DIMMED_CLASS, m_dimmed);
            }
        }

        public event Action<VisualElement> OnPositionChanged;

        static PortView()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DestroyChevronImages;
        }

        public PortView(string nodeId, string outportId, Direction direction, Orientation orientation = Orientation.Horizontal)
        {
            NodeId = nodeId;
            OutportId = outportId;
            Direction = direction;
            Orientation = orientation;

            CreateLayout();

            AddToClassList(PORT_VIEW_CLASS);
            AddToClassList($"{PORT_VIEW_CLASS}{Direction}{Orientation}");
            m_root.AddToClassList($"port{Direction}{Orientation}");
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void CreateLayout()
        {
            VisualTreeAsset uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_root = this.Q<VisualElement>(ROOT_TAG);
            m_nameLabel = this.Q<Label>(CONNECTOR_LABEL_TAG);
            m_connectorBox = this.Q<VisualElement>(CONNECTOR_BOX_TAG);
            m_connectorCap = this.Q<VisualElement>(CONNECTOR_CAP_TAG);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            if (!e.customStyle.TryGetValue(PORT_SIZE_PROPERTY, out float size) || !e.customStyle.TryGetValue(PORT_LINE_WIDTH_PROPERTY, out float lineWidth))
            {
                return;
            }

            if (size.Equals(m_portSize) && lineWidth.Equals(m_portLineWidth))
            {
                return;
            }

            m_portSize = size;
            m_portLineWidth = lineWidth;

            m_connectorBox.style.backgroundImage = new StyleBackground(GetChevronImage(size, lineWidth, filled: true));
            m_connectorCap.style.backgroundImage = new StyleBackground(GetChevronImage(size, lineWidth, filled: false));
        }

        internal void UpdateConnectedState()
        {
            EnableInClassList(CONNECTED_CLASS, IsConnected());
        }

        public void SetNodeView(BaseNodeView nodeView)
        {
            if (m_nodeView == nodeView)
            {
                return;
            }

            if (m_nodeView != null)
            {
                m_nodeView.OnPositionChanged -= OnNodeViewPositionChanged;
            }

            m_nodeView = nodeView;
            if (m_nodeView != null)
            {
                m_nodeView.OnPositionChanged += OnNodeViewPositionChanged;
            }
        }

        private void OnNodeViewPositionChanged(VisualElement nodeView)
        {
            OnPositionChanged?.Invoke(this);
        }

        public void Connect(EdgeView edge)
        {
            if (edge == null)
            {
                return;
            }

            m_connections.Add(edge);
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

        private bool IsConnected()
        {
            foreach (EdgeView edge in m_connections)
            {
                if (edge.IsDragged)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public bool CanConnectTo(PortView other)
        {
            return !IsSameDirection(other) && !IsOnSameNode(other) && CanConnectToMore() && other.CanConnectToMore();
        }

        private bool CanConnectToMore() => Direction == Direction.Input || !IsConnected();
        private bool IsSameDirection(PortView other) => Direction == other.Direction;
        private bool IsOnSameNode(PortView other) => other.m_nodeView == m_nodeView;

        public Vector2 GetGlobalCenter() => m_connectorBox.LocalToWorld(m_connectorBox.layout.size / 2f);

        [EventInterest(typeof(DragOfferEvent), typeof(DropEnterEvent), typeof(DropEvent), typeof(DropExitEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt.target != this)
            {
                return;
            }

            if (evt.eventTypeId == DragOfferEvent.TypeId())
            {
                OnDragOffer((DragOfferEvent)evt);
            }
            else if (evt.eventTypeId == DropEnterEvent.TypeId())
            {
                OnDropEnter((DropEnterEvent)evt);
            }
            else if (evt.eventTypeId == DropEvent.TypeId())
            {
                OnDrop((DropEvent)evt);
            }
            else if (evt.eventTypeId == DropExitEvent.TypeId())
            {
                OnDropExit((DropExitEvent)evt);
            }
        }

        private void OnDragOffer(DragOfferEvent e)
        {
            GraphElement.OfferDrag(e, m_nodeView?.Graph, this, CanConnectToMore());
            if (e.GetDraggedElement() == this)
            {
                e.SetDragThreshold(DRAG_THRESHOLD);
            }
        }

        internal EdgeView CreateCandidateEdge()
        {
            GraphView graph = m_nodeView?.Graph;
            if (graph == null)
            {
                return null;
            }

            EdgeView edge = new(this);
            graph.AddGraphElement(edge);
            return edge;
        }

        private void OnDropEnter(DropEnterEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture edgeDrag)
            {
                e.StopImmediatePropagation();
                if (CanConnectAll(edgeDrag))
                {
                    AddToClassList(DROP_TARGET_CLASS);
                }
            }
        }

        private void OnDrop(DropEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture edgeDrag)
            {
                e.StopImmediatePropagation();
                RemoveFromClassList(DROP_TARGET_CLASS);
                ConnectDraggedEdges(e, edgeDrag);
            }
        }

        private void OnDropExit(DropExitEvent e)
        {
            if (e.GetUserData() is EdgeDragGesture)
            {
                e.StopImmediatePropagation();
                RemoveFromClassList(DROP_TARGET_CLASS);
            }
        }

        private bool CanConnectAll(EdgeDragGesture edgeDrag)
        {
            foreach (EdgeView edge in edgeDrag.Edges)
            {
                if (!CanConnectTo(edgeDrag.GetAnchoredPort(edge)))
                {
                    return false;
                }
            }

            return true;
        }

        private void ConnectDraggedEdges(DropEvent e, EdgeDragGesture edgeDrag)
        {
            List<EdgeView> movedEdges = new();
            List<IGraphCommand> commands = new();
            foreach (EdgeView edge in edgeDrag.Edges)
            {
                PortView anchoredPort = edgeDrag.GetAnchoredPort(edge);
                if (!CanConnectTo(anchoredPort))
                {
                    e.CancelDrag();
                    return;
                }

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

                if (edge.HasModelIdentity)
                {
                    if (edge.InputPort == inputPort && edge.OutputPort == outputPort)
                    {
                        continue;
                    }

                    bool sameOutport = edge.FromNodeId == outputPort.NodeId && edge.OutportId == outputPort.OutportId;
                    if (!sameOutport)
                    {
                        commands.Add(new DestroyEdgeCommand(new EdgeId(edge.FromNodeId, edge.OutportId)));
                    }
                }

                commands.Add(new CreateEdgeCommand(outputPort.NodeId, outputPort.OutportId, inputPort.NodeId));
                movedEdges.Add(edge);
            }

            if (movedEdges.Count == 0)
            {
                e.CancelDrag();
                return;
            }

            string undoName;
            if (!movedEdges[0].HasModelIdentity)
            {
                undoName = "Connect Edge";
            }
            else if (movedEdges.Count == 1)
            {
                undoName = "Reconnect Edge";
            }
            else
            {
                undoName = "Reconnect Edges";
            }

            foreach (EdgeView edge in movedEdges)
            {
                edgeDrag.Take(edge);
            }

            m_nodeView.Graph.RequestCommandBatch(undoName, commands.ToArray());
        }

        private static VectorImage GetChevronImage(float size, float lineWidth, bool filled)
        {
            if (CHEVRON_IMAGES.TryGetValue((size, lineWidth, filled), out VectorImage image) && image != null)
            {
                return image;
            }

            image = VectorImage.CreateInstance<VectorImage>();
            image.name = $"PortChevron_{(filled ? "Fill" : "Outline")}_s{size}_w{lineWidth}";
            image.hideFlags = HideFlags.HideAndDontSave;

            Painter2D painter = new();
            float scale = size / CHEVRON_GRID;

            painter.fillColor = Color.clear;
            painter.BeginPath();
            painter.MoveTo(Vector2.zero);
            painter.LineTo(new Vector2(size, 0));
            painter.LineTo(new Vector2(size, size));
            painter.LineTo(new Vector2(0, size));
            painter.ClosePath();
            painter.Fill();

            painter.BeginPath();
            painter.MoveTo(CHEVRON_POINTS[0] * scale);
            for (int i = 1; i < CHEVRON_POINTS.Length; i++)
            {
                painter.LineTo(CHEVRON_POINTS[i] * scale);
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
            CHEVRON_IMAGES[(size, lineWidth, filled)] = image;
            return image;
        }

        private static void DestroyChevronImages()
        {
            foreach (VectorImage image in CHEVRON_IMAGES.Values)
            {
                if (image != null)
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }

            CHEVRON_IMAGES.Clear();
        }

        public override string ToString() => PortName;
    }
}
