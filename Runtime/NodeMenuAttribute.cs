using System;

namespace Soup.Runtime
{
    /// <summary>
    /// Exposes a node type in the editor's node creation menu at the given path (e.g. "Flow/Wait").
    /// Node types without this attribute do not appear in the creation menu.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NodeMenuAttribute : Attribute
    {
        public string MenuPath { get; }

        public NodeMenuAttribute(string menuPath)
        {
            MenuPath = menuPath;
        }
    }
}
