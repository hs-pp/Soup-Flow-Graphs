using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators.Gestures
{
    /// <summary>
    /// Moves the selected nodes and groups, plus everything inside the selected groups, by one snapped offset taken from the dragged element.
    /// </summary>
    internal sealed class MoveSelectionGesture : DragGesture
    {
        private readonly List<GraphElement> m_moving = new();
        private readonly List<Vector2> m_startPositions = new(); // Parallel to m_moving.
        private Vector2 m_anchorStart;
        private Vector2 m_rawOffset;
        private Vector2 m_appliedOffset;

        public MoveSelectionGesture(GraphView graphView, GraphElement dragged) : base(graphView)
        {
            Dragged = dragged;
        }

        public override bool Begin(DragBeginEvent e)
        {
            HashSet<GraphElement> moving = new();
            foreach (BaseNodeView node in Content.NodesSelected)
            {
                moving.Add(node);
            }

            foreach (GroupView group in Content.GroupsSelected.ToList())
            {
                moving.Add(group);
                Content.CollectGroupContents(group, moving);
            }

            foreach (GraphElement element in moving)
            {
                m_moving.Add(element);
                m_startPositions.Add(element.GetPosition());
                element.pickingMode = PickingMode.Ignore;
            }

            m_anchorStart = Dragged.GetPosition();
            return true;
        }

        public override void Drag(DragEvent e)
        {
            MoveBy(e.mouseDelta / Graph.CurrentScale);
        }

        public override void ApplyPanDelta(Vector2 delta)
        {
            MoveBy(delta);
        }

        private void MoveBy(Vector2 rawDelta)
        {
            m_rawOffset += rawDelta;
            Vector2 offset = Graph.SnapPosition(m_anchorStart + m_rawOffset) - m_anchorStart;
            if (offset == m_appliedOffset)
            {
                return;
            }

            m_appliedOffset = offset;
            for (int i = 0; i < m_moving.Count; i++)
            {
                m_moving[i].SetPosition(m_startPositions[i] + offset);
            }
        }

        public override void End()
        {
            Dictionary<string, Vector2> nodePositions = new();
            Dictionary<string, Vector2> groupPositions = new();
            for (int i = 0; i < m_moving.Count; i++)
            {
                GraphElement element = m_moving[i];
                element.pickingMode = PickingMode.Position;

                // A drag that snapped back to where it started changes nothing.
                if (element.Id == null || m_appliedOffset == Vector2.zero)
                {
                    continue;
                }

                // Read from the drag rather than the views, which a reconcile mid-drag may have moved back to the model.
                Vector2 position = m_startPositions[i] + m_appliedOffset;
                if (element is GroupView)
                {
                    groupPositions[element.Id] = position;
                }
                else
                {
                    nodePositions[element.Id] = position;
                }
            }

            if (nodePositions.Count > 0 || groupPositions.Count > 0)
            {
                Graph.RequestCommand(new MoveGraphElementsCommand(nodePositions, groupPositions));
            }
        }

        public override void Cancel(bool keepPositions)
        {
            for (int i = 0; i < m_moving.Count; i++)
            {
                GraphElement element = m_moving[i];
                element.pickingMode = PickingMode.Position;

                // After an undo the observer has already put every surviving view at its model position.
                if (!keepPositions)
                {
                    element.SetPosition(m_startPositions[i]);
                }
            }
        }

        public override bool IsStale() => base.IsStale() || AnyRemoved(m_moving);
    }
}
