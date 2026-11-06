using System;

namespace Soup.Editor
{
    /// <summary>
    /// Definition of an edge aka a connection between an outport and an inport.
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