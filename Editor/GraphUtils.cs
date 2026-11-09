using System;
using System.Collections.Generic;
using System.Reflection;
using Soup.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor
{
    public static class MouseUtils
    {
        public static bool IsNone(this EventModifiers modifiers) => modifiers == EventModifiers.None;
        public static bool IsShift(this EventModifiers modifiers) => (modifiers & EventModifiers.Shift) != 0;
        public static bool IsActionKey(this EventModifiers modifiers) => PlatformUtils.IsMac ? (modifiers & EventModifiers.Command) != 0 : (modifiers & EventModifiers.Control) != 0;
        public static bool IsExclusiveShift(this EventModifiers modifiers) => modifiers == EventModifiers.Shift;
        public static bool IsExclusiveActionKey(this EventModifiers modifiers) => PlatformUtils.IsMac ? modifiers == EventModifiers.Command : modifiers == EventModifiers.Control;
    }

    public static class PlatformUtils
    {
        public static readonly bool IsMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
    }

    public static class RectUtils
    {
        public static bool IntersectsSegment(Rect rect, Vector2 p1, Vector2 p2)
        {
            float minX = Mathf.Min(p1.x, p2.x);
            float maxX = Mathf.Max(p1.x, p2.x);

            if (maxX > rect.xMax)
            {
                maxX = rect.xMax;
            }

            if (minX < rect.xMin)
            {
                minX = rect.xMin;
            }

            if (minX > maxX)
            {
                return false;
            }

            float minY = Mathf.Min(p1.y, p2.y);
            float maxY = Mathf.Max(p1.y, p2.y);

            float dx = p2.x - p1.x;

            if (Mathf.Abs(dx) > float.Epsilon)
            {
                float a = (p2.y - p1.y) / dx;
                float b = p1.y - a * p1.x;
                minY = a * minX + b;
                maxY = a * maxX + b;
            }

            if (minY > maxY)
            {
                (minY, maxY) = (maxY, minY);
            }

            if (maxY > rect.yMax)
            {
                maxY = rect.yMax;
            }

            if (minY < rect.yMin)
            {
                minY = rect.yMin;
            }

            if (minY > maxY)
            {
                return false;
            }

            return true;
        }

        public static Rect Encompass(Rect a, Rect b) => new()
            {
                xMin = Math.Min(a.xMin, b.xMin),
                yMin = Math.Min(a.yMin, b.yMin),
                xMax = Math.Max(a.xMax, b.xMax),
                yMax = Math.Max(a.yMax, b.yMax)
            };

        public static Rect Inflate(Rect a, float left, float top, float right, float bottom) => new()
            {
                xMin = a.xMin - left,
                yMin = a.yMin - top,
                xMax = a.xMax + right,
                yMax = a.yMax + bottom
            };

        internal static Rect Rect(this VisualElement ve) => new(Vector2.zero, ve.layout.size);
    }

    public static class SerializedPropertyUtils
    {
        public static IEnumerable<SerializedProperty> EnumerateVisibleChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                yield return iterator.Copy();
            }
        }

        public static void AddVisibleChildFields(VisualElement container, SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            foreach (SerializedProperty child in EnumerateVisibleChildren(property))
            {
                PropertyField field = new(child);
                field.BindProperty(child);
                container.Add(field);
            }
        }
    }

    public static class NodeUtils
    {
        public static bool IsConcreteNodeType(Type type)
        {
            return type != null && !type.IsAbstract && typeof(ANode).IsAssignableFrom(type);
        }

        public static List<OutportDefinition> GetOutportDefinitions(Type nodeType)
        {
            if (!IsConcreteNodeType(nodeType))
            {
                return new List<OutportDefinition>();
            }

            return GetOutportDefinitions((ANode)Activator.CreateInstance(nodeType));
        }

        public static List<OutportDefinition> GetOutportDefinitions(ANode node)
        {
            List<OutportDefinition> definitions = new();
            if (node == null)
            {
                return definitions;
            }

            try
            {
                node.DefineOutports(definitions);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"[Soup] NodeUtils: {node.GetType().Name} ({node.Id}) threw in DefineOutports, so its outports are left as they are.");
                return null;
            }

            HashSet<string> seenIds = new();
            for (int i = definitions.Count - 1; i >= 0; i--)
            {
                string outportId = definitions[i].Id;
                if (string.IsNullOrEmpty(outportId))
                {
                    Debug.LogError($"[Soup] {node.GetType().Name}: outport definition at {i} has a null or empty id. Dropping it.");
                    definitions.RemoveAt(i);
                }
                else if (outportId.Contains('#'))
                {
                    Debug.LogError($"[Soup] {node.GetType().Name}: outport id '{outportId}' contains '#', which is reserved as the edge id separator. Dropping it.");
                    definitions.RemoveAt(i);
                }
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (!seenIds.Add(definitions[i].Id))
                {
                    Debug.LogError($"[Soup] {node.GetType().Name}: duplicate outport id '{definitions[i].Id}'. Dropping the duplicate.");
                    definitions.RemoveAt(i);
                    i--;
                }
            }

            return definitions;
        }

        public readonly struct MenuEntry
        {
            public readonly Type NodeType;
            public readonly string MenuPath;

            public MenuEntry(Type nodeType, string menuPath)
            {
                NodeType = nodeType;
                MenuPath = menuPath;
            }
        }

        public static List<MenuEntry> GetMenuEntries(Type graphType)
        {
            List<MenuEntry> entries = new();
            foreach (Type type in TypeCache.GetTypesWithAttribute<CustomNodeAttribute>())
            {
                if (!IsConcreteNodeType(type))
                {
                    continue;
                }

                CustomNodeAttribute attribute = type.GetCustomAttribute<CustomNodeAttribute>();
                if (!attribute.AllowsGraphType(graphType))
                {
                    continue;
                }

                entries.Add(new MenuEntry(type, attribute.MenuPath));
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.MenuPath, b.MenuPath));
            return entries;
        }

        public static bool IsNodeTypeAllowed(Type nodeType, Type graphType)
        {
            CustomNodeAttribute attribute = nodeType.GetCustomAttribute<CustomNodeAttribute>();
            return attribute == null || attribute.AllowsGraphType(graphType);
        }
    }
}
