using Soup.Runtime;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public struct OutportRef
    {
        public readonly string Id;
        public readonly SerializedProperty OutportSP;
        public readonly int Index;

        public OutportRef(SerializedProperty outportSP, int index)
        {
            Id = outportSP.FindPropertyRelative(OutportEdge.OUTPORT_ID_FIELD_NAME)?.stringValue;
            OutportSP = outportSP;
            Index = index;
        }

        public SerializedProperty ConnectedNodeIdSP => OutportSP?.FindPropertyRelative(OutportEdge.CONNECTED_NODE_ID_FIELD_NAME);
        public string ConnectedNodeId => ConnectedNodeIdSP?.stringValue;
    }
}