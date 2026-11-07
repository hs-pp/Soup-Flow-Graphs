using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// A test flow graph asset with no properties. Maybe I can convert this into an example.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraph", menuName = "Soup/Flow Graph")]
    public sealed class BasicFlowGraph : AFlowGraph<EmptyGraphProperties>
    {
        
    }
}
