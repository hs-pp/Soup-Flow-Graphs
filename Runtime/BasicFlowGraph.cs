using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// A plain, ready-to-use flow graph asset with no properties. Projects that need graph properties define their own
    /// AFlowGraph&lt;TProperties&gt; subclass.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraph", menuName = "Soup/Flow Graph")]
    public sealed class BasicFlowGraph : AFlowGraph<EmptyGraphProperties>
    {
    }
}
