using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [UxmlElement]
    public partial class GroupInspectorView : VisualElement
    {
        private const string UXML_PATH = "Soup/GroupInspectorView";
        private const string HEADER_CONTAINER_TAG = "header-container";
        private const string TITLE_FIELD_TAG = "title-field";
        private const string COLOR_FIELD_TAG = "color-field";

        private VisualElement m_headerContainer;
        private TextField m_titleField;
        private ColorField m_colorField;
        private SerializedObject m_boundObject;
        private string m_boundPath;

        public GroupInspectorView()
        {
            CreateLayout();
        }
        
        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_headerContainer = this.Q(HEADER_CONTAINER_TAG);
            m_titleField =  this.Q<TextField>(TITLE_FIELD_TAG);
            m_colorField = this.Q<ColorField>(COLOR_FIELD_TAG);
            m_colorField.RegisterValueChangedCallback(HandleColorChanged);
        }

        private void HandleColorChanged(ChangeEvent<Color> evt)
        {
            m_headerContainer.style.backgroundColor = evt.newValue;
        }

        public void SetGroup(GroupMetaDataRef group)
        {
            if (group.IsValid && group.GroupSP.serializedObject == m_boundObject && group.GroupSP.propertyPath == m_boundPath)
            {
                return;
            }

            m_titleField.Unbind();
            m_colorField.Unbind();
            m_boundObject = null;
            m_boundPath = null;

            if (!group.IsValid)
            {
                return;
            }

            m_boundObject = group.GroupSP.serializedObject;
            m_boundPath = group.GroupSP.propertyPath;

            m_titleField.BindProperty(group.TitleSP);
            m_colorField.BindProperty(group.ColorSP);
        }
    }
}
