using System;
using Soup.Runtime.Blackboards;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// One condition on a BlackboardConditionalNode.
    /// </summary>
    [Serializable]
    public abstract class ABlackboardConditional
    {
        public abstract Type ValueType { get; }

        private protected ABlackboardConditional() { }

        /// <summary>
        /// Returns whether the evaluation succeeded. result is the result of the evaluation.
        /// </summary>
        public abstract bool TryEvaluate(ABlackboardVariable variable, out bool result);

        /// <summary>
        /// For displaying in editor.
        /// </summary>
        public abstract string GetLabel();
    }

    [Serializable]
    public abstract class ABlackboardConditional<T> : ABlackboardConditional
    {
        public sealed override Type ValueType => typeof(T);

        public sealed override bool TryEvaluate(ABlackboardVariable variable, out bool result)
        {
            if (variable is not ABlackboardVariable<T> typedVariable)
            {
                result = false;
                return false;
            }

            result = Evaluate(typedVariable.Value);
            return true;
        }

        protected abstract bool Evaluate(T value);
    }
}
