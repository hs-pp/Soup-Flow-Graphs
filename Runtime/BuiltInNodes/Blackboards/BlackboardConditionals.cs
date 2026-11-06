using System;
using System.Globalization;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    [Serializable]
    public sealed class BoolBlackboardConditional : ABlackboardConditional<bool>
    {
        public enum Comparison
        {
            Equals,
            DoesNotEqual,
        }

        [SerializeField]
        private Comparison m_comparison = Comparison.Equals;

        [SerializeField]
        private bool m_value = true;

        protected override bool Evaluate(bool value)
        {
            return m_comparison == Comparison.Equals ? value == m_value : value != m_value;
        }

        public override string GetLabel()
        {
            return $"{(m_comparison == Comparison.Equals ? "==" : "!=")} {(m_value ? "true" : "false")}";
        }
    }

    [Serializable]
    public sealed class IntBlackboardConditional : ABlackboardConditional<int>
    {
        public enum Comparison
        {
            Equals,
            DoesNotEqual,
            LessThan,
            GreaterThan,
            LessThanOrEqualTo,
            GreaterThanOrEqualTo,
        }

        [SerializeField]
        private Comparison m_comparison = Comparison.Equals;

        [SerializeField]
        private int m_value;

        protected override bool Evaluate(int value)
        {
            switch (m_comparison)
            {
                case Comparison.Equals:
                    return value == m_value;
                case Comparison.DoesNotEqual:
                    return value != m_value;
                case Comparison.LessThan:
                    return value < m_value;
                case Comparison.GreaterThan:
                    return value > m_value;
                case Comparison.LessThanOrEqualTo:
                    return value <= m_value;
                case Comparison.GreaterThanOrEqualTo:
                    return value >= m_value;
                default:
                    return false;
            }
        }

        public override string GetLabel()
        {
            string symbol = m_comparison switch
            {
                Comparison.DoesNotEqual => "!=",
                Comparison.LessThan => "<",
                Comparison.GreaterThan => ">",
                Comparison.LessThanOrEqualTo => "<=",
                Comparison.GreaterThanOrEqualTo => ">=",
                _ => "==",
            };
            return $"{symbol} {m_value.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>
    /// Equality is approximate (Mathf.Approximately), and the other comparisons agree with it: a value approximately equal to the
    /// compared one is never less or greater than it.
    /// </summary>
    [Serializable]
    public sealed class FloatBlackboardConditional : ABlackboardConditional<float>
    {
        public enum Comparison
        {
            Equals,
            DoesNotEqual,
            LessThan,
            GreaterThan,
            LessThanOrEqualTo,
            GreaterThanOrEqualTo,
        }

        [SerializeField]
        private Comparison m_comparison = Comparison.Equals;

        [SerializeField]
        private float m_value;

        protected override bool Evaluate(float value)
        {
            bool approximatelyEqual = Mathf.Approximately(value, m_value);
            switch (m_comparison)
            {
                case Comparison.Equals:
                    return approximatelyEqual;
                case Comparison.DoesNotEqual:
                    return !approximatelyEqual;
                case Comparison.LessThan:
                    return value < m_value && !approximatelyEqual;
                case Comparison.GreaterThan:
                    return value > m_value && !approximatelyEqual;
                case Comparison.LessThanOrEqualTo:
                    return value < m_value || approximatelyEqual;
                case Comparison.GreaterThanOrEqualTo:
                    return value > m_value || approximatelyEqual;
                default:
                    return false;
            }
        }

        public override string GetLabel()
        {
            string symbol = m_comparison switch
            {
                Comparison.DoesNotEqual => "!=",
                Comparison.LessThan => "<",
                Comparison.GreaterThan => ">",
                Comparison.LessThanOrEqualTo => "<=",
                Comparison.GreaterThanOrEqualTo => ">=",
                _ => "==",
            };
            return $"{symbol} {m_value.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    [Serializable]
    public sealed class StringBlackboardConditional : ABlackboardConditional<string>
    {
        public enum Comparison
        {
            Equals,
            DoesNotEqual,
        }

        [SerializeField]
        private Comparison m_comparison = Comparison.Equals;

        [SerializeField]
        private string m_value = string.Empty;

        protected override bool Evaluate(string value)
        {
            // Unity serializes a null string as an empty one, so the two are treated as equal.
            bool equal = string.Equals(value ?? string.Empty, m_value ?? string.Empty, StringComparison.Ordinal);
            return m_comparison == Comparison.Equals ? equal : !equal;
        }

        public override string GetLabel()
        {
            return $"{(m_comparison == Comparison.Equals ? "==" : "!=")} \"{m_value}\"";
        }
    }
}
