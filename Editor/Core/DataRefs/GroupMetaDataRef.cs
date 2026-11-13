using Soup.Editor.State;
using UnityEditor;
using UnityEngine;

namespace Soup.Editor.Core.DataRefs
{
    public readonly struct GroupMetaDataRef
    {
        public readonly string GroupId;
        public readonly SerializedProperty GroupSP;
        public readonly int Index;

        public bool IsValid => GroupSP != null;

        public SerializedProperty TitleSP => GroupSP?.FindPropertyRelative(GroupMetaData.TitleFieldName);
        public SerializedProperty RectSP => GroupSP?.FindPropertyRelative(GroupMetaData.RectFieldName);
        public SerializedProperty ColorSP => GroupSP?.FindPropertyRelative(GroupMetaData.ColorFieldName);

        public string Title => TitleSP?.stringValue;
        public Rect Rect => RectSP?.rectValue ?? default;
        public Color Color => ColorSP?.colorValue ?? default;
        
        public GroupMetaDataRef(SerializedProperty groupSP, int index)
        {
            GroupId = groupSP?.FindPropertyRelative(GroupMetaData.GroupIdFieldName)?.stringValue;
            GroupSP = groupSP;
            Index = index;
        }
    }
}
