using Soup.Runtime;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public readonly struct OutportRef
    {
        public readonly string OutportId;
        public readonly SerializedProperty OutportSP;
        public readonly int Index;

        public SerializedProperty ConnectedNodeIdSP => OutportSP?.FindPropertyRelative(OutportEdge.CONNECTED_NODE_ID_FIELD_NAME);
        public string ConnectedNodeId => ConnectedNodeIdSP?.stringValue;
        
        public OutportRef(SerializedProperty outportSP, int index)
        {
            OutportId = outportSP.FindPropertyRelative(OutportEdge.OUTPORT_ID_FIELD_NAME)?.stringValue;
            OutportSP = outportSP;
            Index = index;
        }
    }
}