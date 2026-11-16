using System;
using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Manipulators.Gestures;
using Soup.Editor.Views.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators
{
    public class ElementDragManipulator : Manipulator
    {
        private const float PAN_AREA_WIDTH = 100f;
        private const float PAN_SPEED = 4f;
        private const float MAX_PAN_SPEED = 2.5f * PAN_SPEED;
        private const int PAN_INTERVAL = 10;

        private readonly GraphView m_graphView;
        private readonly IVisualElementScheduledItem m_panSchedule;
        private DragGesture m_gesture;
        private bool m_begun;
        private bool m_keepPositionsOnCancel;
        private Vector2 m_panOriginDiff;

        public ElementDragManipulator(GraphView graphView)
        {
            m_graphView = graphView;
            m_panSchedule = graphView.schedule
                .Execute(Pan)
                .Every(PAN_INTERVAL)
                .StartingIn(PAN_INTERVAL);
            m_panSchedule.Pause();
        }

        protected override void RegisterCallbacksOnTarget()
        {
            if (target is not GraphElementContainer)
            {
                throw new InvalidOperationException("[Soup] ElementDragManipulator can only be added to a GraphElementContainer.");
            }

            target.RegisterCallback<DragOfferEvent>(OnDragOffer);
            target.RegisterCallback<DragBeginEvent>(OnDragBegin);
            target.RegisterCallback<DragEvent>(OnDrag);
            target.RegisterCallback<DragEndEvent>(OnDragEnd);
            target.RegisterCallback<DragCancelEvent>(OnDragCancel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<DragOfferEvent>(OnDragOffer);
            target.UnregisterCallback<DragBeginEvent>(OnDragBegin);
            target.UnregisterCallback<DragEvent>(OnDrag);
            target.UnregisterCallback<DragEndEvent>(OnDragEnd);
            target.UnregisterCallback<DragCancelEvent>(OnDragCancel);
        }

        #region Drag and Drop
        internal void KeepPositionsOnCancel() => m_keepPositionsOnCancel = true;

        private DragGesture CreateGesture(DragOfferEvent e)
        {
            switch (e.GetDraggedElement())
            {
                case GroupResizeHandle handle:
                    return new GroupResizeGesture(m_graphView, handle.Group, e.mousePosition);
                case PortView port:
                    return EdgeDragGesture.FromPort(m_graphView, port);
                case EdgeView edge:
                    return EdgeDragGesture.FromEdge(m_graphView, edge);
                case BaseNodeView node:
                    return new MoveSelectionGesture(m_graphView, node);
                case GroupView group:
                    return new MoveSelectionGesture(m_graphView, group);
                default:
                    return null;
            }
        }

        private void OnDragOffer(DragOfferEvent e)
        {
            Reset();

            m_gesture = CreateGesture(e);
            if (m_gesture == null)
            {
                return;
            }

            e.StopImmediatePropagation();
            e.AcceptDrag(target);
        }

        private void OnDragBegin(DragBeginEvent e)
        {
            if (m_gesture == null)
            {
                return;
            }

            e.StopImmediatePropagation();

            if (!m_gesture.Begin(e))
            {
                e.CancelDrag();
                return;
            }

            m_begun = true;
            if (m_gesture.PansView)
            {
                StartPan();
            }
        }

        private void OnDrag(DragEvent e)
        {
            e.StopImmediatePropagation();

            if (!m_begun || m_gesture.IsStale())
            {
                e.CancelDrag();
                return;
            }

            m_gesture.Drag(e);
        }

        private void OnDragEnd(DragEndEvent e)
        {
            e.StopImmediatePropagation();

            if (m_begun)
            {
                // Only a cancel scrolls the view back.
                StopPan(false);

                if (m_gesture.IsStale())
                {
                    m_gesture.Cancel(m_keepPositionsOnCancel);
                }
                else
                {
                    m_gesture.End();
                }
            }

            Reset();
        }

        private void OnDragCancel(DragCancelEvent e)
        {
            e.StopImmediatePropagation();

            StopPan(true);
            if (m_begun)
            {
                m_gesture.Cancel(m_keepPositionsOnCancel);
            }

            Reset();
        }

        private void Reset()
        {
            m_gesture = null;
            m_begun = false;
            m_keepPositionsOnCancel = false;
        }
        #endregion

        #region Auto-Pan
        /// <summary>
        /// Auto-pans while the dragged element nears the edges of the view, until StopPan.
        /// </summary>
        private void StartPan()
        {
            m_panOriginDiff = Vector2.zero;
            m_panSchedule.Resume();
        }

        private void StopPan(bool resetView)
        {
            if (!m_panSchedule.isActive)
            {
                return;
            }

            m_panSchedule.Pause();
            if (resetView)
            {
                m_graphView.SetViewTransform(m_graphView.ViewPosition + m_panOriginDiff, m_graphView.ViewScale);
            }
        }

        /// <summary>
        /// The pan speed along one axis of the given length: zero outside the pan areas, PAN_SPEED at the edge,
        /// and faster past it.
        /// </summary>
        private static float GetPanSpeed(float position, float length)
        {
            if (position <= PAN_AREA_WIDTH)
            {
                return -(PAN_AREA_WIDTH - position) / PAN_AREA_WIDTH * PAN_SPEED;
            }

            if (position >= length - PAN_AREA_WIDTH)
            {
                return (position - (length - PAN_AREA_WIDTH)) / PAN_AREA_WIDTH * PAN_SPEED;
            }

            return 0f;
        }

        private void Pan()
        {
            // The pan areas are measured from the view's own edges, not the window's.
            Vector2 position = m_graphView.WorldToLocal(m_gesture.Dragged.GetGlobalCenter());
            Vector2 size = m_graphView.layout.size;
            Vector2 speed = new(GetPanSpeed(position.x, size.x), GetPanSpeed(position.y, size.y));
            speed = Vector2.ClampMagnitude(speed, MAX_PAN_SPEED);
            if (Vector2.zero == speed)
            {
                return;
            }

            m_panOriginDiff += speed;

            float scale = m_graphView.ViewScale;
            m_graphView.SetViewTransform(m_graphView.ViewPosition - speed, scale);
            m_gesture.ApplyPanDelta(speed / scale);
        }
        #endregion
    }
}
