using System;

namespace Soup.Editor
{
    /// <summary>
    /// Specify the node name of a NodeView.
    /// Add this to a class inheriting BaseNodeView.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CustomNodeNameAttribute : Attribute
    {
        public string Name { get; }

        public CustomNodeNameAttribute(string name)
        {
            Name = name;
        }
    }
}
