namespace Soup.Runtime
{
    /// <summary>
    /// Base type for mutable per-node execution data created fresh for each graph run.
    /// </summary>
    public abstract class NodeRuntimeState
    {
    }

    public sealed class EmptyNodeRuntimeState : NodeRuntimeState
    {
    }
}
