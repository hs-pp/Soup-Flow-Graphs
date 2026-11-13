using System;
using System.Collections.Generic;
using System.Text;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.State;
using Soup.Runtime;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core
{
    public static class FlowGraphValidator
    {
        private const int MAX_SUBGRAPH_DEPTH = 32;

        #region Edge Validation
        public sealed class EdgeValidationReport
        {
            public readonly UnityEngine.Object Asset;
            public readonly List<DroppedEdge> DroppedEdges = new();
            public int MissingTypeNodeCount;
            public bool Changed;

            public EdgeValidationReport(UnityEngine.Object asset)
            {
                Asset = asset;
            }
        }
        
        public readonly struct DroppedEdge
        {
            public readonly string NodeTypeName;
            public readonly string NodeId;
            public readonly string OutportId;
            public readonly string TargetNodeId;

            public DroppedEdge(string nodeTypeName, string nodeId, string outportId, string targetNodeId)
            {
                NodeTypeName = nodeTypeName;
                NodeId = nodeId;
                OutportId = outportId;
                TargetNodeId = targetNodeId;
            }
        }
        
        public static EdgeValidationReport ValidateAllEdges(NodeCollectionRef rootCollection)
        {
            EdgeValidationReport report = new(rootCollection.CollectionSP?.serializedObject.targetObject);
            ValidateEdgesInCollection(rootCollection, report, 0);
            return report;
        }
        
        /// <summary>
        /// Validates and REPAIRS broken edges due to outport definitions changing in code.
        /// </summary>
        private static bool ValidateNodeEdges(NodeRef nodeRef, List<DroppedEdge> droppedEdges = null)
        {
            if (!nodeRef.IsValid)
            {
                return false;
            }

            List<OutportDefinition> definitions = NodeUtils.GetOutportDefinitions(nodeRef.GetNodeInstance());
            if (definitions == null)
            {
                return false; // DefineOutports threw. Like a node whose type is missing, its outports and connections are kept.
            }

            // Materialized before anything resizes the array below.
            List<OutportRef> currentOutports = new(nodeRef.EnumerateOutports());

            // Check if anything has changed at all.
            if (currentOutports.Count == definitions.Count)
            {
                bool identical = true;
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (currentOutports[i].OutportId != definitions[i].Id)
                    {
                        identical = false;
                        break;
                    }
                }

                if (identical)
                {
                    return false;
                }
            }

            // Edges whose outport id no longer exists are dropped.
            Dictionary<string, string> connectedNodeIdsByOutportId = new();
            foreach (OutportRef outport in currentOutports)
            {
                if (!string.IsNullOrEmpty(outport.OutportId))
                {
                    connectedNodeIdsByOutportId.TryAdd(outport.OutportId, outport.ConnectedNodeId);
                }
            }

            // If we passed in a dropped list, populate it.
            if (droppedEdges != null)
            {
                HashSet<string> survivingOutportIds = new(definitions.Count);
                foreach (OutportDefinition definition in definitions)
                {
                    survivingOutportIds.Add(definition.Id);
                }

                string nodeTypeName = nodeRef.GetNodeType()?.Name;
                foreach (KeyValuePair<string, string> connection in connectedNodeIdsByOutportId)
                {
                    if (!string.IsNullOrEmpty(connection.Value) && !survivingOutportIds.Contains(connection.Key))
                    {
                        droppedEdges.Add(new DroppedEdge(nodeTypeName, nodeRef.Id, connection.Key, connection.Value));
                    }
                }
            }

            SerializedProperty outports = nodeRef.OutportsSP;
            outports.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
            {
                SerializedProperty element = outports.GetArrayElementAtIndex(i);
                string connectedNodeId = string.Empty;
                if (connectedNodeIdsByOutportId.TryGetValue(definitions[i].Id, out string target) && target != null)
                {
                    connectedNodeId = target;
                }

                element.FindPropertyRelative(OutportEdge.OUTPORT_ID_FIELD_NAME).stringValue = definitions[i].Id;
                element.FindPropertyRelative(OutportEdge.CONNECTED_NODE_ID_FIELD_NAME).stringValue = connectedNodeId;
            }

            return true;
        }

        public static bool ValidateNodeTreeEdges(NodeRef nodeRef)
        {
            EdgeValidationReport report = new(null);
            ValidateNodeTreeEdges(nodeRef, report, 0);
            return report.Changed;
        }

        private static void ValidateEdgesInCollection(NodeCollectionRef collection, EdgeValidationReport report, int depth)
        {
            if (!collection.IsValid)
            {
                return;
            }

            if (depth > MAX_SUBGRAPH_DEPTH)
            {
                Debug.LogError($"[Soup] FlowGraphValidator: subgraph nesting exceeded {MAX_SUBGRAPH_DEPTH} levels.");
                return;
            }

            // Resizing a node's outports never changes the nodes array itself, so lazily walking it here is safe.
            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeRef.IsMissingType)
                {
                    report.MissingTypeNodeCount++;
                    continue;
                }

                ValidateNodeTreeEdges(nodeRef, report, depth);
            }
        }

        private static void ValidateNodeTreeEdges(NodeRef nodeRef, EdgeValidationReport report, int depth)
        {
            report.Changed |= ValidateNodeEdges(nodeRef, report.DroppedEdges);

            if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph))
            {
                ValidateEdgesInCollection(subGraph, report, depth + 1);
            }
        }
        
        public static void LogReport(EdgeValidationReport report)
        {
            if (report.DroppedEdges.Count > 0)
            {
                StringBuilder message = new();
                message.AppendLine($"[Soup] '{report.Asset.name}' {report.DroppedEdges.Count} connection(s) were dropped. Asset is now dirty and saving makes this permanent.");
                foreach (DroppedEdge drop in report.DroppedEdges)
                {
                    message.AppendLine($"  • {drop.NodeTypeName} ({drop.NodeId}) outport '{drop.OutportId}' → {drop.TargetNodeId}");
                }

                Debug.LogWarning(message.ToString(), report.Asset);
            }

            if (report.MissingTypeNodeCount > 0)
            {
                StringBuilder message = new();
                message.AppendLine($"[Soup] '{report.Asset.name}' {report.MissingTypeNodeCount} node(s) could not be loaded because their class was renamed, moved to another namespace or assembly, or deleted. They are hidden from the canvas and connections to them dangle, but their data is still in the asset and comes back with the type.");

                List<string> missingTypes = GetMissingTypeNames(report.Asset, out _);
                if (missingTypes.Count > 0)
                {
                    message.AppendLine("Missing types in this asset:");
                    foreach (string typeName in missingTypes)
                    {
                        message.AppendLine($"  • {typeName}");
                    }
                }

                Debug.LogWarning(message.ToString(), report.Asset);
            }
        }
        #endregion
        
        #region Meta Data Validation
        public sealed class MetaDataValidationReport
        {
            public readonly UnityEngine.Object Asset;
            public readonly List<string> MissingTypes = new();
            public int MissingTypeReferenceCount;
            public int UnresolvedMetaDataCount;
            public bool Changed;

            public MetaDataValidationReport(UnityEngine.Object asset)
            {
                Asset = asset;
            }
        }
        
        public static MetaDataValidationReport ValidateAllMetaData(NodeCollectionMetaDataRef rootCollectionMetaData, NodeCollectionRef rootCollection)
        {
            MetaDataValidationReport report = new(rootCollection.CollectionSP?.serializedObject.targetObject);
            report.MissingTypes.AddRange(GetMissingTypeNames(rootCollectionMetaData.CollectionSP?.serializedObject.targetObject, out int missingTypeReferenceCount));
            report.MissingTypeReferenceCount = missingTypeReferenceCount;

            ValidateNodeCollectionMetaData(rootCollectionMetaData, rootCollection, 0, report);
            return report;
        }

        private static void ValidateNodeCollectionMetaData(NodeCollectionMetaDataRef collectionMetaData, NodeCollectionRef collection, int depth, MetaDataValidationReport report)
        {
            // Edge validation already logs nesting that's too deep.
            if (!collectionMetaData.IsValid || !collection.IsValid || depth > MAX_SUBGRAPH_DEPTH)
            {
                return;
            }

            bool metaDataHasMissingTypes = report.MissingTypeReferenceCount > 0;
            bool hasMissingNodes = HasMissingNodes(collection);
            Dictionary<string, NodeRef> nodesById = collection.GetNodesById();

            // Walked in reverse and indexed by hand because entries are deleted in place, which rules out enumerating
            // NodeCollectionMetaDataRef.GetNodeMetaDatas here.
            for (int i = collectionMetaData.NodesSP.arraySize - 1; i >= 0; i--)
            {
                NodeMetaDataRef nodeMetaData = new(collectionMetaData.NodesSP.GetArrayElementAtIndex(i), i);
                if (nodeMetaData.IsMissingType)
                {
                    if (metaDataHasMissingTypes)
                    {
                        continue;
                    }

                    collectionMetaData.NodesSP.DeleteArrayElementAtIndex(i);
                    report.Changed = true;
                    continue;
                }

                if (!nodesById.TryGetValue(nodeMetaData.NodeId, out NodeRef nodeRef))
                {
                    if (hasMissingNodes)
                    {
                        continue;
                    }

                    collectionMetaData.NodesSP.DeleteArrayElementAtIndex(i);
                    report.Changed = true;
                    continue;
                }

                Type nodeType = nodeRef.GetNodeType();
                if (ValidateNodeMetaDataType(nodeMetaData, nodeType))
                {
                    report.Changed = true;
                    continue;
                }

                if (EditorTypesFactory.HasNoRegisteredMetaDataType(nodeMetaData.GetNodeMetaDataInstance(), nodeType))
                {
                    report.UnresolvedMetaDataCount++;
                }

                if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph)
                    && nodeMetaData.TryGetSubGraphCollectionMetaData(out NodeCollectionMetaDataRef subGraphMetaData))
                {
                    ValidateNodeCollectionMetaData(subGraphMetaData, subGraph, depth + 1, report);
                }
            }
        }
        
        public static bool ValidateNodeMetaDataType(NodeMetaDataRef nodeMetaData, Type nodeType)
        {
            NodeMetaData existing = nodeMetaData.GetNodeMetaDataInstance();
            if (EditorTypesFactory.IsUsingCorrectMetaDataType(existing, nodeType))
            {
                return false;
            }

            // repair if wrong type.
            nodeMetaData.MetaDataSP.managedReferenceValue = EditorTypesFactory.ConvertMetaDataToCorrectType(existing, nodeType, nodeMetaData.NodeId);
            return true;
        }

        private static bool HasMissingNodes(NodeCollectionRef collection)
        {
            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeRef.IsMissingType)
                {
                    return true;
                }
            }

            return false;
        }
        
        // TODO: Improve on report logging.
        public static void LogReport(MetaDataValidationReport report)
        {
            if (report.MissingTypes.Count > 0)
            {
                StringBuilder message = new();
                message.AppendLine($"[Soup] '{report.Asset.name}' {report.MissingTypeReferenceCount} node meta data entry(s) could not be loaded because their class was renamed, moved to another namespace or assembly, or deleted. Nothing was deleted in response: positions, and for a subgraph the whole nested layout, stay in the asset and come back with the type.");
                message.AppendLine("Missing meta data types:");
                foreach (string typeName in report.MissingTypes)
                {
                    message.AppendLine($"  • {typeName}");
                }

                Debug.LogWarning(message.ToString(), report.Asset);
            }

            if (report.UnresolvedMetaDataCount > 0)
            {
                Debug.LogWarning($"[Soup] '{report.Asset.name}' {report.UnresolvedMetaDataCount} node meta data entry(s) were kept as-is because their node's type has no registered meta data type. Check the console for EditorTypesFactory errors; the entries are preserved until a registration is back.", report.Asset);
            }
        }
        #endregion

        #region Node Type Validation
        /// <summary>
        /// Lists the nodes, subgraphs included, whose type the graph type doesn't allow. Nothing is changed.
        /// </summary>
        public static List<string> FindDisallowedNodes(NodeCollectionRef rootCollection, Type graphType)
        {
            List<string> disallowedNodes = new();
            FindDisallowedNodes(rootCollection, graphType, disallowedNodes, 0);
            return disallowedNodes;
        }

        private static void FindDisallowedNodes(NodeCollectionRef collection, Type graphType, List<string> disallowedNodes, int depth)
        {
            // Edge validation already logs nesting that's too deep.
            if (!collection.IsValid || depth > MAX_SUBGRAPH_DEPTH)
            {
                return;
            }

            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeRef.IsMissingType)
                {
                    continue;
                }

                Type nodeType = nodeRef.GetNodeType();
                if (nodeType != null && !NodeUtils.IsNodeTypeAllowed(nodeType, graphType))
                {
                    disallowedNodes.Add($"{nodeType.Name} ({nodeRef.Id})");
                }

                if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph))
                {
                    FindDisallowedNodes(subGraph, graphType, disallowedNodes, depth + 1);
                }
            }
        }

        /// <summary>
        /// Warns about the nodes FindDisallowedNodes found, if there are any.
        /// </summary>
        public static void LogDisallowedNodes(UnityEngine.Object asset, List<string> disallowedNodes)
        {
            if (disallowedNodes.Count == 0)
            {
                return;
            }

            StringBuilder message = new();
            message.AppendLine($"[Soup] '{asset.name}' has {disallowedNodes.Count} node(s) its graph type doesn't allow. They still run and stay in the asset, but new ones can't be created or pasted.");
            foreach (string node in disallowedNodes)
            {
                message.AppendLine($"  • {node}");
            }

            Debug.LogWarning(message.ToString(), asset);
        }
        #endregion

        private static List<string> GetMissingTypeNames(UnityEngine.Object asset, out int referenceCount)
        {
            if (asset == null)
            {
                referenceCount = 0;
                return new List<string>();
            }

            ManagedReferenceMissingType[] allMissing = SerializationUtility.GetManagedReferencesWithMissingTypes(asset);
            referenceCount = allMissing.Length;
            HashSet<string> missingClasses = new();

            foreach (ManagedReferenceMissingType missing in allMissing)
            {
                string prefix = string.IsNullOrEmpty(missing.namespaceName) ? string.Empty : missing.namespaceName + ".";
                string name = $"{prefix}{missing.className} (assembly '{missing.assemblyName}')";
                missingClasses.Add(name);
            }

            List<string> sorted = new(missingClasses);
            sorted.Sort(string.CompareOrdinal);
            return sorted;
        }
    }
}
