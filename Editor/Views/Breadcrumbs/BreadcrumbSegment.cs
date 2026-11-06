using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Breadcrumbs
{
    public class BreadcrumbSegment : VisualElement
    {
        private const string UXML_PATH = "Soup/BreadcrumbSegment";
        private const string SLASH_PATH = "slash";
        private const string SEGMENT_TAG = "segment-name-label";

        private Label m_slashLabel;
        private Label m_segmentLabel;

        private int m_subGraphIndex;
        private Action<int> m_onBreadcrumbClicked;

        public BreadcrumbSegment(Action<int> onBreadcrumbClicked)
        {
            m_onBreadcrumbClicked = onBreadcrumbClicked;
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);
            
            m_slashLabel = this.Q<Label>(SLASH_PATH);
            m_segmentLabel = this.Q<Label>(SEGMENT_TAG);
            
            m_segmentLabel.AddManipulator(new Clickable(HandleSegmentClicked));
        }

        public void SetBreadcrumbSegment(int subGraphIndex, string subgraphName, bool isCurrent)
        {
            m_subGraphIndex = subGraphIndex;
            m_segmentLabel.text = subgraphName;
            
            EnableSlash(m_subGraphIndex != 0);
            EnableInClassList("breadcrumbSegmentCurrent", isCurrent);
        }

        private void EnableSlash(bool enable)
        {
            m_slashLabel.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleSegmentClicked()
        {
            m_onBreadcrumbClicked?.Invoke(m_subGraphIndex);
        }
    }
}
