using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Editor.Views.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Groups
{
    public sealed class GroupView : GraphElement
    {
        public const int LAYER = -2;
        public const float TITLE_HEIGHT = 28f;

        private const string UXML_PATH = "Soup/GroupView";
        private const string TITLE_CONTAINER_TAG = "title-container";
        private const string TITLE_LABEL_TAG = "title-label";
        private const string RESIZE_HANDLE_TAG = "resize-handle";

        public override int Layer => LAYER;

        private VisualElement m_titleContainer;
        private Label m_titleLabel;
        private Vector2 m_size;
        private int m_modelIndex;
        
        public override bool Selected
        {
            get => base.Selected;
            set
            {
                if (base.Selected == value)
                {
                    return;
                }

                base.Selected = value;
                EnableInClassList("groupSelected", base.Selected);
            }
        }

        public override Rect CanvasRect => new(GetPosition(), m_size);

        public GroupView(string groupId)
        {
            Id = groupId;
            Capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Resizable | Capabilities.Copiable; // Not Ascendable: stacking order comes from size.
            usageHints = UsageHints.DynamicTransform;
            AddToClassList("group");

            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_titleContainer = this.Q<VisualElement>(TITLE_CONTAINER_TAG);
            m_titleLabel = this.Q<Label>(TITLE_LABEL_TAG);
            this.Q<GroupResizeHandle>(RESIZE_HANDLE_TAG).Group = this;
        }

        public bool UpdateFromModel(GroupMetaDataRef group)
        {
            m_modelIndex = group.Index;
            style.backgroundColor = group.Color;
            m_titleLabel.text = group.Title;

            Rect rect = group.Rect;

            bool changed = false;
            if (GetPosition() != rect.position)
            {
                SetPosition(rect.position);
                changed = true;
            }

            if (m_size != rect.size)
            {
                m_size = rect.size;
                style.width = m_size.x;
                style.height = m_size.y;
                changed = true;
            }

            return changed;
        }

        internal void PreviewSize(Vector2 size)
        {
            style.width = size.x;
            style.height = size.y;
        }

        /// <summary>
        /// Back-to-front order: larger groups first, so a group always draws above any group that encloses it. Ties keep list order.
        /// </summary>
        public static int CompareStacking(GroupView a, GroupView b)
        {
            int byArea = Area(b.m_size).CompareTo(Area(a.m_size));
            return byArea != 0 ? byArea : a.m_modelIndex.CompareTo(b.m_modelIndex);
        }

        private static float Area(Vector2 size) => size.x * size.y;
        public override bool ContainsPoint(Vector2 localPoint) => m_titleContainer.layout.Contains(localPoint);
        public override bool Overlaps(Rect rectangle) => m_titleContainer.layout.Overlaps(rectangle, true);

        [EventInterest(typeof(DragOfferEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt.target == this && evt.eventTypeId == DragOfferEvent.TypeId())
            {
                OfferSelectionDrag((DragOfferEvent)evt);
            }
        }

        public override void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            List<string> contentIds = CollectIds(graphView.GetGroupContents(this), element => element.IsSelectable());
            evt.menu.AppendAction("Select Contents", _ => graphView.RequestCommand(new SelectGraphElementsCommand(contentIds, GraphSelectionMode.Replace)),
                contentIds.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendSeparator();

            evt.menu.AppendAction(GraphShortcuts.WithHint("Delete Group", GraphShortcuts.DeleteSelectionHintId), _ =>
                graphView.RequestCommand(new DeleteGraphElementsCommand(null, null, new[] { Id })), IsDeletable() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }
        
        private List<string> CollectIds<T>(IEnumerable<T> elements, Func<T, bool> include) where T : GraphElement
        {
            List<string> ids = new();
            foreach (T element in elements)
            {
                if (element.Id != null && include(element))
                {
                    ids.Add(element.Id);
                }
            }

            return ids;
        }
    }
}
