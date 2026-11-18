using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Breadcrumbs
{
    public class BreadcrumbsView : VisualElement
    {
        private const string STYLESHEET_PATH = "Soup/BreadcrumbsView";

        private Action<int> m_onBreadcrumbClicked;
        
        public BreadcrumbsView(Action<int> onBreadcrumbClicked)
        {
            m_onBreadcrumbClicked = onBreadcrumbClicked;
            styleSheets.Add(Resources.Load<StyleSheet>(STYLESHEET_PATH));
            AddToClassList("breadcrumbView");

            RegisterCallback<MouseDownEvent>(StopPropagation);
            RegisterCallback<MouseMoveEvent>(StopPropagation);
            RegisterCallback<MouseUpEvent>(StopPropagation);
            RegisterCallback<ContextualMenuPopulateEvent>(StopPropagation);
        }

        private static void StopPropagation(EventBase evt) => evt.StopPropagation();

        public void SetPath(List<string> path)
        {
            Clear();
            style.display = path.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None; // No graph loaded: no empty pill.

            for (int i = 0; i < path.Count; i++)
            {
                BreadcrumbSegment segment = new BreadcrumbSegment(m_onBreadcrumbClicked);
                segment.SetBreadcrumbSegment(i, path[i], i == path.Count - 1);
                Add(segment);
            }
        }
    }
}