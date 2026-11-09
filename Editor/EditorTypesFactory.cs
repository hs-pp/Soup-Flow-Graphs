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
    public static class EditorTypesFactory
    {
        private static readonly Dictionary<Type, Type> VIEW_TYPES_BY_NODE_TYPE = AddTypesByNodeType<CustomNodeViewAttribute>(new Dictionary<Type, Type>(), typeof(BaseNodeView), attribute => attribute.NodeType);
        
        private static readonly Dictionary<Type, Color?> DEFAULT_COLORS_BY_VIEW_TYPE = new();
        
        private static readonly Dictionary<Type, string> DISPLAY_NAMES_BY_NODE_TYPE = new();
        
        private static readonly Dictionary<Type, Type> META_DATA_TYPES_BY_NODE_TYPE = AddTypesByNodeType<CustomNodeMetaDataAttribute>(
            new Dictionary<Type, Type>
            {
                // Built-in mappings
                { typeof(EntryNode), typeof(EntryNodeMetaData) },
                { typeof(SubGraphNode), typeof(SubGraphNodeMetaData) },
            },
            typeof(NodeMetaData), attribute => attribute.NodeType);

        private static readonly Dictionary<Type, Type> INSPECTOR_TYPES_BY_NODE_TYPE = AddTypesByNodeType<CustomNodeInspectorAttribute>(new Dictionary<Type, Type>(), typeof(ANodeInspector), attribute => attribute.NodeType);
        
        private static readonly Dictionary<Type, Type> BLACKBOARD_VARIABLE_TYPES_BY_VALUE_TYPE = FindBlackboardTypes(typeof(ABlackboardVariable), typeof(ABlackboardVariable<>));

        private static readonly Dictionary<Type, Type> BLACKBOARD_SETTER_TYPES_BY_VALUE_TYPE = FindBlackboardTypes(typeof(ABlackboardSetter), typeof(ABlackboardSetter<>));

        private static readonly Dictionary<Type, Type> BLACKBOARD_CONDITIONAL_TYPES_BY_VALUE_TYPE = FindBlackboardTypes(typeof(ABlackboardConditional), typeof(ABlackboardConditional<>));
        
        
        #region Node Type Registrations
        private static Dictionary<Type, Type> AddTypesByNodeType<TAttribute>(Dictionary<Type, Type> typesByNodeType, Type baseType, Func<TAttribute, Type> getNodeType) where TAttribute : Attribute
        {
            string attributeName = typeof(TAttribute).Name;
            if (attributeName.EndsWith(nameof(Attribute), StringComparison.Ordinal))
            {
                attributeName = attributeName.Substring(0, attributeName.Length - nameof(Attribute).Length);
            }

            List<Type> types = new(TypeCache.GetTypesWithAttribute<TAttribute>());
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            foreach (Type type in types)
            {
                if (!baseType.IsAssignableFrom(type) || type.IsAbstract)
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: {attributeName} attribute can only be used in concrete {baseType.Name} subclasses. ({type.Name})");
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: '{type.Name}' needs a public parameterless constructor.");
                    continue;
                }

                TAttribute attribute = (TAttribute)Attribute.GetCustomAttribute(type, typeof(TAttribute), false);
                Type nodeType = getNodeType(attribute);
                if (nodeType == null)
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: {attributeName} attribute in '{type.Name}' is for a null node type.");
                    continue;
                }
                if (!typeof(ANode).IsAssignableFrom(nodeType))
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: {attributeName} attribute in '{type.Name}' is for an invalid node type.");
                    continue;
                }

                // First registration wins
                if (!typesByNodeType.TryAdd(nodeType, type))
                {
                    Debug.LogError($"[Soup] EditorTypesFactory: '{nodeType.Name}' is already mapped to '{typesByNodeType[nodeType].Name}', so the registration on '{type.Name}' is ignored. Remove one of the two {attributeName} attributes.");
                }
            }

            return typesByNodeType;
        }

        private static Type FindClosestType(Dictionary<Type, Type> typesByNodeType, Type nodeType, Type fallbackType)
        {
            for (Type t = nodeType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (typesByNodeType.TryGetValue(t, out Type mapped))
                {
                    return mapped;
                }
            }

            return fallbackType;
        }
        #endregion

        #region NodeView Types
        public static BaseNodeView CreateNodeView(Type nodeType)
        {
            return (BaseNodeView)Activator.CreateInstance(GetNodeViewType(nodeType));
        }

        public static bool HasOwnNodeView(Type nodeType)
        {
            return nodeType != null && VIEW_TYPES_BY_NODE_TYPE.ContainsKey(nodeType);
        }

        public static Type GetNodeViewType(Type nodeType)
        {
            return FindClosestType(VIEW_TYPES_BY_NODE_TYPE, nodeType, typeof(DefaultNodeView));
        }

        private static bool TryGetDefaultNodeColor(Type nodeType, out Color color)
        {
            Type viewType = GetNodeViewType(nodeType);
            if (!DEFAULT_COLORS_BY_VIEW_TYPE.TryGetValue(viewType, out Color? defaultColor))
            {
                defaultColor = ReadDefaultNodeColor(viewType);
                DEFAULT_COLORS_BY_VIEW_TYPE[viewType] = defaultColor;
            }

            color = defaultColor.GetValueOrDefault();
            return defaultColor.HasValue;
        }

        private static Color? ReadDefaultNodeColor(Type viewType)
        {
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
        
        public static string GetNodeDisplayName(Type nodeType)
        {
            if (nodeType == null)
            {
                return string.Empty;
            }

            if (!DISPLAY_NAMES_BY_NODE_TYPE.TryGetValue(nodeType, out string displayName))
            {
                displayName = ReadNodeDisplayName(nodeType);
                DISPLAY_NAMES_BY_NODE_TYPE[nodeType] = displayName;
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
        private static bool CustomMetaDataTypeExists(Type nodeType)
        {
            return GetMetaDataType(nodeType) != typeof(NodeMetaData);
        }

        private static Type GetMetaDataType(Type nodeType)
        {
            return FindClosestType(META_DATA_TYPES_BY_NODE_TYPE, nodeType, typeof(NodeMetaData));
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

            Type expectedMetaDataType = GetMetaDataType(nodeType);
            return expectedMetaDataType == typeof(NodeMetaData) || metaData.GetType() == expectedMetaDataType;
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
        public static bool IsCorrectNodeInspectorType(Type nodeType, Type nodeInspectorType)
        {
            return nodeInspectorType == GetNodeInspectorType(nodeType);
        }
        
        public static ANodeInspector CreateNodeInspector(Type nodeType)
        {
            return (ANodeInspector)Activator.CreateInstance(GetNodeInspectorType(nodeType));
        }

        public static bool HasOwnNodeInspector(Type nodeType)
        {
            return nodeType != null && INSPECTOR_TYPES_BY_NODE_TYPE.ContainsKey(nodeType);
        }

        public static Type GetNodeInspectorType(Type nodeType)
        {
            return FindClosestType(INSPECTOR_TYPES_BY_NODE_TYPE, nodeType, typeof(DefaultNodeInspector));
        }
        #endregion

        #region Blackboard Types
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

        public static List<Type> GetBlackboardVariableTypes()
        {
            List<Type> types = new(BLACKBOARD_VARIABLE_TYPES_BY_VALUE_TYPE.Values);
            types.Sort((a, b) => string.CompareOrdinal(GetBlackboardVariableDisplayName(a), GetBlackboardVariableDisplayName(b)));
            return types;
        }

        public static bool IsBlackboardVariableType(Type type)
        {
            Type valueType = type != null ? GetBlackboardValueType(type, typeof(ABlackboardVariable<>)) : null;
            return valueType != null && BLACKBOARD_VARIABLE_TYPES_BY_VALUE_TYPE.TryGetValue(valueType, out Type registered) && registered == type;
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

            return BLACKBOARD_VARIABLE_TYPES_BY_VALUE_TYPE.TryGetValue(valueType, out Type variableType) ? GetBlackboardVariableDisplayName(variableType) : valueType.Name;
        }

        public static bool HasBlackboardSetter(Type valueType)
        {
            return valueType != null && BLACKBOARD_SETTER_TYPES_BY_VALUE_TYPE.ContainsKey(valueType);
        }

        public static ABlackboardSetter CreateBlackboardSetter(Type valueType)
        {
            return valueType != null && BLACKBOARD_SETTER_TYPES_BY_VALUE_TYPE.TryGetValue(valueType, out Type setterType) ? (ABlackboardSetter)Activator.CreateInstance(setterType) : null;
        }

        public static bool HasBlackboardConditional(Type valueType)
        {
            return valueType != null && BLACKBOARD_CONDITIONAL_TYPES_BY_VALUE_TYPE.ContainsKey(valueType);
        }

        public static ABlackboardConditional CreateBlackboardConditional(Type valueType)
        {
            return valueType != null && BLACKBOARD_CONDITIONAL_TYPES_BY_VALUE_TYPE.TryGetValue(valueType, out Type conditionalType) ? (ABlackboardConditional)Activator.CreateInstance(conditionalType) : null;
        }
        #endregion
    }
}