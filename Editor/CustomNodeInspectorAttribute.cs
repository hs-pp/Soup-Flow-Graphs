using System;

namespace Soup.Editor
{
    /// <summary>
    /// Specify the node inspector type used for a NodeView.
    /// Add this to a class inheriting ANodeInspector to link it to an ANode type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CustomNodeInspectorAttribute : Attribute
    {
        public Type NodeType { get; }

        public CustomNodeInspectorAttribute(Type nodeType)
        {
            NodeType = nodeType;
        }
    }
}
