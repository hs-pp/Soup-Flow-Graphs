using System;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.Views.Extraneous;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [UxmlElement]
    public partial class NodeInspectorHeader : VisualElement
    {
        private const string UXML_PATH = "Soup/NodeInspectorHeader";
        private const string HEADER_CONTAINER_TAG = "header-container";
        private const string TITLE_TAG = "title";
        private const string ID_LABEL_TAG = "id-label";
        private const string COLOR_SWATCH_TAG = "color-swatch";
        private const string COMMENT_BUBBLE_TAG = "comment-bubble";
        private const string COMMENT_FIELD_TAG = "comment-field";
        private const string COMMENT_EMPTY_CLASS = "nodeInspectorComment--empty";
        private const string COMMENT_FOCUSED_CLASS = "nodeInspectorComment--focused";

        private VisualElement m_headerContainer;
        private Label m_titleLabel;
        private Label m_idLabel;
        private ColorField m_colorSwatch;
        private SpeechBubbleElement m_commentBubble;
        private TextField m_commentField;

        public NodeInspectorHeader()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_headerContainer = this.Q(HEADER_CONTAINER_TAG);
            m_titleLabel = this.Q<Label>(TITLE_TAG);
            m_idLabel = this.Q<Label>(ID_LABEL_TAG);
            m_colorSwatch = this.Q<ColorField>(COLOR_SWATCH_TAG);
            m_commentBubble = this.Q<SpeechBubbleElement>(COMMENT_BUBBLE_TAG);

            // Clicking a ColorField opens Unity's color picker; trimmed down, the field is just the swatch.
            m_colorSwatch.showEyeDropper = false;
            m_colorSwatch.showAlpha = false;
            m_commentField = this.Q<TextField>(COMMENT_FIELD_TAG);

            // Focus lands on the field's inner text element; FocusIn/FocusOut bubble up to the field.
            m_commentField.RegisterCallback<FocusInEvent>(_ => m_commentBubble.AddToClassList(COMMENT_FOCUSED_CLASS));
            m_commentField.RegisterCallback<FocusOutEvent>(_ => m_commentBubble.RemoveFromClassList(COMMENT_FOCUSED_CLASS));
            m_commentField.RegisterValueChangedCallback(evt => RefreshCommentEmpty(evt.newValue));
        }

        public void SetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            Type nodeType = nodeRef.GetNodeType();
            m_titleLabel.text = nodeType != null ? EditorTypesFactory.GetNodeDisplayName(nodeType) : "error";
            m_titleLabel.tooltip = nodeType != null ? nodeType.FullName : string.Empty; // The display name can hide which class this is.
            m_idLabel.text = !string.IsNullOrEmpty(nodeRef.Id) ? nodeRef.Id : "error";

            // Subscribe to color changes. Unbinding the container also unbinds the swatch inside it.
            m_headerContainer.Unbind();
            SerializedProperty colorSP = metaDataRef.ColorSP;
            m_colorSwatch.style.display = colorSP != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (colorSP != null)
            {
                m_headerContainer.TrackPropertyValue(colorSP, HandleColorChanged);
                m_colorSwatch.BindProperty(colorSP);
                HandleColorChanged(colorSP);
            }

            BindComment(metaDataRef.CommentSP);
        }

        private void BindComment(SerializedProperty commentSP)
        {
            // Also unbinds the field inside the bubble.
            m_commentBubble.Unbind();

            // A node with no meta data entry has nowhere to store a comment.
            m_commentBubble.style.display = commentSP != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (commentSP == null)
            {
                return;
            }

            m_commentField.BindProperty(commentSP);
            // The field's change event covers typing; the tracker covers undo and edits from another window.
            m_commentBubble.TrackPropertyValue(commentSP, property => RefreshCommentEmpty(property.stringValue));
            RefreshCommentEmpty(commentSP.stringValue);
        }

        private void RefreshCommentEmpty(string comment)
        {
            m_commentBubble.EnableInClassList(COMMENT_EMPTY_CLASS, string.IsNullOrEmpty(comment));
        }

        private void HandleColorChanged(SerializedProperty colorSP)
        {
            m_headerContainer.style.backgroundColor = colorSP.colorValue;
        }
    }
}
