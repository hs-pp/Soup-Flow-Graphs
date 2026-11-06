using Soup.Editor.State;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.DataRefs
{
    public struct NodeMetaDataRef
    {
        public readonly string NodeId;
        public readonly SerializedProperty MetaDataSP;
        public readonly int Index;

        public bool IsValid => MetaDataSP != null;
        public bool IsMissingType => string.IsNullOrEmpty(NodeId);

        public SerializedProperty PositionSP => MetaDataSP?.FindPropertyRelative(NodeMetaData.PositionFieldName);
        public Vector2 Position => PositionSP?.vector2Value ?? Vector2.zero;

        public SerializedProperty ColorSP => MetaDataSP?.FindPropertyRelative(NodeMetaData.ColorFieldName);
        public Color Color => ColorSP?.colorValue ?? default;
        
        public SerializedProperty CommentSP => MetaDataSP?.FindPropertyRelative(NodeMetaData.CommentFieldName);
        public string Comment => CommentSP?.stringValue;
        
        public NodeMetaDataRef(SerializedProperty metaDataSP, int index)
        {
            NodeId = metaDataSP?.FindPropertyRelative(NodeMetaData.NodeIdFieldName)?.stringValue;
            MetaDataSP = metaDataSP;
            Index = index;
        }
        
        public bool TryGetSubGraphCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData)
        {
            collectionMetaData = new NodeCollectionMetaDataRef(MetaDataSP?.FindPropertyRelative(SubGraphNodeMetaData.SubGraphFieldName));
            return collectionMetaData.IsValid;
        }

        public NodeMetaData GetNodeMetaDataInstance()
        {
            if (!IsValid || MetaDataSP.propertyType != SerializedPropertyType.ManagedReference)
            {
                return null;
            }

            return MetaDataSP.managedReferenceValue as NodeMetaData;
        }
    }
}