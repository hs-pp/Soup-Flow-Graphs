using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators
{
    public class ZoomManipulator : Manipulator
    {
        public static readonly float DefaultReferenceScale = 1;
        public static readonly float DefaultMinScale = 0.25f;
        public static readonly float DefaultMaxScale = 1;
        public static readonly float DefaultScaleStep = 0.15f;

        /// <summary>
        ///     Scale that should be computed when scroll wheel offset is at zero.
        /// </summary>
        public float ReferenceScale { get; set; } = DefaultReferenceScale;
        
        public float MinScale { get; set; } = DefaultMinScale;
        public float MaxScale { get; set; } = DefaultMaxScale;
        public float ScaleStep { get; set; } = DefaultScaleStep;

        protected override void RegisterCallbacksOnTarget()
        {
            GraphView graphView = target as GraphView;
            if (graphView == null)
            {
                throw new InvalidOperationException("Manipulator can only be added to a GraphView");
            }

            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }
        
        internal static float CalculateNewZoom(float currentZoom, float wheelDelta, float zoomStep, float referenceZoom, float minZoom, float maxZoom)
        {
            if (minZoom <= 0)
            {
                Debug.LogError($"[Soup] ZoomManipulator: The minimum zoom ({minZoom}) must be greater than zero.");
                return currentZoom;
            }

            if (referenceZoom < minZoom)
            {
                Debug.LogError(
                    $"[Soup] ZoomManipulator: The reference zoom ({referenceZoom}) must be greater than or equal to the minimum zoom ({minZoom}).");
                return currentZoom;
            }

            if (referenceZoom > maxZoom)
            {
                Debug.LogError(
                    $"[Soup] ZoomManipulator: The reference zoom ({referenceZoom}) must be less than or equal to the maximum zoom ({maxZoom}).");
                return currentZoom;
            }

            if (zoomStep < 0)
            {
                Debug.LogError($"[Soup] ZoomManipulator: The zoom step ({zoomStep}) must be greater than or equal to zero.");
                return currentZoom;
            }

            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

            if (Mathf.Approximately(wheelDelta, 0))
            {
                return currentZoom;
            }

            double a = Math.Log(referenceZoom, 1 + zoomStep);
            double b = referenceZoom - Math.Pow(1 + zoomStep, a);

            // Convert zoom levels to scroll wheel values.
            double minWheel = Math.Log(minZoom - b, 1 + zoomStep) - a;
            double maxWheel = Math.Log(maxZoom - b, 1 + zoomStep) - a;
            double currentWheel = Math.Log(currentZoom - b, 1 + zoomStep) - a;

            wheelDelta = Math.Sign(wheelDelta);
            currentWheel += wheelDelta;

            // Assimilate to the boundary when it is nearby.
            if (currentWheel > maxWheel - 0.5)
            {
                return maxZoom;
            }

            if (currentWheel < minWheel + 0.5)
            {
                return minZoom;
            }

            // Snap the wheel to the unit grid.
            currentWheel = Math.Round(currentWheel);

            // Calculate the corresponding zoom level.
            return (float)(Math.Pow(1 + zoomStep, currentWheel + a) + b);
        }

        private void OnWheel(WheelEvent evt)
        {
            GraphView graphView = target as GraphView;
            if (graphView == null)
            {
                return;
            }

            if (graphView.panel?.GetCapturingElement(PointerId.mousePointerId) != null)
            {
                return;
            }

            Vector3 position = graphView.GetViewTransformPosition();
            Vector3 scale = graphView.GetViewTransformScale();

            Vector2 zoomCenter = target.ChangeCoordinatesTo(graphView.ContentContainer, evt.localMousePosition);
            float x = zoomCenter.x + graphView.ContentContainer.layout.x;
            float y = zoomCenter.y + graphView.ContentContainer.layout.y;

            position += Vector3.Scale(new(x, y, 0), scale);

            // Apply the new zoom.
            float zoom = CalculateNewZoom(scale.y, -evt.delta.y, ScaleStep, ReferenceScale, MinScale, MaxScale);
            scale.x = zoom;
            scale.y = zoom;
            scale.z = 1;

            position -= Vector3.Scale(new(x, y, 0), scale);

            graphView.UpdateViewTransform(position, scale);

            evt.StopPropagation();
        }
    }
}