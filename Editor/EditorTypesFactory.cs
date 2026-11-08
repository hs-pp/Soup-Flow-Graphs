using System;
using System.Collections.Generic;
using Soup.Editor.State;
using Soup.Editor.Views.Inspector;
using Soup.Editor.Views.Nodes;
using Soup.Runtime;
using Soup.Runtime.Blackboards;
using Soup.Runtime.BuiltInNodes;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CustomNodeViewAttribute : Attribute
    {
        public Type NodeType { get; }

        public CustomNodeViewAttribute(Type nodeType)
        {
            NodeType = nodeType;
        }
    }
    
    /// <summary>
    /// The color a node starts with when it is created. Goes on a node view, and views deriving from it inherit it.
    /// After creation the color lives in the node's meta data and is edited from the inspector, so changing this
    /// attribute later does not recolor existing nodes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class DefaultNodeColorAttribute : Attribute
    {
        public string Hex { get; }
        public Color Color { get; }
        public bool IsValid { get; }

        /// <param name="hex">"#RRGGBB" or "#RRGGBBAA".</param>
        public DefaultNodeColorAttribute(string hex)
        {
            Hex = hex;
            // The '#' check keeps out the color names TryParseHtmlString would also accept.
            if (hex != null && hex.StartsWith("#", StringComparison.Ordinal) && ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                Color = color;
                IsValid = true;
            }
        }
    }

    /// <summary>
    /// The name a node shows on its node view, in the inspector header and in sub graph breadcrumbs. Goes on a node
    /// view and is not inherited by views deriving from it. Every node type that resolves to the view shows this name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CustomNodeNameAttribute : Attribute
    {
        public string Name { get; }

        public CustomNodeNameAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CustomNodeMetaDataAttribute : Attribute
    {
        public Type NodeType { get; }

        public CustomNodeMetaDataAttribute(Type nodeType)
        {
            NodeType = nodeType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CustomNodeInspectorAttribute : Attribute
    {
        public Type NodeType { get; }

        public CustomNodeInspectorAttribute(Type nodeType)
        {
            NodeType = nodeType;
        }
    }
    
    public static class EditorTypesFactory
    {
        #region NodeView Types
        private static Dictionary<Type, Type> m_viewTypesByNodeType;
        private static Dictionary<Type, Type> ViewTypesByNodeType
        {
            get
            {
                if (m_viewTypesByNodeType != null)
                {
                    return m_viewTypesByNodeType;
                }

                m_viewTypesByNodeType = new Dictionary<Type, Type>();

                List<Type> viewTypes = new(TypeCache.GetTypesWithAttribute<CustomNodeViewAttribute>());
                viewTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

                foreach (Type viewType in viewTypes)
                {
                    if (!typeof(BaseNodeView).IsAssignableFrom(viewType) || viewType.IsAbstract)
                    {
                        Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeView attribute can only be used in concrete BaseNodeView subclasses. ({viewType.Name})");
                        continue;
                    }

                    foreach (object attribute in viewType.GetCustomAttributes(typeof(CustomNodeViewAttribute), false))
                    {
                        Type nodeType = ((CustomNodeViewAttribute)attribute).NodeType;
                        if (nodeType == null)
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeView attribute in '{viewType.Name}' is for a null node type.");
                            continue;
                        }
                        if (!typeof(ANode).IsAssignableFrom(nodeType))
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeView attribute in '{viewType.Name}' is for an invalid node type.");
                            continue;
                        }

                        // First registration wins
                        if (!m_viewTypesByNodeType.TryAdd(nodeType, viewType))
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: '{nodeType.Name}' is already mapped to '{m_viewTypesByNodeType[nodeType].Name}', so the registration on '{viewType.Name}' is ignored. Remove one of the two CustomNodeView attributes.");
                        }
                    }
                }

                return m_viewTypesByNodeType;
            }
        }

        public static BaseNodeView CreateNodeView(Type nodeType)
        {
            return (BaseNodeView)Activator.CreateInstance(GetNodeViewType(nodeType));
        }

        // True when a view registers for this exact node type, rather than one of its base types.
        public static bool HasOwnNodeView(Type nodeType)
        {
            return nodeType != null && ViewTypesByNodeType.ContainsKey(nodeType);
        }

        // The closest registration up the node's type chain wins, falling back to the default view.
        public static Type GetNodeViewType(Type nodeType)
        {
            for (Type t = nodeType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (ViewTypesByNodeType.TryGetValue(t, out Type mapped))
                {
                    return mapped;
                }
            }

            return typeof(DefaultNodeView);
        }

        // Null when the view has no usable DefaultNodeColor attribute.
        private static readonly Dictionary<Type, Color?> s_defaultColorsByViewType = new();

        private static bool TryGetDefaultNodeColor(Type nodeType, out Color color)
        {
            Type viewType = GetNodeViewType(nodeType);
            if (!s_defaultColorsByViewType.TryGetValue(viewType, out Color? defaultColor))
            {
                defaultColor = ReadDefaultNodeColor(viewType);
                s_defaultColorsByViewType[viewType] = defaultColor;
            }

            color = defaultColor.GetValueOrDefault();
            return defaultColor.HasValue;
        }

        private static Color? ReadDefaultNodeColor(Type viewType)
        {
            // Inherited from base views, and the most derived attribute wins.
            DefaultNodeColorAttribute attribute = (DefaultNodeColorAttribute)Attribute.GetCustomAttribute(viewType, typeof(DefaultNodeColorAttribute), true);
            if (attribute == null)
            {
                return null;
            }

            if (!attribute.IsValid)
            {
                Debug.LogError($"[Soup] EditorTypesFactory: DefaultNodeColor attribute on '{viewType.Name}' has an invalid hex color '{attribute.Hex}', so new nodes use the standard color. Use \"#RRGGBB\" or \"#RRGGBBAA\".");
                return null;
            }

            return attribute.Color;
        }

        private static readonly Dictionary<Type, string> s_displayNamesByNodeType = new();

        /// <summary>
        /// The name a node shows in the editor: its view's CustomNodeName, or else its type name without the "Node"
        /// suffix, so "SubGraphNode" reads "Sub Graph". Logs and errors keep the type name so it matches the code.
        /// </summary>
        public static string GetNodeDisplayName(Type nodeType)
        {
            if (nodeType == null)
            {
                return string.Empty;
            }

            if (!s_displayNamesByNodeType.TryGetValue(nodeType, out string displayName))
            {
                displayName = ReadNodeDisplayName(nodeType);
                s_displayNamesByNodeType[nodeType] = displayName;
            }

            return displayName;
        }

        private static string ReadNodeDisplayName(Type nodeType)
        {
            Type viewType = GetNodeViewType(nodeType);
            CustomNodeNameAttribute attribute = (CustomNodeNameAttribute)Attribute.GetCustomAttribute(viewType, typeof(CustomNodeNameAttribute), false);
            if (attribute != null)
            {
                if (!string.IsNullOrWhiteSpace(attribute.Name))
                {
                    return attribute.Name;
                }

                Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeName attribute on '{viewType.Name}' is empty, so its nodes show their type name.");
            }

            return nodeType.Name;
        }
        #endregion
        
        #region MetaData Types
        private static Dictionary<Type, Type> s_metaDataTypesByNodeType;
        private static Dictionary<Type, Type> MetaDataTypesByNodeType
        {
            get
            {
                if (s_metaDataTypesByNodeType != null)
                {
                    return s_metaDataTypesByNodeType;
                }

                s_metaDataTypesByNodeType = new Dictionary<Type, Type>();
                
                // Built-in mappings to avoid user error
                s_metaDataTypesByNodeType[typeof(EntryNode)] = typeof(EntryNodeMetaData);
                s_metaDataTypesByNodeType[typeof(SubGraphNode)] = typeof(SubGraphNodeMetaData);

                List<Type> metaDataTypes = new(TypeCache.GetTypesWithAttribute<CustomNodeMetaDataAttribute>());
                metaDataTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

                foreach (Type metaDataType in metaDataTypes)
                {
                    if (!typeof(NodeMetaData).IsAssignableFrom(metaDataType) || metaDataType.IsAbstract)
                    {
                        Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeMetaData attribute can only be used in concrete NodeMetaData subclasses. ({metaDataType.Name})");
                        continue;
                    }

                    if (metaDataType.GetConstructor(Type.EmptyTypes) == null)
                    {
                        Debug.LogError($"[Soup] EditorTypesFactory: '{metaDataType.Name}' needs a public parameterless constructor.");
                        continue;
                    }

                    foreach (object attribute in metaDataType.GetCustomAttributes(typeof(CustomNodeMetaDataAttribute), false))
                    {
                        Type nodeType = ((CustomNodeMetaDataAttribute)attribute).NodeType;
                        if (nodeType == null)
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeMetaData attribute in '{metaDataType.Name}' is for a null node type.");
                            continue;
                        }
                        if (!typeof(ANode).IsAssignableFrom(nodeType))
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeMetaData attribute in '{metaDataType.Name}' is for an invalid node type.");
                            continue;
                        }

                        // First registration wins and loser is reported
                        if (!s_metaDataTypesByNodeType.TryAdd(nodeType, metaDataType))
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: '{nodeType.Name}' is already mapped to '{s_metaDataTypesByNodeType[nodeType].Name}', so the registration on '{metaDataType.Name}' is ignored. Remove one of the two CustomNodeMetaData attributes.");
                        }
                    }
                }

                return s_metaDataTypesByNodeType;
            }
        }
        
        
        private static bool CustomMetaDataTypeExists(Type nodeType)
        {
            return GetMetaDataType(nodeType) != typeof(NodeMetaData);
        }

        private static Type GetMetaDataType(Type nodeType)
        {
            for (Type t = nodeType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (MetaDataTypesByNodeType.TryGetValue(t, out Type metaDataType))
                {
                    return metaDataType;
                }
            }

            return typeof(NodeMetaData);
        }

        public static NodeMetaData CreateMetaDataInstance(Type nodeType, string nodeId)
        {
            Type metaDataType = GetMetaDataType(nodeType);
            NodeMetaData metaData = (NodeMetaData)Activator.CreateInstance(metaDataType);
            metaData.NodeId = nodeId;

            // Only new entries get the view's default. ConvertMetaDataToCorrectType copies the existing color over it.
            if (TryGetDefaultNodeColor(nodeType, out Color defaultColor))
            {
                metaData.Color = defaultColor;
            }

            return metaData;
        }

        public static bool IsUsingCorrectMetaDataType(NodeMetaData metaData, Type nodeType)
        {
            if (metaData == null)
            {
                return false;
            }

            Type expectedNodeType = GetMetaDataType(nodeType);
            return expectedNodeType == typeof(NodeMetaData) || metaData.GetType() == expectedNodeType;
        }

        public static bool HasNoRegisteredMetaDataType(NodeMetaData metaData, Type nodeType)
        {
            return metaData != null && metaData.GetType() != typeof(NodeMetaData) && !CustomMetaDataTypeExists(nodeType);
        }
        
        public static NodeMetaData ConvertMetaDataToCorrectType(NodeMetaData existing, Type nodeType, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                Debug.LogError("[Soup] EditorTypesFactory: ConvertMetaDataToCorrectType was called without a node id. The entry is returned unchanged.");
                return existing;
            }

            NodeMetaData converted = CreateMetaDataInstance(nodeType, nodeId);
            converted.CopyFrom(existing);
            converted.NodeId = nodeId;
            return converted;
        }
        #endregion
        
        #region NodeInspector Types
        private static Dictionary<Type, Type> m_inspectorTypesByNodeType;
        private static Dictionary<Type, Type> InspectorTypesByNodeType
        {
            get
            {
                if (m_inspectorTypesByNodeType != null)
                {
                    return m_inspectorTypesByNodeType;
                }

                m_inspectorTypesByNodeType = new Dictionary<Type, Type>();

                List<Type> viewTypes = new(TypeCache.GetTypesWithAttribute<CustomNodeInspectorAttribute>());
                viewTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

                foreach (Type viewType in viewTypes)
                {
                    if (!typeof(ANodeInspector).IsAssignableFrom(viewType) || viewType.IsAbstract)
                    {
                        Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeInspector attribute can only be used in concrete ANodeInspector subclasses. ({viewType.Name})");
                        continue;
                    }

                    foreach (object attribute in viewType.GetCustomAttributes(typeof(CustomNodeInspectorAttribute), false))
                    {
                        Type nodeType = ((CustomNodeInspectorAttribute)attribute).NodeType;
                        if (nodeType == null)
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeInspector attribute in '{viewType.Name}' is for a null node type.");
                            continue;
                        }
                        if (!typeof(ANode).IsAssignableFrom(nodeType))
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: CustomNodeInspector attribute in '{viewType.Name}' is for an invalid node type.");
                            continue;
                        }

                        // First registration wins
                        if (!m_inspectorTypesByNodeType.TryAdd(nodeType, viewType))
                        {
                            Debug.LogError($"[Soup] EditorTypesFactory: '{nodeType.Name}' is already mapped to '{m_inspectorTypesByNodeType[nodeType].Name}', so the registration on '{viewType.Name}' is ignored. Remove one of the two CustomNodeInspector attributes.");
                        }
                    }
                }

                return m_inspectorTypesByNodeType;
            }
        }

        public static bool IsCorrectNodeInspectorType(Type nodeType, Type nodeInspectorType)
        {
            return nodeInspectorType == GetNodeInspectorType(nodeType);
        }
        
        public static ANodeInspector CreateNodeInspector(Type nodeType)
        {
            return (ANodeInspector)Activator.CreateInstance(GetNodeInspectorType(nodeType));
        }

        // True when an inspector registers for this exact node type, rather than one of its base types.
        public static bool HasOwnNodeInspector(Type nodeType)
        {
            return nodeType != null && InspectorTypesByNodeType.ContainsKey(nodeType);
        }

        // The closest registration up the node's type chain wins, falling back to the default inspector.
        public static Type GetNodeInspectorType(Type nodeType)
        {
            for (Type t = nodeType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (InspectorTypesByNodeType.TryGetValue(t, out Type mapped))
                {
                    return mapped;
                }
            }

            return typeof(DefaultNodeInspector);
        }
        #endregion

        #region Blackboard Types

        private static Dictionary<Type, Type> s_blackboardVariableTypesByValueType;
        private static Dictionary<Type, Type> BlackboardVariableTypesByValueType =>
            s_blackboardVariableTypesByValueType ??= FindBlackboardTypes(typeof(ABlackboardVariable), typeof(ABlackboardVariable<>));

        private static Dictionary<Type, Type> s_blackboardSetterTypesByValueType;
        private static Dictionary<Type, Type> BlackboardSetterTypesByValueType =>
            s_blackboardSetterTypesByValueType ??= FindBlackboardTypes(typeof(ABlackboardSetter), typeof(ABlackboardSetter<>));

        private static Dictionary<Type, Type> s_blackboardConditionalTypesByValueType;
        private static Dictionary<Type, Type> BlackboardConditionalTypesByValueType =>
            s_blackboardConditionalTypesByValueType ??= FindBlackboardTypes(typeof(ABlackboardConditional), typeof(ABlackboardConditional<>));

        // One concrete class per value type for each kind. The first by full name wins and the rest are reported.
        private static Dictionary<Type, Type> FindBlackboardTypes(Type baseType, Type genericBaseType)
        {
            Dictionary<Type, Type> typesByValueType = new();

            List<Type> types = new(TypeCache.GetTypesDerivedFrom(baseType));
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            foreach (Type type in types)
            {
                if (type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: '{type.Name}' needs a public parameterless constructor.");
                    continue;
                }

                Type valueType = GetBlackboardValueType(type, genericBaseType);
                if (valueType == null)
                {
                    continue;
                }

                if (!typesByValueType.TryAdd(valueType, type))
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: '{valueType.Name}' already has '{typesByValueType[valueType].Name}', so '{type.Name}' is ignored. Keep one {genericBaseType.Name.Split('`')[0]} per value type.");
                }
            }

            return typesByValueType;
        }

        private static Type GetBlackboardValueType(Type type, Type genericBaseType)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == genericBaseType)
                {
                    return t.GetGenericArguments()[0];
                }
            }

            return null;
        }

        /// <summary>
        /// The variable class registered for each value type, sorted by display name.
        /// </summary>
        public static List<Type> GetBlackboardVariableTypes()
        {
            List<Type> types = new(BlackboardVariableTypesByValueType.Values);
            types.Sort((a, b) => string.CompareOrdinal(GetBlackboardVariableDisplayName(a), GetBlackboardVariableDisplayName(b)));
            return types;
        }

        public static bool IsBlackboardVariableType(Type type)
        {
            Type valueType = type != null ? GetBlackboardValueType(type, typeof(ABlackboardVariable<>)) : null;
            return valueType != null && BlackboardVariableTypesByValueType.TryGetValue(valueType, out Type registered) && registered == type;
        }

        public static string GetBlackboardVariableDisplayName(Type variableType)
        {
            if (variableType == null)
            {
                return string.Empty;
            }

            return variableType.Name;
        }

        public static string GetBlackboardValueTypeDisplayName(Type valueType)
        {
            if (valueType == null)
            {
                return string.Empty;
            }

            return BlackboardVariableTypesByValueType.TryGetValue(valueType, out Type variableType) ? GetBlackboardVariableDisplayName(variableType) : valueType.Name;
        }

        public static bool HasBlackboardSetter(Type valueType)
        {
            return valueType != null && BlackboardSetterTypesByValueType.ContainsKey(valueType);
        }

        public static ABlackboardSetter CreateBlackboardSetter(Type valueType)
        {
            return valueType != null && BlackboardSetterTypesByValueType.TryGetValue(valueType, out Type setterType)
                ? (ABlackboardSetter)Activator.CreateInstance(setterType)
                : null;
        }

        public static bool HasBlackboardConditional(Type valueType)
        {
            return valueType != null && BlackboardConditionalTypesByValueType.ContainsKey(valueType);
        }

        public static ABlackboardConditional CreateBlackboardConditional(Type valueType)
        {
            return valueType != null && BlackboardConditionalTypesByValueType.TryGetValue(valueType, out Type conditionalType)
                ? (ABlackboardConditional)Activator.CreateInstance(conditionalType)
                : null;
        }
        #endregion
    }
}