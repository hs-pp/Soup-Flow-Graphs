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

        public SerializedProperty TitleSP => GroupSP?.FindPropertyRelative(GroupMetaData.TITLE_FIELD_NAME);
        public SerializedProperty RectSP => GroupSP?.FindPropertyRelative(GroupMetaData.RECT_FIELD_NAME);
        public SerializedProperty ColorSP => GroupSP?.FindPropertyRelative(GroupMetaData.COLOR_FIELD_NAME);

        public string Title => TitleSP?.stringValue;
        public Rect Rect => RectSP?.rectValue ?? default;
        public Color Color => ColorSP?.colorValue ?? default;
        
        public GroupMetaDataRef(SerializedProperty groupSP, int index)
        {
            GroupId = groupSP?.FindPropertyRelative(GroupMetaData.GROUP_ID_FIELD_NAME)?.stringValue;
            GroupSP = groupSP;
            Index = index;
        }
    }
}
