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
        protected readonly List<PortView> m_outputPorts = new();
        
        private NodeCommentElement m_commentElement;
        
        protected virtual void OnInitializeView(VisualElement root) { } // All child elements should be set to PickingMode=Ignore
        protected virtual void OnPropertyBound(NodeRef nodeRef, NodeMetaDataRef metaData) { }
        protected virtual void OnNodeDataChanged(NodeRef nodeRef) { }

        protected virtual void OnMetaDataChanged(NodeMetaDataRef metaData) { }
        protected virtual void AddPort(PortView port) { port.SetNodeView(this); }
        
        protected override void OnSelectedChanged() => EnableInClassList("nodeSelected", Selected);

        public void InitializeView(NodeRef nodeRef, NodeMetaDataRef metaData, Type nodeType)
        {
            Capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable |
                            Capabilities.Ascendable | Capabilities.Copiable;
            usageHints = UsageHints.DynamicTransform;
            
            CreateLayout();
            OnInitializeView(m_root);

            Id = nodeRef.Id;
            NodeType = nodeType;
            BuildPorts(BuildOutportDefinitions(nodeRef));
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

        public void UpdateFromModel(NodeRef nodeRef, NodeMetaDataRef metaData)
        {
            string previousPath = m_boundPropertyPath;
            NodeRef = nodeRef;
            m_boundPropertyPath = nodeRef.NodeSP?.propertyPath;

            if (previousPath == null || previousPath != m_boundPropertyPath)
            {
                OnPropertyBound(nodeRef, metaData);
            }

            RefreshOutportNames(nodeRef);
            OnNodeDataChanged(nodeRef);
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
            Graph?.NotifyContentChanged();
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
        
        private List<OutportDefinition> BuildOutportDefinitions(NodeRef nodeRef)
        {
            List<OutportDefinition> definitions = NodeUtils.GetOutportDefinitions(nodeRef.GetNodeInstance());

            List<OutportDefinition> outportDefinitions = new();
            foreach (OutportRef outport in nodeRef.EnumerateOutports())
            {
                string name = definitions?.Find(x => x.Id == outport.OutportId).Name;
                if (name == null)
                {
                    name = outport.OutportId;
                }

                outportDefinitions.Add(new OutportDefinition(outport.OutportId, name));
            }

            return outportDefinitions;
        }

        private void RefreshOutportNames(NodeRef nodeRef)
        {
            List<OutportDefinition> outportDefinitions = BuildOutportDefinitions(nodeRef);
            for (int i = 0; i < m_outputPorts.Count && i < outportDefinitions.Count; i++)
            {
                if (m_outputPorts[i].OutportId == outportDefinitions[i].Id)
                {
                    m_outputPorts[i].PortName = outportDefinitions[i].Name;
                }
            }
        }

        private void BuildPorts(List<OutportDefinition> outportDefinitions)
        {
            InputPort = CreateInputPort(); // A view with no input returns null.
            if (InputPort != null)
            {
                AddPort(InputPort);
            }

            foreach (OutportDefinition definition in outportDefinitions)
            {
                PortView outputPort = CreateOutputPort(definition);
                m_outputPorts.Add(outputPort);
                AddPort(outputPort);
            }
        }

        protected virtual PortView CreateInputPort()
        {
            return new PortView(Id, null, Direction.Input);
        }

        protected virtual PortView CreateOutputPort(OutportDefinition definition)
        {
            return new PortView(Id, definition.Id, Direction.Output)
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

        private void ApplyWidthLimits()
        {
            float step = GraphViewConstants.DefaultSnapStep;
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
                contentWidth = rootWidth;
            }
            else if (rootWidth < m_snappedWidth - epsilon)
            {
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

        public override void BuildContextMenu(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            evt.menu.AppendAction(GraphShortcuts.WithHint("Duplicate Node", GraphShortcuts.EDIT_DUPLICATE), _ =>
                graphView.RequestCommand(new DuplicateGraphElementsCommand(new[] { Id })), IsCopiable() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(GraphShortcuts.WithHint("Delete Node", GraphShortcuts.DeleteSelectionHintId), _ =>
                graphView.RequestCommand(new DeleteGraphElementsCommand(new[] { Id })), IsDeletable() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendSeparator();
            SoupScriptCreator.AppendNodeMenuItems(evt.menu, NodeType);
        }

        [EventInterest(typeof(DragOfferEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (evt.eventTypeId == DragOfferEvent.TypeId())
            {
                DragOfferEvent dragOffer = (DragOfferEvent)evt;
                if (evt.target == this || dragOffer.GetDraggedElement() == null)
                {
                    OfferSelectionDrag(dragOffer);
                }
            }
        }
    }
}
