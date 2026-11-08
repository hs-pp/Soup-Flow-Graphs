namespace Soup.Runtime
{
    /// <summary>
    /// Base type for mutable per-node execution data created fresh for each node.
    /// Create a custom class of this and give it to your ANode custom class to get access to mutable data at runtime.
    /// You can find this data in the NodeExecutionContext in the ANode lifecycle functions. 
    /// </summary>
    public abstract class NodeRuntimeState
    {
    }
    
    public sealed class EmptyNodeRuntimeState : NodeRuntimeState { }
}
