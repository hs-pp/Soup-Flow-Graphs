using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Manipulators.Gestures;
using Soup.Editor.Views.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators
{
    /// <summary>
    /// Runs the drags of graph content. Elements offer themselves, and this accepts every drag as the container,
    /// which is never detached, so the drag manipulator's checks for a dragged element leaving the hierarchy stay meaningful.
    /// Each offer gets a new <see cref="DragGesture"/>, so nothing carries over from a drag that was cancelled before it began.
    /// </summary>
    internal sealed class ElementDragManipulator : Manipulator
    {
        private readonly GraphView m_graphView;
        private DragGesture m_gesture;
        private bool m_begun; // A click ends without a begin.
        private bool m_keepPositionsOnCancel;

        public ElementDragManipulator(GraphView graphView)
        {
            m_graphView = graphView;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            if (target is not GraphElementContainer)
            {
                throw new("ElementDragManipulator can only be added to a GraphElementContainer");
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

        /// <summary>
        /// For a cancel that follows a reconcile, which has already put every surviving view at its model position.
        /// Stays set until the cancel arrives or the next offer, since the cancel event may be dispatched later.
        /// </summary>
        internal void KeepPositionsOnCancel() => m_keepPositionsOnCancel = true;

        private DragGesture CreateGesture(DragOfferEvent e)
        {
            VisualElement dragged = e.GetDraggedElement();
            return dragged switch
            {
                GroupResizeHandle handle => new GroupResizeGesture(m_graphView, handle.Group, e.mousePosition),
                PortView port => EdgeDragGesture.FromPort(m_graphView, port),
                EdgeView edge => EdgeDragGesture.FromEdge(m_graphView, edge),
                BaseNodeView or GroupView => new MoveSelectionGesture(m_graphView, (GraphElement)dragged),
                _ => null,
            };
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
                // Only a cancel scrolls the view back. A drop keeps it, including one whose handler took the dragged edges.
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

        /// <summary>
        /// The point, in world space, whose distance from the view's edges sets the auto-pan speed.
        /// </summary>
        internal Vector2 PanAnchor => m_gesture.Dragged.GetGlobalCenter();

        /// <summary>
        /// Moves the drag with the view as it auto-pans, in content space.
        /// </summary>
        internal void ApplyPanDelta(Vector2 delta) => m_gesture.ApplyPanDelta(delta);

        #endregion
    }
}
