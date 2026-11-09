using System;

namespace Soup.Editor
{
    /// <summary>
    /// Specify a custom NodeView class for an ANode type.
    /// Add this to a class inheriting BaseNodeView to link it to an ANode type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CustomNodeViewAttribute : Attribute
    {
        public Type NodeType { get; }

        public CustomNodeViewAttribute(Type nodeType)
        {
            NodeType = nodeType;
        }
    }
}
