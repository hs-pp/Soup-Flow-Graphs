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
        private readonly GraphView m_graphView;
        private DragGesture m_gesture;
        private bool m_begun;
        private bool m_keepPositionsOnCancel;

        public ElementDragManipulator(GraphView graphView)
        {
            m_graphView = graphView;
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
                m_graphView.StartPan();
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
                m_graphView.StopPan(false);

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

            m_graphView.StopPan(true);
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
        internal Vector2 PanAnchor => m_gesture.Dragged.GetGlobalCenter();
        internal void ApplyPanDelta(Vector2 delta) => m_gesture.ApplyPanDelta(delta);
        #endregion
    }
}
