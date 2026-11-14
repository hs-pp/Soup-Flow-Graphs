using System;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    public abstract class ANodeInspector : VisualElement
    {
        protected abstract bool UseDefaultHeader { get; }

        protected NodeRef m_nodeRef;
        protected NodeMetaDataRef m_metaDataRef;
        
        // We need to check all 3 to be sure it's the same node.
        private SerializedObject m_boundObject;
        private string m_boundPath;
        private string m_boundNodeId;

        private string m_boundMetaDataPath;

        private Action<string> m_onSaveStateHandler;
        private Action<ICommand> m_onCommandRequestedHandler;

        public ANodeInspector()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            VisualElement customLayout = CreateCustomLayout();
            if (customLayout != null)
            {
                Add(customLayout);
            }
        }
        
        public void SetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            SerializedObject serializedObject = nodeRef.NodeSP?.serializedObject;
            string path = nodeRef.NodeSP?.propertyPath;
            string metaDataPath = metaDataRef.MetaDataSP?.propertyPath;

            // if exactly the same, early out.
            if (serializedObject != null && serializedObject == m_boundObject && path == m_boundPath && nodeRef.Id == m_boundNodeId && metaDataPath == m_boundMetaDataPath)
            {
                m_nodeRef = nodeRef;
                return;
            }

            m_boundObject = serializedObject;
            m_boundPath = path;
            m_boundNodeId = nodeRef.Id;
            m_boundMetaDataPath = metaDataPath;

            m_nodeRef = nodeRef;
            m_metaDataRef =  metaDataRef;
            OnSetNode(nodeRef, metaDataRef);
        }

        public void SetOnSaveStateHandler(Action<string> onSaveStateHandler)
        {
            m_onSaveStateHandler = onSaveStateHandler;
        }
        
        public void SetOnCommandRequestedHandler(Action<ICommand> onCommandRequestedHandler)
        {
            m_onCommandRequestedHandler = onCommandRequestedHandler;
        }

        protected void RequestCommand(ICommand command)
        {
            m_onCommandRequestedHandler?.Invoke(command);
        }
        
        public void SaveState()
        {
            m_onSaveStateHandler?.Invoke(GetSaveState());
        }

        public void LoadState(string saveState)
        {
            HandleLoadState(saveState);
        }

        protected abstract VisualElement CreateCustomLayout();
        protected abstract void OnSetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef);
        protected abstract void HandleLoadState(string saveState);
        protected abstract string GetSaveState();
    }

    public class DefaultNodeInspector : ANodeInspector
    {
        protected override bool UseDefaultHeader => true;
        private VisualElement m_propertyFieldsContainer;

        protected override VisualElement CreateCustomLayout()
        {
            m_propertyFieldsContainer = new VisualElement();
            return m_propertyFieldsContainer;
        }

        protected override void OnSetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            m_propertyFieldsContainer.Unbind();
            m_propertyFieldsContainer.Clear();
            if (!nodeRef.IsValid)
            {
                return;
            }

            // [HideInInspector] keeps the id and outports out.
            SerializedPropertyUtils.AddVisibleChildFields(m_propertyFieldsContainer, nodeRef.NodeSP);
        }
        
        protected override void HandleLoadState(string saveState)
        {
        }

        protected override string GetSaveState()
        {
            return "";
        }
    }
}