using System;
using System.Collections.Generic;
using System.Linq;
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
            public int MissingNodeTypes;
            public bool Changed;

            public EdgeValidationReport(UnityEngine.Object asset)
            {
                Asset = asset;
            }
        }
        
        public readonly struct DroppedEdge
        {
            public readonly string NodeType;
            public readonly string NodeId;
            public readonly string OutportKey;
            public readonly string TargetNodeId;

            public DroppedEdge(string nodeTypeName, string nodeId, string outportKey, string targetNodeId)
            {
                NodeType = nodeTypeName;
                NodeId = nodeId;
                OutportKey = outportKey;
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
        private static bool ValidateNodeEdges(NodeRef nodeRef, List<DroppedEdge> inDroppedEdges = null)
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
                    if (currentOutports[i].Id != definitions[i].Id)
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

            // Any edges whose key no longer exist is dropped.
            Dictionary<string, string> connectionsByKey = new();
            foreach (OutportRef outport in currentOutports)
            {
                if (!string.IsNullOrEmpty(outport.Id))
                {
                    connectionsByKey.TryAdd(outport.Id, outport.ConnectedNodeId);
                }
            }

            // If we passed in a dropped list, populate it.
            if (inDroppedEdges != null)
            {
                HashSet<string> survivingKeys = new(definitions.Count);
                foreach (OutportDefinition definition in definitions)
                {
                    survivingKeys.Add(definition.Id);
                }

                string nodeTypeName = nodeRef.GetNodeType()?.Name;
                foreach (KeyValuePair<string, string> connection in connectionsByKey)
                {
                    if (!string.IsNullOrEmpty(connection.Value) && !survivingKeys.Contains(connection.Key))
                    {
                        inDroppedEdges.Add(new DroppedEdge(nodeTypeName, nodeRef.Id, connection.Key, connection.Value));
                    }
                }
            }

            SerializedProperty outports = nodeRef.OutportsSP;
            outports.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
            {
                SerializedProperty element = outports.GetArrayElementAtIndex(i);
                element.FindPropertyRelative(OutportEdge.OUTPORT_ID_FIELD_NAME).stringValue = definitions[i].Id;
                element.FindPropertyRelative(OutportEdge.CONNECTED_NODE_ID_FIELD_NAME).stringValue = connectionsByKey.TryGetValue(definitions[i].Id, out string target) ? target ?? string.Empty : string.Empty;
            }

            return true;
        }

        public static bool ValidateNodeTreeEdges(NodeRef nodeRef)
        {
            EdgeValidationReport report = new(null);
            ValidateNodeTreeEdges(nodeRef, report, 0);
            return report.Changed;
        }

        private static void ValidateEdgesInCollection(NodeCollectionRef collection, EdgeValidationReport inReport, int depth)
        {
            if (!collection.IsValid)
            {
                return;
            }

            if (depth > MAX_SUBGRAPH_DEPTH)
            {
                Debug.LogError($"[Soup] Subgraph nesting exceeded {MAX_SUBGRAPH_DEPTH} levels.");
                return;
            }

            // Resizing a node's outports never changes the nodes array itself, so lazily walking it here is safe.
            foreach (NodeRef nodeRef in collection.GetNodes())
            {
                if (nodeRef.IsMissingType)
                {
                    inReport.MissingNodeTypes++;
                    continue;
                }

                ValidateNodeTreeEdges(nodeRef, inReport, depth);
            }
        }

        private static void ValidateNodeTreeEdges(NodeRef nodeRef, EdgeValidationReport inReport, int depth)
        {
            inReport.Changed |= ValidateNodeEdges(nodeRef, inReport.DroppedEdges);

            if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph))
            {
                ValidateEdgesInCollection(subGraph, inReport, depth + 1);
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
                    message.AppendLine($"  • {drop.NodeType} ({drop.NodeId}) outport '{drop.OutportKey}' → {drop.TargetNodeId}");
                }

                Debug.LogWarning(message.ToString(), report.Asset);
            }

            if (report.MissingNodeTypes > 0)
            {
                StringBuilder message = new();
                message.AppendLine($"[Soup] '{report.Asset.name}' {report.MissingNodeTypes} node(s) could not be loaded because their class was renamed, moved to another namespace or assembly, or deleted. They are hidden from the canvas and connections to them dangle, but their data is still in the asset and comes back with the type.");

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
            public int MissingTypeReferences;
            public bool Changed;
            public int UnresolvedMetaDatas;

            public MetaDataValidationReport(UnityEngine.Object asset)
            {
                Asset = asset;
            }
        }
        
        public static MetaDataValidationReport ValidateFlowGraphMetaData(NodeCollectionMetaDataRef rootCollectionMetaData, NodeCollectionRef rootCollection)
        {
            MetaDataValidationReport report = new(rootCollection.CollectionSP?.serializedObject.targetObject);
            report.MissingTypes.AddRange(GetMissingTypeNames(rootCollectionMetaData.CollectionSP?.serializedObject.targetObject, out int missingTypeReferences));
            report.MissingTypeReferences = missingTypeReferences;

            ValidateNodeCollectionMetaData(rootCollectionMetaData, rootCollection, 0, report.MissingTypeReferences > 0, report);
            return report;
        }
        
        private static void ValidateNodeCollectionMetaData(NodeCollectionMetaDataRef collectionMetaData, NodeCollectionRef collection, int depth, bool metaDataHasMissingTypes, MetaDataValidationReport report)
        {
            if (!collectionMetaData.IsValid || !collection.IsValid)
            {
                return;
            }

            if (depth > MAX_SUBGRAPH_DEPTH)
            {
                Debug.LogError($"[Soup] Subgraph nesting exceeded {MAX_SUBGRAPH_DEPTH} levels.");
                return;
            }

            bool hasMissingNodes = HasMissingNodes(collection);

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

                if (!collection.TryGetNode(nodeMetaData.NodeId, out NodeRef nodeRef))
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
                    report.UnresolvedMetaDatas++;
                }

                if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef subGraph)
                    && nodeMetaData.TryGetSubGraphCollectionMetaData(out NodeCollectionMetaDataRef subGraphMetaData))
                {
                    ValidateNodeCollectionMetaData(subGraphMetaData, subGraph, depth + 1, metaDataHasMissingTypes, report);
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
                message.AppendLine($"[Soup] '{report.Asset.name}' {report.MissingTypeReferences} node meta data entry(s) could not be loaded because their class was renamed, moved to another namespace or assembly, or deleted. Nothing was deleted in response: positions, and for a subgraph the whole nested layout, stay in the asset and come back with the type.");
                message.AppendLine("Missing meta data types:");
                foreach (string typeName in report.MissingTypes)
                {
                    message.AppendLine($"  • {typeName}");
                }

                Debug.LogWarning(message.ToString(), report.Asset);
            }

            if (report.UnresolvedMetaDatas > 0)
            {
                Debug.LogWarning($"[Soup] '{report.Asset.name}' {report.UnresolvedMetaDatas} node meta data entry(s) were kept as-is because their node's type has no registered meta data type. Check the console for EditorTypesFactory errors; the entries are preserved until a registration is back.", report.Asset);
            }
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
                string name = $"{prefix}{missing.className}  (assembly '{missing.assemblyName}')";
                missingClasses.Add(name);
            }

            List<string> sorted = missingClasses.ToList();
            sorted.Sort((a, b) => string.CompareOrdinal(a, b));
            return sorted;
        }
    }
}
