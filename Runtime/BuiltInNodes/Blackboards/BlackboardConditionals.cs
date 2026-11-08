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
            string symbol;
            switch (m_comparison)
            {
                case Comparison.DoesNotEqual:
                    symbol = "!=";
                    break;
                case Comparison.LessThan:
                    symbol = "<";
                    break;
                case Comparison.GreaterThan:
                    symbol = ">";
                    break;
                case Comparison.LessThanOrEqualTo:
                    symbol = "<=";
                    break;
                case Comparison.GreaterThanOrEqualTo:
                    symbol = ">=";
                    break;
                default:
                    symbol = "==";
                    break;
            }
            return $"{symbol} {m_value.ToString(CultureInfo.InvariantCulture)}";
        }
    }

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
            string symbol;
            switch (m_comparison)
            {
                case Comparison.DoesNotEqual:
                    symbol = "!=";
                    break;
                case Comparison.LessThan:
                    symbol = "<";
                    break;
                case Comparison.GreaterThan:
                    symbol = ">";
                    break;
                case Comparison.LessThanOrEqualTo:
                    symbol = "<=";
                    break;
                case Comparison.GreaterThanOrEqualTo:
                    symbol = ">=";
                    break;
                default:
                    symbol = "==";
                    break;
            }
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
            bool equal = string.IsNullOrEmpty(value) ? string.IsNullOrEmpty(m_value) : string.Equals(value, m_value, StringComparison.Ordinal);
            return m_comparison == Comparison.Equals ? equal : !equal;
        }

        public override string GetLabel()
        {
            return $"{(m_comparison == Comparison.Equals ? "==" : "!=")} \"{m_value}\"";
        }
    }
}
