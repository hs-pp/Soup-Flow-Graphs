using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.State;
using Soup.Editor.Views.Groups;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators.Gestures
{
    /// <summary>
    /// Drags a group's bottom-right corner. Its position never changes.
    /// </summary>
    internal sealed class GroupResizeGesture : DragGesture
    {
        private readonly GroupView m_group;
        private readonly Vector2 m_startMouse;
        private Vector2 m_size;

        /// <param name="mousePosition">The offer's mouse position, so the corner moves with the mouse from where it was pressed.</param>
        public GroupResizeGesture(GraphView graphView, GroupView group, Vector2 mousePosition) : base(graphView)
        {
            Dragged = group;
            m_group = group;
            m_startMouse = Content.WorldToLocal(mousePosition);
            m_size = group.CanvasRect.size;
        }

        public override bool PansView => false;

        public override bool Begin(DragBeginEvent e) => true;

        public override void Drag(DragEvent e)
        {
            Rect rect = m_group.CanvasRect;
            Vector2 corner = rect.max + Content.WorldToLocal(e.mousePosition) - m_startMouse;
            m_size = GroupMetaData.ClampSize(Graph.SnapPosition(corner) - rect.position);
            m_group.PreviewSize(m_size);
        }

        public override void End()
        {
            if (m_size != m_group.CanvasRect.size)
            {
                Graph.RequestCommand(new ResizeGroupCommand(m_group.Id, m_size));
            }
        }

        public override void Cancel(bool keepPositions)
        {
            m_group.PreviewSize(m_group.CanvasRect.size);
        }
    }
}
