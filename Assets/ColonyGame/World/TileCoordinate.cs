using System;

namespace ColonyGame.World
{
    // Logical X/Z axes; presentation chooses physical scale and position.
    public readonly struct TileCoordinate : IEquatable<TileCoordinate>
    {
        public int X { get; }
        public int Z { get; }
        public TileCoordinate(int x, int z) { X = x; Z = z; }
        public bool Equals(TileCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is TileCoordinate other && Equals(other);
        public override int GetHashCode() { unchecked { return (X * 397) ^ Z; } }
        public override string ToString() => $"({X}, {Z})";
    }
}
