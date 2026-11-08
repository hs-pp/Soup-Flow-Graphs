using System;

namespace Soup.Runtime
{
    /// <summary>
    /// Specify which soup flow graph types this node is allowed in as well as the create menu directory to show it in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CustomNodeAttribute : Attribute
    {
        public Type GraphType { get; }
        public string MenuPath { get; }
        
        public CustomNodeAttribute(Type graphType, string menuPath)
        {
            MenuPath = menuPath;
            GraphType = graphType;
            if (GraphType == null)
            {
                GraphType = typeof(AFlowGraph);
            }
        }

        public bool AllowsGraphType(Type graphType)
        {
            return GraphType.IsAssignableFrom(graphType);
        }
    }
}
