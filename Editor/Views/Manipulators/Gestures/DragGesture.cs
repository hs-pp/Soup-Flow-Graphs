using System.Collections.Generic;
using UnityEngine;

namespace Soup.Editor.Views.Manipulators.Gestures
{
    public abstract class DragGesture
    {
        protected DragGesture(GraphView graphView)
        {
            Graph = graphView;
        }

        protected GraphView Graph { get; }
        protected GraphElementContainer Content => Graph.ContentContainer;

        public GraphElement Dragged { get; protected set; }

        public virtual bool PansView => true;


        public abstract bool Begin(DragBeginEvent e);
        public abstract void Drag(DragEvent e);
        public virtual void ApplyPanDelta(Vector2 delta) { }
        public abstract void End();
        public abstract void Cancel(bool keepPositions);

        /// <summary>
        /// True if anything has changed while this drag is alive.
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
