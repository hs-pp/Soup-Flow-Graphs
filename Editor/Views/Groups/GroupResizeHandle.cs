using Soup.Editor.Views.Manipulators;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Groups
{
    [UxmlElement]
    public sealed partial class GroupResizeHandle : VisualElement
    {
        /// <summary>
        /// Set by the owning <see cref="GroupView"/> once it has cloned its layout.
        /// </summary>
        public GroupView Group { get; internal set; }

        [EventInterest(typeof(DragOfferEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt.target == this && evt.eventTypeId == DragOfferEvent.TypeId())
            {
                GraphElement.OfferDrag((DragOfferEvent)evt, Group.Graph, this, Group.Graph != null && Group.IsResizable());
            }
        }
    }
}
