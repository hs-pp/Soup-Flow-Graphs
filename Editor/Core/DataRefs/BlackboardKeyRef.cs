using Soup.Runtime.Blackboards;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public readonly struct BlackboardKeyRef
    {
        public readonly SerializedProperty KeySP;
        public readonly SerializedProperty VariableIdSP;

        public bool IsValid => VariableIdSP != null;
        public string VariableId => VariableIdSP?.stringValue;

        public BlackboardKeyRef(SerializedProperty keySP)
        {
            KeySP = keySP;
            VariableIdSP = KeySP?.FindPropertyRelative(BlackboardKey.VARIABLE_ID_FIELD_NAME);
        }
    }
}
