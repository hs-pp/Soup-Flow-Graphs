using System.Collections.Generic;
using UnityEngine;

namespace Soup.Editor.Views.Manipulators.Gestures
{
    /// <summary>
    /// One drag of graph content. <see cref="ElementDragManipulator"/> creates one per drag offer and drops it when the drag ends,
    /// and only calls <see cref="End"/> or <see cref="Cancel"/> after a successful <see cref="Begin"/>.
    /// </summary>
    internal abstract class DragGesture
    {
        protected DragGesture(GraphView graphView)
        {
            Graph = graphView;
        }

        protected GraphView Graph { get; }
        protected GraphElementContainer Content => Graph.ContentContainer;

        /// <summary>
        /// The element the drag is anchored to. A drag from a port only gets its candidate edge at begin.
        /// </summary>
        public GraphElement Dragged { get; protected set; }

        public virtual bool PansView => true;

        /// <summary>
        /// Returns false to refuse the drag.
        /// </summary>
        public abstract bool Begin(DragBeginEvent e);

        public abstract void Drag(DragEvent e);

        /// <summary>
        /// Moves the drag with the view as it auto-pans, in content space.
        /// </summary>
        public virtual void ApplyPanDelta(Vector2 delta) { }

        public abstract void End();

        /// <param name="keepPositions">Set when a reconcile has already put every surviving view at its model position.</param>
        public abstract void Cancel(bool keepPositions);

        /// <summary>
        /// True once the model has removed anything this drag holds.
        /// </summary>
        public virtual bool IsStale() => Dragged == null || Dragged.Graph == null;

        protected static bool AnyRemoved(IReadOnlyList<GraphElement> elements)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Graph == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
