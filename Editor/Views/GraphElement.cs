using System;
using Soup.Editor.Views.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public abstract class GraphElement : VisualElement
    {
        private const string STYLECLASS_NAME = "graphElement";

        private GraphView m_graphView;
        private SelectableManipulator m_selectableManipulator;
        private Vector2 m_position;
        private bool m_selected;

        public virtual string Id { get; protected set; }
        public abstract int Layer { get; }
        protected Capabilities Capabilities { get; set; }

        public GraphView Graph
        {
            get => m_graphView;
            set
            {
                if (m_graphView == value)
                {
                    return;
                }

                if (value == null)
                {
                    OnRemovedFromGraphView();
                    m_graphView = null;
                }
                else
                {
                    m_graphView = value;
                    OnAddedToGraphView();
                }
            }
        }

        public bool Selected
        {
            get => m_selected;
            set
            {
                if (m_selected == value || (value && !IsSelectable()))
                {
                    return;
                }

                m_selected = value;
                if (value && IsAscendable() && resolvedStyle.position != Position.Relative)
                {
                    BringToFront();
                }

                OnSelectedChanged();
                m_graphView?.NotifyContentChanged();
            }
        }

        public virtual Rect CanvasRect => new(layout.position + m_position, layout.size);

        public event Action<VisualElement> OnPositionChanged;

        protected GraphElement()
        {
            AddToClassList(STYLECLASS_NAME);
            RegisterCallback<GeometryChangedEvent>(_ => m_graphView?.NotifyContentChanged());
        }

        protected virtual void OnAddedToGraphView()
        {
            AddManipulators();
        }

        protected virtual void OnRemovedFromGraphView()
        {
            pickingMode = PickingMode.Position;
            Selected = false;
        }

        protected virtual void AddManipulators()
        {
            if (m_selectableManipulator != null)
            {
                this.RemoveManipulator(m_selectableManipulator);
                m_selectableManipulator = null;
            }

            m_selectableManipulator = new SelectableManipulator();
            this.AddManipulator(m_selectableManipulator);
        }

        public virtual void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView) { }

        /// <summary>
        /// Called after Selected changes, so Selected already holds the new value.
        /// </summary>
        protected virtual void OnSelectedChanged() { }

        public virtual Vector2 GetPosition() => m_position;

        public virtual void SetPosition(Vector2 newPosition)
        {
            SetPositionWithoutNotify(newPosition);
            OnPositionChanged?.Invoke(this);
            m_graphView?.NotifyContentChanged();
        }

        protected void SetPositionWithoutNotify(Vector2 newPosition)
        {
            m_position = newPosition;
            style.translate = new Translate(newPosition.x, newPosition.y);
        }

        public virtual void ApplyDeltaToPosition(Vector2 delta)
        {
            SetPosition(m_position + delta);
        }

        public virtual Vector2 GetCenter() => layout.center + m_position;
        public Vector2 GetGlobalCenter() => Graph.ContentContainer.LocalToWorld(GetCenter());

        protected void OfferSelectionDrag(DragOfferEvent e) => OfferDrag(e, Graph, this, IsMovable());

        internal static void OfferDrag(DragOfferEvent e, GraphView graph, VisualElement dragged, bool canDrag)
        {
            if (graph != null && graph.IsViewDrag(e))
            {
                graph.OnDragOffer(e, true);
            }
            else if (canDrag && (MouseButton)e.button == MouseButton.LeftMouse && e.modifiers.IsNone())
            {
                e.AcceptDrag(dragged);
            }
        }

        public bool IsMovable() => (Capabilities & Capabilities.Movable) == Capabilities.Movable;
        public bool IsAscendable() => (Capabilities & Capabilities.Ascendable) == Capabilities.Ascendable;
        public bool IsCopiable() => (Capabilities & Capabilities.Copiable) == Capabilities.Copiable;
        public bool IsDeletable() => (Capabilities & Capabilities.Deletable) == Capabilities.Deletable;
        public bool IsResizable() => (Capabilities & Capabilities.Resizable) == Capabilities.Resizable;
        public bool IsSelectable() => (Capabilities & Capabilities.Selectable) == Capabilities.Selectable && visible;
    }
}
