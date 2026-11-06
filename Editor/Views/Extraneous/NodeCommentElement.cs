using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class NodeCommentElement : VisualElement
    {
        private const string UXML_PATH = "Soup/NodeCommentElement";
        private const string COMMENT_LABEL_TAG = "comment-label";
        private const string STYLECLASS_NAME = "nodeComment";

        private Label m_commentLabel;

        public NodeCommentElement()
        {
            AddToClassList(STYLECLASS_NAME);
            pickingMode = PickingMode.Ignore;
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_commentLabel = this.Q<Label>(COMMENT_LABEL_TAG);
        }

        public void SetComment(string comment)
        {
            style.display = string.IsNullOrEmpty(comment) ? DisplayStyle.None : DisplayStyle.Flex;
            m_commentLabel.text = comment;
        }
    }
}
