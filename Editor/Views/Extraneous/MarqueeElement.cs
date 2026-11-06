using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class MarqueeElement : VisualElement
    {
        private Vector2 m_startPoint;
        private Vector2 m_endPoint;

        public MarqueeElement()
        {
            AddToClassList("marquee");
            pickingMode = PickingMode.Ignore;
            generateVisualContent = OnGenerateVisualContent;
        }

        public void SetCoordinates(Vector2 startPoint, Vector2 endPoint)
        {
            m_startPoint = startPoint;
            m_endPoint = endPoint;
            MarkDirtyRepaint();
        }

        public void SetEndPoint(Vector2 endPoint)
        {
            m_endPoint = endPoint;
            MarkDirtyRepaint();
        }

        public Rect SelectionRect => new()
            {
                min = new(Math.Min(m_startPoint.x, m_endPoint.x), Math.Min(m_startPoint.y, m_endPoint.y)),
                max = new(Math.Max(m_startPoint.x, m_endPoint.x), Math.Max(m_startPoint.y, m_endPoint.y))
            };

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect selectionRect = SelectionRect;
            Painter2D painter = ctx.painter2D;
            painter.lineWidth = 1.0f;
            painter.strokeColor = Color.white;
            painter.fillColor = Color.gray;
            painter.BeginPath();
            painter.MoveTo(new(selectionRect.xMin, selectionRect.yMin));
            painter.LineTo(new(selectionRect.xMax, selectionRect.yMin));
            painter.LineTo(new(selectionRect.xMax, selectionRect.yMax));
            painter.LineTo(new(selectionRect.xMin, selectionRect.yMax));
            painter.LineTo(new(selectionRect.xMin, selectionRect.yMin));
            painter.Stroke();
            painter.Fill();
        }
    }
}