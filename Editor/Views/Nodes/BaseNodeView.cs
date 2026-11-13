using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.ScriptTemplates;
using Soup.Editor.State;
using Soup.Editor.Views.Extraneous;
using Soup.Editor.Views.Manipulators;
using Soup.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes
{
    public abstract class BaseNodeView : GraphElement
    {
        private const float WIDTH_PROBE_BASIS = GraphViewConstants.DefaultSnapStep - 0.05f;

        public override int Layer => 0;

        /// <summary>
        /// Width limits of the node, in graph pixels, read once when the view is built. MaxWidth is rounded down to
        /// a snap step and never drops below MinWidth. Content wider than MaxWidth overflows, so a view that sets it
        /// should wrap or truncate its labels.
        /// </summary>
        protected virtual float MinWidth => 100f;
        protected virtual float MaxWidth => float.PositiveInfinity;

        private const string UXML_PATH = "Soup/BaseNodeView";
        private const string ROOT_TAG = "root";

        private VisualElement m_root;
        private VisualElement m_widthProbe;
        private float m_snappedWidth;

        protected Type NodeType { get; private set; }
        public NodeRef NodeRef { get; private set; }
        public NodeMetaDataRef MetaData { get; private set; }
        private string m_boundPropertyPath;

        public PortView InputPort { get; protected set; }
        protected List<OutportDefinition> m_outportDefinitions = new();
        protected readonly List<PortView> m_outputPorts = new();
        private bool m_outportDefinitionsFailed; // DefineOutports threw. It has logged, and would again on every refresh.
        
        private NodeCommentElement m_commentElement;
        
        protected virtual void OnInitializeView(VisualElement root) { } // All child elements should be set to PickingMode=Ignore
        protected virtual void OnPropertyBound(NodeRef nodeRef, NodeMetaDataRef metaData) { }
        protected virtual void OnNodeDataChanged(NodeRef nodeRef) { }

        protected virtual void OnMetaDataChanged(NodeMetaDataRef metaData) { }
        protected virtual void AddPort(PortView port) { port.SetParent(this); }
        
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
                EnableInClassList("nodeSelected", value);
            }
        }

        public void InitializeView(NodeRef nodeRef, NodeMetaDataRef metaData, Type nodeType)
        {
            Capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable |
                            Capabilities.Ascendable | Capabilities.Copiable;
            usageHints = UsageHints.DynamicTransform;
            
            CreateLayout();
            OnInitializeView(m_root);

            Id = nodeRef.Id;
            NodeType = nodeType;
            BuildOutportDefinitions(nodeRef);
            BuildPorts();
            UpdateFromModel(nodeRef, metaData);
        }

        private void CreateLayout()
        {
            VisualTreeAsset uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            hierarchy.Insert(0, new DropShadowElement());

            m_root = this.Q<VisualElement>(ROOT_TAG);
            SetUpWidthSnapping();
        }

        // Width snaps up to whole snap steps.
        private void SetUpWidthSnapping()
        {
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            m_root.style.flexShrink = 0;
            ApplyWidthLimits();

            m_widthProbe = new VisualElement { pickingMode = PickingMode.Ignore };
            m_widthProbe.style.flexBasis = Length.Percent(100); // Always wraps until the first snap.
            m_widthProbe.style.flexGrow = 1;
            m_widthProbe.style.flexShrink = 0;
            m_widthProbe.style.height = 0;
            hierarchy.Add(m_widthProbe);

            m_root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        // The snapped width rounds up, so a max between steps is unreachable: the capped root reads as a shrink and
        // the snap flips between small and large forever. Rounding the max down to a step keeps it reachable.
        private void ApplyWidthLimits()
        {
            const float step = GraphViewConstants.DefaultSnapStep;
            float minWidth = Mathf.Max(0f, MinWidth);
            m_root.style.minWidth = minWidth;

            if (MaxWidth < float.PositiveInfinity)
            {
                float maxWidth = Mathf.Floor(MaxWidth / step + 0.001f) * step;
                float snappedMinWidth = Mathf.Ceil(minWidth / step - 0.001f) * step;
                m_root.style.maxWidth = Mathf.Max(maxWidth, snappedMinWidth);
            }
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            const float epsilon = 0.01f;
            float rootWidth = evt.newRect.width;
            float contentWidth;
            if (rootWidth > m_snappedWidth + epsilon)
            {
                contentWidth = rootWidth; // Outgrown, so the node is as wide as its content.
            }
            else if (rootWidth < m_snappedWidth - epsilon)
            {
                // The probe fits beside the root and the two split the spare width evenly.
                contentWidth = rootWidth - m_widthProbe.layout.width + WIDTH_PROBE_BASIS;
            }
            else
            {
                return;
            }

            const float step = GraphViewConstants.DefaultSnapStep;
            m_snappedWidth = Mathf.Ceil(contentWidth / step - 0.01f) * step; // Tolerance must exceed the probe's shortfall.
            style.minWidth = m_snappedWidth;
            m_widthProbe.style.flexBasis = Length.Percent(WIDTH_PROBE_BASIS / m_snappedWidth * 100f);
        }

        public void UpdateFromModel(NodeRef nodeRef, NodeMetaDataRef metaData)
        {
            string previousPath = m_boundPropertyPath;
            NodeRef = nodeRef;
            m_boundPropertyPath = nodeRef.NodeSP?.propertyPath;

            if (previousPath == null || m_boundPropertyPath == null || previousPath != m_boundPropertyPath)
            {
                OnPropertyBound(nodeRef,  metaData);
            }

            RefreshOutportNames(nodeRef);
            OnNodeDataChanged(NodeRef);
            UpdateMetaData(metaData);
        }

        public void UpdateMetaData(NodeMetaDataRef metaData)
        {
            MetaData = metaData;
            Vector2 position = metaData.IsValid ? metaData.Position : EntryNodeMetaData.DEFAULT_POSITION;
            if (GetPosition() != position)
            {
                SetPosition(position);
            }

            SetComment(metaData.Comment);
            OnMetaDataChanged(metaData);
            Graph?.NotifyContentChanged(); // The minimap draws the node's color, which no position or geometry change reports.
        }

        private void SetComment(string comment)
        {
            if (string.IsNullOrEmpty(comment))
            {
                // destroy comment element if it exists
                if (m_commentElement != null)
                {
                    Remove(m_commentElement);
                    m_commentElement = null;
                }
            }
            else
            {
                if (m_commentElement == null)
                {
                    m_commentElement = new NodeCommentElement();
                    Add(m_commentElement);
                }
                
                m_commentElement.SetComment(comment);
            }
        }
        
        private void BuildOutportDefinitions(NodeRef nodeRef)
        {
            // Null when DefineOutports threw, and then every port is labelled with its outport id.
            List<OutportDefinition> definitions = NodeUtils.GetOutportDefinitions(nodeRef.GetNodeInstance());
            m_outportDefinitionsFailed = definitions == null;

            m_outportDefinitions.Clear();
            foreach (OutportRef outport in nodeRef.EnumerateOutports())
            {
                string name = definitions?.Find(x => x.Id == outport.OutportId).Name;
                m_outportDefinitions.Add(new OutportDefinition(outport.OutportId, name ?? outport.OutportId));
            }
        }

        // Names are only labels, so a node whose labels follow its data relabels in place. Changed keys rebuild the view instead.
        private void RefreshOutportNames(NodeRef nodeRef)
        {
            // The labels stay keys until the view is rebuilt.
            if (m_outportDefinitionsFailed)
            {
                return;
            }

            BuildOutportDefinitions(nodeRef);
            for (int i = 0; i < m_outputPorts.Count && i < m_outportDefinitions.Count; i++)
            {
                if (m_outputPorts[i].OutportId == m_outportDefinitions[i].Id)
                {
                    m_outputPorts[i].PortName = m_outportDefinitions[i].Name;
                }
            }
        }

        private void BuildPorts()
        {
            InputPort = CreateInputPort(); // A view with no input returns null.
            if (InputPort != null)
            {
                AddPort(InputPort);
            }

            foreach (OutportDefinition definition in m_outportDefinitions)
            {
                PortView outputPort = CreateOutputPort(definition);
                m_outputPorts.Add(outputPort);
                AddPort(outputPort);
            }
        }

        protected virtual PortView CreateInputPort()
        {
            return new PortView(Id, null, Direction.Input, PortCapacity.Multi);
        }

        protected virtual PortView CreateOutputPort(OutportDefinition definition)
        {
            return new PortView(Id, definition.Id, Direction.Output, PortCapacity.Single)
            {
                PortName = definition.Name
            };
        }

        public PortView GetOutputPort(string outportId)
        {
            if (string.IsNullOrEmpty(outportId))
            {
                return null;
            }
            return m_outputPorts.Find(x => x.OutportId == outportId);
        }

        public bool OutportsMatch(NodeRef nodeRef)
        {
            int i = 0;
            foreach (OutportRef outport in nodeRef.EnumerateOutports())
            {
                if (i >= m_outputPorts.Count || m_outputPorts[i].OutportId != outport.OutportId)
                {
                    return false;
                }

                i++;
            }

            return i == m_outputPorts.Count;
        }

        public override void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            evt.menu.AppendAction(GraphShortcuts.WithHint("Duplicate", GraphShortcuts.EDIT_DUPLICATE), _ =>
                graphView.RequestCommand(new DuplicateGraphElementsCommand(new[] { Id })), IsCopiable() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(GraphShortcuts.WithHint("Delete", GraphShortcuts.DeleteSelectionHintId), _ =>
                graphView.RequestCommand(new DeleteGraphElementsCommand(new[] { Id }, null)), IsDeletable() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendSeparator();
            SoupScriptCreator.AppendNodeMenuItems(evt.menu, NodeType);
        }

        [EventInterest(typeof(DragOfferEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt is DragOfferEvent dragOffer && (evt.target == this || dragOffer.GetDraggedElement() == null))
            {
                OfferSelectionDrag(dragOffer);
            }
        }
    }
}
