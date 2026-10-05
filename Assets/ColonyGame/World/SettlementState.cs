using System;
using System.Collections.Generic;

namespace ColonyGame.World
{
    public sealed class SettlementTerritory
    {
        private readonly HashSet<TileCoordinate> claimed;
        public int Count => claimed.Count;
        public SettlementTerritory(IEnumerable<TileCoordinate> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            claimed = new HashSet<TileCoordinate>(tiles);
        }
        public bool IsClaimed(TileCoordinate tile) => claimed.Contains(tile);
        public IEnumerable<TileCoordinate> ClaimedTiles
        {
            get { foreach (var tile in claimed) yield return tile; }
        }
    }

    public enum BuildingType { TownHall }

    public sealed class BuildingState
    {
        public string Id { get; }
        public BuildingType Type { get; }
        public TileCoordinate Origin { get; }
        public int Width { get; }
        public int Depth { get; }
        public int OrientationDegrees { get; }
        public bool IsCompleted { get; }
        public IEnumerable<TileCoordinate> OccupiedTiles
        {
            get
            {
                for (int z = 0; z < Depth; z++)
                    for (int x = 0; x < Width; x++)
                        yield return new TileCoordinate(Origin.X + x, Origin.Z + z);
            }
        }
        // This slice creates only the already-completed, unrotated starting Town Hall.
        internal BuildingState(string id, TileCoordinate origin)
        {
            Id = id;
            Type = BuildingType.TownHall;
            Origin = origin;
            Width = 5;
            Depth = 5;
            OrientationDegrees = 0;
            IsCompleted = true;
        }
    }

    public sealed class SettlementState
    {
        public SettlementTerritory Territory { get; }
        public BuildingState TownHall { get; }
        private SettlementState(SettlementTerritory territory, BuildingState townHall)
        { Territory = territory; TownHall = townHall; }

        public static SettlementState CreatePrototype()
        {
            var tiles = new List<TileCoordinate>();
            for (int z = 0; z < 20; z++)
                for (int x = 0; x < 20; x++) tiles.Add(new TileCoordinate(x, z));
            // Starting buildings have no territory expansion side effect.
            return new SettlementState(new SettlementTerritory(tiles),
                new BuildingState("starting-town-hall", new TileCoordinate(7, 7)));
        }
    }
}
