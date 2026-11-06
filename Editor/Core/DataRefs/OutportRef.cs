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
            Id = outportSP.FindPropertyRelative(nameof(OutportEdge.Id))?.stringValue;
            OutportSP = outportSP;
            Index = index;
        }

        public SerializedProperty ConnectedNodeIdSP => OutportSP?.FindPropertyRelative(nameof(OutportEdge.ConnectedNodeId));
        public string ConnectedNodeId => ConnectedNodeIdSP?.stringValue;
    }
}