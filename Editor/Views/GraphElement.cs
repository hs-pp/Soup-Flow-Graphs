using System;
using Soup.Editor.Views.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public abstract class GraphElement : VisualElement
    {
        public virtual string Id { get; protected set; }
        public abstract int Layer { get; }

        private SelectableManipulator m_selectableManipulator;

        private Vector2 m_position;
        public virtual event Action<PositionData> OnPositionChange;
        public virtual Vector2 GetGlobalCenter() => Graph.ContentContainer.LocalToWorld(GetCenter());
        public virtual Vector2 GetCenter() => layout.center + m_position;
        public virtual Vector2 GetPosition() => m_position;
        public virtual Rect CanvasRect => new(layout.position + m_position, layout.size);
        public virtual void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView) { }

        protected Capabilities Capabilities { get; set; }

        private GraphView m_graphView;
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
        
        private bool m_selected;
        public virtual bool Selected
        {
            get => m_selected;
            set
            {
                if (m_selected == value)
                {
                    return;
                }

                if (value && IsSelectable())
                {
                    m_selected = true;
                    if (IsAscendable() && resolvedStyle.position != Position.Relative)
                    {
                        BringToFront();
                    }
                }
                else
                {
                    m_selected = false;
                }

                m_graphView?.NotifyContentChanged();
            }
        }

        protected GraphElement()
        {
            AddToClassList("graphElement");
            RegisterCallback<GeometryChangedEvent>(_ => m_graphView?.NotifyContentChanged());
        }
        
        protected virtual void OnAddedToGraphView()
        {
            BindManipulators();
        }

        protected virtual void OnRemovedFromGraphView()
        {
            pickingMode = PickingMode.Position;
            Selected = false;
        }
        
        protected virtual void BindManipulators()
        {
            if (m_selectableManipulator != null)
            {
                this.RemoveManipulator(m_selectableManipulator);
                m_selectableManipulator = null;
            }

            m_selectableManipulator = new SelectableManipulator();
            this.AddManipulator(m_selectableManipulator);
        }
        
        protected void SetElementPosition(Vector2 newPosition)
        {
            m_position = newPosition;
            style.translate = new Translate(newPosition.x, newPosition.y);
        }

        public virtual void SetPosition(Vector2 newPosition)
        {
            SetElementPosition(newPosition);
            OnPositionChange?.Invoke(new()
            {
                Element = this,
                Position = newPosition
            });
            m_graphView?.NotifyContentChanged();
        }

        public virtual void ApplyDeltaToPosition(Vector2 delta)
        {
            SetPosition(m_position + delta);
        }

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
        
        public virtual bool IsMovable() => (Capabilities & Capabilities.Movable) == Capabilities.Movable;
        public virtual bool IsDroppable() => (Capabilities & Capabilities.Droppable) == Capabilities.Droppable;
        public virtual bool IsAscendable() => (Capabilities & Capabilities.Ascendable) == Capabilities.Ascendable;
        public virtual bool IsRenamable() => (Capabilities & Capabilities.Renamable) == Capabilities.Renamable;
        public virtual bool IsCopiable() => (Capabilities & Capabilities.Copiable) == Capabilities.Copiable;
        public virtual bool IsSnappable() => (Capabilities & Capabilities.Snappable) == Capabilities.Snappable;
        public virtual bool IsDeletable() => (Capabilities & Capabilities.Deletable) == Capabilities.Deletable;
        public virtual bool IsResizable() => (Capabilities & Capabilities.Resizable) == Capabilities.Resizable;
        public virtual bool IsGroupable() => false;
        public virtual bool IsStackable() => false;
        public virtual bool IsSelectable() => (Capabilities & Capabilities.Selectable) == Capabilities.Selectable && visible;
    }
}
