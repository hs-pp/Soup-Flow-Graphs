using System;
using System.Collections.Generic;
using Soup.Runtime;
using Soup.Runtime.BuiltInNodes;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public struct NodeRef
    {
        public const int ENTRY_NODE_INDEX = -1;

        public readonly string Id;
        public readonly SerializedProperty NodeSP;
        public readonly int Index;

        public NodeRef(SerializedProperty nodeSP, int index)
        {
            Id = nodeSP?.FindPropertyRelative(ANode.ID_FIELD_NAME)?.stringValue;
            NodeSP = nodeSP;
            Index = index;
        }

        public bool IsEntryNode => Index == ENTRY_NODE_INDEX;
        public bool IsMissingType => string.IsNullOrEmpty(Id);
        public bool IsValid => NodeSP != null;

        public SerializedProperty OutportsSP => NodeSP?.FindPropertyRelative(ANode.OUTPORTS_FIELD_NAME);

        public bool TryGetSubGraphCollection(out NodeCollectionRef collection)
        {
            collection = new NodeCollectionRef(NodeSP?.FindPropertyRelative(SubGraphNode.SubGraphFieldName));
            return collection.IsValid;
        }

        public Type GetNodeType()
        {
            return GetNodeInstance()?.GetType();
        }

        public ANode GetNodeInstance()
        {
            if (!IsValid || NodeSP.propertyType != SerializedPropertyType.ManagedReference)
            {
                return null;
            }

            return NodeSP.managedReferenceValue as ANode;
        }

        public IEnumerable<OutportRef> EnumerateOutports()
        {
            SerializedProperty outports = OutportsSP;
            if (outports == null)
            {
                yield break;
            }

            for (int i = 0; i < outports.arraySize; i++)
            {
                SerializedProperty outportSP = outports.GetArrayElementAtIndex(i);
                yield return new OutportRef(outportSP, i);
            }
        }

        public bool TryGetOutport(string outportId, out OutportRef outportRef)
        {
            outportRef = new OutportRef();
            if (string.IsNullOrEmpty(outportId))
            {
                return false;
            }

            foreach (OutportRef outport in EnumerateOutports())
            {
                if (outport.Id == outportId)
                {
                    outportRef = outport;
                    return true;
                }
            }

            return false;
        }
    }
}