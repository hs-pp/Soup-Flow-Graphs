using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Edges
{
    public interface IEdgeRenderStrategy
    {
        void UpdateLayout(EdgeView edge);
        void SetSelected(EdgeView edge, bool selected);
        void Render(EdgeView edge, MeshGenerationContext context);
        void Invalidate();
        
        bool ContainsPoint(EdgeView edge, Vector2 localPoint);
        bool Overlaps(EdgeView edge, Rect rect);
    }
}
