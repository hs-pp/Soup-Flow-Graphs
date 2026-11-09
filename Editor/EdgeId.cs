using System;

namespace Soup.Editor
{
    /// <summary>
    /// Identifies an edge by the node it leaves and its outport.
    /// </summary>
    public readonly struct EdgeId : IEquatable<EdgeId>
    {
        public readonly string FromNodeId;
        public readonly string OutportId;
        public string Key => $"{FromNodeId}#{OutportId}";

        public EdgeId(string fromNodeId, string outportId)
        {
            FromNodeId = fromNodeId;
            OutportId = outportId;
        }

        public bool Equals(EdgeId other) => FromNodeId == other.FromNodeId && OutportId == other.OutportId;
        public override bool Equals(object obj) => obj is EdgeId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FromNodeId, OutportId);
    }
}