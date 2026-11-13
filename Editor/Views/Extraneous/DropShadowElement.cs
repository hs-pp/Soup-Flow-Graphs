using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class DropShadowElement : VisualElement
    {
        private const string STYLESHEET_PATH = "Soup/DropShadowElement";
        private const string STYLECLASS_NAME = "dropShadow";

        private static readonly CustomStyleProperty<float> BLUR_PROPERTY = new("--drop-shadow-blur");
        private static readonly CustomStyleProperty<float> CORNER_RADIUS_PROPERTY = new("--drop-shadow-corner-radius");
        private static readonly CustomStyleProperty<float> OFFSET_X_PROPERTY = new("--drop-shadow-offset-x");
        private static readonly CustomStyleProperty<float> OFFSET_Y_PROPERTY = new("--drop-shadow-offset-y");

        private static readonly Dictionary<(int, int), Texture2D> s_textures = new();

        private int m_blur = -1;
        private int m_cornerRadius;
        private float m_offsetX;
        private float m_offsetY;

        static DropShadowElement()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DestroyTextures;
        }

        public DropShadowElement()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLESHEET_PATH));
            AddToClassList(STYLECLASS_NAME);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;
            customStyle.TryGetValue(BLUR_PROPERTY, out float blurValue);
            customStyle.TryGetValue(CORNER_RADIUS_PROPERTY, out float cornerRadiusValue);
            customStyle.TryGetValue(OFFSET_X_PROPERTY, out float offsetX);
            customStyle.TryGetValue(OFFSET_Y_PROPERTY, out float offsetY);

            int blur = Mathf.Max(1, Mathf.RoundToInt(blurValue));
            int cornerRadius = Mathf.Max(0, Mathf.RoundToInt(cornerRadiusValue));

            if (blur == m_blur && cornerRadius == m_cornerRadius && offsetX == m_offsetX && offsetY == m_offsetY)
            {
                return;
            }

            m_blur = blur;
            m_cornerRadius = cornerRadius;
            m_offsetX = offsetX;
            m_offsetY = offsetY;

            style.backgroundImage = GetTexture(blur, cornerRadius, out int slice);
            style.unitySliceLeft = slice;
            style.unitySliceTop = slice;
            style.unitySliceRight = slice;
            style.unitySliceBottom = slice;

            // The texture pads the shape by the blur on every side, so overhang the parent by as much.
            style.left = -blur + offsetX;
            style.right = -blur - offsetX;
            style.top = -blur + offsetY;
            style.bottom = -blur - offsetY;
        }

        private static Texture2D GetTexture(int blur, int cornerRadius, out int slice)
        {
            slice = 2 * blur + cornerRadius;

            if (s_textures.TryGetValue((blur, cornerRadius), out Texture2D texture) && texture != null)
            {
                return texture;
            }

            int size = 2 * slice + 2;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"DropShadow_b{blur}_r{cornerRadius}",
                hideFlags = HideFlags.HideAndDontSave,
            };

            float sigma = blur * 0.5f;
            float halfExtent = size * 0.5f - blur;
            Vector2 center = new(size * 0.5f, size * 0.5f);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = RoundedRectDistance(new Vector2(x + 0.5f, y + 0.5f) - center, halfExtent, cornerRadius);
                    float alpha = 0.5f * (1f - Erf(distance / (sigma * Mathf.Sqrt(2f))));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true); // Frees the CPU copy, since the pixels are never read back.
            s_textures[(blur, cornerRadius)] = texture;
            return texture;
        }

        private static void DestroyTextures()
        {
            foreach (Texture2D texture in s_textures.Values)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            s_textures.Clear();
        }

        private static float RoundedRectDistance(Vector2 point, float halfExtent, float cornerRadius)
        {
            Vector2 q = new(Mathf.Abs(point.x) - (halfExtent - cornerRadius), Mathf.Abs(point.y) - (halfExtent - cornerRadius));
            Vector2 outside = Vector2.Max(q, Vector2.zero);
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - cornerRadius;
        }

        private static float Erf(float x)
        {
            return (float)System.Math.Tanh(x * (1.1283791671f + 0.1009f * x * x));
        }
    }
}
