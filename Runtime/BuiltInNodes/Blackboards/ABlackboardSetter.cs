using System;
using Soup.Runtime.Blackboards;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// A blackboard variable setter base class.
    /// </summary>
    [Serializable]
    public abstract class ABlackboardSetter
    {
        public abstract Type ValueType { get; }

        private protected ABlackboardSetter() { }

        /// <summary>
        /// Tries to set the variable.
        /// </summary>
        public abstract bool TryApply(ABlackboardVariable variable);

        /// <summary>
        /// For displaying in editor.
        /// </summary>
        public abstract string GetLabel(string variableName);
    }

    /// <summary>
    /// Implement this class for custom set logic for blackboard variable types.
    /// </summary>
    [Serializable]
    public abstract class ABlackboardSetter<T> : ABlackboardSetter
    {
        public sealed override Type ValueType => typeof(T);

        public sealed override bool TryApply(ABlackboardVariable variable)
        {
            if (variable is not ABlackboardVariable<T> typedVariable)
            {
                return false;
            }

            typedVariable.Value = Evaluate(typedVariable.Value);
            return true;
        }

        /// <summary>
        /// Returns the variable's new value.
        /// </summary>
        protected abstract T Evaluate(T currentValue);
    }
}
