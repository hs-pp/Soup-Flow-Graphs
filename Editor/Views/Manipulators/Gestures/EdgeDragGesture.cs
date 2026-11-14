using System.Collections.Generic;
using Soup.Editor.Views.Edges;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators.Gestures
{
    public class EdgeDragGesture : DragGesture
    {
        private readonly PortView m_port;
        private readonly List<EdgeView> m_edges = new();
        public IReadOnlyList<EdgeView> Edges => m_edges;
        private readonly HashSet<EdgeView> m_taken = new();
        private bool m_draggedInput;

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
            if (draggedPort != null)
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
                // The dragged edge itself wasn't selected. Drag just it.
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
                edge.SetInputPositionOverride(edge.InputPosition);
            }
            else
            {
                edge.SetOutputPositionOverride(edge.OutputPosition);
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

        public void Take(EdgeView edge)
        {
            m_taken.Add(edge);

            // A reconcile may have removed it already.
            if (edge.Graph != null)
            {
                Graph.RemoveGraphElement(edge);
            }
        }

        public PortView GetAnchoredPort(EdgeView edge) => m_draggedInput ? edge.OutputPort : edge.InputPort;

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

                if (edge.HasModelIdentity)
                {
                    edge.ClearPositionOverrides();
                    edge.Selected = false;
                }
                else
                {
                    Graph.RemoveGraphElement(edge);
                }
            }

            Graph.IlluminateAllPorts();
        }

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

        private bool IsRemoved(EdgeView edge)
        {
            return edge.Graph == null && !m_taken.Contains(edge);
        }
    }
}
