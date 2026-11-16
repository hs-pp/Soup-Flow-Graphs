using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators
{
    public class DragAndDropManipulator : Manipulator
    {
        private bool m_dragBegun;
        private float m_dragThreshold;
        private Vector2 m_lastMousePosition;

        internal VisualElement DraggedElement { get; set; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut);
            target.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        #region Event Handlers
        private void OnMouseDown(MouseDownEvent e)
        {
            // Don't tolerate clicking other buttons while dragging
            if (DraggedElement != null)
            {
                CancelDrag();
                return;
            }

            // Create event
            using (DragOfferEvent dragOfferEvent = DragOfferEvent.GetPooled(e))
            {
                // Set parent
                dragOfferEvent.Manipulator = this;

                // Send event
                target.SendEvent(dragOfferEvent);
            }

            // Record mouse position
            m_lastMousePosition = e.mousePosition;
        }

        internal void OnDragOfferComplete(DragOfferEvent dragOfferEvent)
        {
            // Check for cancel request or nobody accepting the event
            if (dragOfferEvent.IsCancelled() || DraggedElement == null)
            {
                Reset();
            }
            else
            {
                // Capture any threshold requests
                m_dragThreshold = dragOfferEvent.GetDragThreshold();

                // Capturing ensures we retain the mouse events even if the mouse leaves the target.
                target.CaptureMouse();
            }
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            // If there's no drag, bail
            if (DraggedElement == null)
            {
                return;
            }

            // Check if we're starting a drag
            if (!m_dragBegun)
            {
                // Have we breached the drag threshold?
                if (Vector2.Distance(m_lastMousePosition, e.mousePosition) < m_dragThreshold)
                {
                    // Not time yet to start dragging
                    return;
                }

                // We are starting a drag!
                m_dragBegun = true;

                // Drag begin
                using (DragBeginEvent dragBeginEvent = DragBeginEvent.GetPooled(e))
                {
                    // Set parent
                    dragBeginEvent.Manipulator = this;

                    // Set target
                    dragBeginEvent.target = DraggedElement;

                    // Send event
                    target.SendEvent(dragBeginEvent);
                }

                // Sending drag event after drag begin so we can check for cancellation
                return;
            }

            // Send drag event
            SendDragEvent(e);
        }

        internal void OnDragBeginComplete(DragBeginEvent dragBeginEvent)
        {
            // Check for cancel request
            if (dragBeginEvent.IsCancelled())
            {
                CancelDrag();
                return;
            }

            // No cancellation, send drag event
            SendDragEvent(dragBeginEvent);
        }

        private void SendDragEvent<T>(MouseEventBase<T> e) where T : MouseEventBase<T>, new()
        {
            // We breached the threshold, time to drag
            using (DragEvent dragEvent = DragEvent.GetPooled(e))
            {
                // Set parent
                dragEvent.Manipulator = this;

                // Set target
                dragEvent.target = DraggedElement;

                // Set correct drag delta
                dragEvent.SetMouseDelta(e.mousePosition - m_lastMousePosition);

                // Send event
                target.SendEvent(dragEvent);
            }

            // Record mouse position
            m_lastMousePosition = e.mousePosition;
        }

        internal void OnDragComplete(DragEvent dragEvent)
        {
            // Check for cancel request
            if (dragEvent.IsCancelled())
            {
                CancelDrag();
            }
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            // If there's no drag, bail
            if (DraggedElement == null)
            {
                return;
            }

            // Complete drag
            using (DragEndEvent dragEndEvent = DragEndEvent.GetPooled(e))
            {
                // Set parent
                dragEndEvent.Manipulator = this;

                // Set target
                dragEndEvent.target = DraggedElement;

                // Send event
                target.SendEvent(dragEndEvent);
            }
        }

        internal void OnDragEndComplete(DragEndEvent dragEndEvent)
        {
            // Reset everything
            Reset();
        }

        internal void CancelActiveDrag()
        {
            if (DraggedElement != null)
            {
                CancelDrag();
            }
        }

        private void CancelDrag()
        {
            if (m_dragBegun)
            {
                using (DragCancelEvent dragCancelEvent = DragCancelEvent.GetPooled())
                {
                    // Set parent
                    dragCancelEvent.Manipulator = this;

                    // Set target
                    dragCancelEvent.target = DraggedElement;

                    // Send cancel event
                    target.SendEvent(dragCancelEvent);
                }
            }
            else
            {
                Reset();
            }
        }

        internal void OnDragCancelComplete(DragCancelEvent dragCancelEvent)
        {
            Reset();
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent e)
        {
            if (DraggedElement != null)
            {
                CancelDrag();
            }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            // Only listen to ESC while dragging
            if (e.keyCode != KeyCode.Escape || DraggedElement == null)
            {
                return;
            }

            // Notify the draggable
            CancelDrag();
        }

        #endregion

        #region Helpers

        private void Reset()
        {
            DraggedElement = null;
            m_dragBegun = false;
            target.ReleaseMouse();
        }

        #endregion
    }

    #region Events

    public abstract class DragAndDropEvent<T> : MouseEventBase<T> where T : DragAndDropEvent<T>, new()
    {
        private bool m_cancelled;
        internal DragAndDropManipulator Manipulator { get; set; }

        internal void SetMouseDelta(Vector2 delta) => mouseDelta = delta;
        public bool IsCancelled() => m_cancelled;
        public void CancelDrag() => m_cancelled = true;

        protected override void Init()
        {
            base.Init();
            m_cancelled = false;
            Manipulator = null;
        }
    }

    public class DragOfferEvent : DragAndDropEvent<DragOfferEvent>
    {
        private float m_dragThreshold;

        public VisualElement GetDraggedElement() => Manipulator.DraggedElement;
        internal float GetDragThreshold() => m_dragThreshold;
        public void SetDragThreshold(float threshold) => m_dragThreshold = threshold;

        public void AcceptDrag(VisualElement draggedElement)
        {
            Manipulator.DraggedElement = draggedElement;
        }

        protected override void Init()
        {
            base.Init();
            m_dragThreshold = 0;
        }

        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            Manipulator.OnDragOfferComplete(this);
        }
    }

    public class DragBeginEvent : DragAndDropEvent<DragBeginEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            Manipulator.OnDragBeginComplete(this);
        }
    }

    public class DragEvent : DragAndDropEvent<DragEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            Manipulator.OnDragComplete(this);
        }
    }

    public class DragEndEvent : DragAndDropEvent<DragEndEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            Manipulator.OnDragEndComplete(this);
        }
    }

    public class DragCancelEvent : DragAndDropEvent<DragCancelEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            Manipulator.OnDragCancelComplete(this);
        }
    }

    #endregion
}
