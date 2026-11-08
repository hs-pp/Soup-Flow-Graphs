using System;
using Soup.Runtime.Blackboards;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// One condition on a BlackboardConditionalNode, with an outport of its own. Conditionals derive from
    /// ABlackboardConditional&lt;T&gt;, never from this directly, and the editor creates the one whose T matches the picked variable's
    /// value type (one conditional per value type).
    /// </summary>
    [Serializable]
    public abstract class ABlackboardConditional
    {
        public abstract Type ValueType { get; }

        // Only ABlackboardConditional<T> can call this, so every conditional works on exactly one value type.
        private protected ABlackboardConditional()
        {
        }

        /// <summary>
        /// Tests the variable's value. False, with isTrue false, when the variable holds another type.
        /// </summary>
        public abstract bool TryEvaluate(ABlackboardVariable variable, out bool isTrue);

        /// <summary>
        /// The label of this condition's outport, e.g. "&gt; 10".
        /// </summary>
        public abstract string GetLabel();
    }

    [Serializable]
    public abstract class ABlackboardConditional<T> : ABlackboardConditional
    {
        public sealed override Type ValueType => typeof(T);

        public sealed override bool TryEvaluate(ABlackboardVariable variable, out bool isTrue)
        {
            if (variable is not ABlackboardVariable<T> typedVariable)
            {
                isTrue = false;
                return false;
            }

            isTrue = Evaluate(typedVariable.Value);
            return true;
        }

        protected abstract bool Evaluate(T value);
    }
}
