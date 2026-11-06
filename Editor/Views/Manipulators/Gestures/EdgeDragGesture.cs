using System.Collections.Generic;
using Soup.Editor.Views.Edges;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators.Gestures
{
    /// <summary>
    /// Drags one end of an edge, or of every selected edge at the grabbed port, and is the payload drop targets read.
    /// A drag from a port drags the free end of a new candidate edge. Drop targets <see cref="Take"/> the edges they accept,
    /// and the drag puts back the rest when it ends.
    /// </summary>
    internal sealed class EdgeDragGesture : DragGesture
    {
        private readonly PortView m_port; // A drag from a port gets its candidate edge at begin, so a click leaves nothing behind.
        private readonly List<EdgeView> m_edges = new();
        private readonly HashSet<EdgeView> m_taken = new();
        private bool m_draggedInput; // Which end of every dragged edge follows the mouse.

        private EdgeDragGesture(GraphView graphView, PortView port, EdgeView edge) : base(graphView)
        {
            m_port = port;
            Dragged = edge;
        }

        public static EdgeDragGesture FromPort(GraphView graphView, PortView port) => new(graphView, port, null);
        public static EdgeDragGesture FromEdge(GraphView graphView, EdgeView edge) => new(graphView, null, edge);

        public override bool Begin(DragBeginEvent e)
        {
            if (m_port != null)
            {
                Dragged = m_port.CreateCandidateEdge();
            }

            if (Dragged is not EdgeView draggedEdge)
            {
                return false;
            }

            if (draggedEdge.InputPort == null)
            {
                m_draggedInput = true;
            }
            else if (draggedEdge.OutputPort == null)
            {
                m_draggedInput = false;
            }
            else
            {
                Vector2 inputPos = draggedEdge.InputPort.GetGlobalCenter();
                Vector2 outputPos = draggedEdge.OutputPort.GetGlobalCenter();
                float distanceFromInput = (e.mousePosition - inputPos).sqrMagnitude;
                float distanceFromOutput = (e.mousePosition - outputPos).sqrMagnitude;
                m_draggedInput = distanceFromInput < distanceFromOutput;
            }

            PortView draggedPort = m_draggedInput ? draggedEdge.InputPort : draggedEdge.OutputPort;

            // Collect selection and set them to drag mode
            if (draggedPort != null && draggedPort.AllowMultiDrag)
            {
                foreach (EdgeView edge in draggedPort.Connections)
                {
                    if (edge.Selected)
                    {
                        Latch(edge);
                    }
                }
            }

            if (m_edges.Count == 0)
            {
                // The dragged edge itself wasn't selected; drag just it.
                Latch(draggedEdge);
            }

            Graph.IlluminateCompatiblePorts(GetAnchoredPort(draggedEdge));
            e.SetUserData(this);
            return true;
        }

        private void Latch(EdgeView edge)
        {
            // Latch the end in place so it enters drag mode without visually jumping.
            if (m_draggedInput)
            {
                edge.SetInputPositionOverride(edge.To);
            }
            else
            {
                edge.SetOutputPositionOverride(edge.From);
            }

            edge.pickingMode = PickingMode.Ignore;
            edge.visible = true;
            m_edges.Add(edge);
        }

        public override void Drag(DragEvent e)
        {
            Vector2 newPosition = Content.WorldToLocal(e.mousePosition);
            for (int i = 0; i < m_edges.Count; i++)
            {
                EdgeView edge = m_edges[i];
                if (m_draggedInput)
                {
                    edge.SetInputPositionOverride(newPosition);
                }
                else
                {
                    edge.SetOutputPositionOverride(newPosition);
                }
            }
        }

        public override void ApplyPanDelta(Vector2 delta)
        {
            for (int i = 0; i < m_edges.Count; i++)
            {
                m_edges[i].ApplyDeltaToPosition(delta);
            }
        }

        /// <summary>
        /// For a drop target that accepts this edge: removes it from the graph, and the drag leaves it alone from then on.
        /// The target then dispatches whatever command gives the edge its new place in the model.
        /// </summary>
        public void Take(EdgeView edge)
        {
            m_taken.Add(edge);

            // A reconcile may have removed it already.
            if (edge.Graph != null)
            {
                Graph.RemoveGraphElement(edge);
            }
        }

        /// <summary>
        /// The port the edge stays attached to while its other end is dragged. Null for an edge that has none.
        /// </summary>
        public PortView GetAnchoredPort(EdgeView edge) => m_draggedInput ? edge.OutputPort : edge.InputPort;

        // Any edge no drop target took was released where nothing accepted it.
        public override void End() => Release();

        public override void Cancel(bool keepPositions) => Release();

        private void Release()
        {
            for (int i = m_edges.Count - 1; i >= 0; i--)
            {
                EdgeView edge = m_edges[i];

                // Taken by a drop target, or removed by a reconcile.
                if (edge.Graph == null)
                {
                    continue;
                }

                edge.pickingMode = PickingMode.Position;

                if (edge.IsRealEdge())
                {
                    edge.UnsetPositionOverrides();
                    edge.Selected = false;
                }
                else
                {
                    Graph.RemoveGraphElement(edge);
                }
            }

            Graph.IlluminateAllPorts();
        }

        /// <summary>
        /// True once a reconcile has removed an edge this drag holds. Edges a drop target took don't count.
        /// </summary>
        public override bool IsStale()
        {
            if (Dragged == null || IsRemoved((EdgeView)Dragged))
            {
                return true;
            }

            for (int i = 0; i < m_edges.Count; i++)
            {
                if (IsRemoved(m_edges[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsRemoved(EdgeView edge) => edge.Graph == null && !m_taken.Contains(edge);

        /// <summary>
        /// The edges being dragged, for drop targets to read.
        /// </summary>
        public IReadOnlyList<EdgeView> Edges => m_edges;
    }
}
