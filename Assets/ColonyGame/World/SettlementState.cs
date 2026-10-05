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
        internal bool Claim(TileCoordinate tile) => claimed.Add(tile);
        public IEnumerable<TileCoordinate> ClaimedTiles
        {
            get { foreach (var tile in claimed) yield return tile; }
        }
    }

    public enum BuildingType { TownHall, House }
    public enum HousePlacementValidity { Valid, OutsideTerritory, Occupied }

    public sealed class BuildingState
    {
        public string Id { get; }
        public BuildingType Type { get; }
        public TileCoordinate Origin { get; }
        public int Width { get; }
        public int Depth { get; }
        public int OrientationDegrees { get; }
        public bool IsCompleted { get; }
        public bool IsStartingBuilding { get; }
        public bool TerritoryInfluenceApplied { get; private set; }
        public IEnumerable<TileCoordinate> OccupiedTiles
        {
            get
            {
                for (int z = 0; z < Depth; z++)
                    for (int x = 0; x < Width; x++)
                        yield return new TileCoordinate(Origin.X + x, Origin.Z + z);
            }
        }
        internal BuildingState(string id, BuildingType type, TileCoordinate origin, bool starting = false, int orientationDegrees = 0)
        {
            if (orientationDegrees < 0 || orientationDegrees >= 360 || orientationDegrees % 90 != 0)
                throw new ArgumentOutOfRangeException(nameof(orientationDegrees));
            Id = id;
            Type = type;
            Origin = origin;
            Width = type == BuildingType.TownHall ? 5 : 3;
            Depth = Width;
            OrientationDegrees = orientationDegrees;
            IsCompleted = true;
            IsStartingBuilding = starting;
        }
        internal void MarkInfluenceApplied() { TerritoryInfluenceApplied = true; }
    }

    public sealed class SettlementState
    {
        private readonly List<BuildingState> buildings = new List<BuildingState>();
        private readonly HashSet<TileCoordinate> occupied = new HashSet<TileCoordinate>();
        public SettlementTerritory Territory { get; }
        public BuildingState TownHall { get; }
        public IReadOnlyList<BuildingState> Buildings { get; }
        private SettlementState(SettlementTerritory territory, BuildingState townHall)
        {
            Territory = territory;
            TownHall = townHall;
            Buildings = buildings.AsReadOnly();
            AddBuilding(townHall);
        }
        public bool IsOccupied(TileCoordinate tile) => occupied.Contains(tile);

        public HousePlacementValidity ValidateHousePlacement(TileCoordinate origin)
        {
            // Avoid coordinate overflow without introducing a world-size/gameplay limit.
            if ((long)origin.X - 3 < int.MinValue || (long)origin.Z - 3 < int.MinValue ||
                (long)origin.X + 5 >= int.MaxValue || (long)origin.Z + 5 >= int.MaxValue)
                return HousePlacementValidity.OutsideTerritory;
            bool overlap = false;
            for (int z = 0; z < 3; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    var tile = new TileCoordinate(origin.X + x, origin.Z + z);
                    if (!Territory.IsClaimed(tile)) return HousePlacementValidity.OutsideTerritory;
                    overlap |= occupied.Contains(tile);
                }
            }
            return overlap ? HousePlacementValidity.Occupied : HousePlacementValidity.Valid;
        }

        public bool TryPlacePrototypeHouse(TileCoordinate origin, out BuildingState house)
        {
            house = null;
            if (ValidateHousePlacement(origin) != HousePlacementValidity.Valid) return false;
            house = new BuildingState(Guid.NewGuid().ToString("N"), BuildingType.House, origin);
            AddBuilding(house);
            ApplyBuildingInfluence(house);
            return true;
        }

        public int ApplyBuildingInfluence(BuildingState building)
        {
            if (building == null || !buildings.Contains(building))
                throw new ArgumentException("Building must belong to this settlement.", nameof(building));
            if (!building.IsCompleted || building.IsStartingBuilding || building.TerritoryInfluenceApplied) return 0;
            int newlyClaimed = 0;
            for (int z = building.Origin.Z - 3; z <= building.Origin.Z + building.Depth + 2; z++)
                for (int x = building.Origin.X - 3; x <= building.Origin.X + building.Width + 2; x++)
                    if (Territory.Claim(new TileCoordinate(x, z))) newlyClaimed++;
            building.MarkInfluenceApplied();
            return newlyClaimed;
        }
        private void AddBuilding(BuildingState building)
        {
            buildings.Add(building);
            foreach (var tile in building.OccupiedTiles) occupied.Add(tile);
        }
        public static SettlementState CreatePrototype()
        {
            var tiles = new List<TileCoordinate>();
            for (int z = 0; z < 20; z++)
                for (int x = 0; x < 20; x++) tiles.Add(new TileCoordinate(x, z));
            return new SettlementState(new SettlementTerritory(tiles),
                new BuildingState("starting-town-hall", BuildingType.TownHall, new TileCoordinate(7, 7), true));
        }
    }
}
