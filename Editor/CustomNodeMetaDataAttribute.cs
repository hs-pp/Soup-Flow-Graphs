using System;

namespace Soup.Editor
{
    /// <summary>
    /// For more advanced nodes we may need to support custom meta data for editor purposes that we do not want stored alongside the node data.
    /// Add this to a class inheriting NodeMetaData to link it to an ANode type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CustomNodeMetaDataAttribute : Attribute
    {
        public Type NodeType { get; }

        public CustomNodeMetaDataAttribute(Type nodeType)
        {
            NodeType = nodeType;
        }
    }
}
