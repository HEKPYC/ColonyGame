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

    public enum BuildingPlacementValidity
    {
        Valid, OutsideTerritory, Occupied, Locked, NotPlayerPlaceable, UnknownType, InvalidOrientation
    }

    public sealed class BuildingState
    {
        internal const int InfluenceMargin = 3;
        public string Id { get; }
        public BuildingDefinition Definition { get; }
        public BuildingType Type => Definition.Type;
        public TileCoordinate Origin { get; }
        public int Width { get; }
        public int Depth { get; }
        public int OrientationDegrees { get; }
        public bool IsCompleted { get; }
        public bool IsStartingBuilding { get; }
        public bool TerritoryInfluenceApplied { get; private set; }
        public IEnumerable<TileCoordinate> OccupiedTiles => TilesInArea(0);
        public IEnumerable<TileCoordinate> InfluenceTiles => TilesInArea(InfluenceMargin);

        private IEnumerable<TileCoordinate> TilesInArea(int margin)
        {
            for (int z = -margin; z < Depth + margin; z++)
                for (int x = -margin; x < Width + margin; x++)
                    yield return new TileCoordinate(Origin.X + x, Origin.Z + z);
        }

        internal BuildingState(string id, BuildingDefinition definition, TileCoordinate origin, int orientationDegrees = 0, bool starting = false)
        {
            var footprint = definition.GetFootprint(orientationDegrees);
            Id = id;
            Definition = definition;
            Origin = origin;
            Width = footprint.Width;
            Depth = footprint.Depth;
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
        public int TownHallLevel { get; }
        public IReadOnlyList<BuildingState> Buildings { get; }
        public IEnumerable<BuildingDefinition> AvailableBuildings => BuildingCatalog.AvailableAt(TownHallLevel);
        private SettlementState(SettlementTerritory territory, BuildingState townHall, int townHallLevel)
        {
            Territory = territory;
            TownHall = townHall;
            TownHallLevel = townHallLevel;
            Buildings = buildings.AsReadOnly();
            AddBuilding(townHall);
        }
        public bool IsOccupied(TileCoordinate tile) => occupied.Contains(tile);

        public BuildingPlacementValidity ValidateBuildingPlacement(BuildingType type, TileCoordinate origin, int orientationDegrees = 0)
        {
            if (!BuildingCatalog.TryGet(type, out var definition)) return BuildingPlacementValidity.UnknownType;
            if (!definition.IsPlayerPlaceable) return BuildingPlacementValidity.NotPlayerPlaceable;
            if (definition.RequiredTownHallLevel > TownHallLevel) return BuildingPlacementValidity.Locked;
            if (!BuildingDefinition.IsValidOrientation(orientationDegrees)) return BuildingPlacementValidity.InvalidOrientation;
            var footprint = definition.GetFootprint(orientationDegrees);
            // Protect coordinate arithmetic for both footprint and subsequent influence.
            int margin = BuildingState.InfluenceMargin;
            if ((long)origin.X - margin < int.MinValue || (long)origin.Z - margin < int.MinValue ||
                (long)origin.X + footprint.Width + margin - 1 > int.MaxValue ||
                (long)origin.Z + footprint.Depth + margin - 1 > int.MaxValue)
                return BuildingPlacementValidity.OutsideTerritory;
            bool overlap = false;
            for (int z = 0; z < footprint.Depth; z++)
            {
                for (int x = 0; x < footprint.Width; x++)
                {
                    var tile = new TileCoordinate(origin.X + x, origin.Z + z);
                    if (!Territory.IsClaimed(tile)) return BuildingPlacementValidity.OutsideTerritory;
                    overlap |= occupied.Contains(tile);
                }
            }
            return overlap ? BuildingPlacementValidity.Occupied : BuildingPlacementValidity.Valid;
        }

        public bool TryPlacePrototypeBuilding(BuildingType type, TileCoordinate origin, int orientationDegrees, out BuildingState building)
        {
            building = null;
            if (ValidateBuildingPlacement(type, origin, orientationDegrees) != BuildingPlacementValidity.Valid) return false;
            building = new BuildingState(Guid.NewGuid().ToString("N"), BuildingCatalog.Get(type), origin, orientationDegrees);
            AddBuilding(building);
            ApplyBuildingInfluence(building);
            return true;
        }

        public int ApplyBuildingInfluence(BuildingState building)
        {
            if (building == null || !buildings.Contains(building))
                throw new ArgumentException("Building must belong to this settlement.", nameof(building));
            if (!building.IsCompleted || building.IsStartingBuilding || building.TerritoryInfluenceApplied) return 0;
            int newlyClaimed = 0;
            foreach (var tile in building.InfluenceTiles)
                if (Territory.Claim(tile)) newlyClaimed++;
            building.MarkInfluenceApplied();
            return newlyClaimed;
        }
        private void AddBuilding(BuildingState building)
        {
            buildings.Add(building);
            foreach (var tile in building.OccupiedTiles) occupied.Add(tile);
        }
        // Level 4 is a testing override, not the intended final new-game progression.
        // Lower values initialize test settlements; there is no upgrade operation.
        public static SettlementState CreatePrototype(int townHallLevel = 4)
        {
            if (townHallLevel < 1 || townHallLevel > 4) throw new ArgumentOutOfRangeException(nameof(townHallLevel));
            var tiles = new List<TileCoordinate>();
            for (int z = 0; z < 20; z++)
                for (int x = 0; x < 20; x++) tiles.Add(new TileCoordinate(x, z));
            return new SettlementState(new SettlementTerritory(tiles),
                new BuildingState("starting-town-hall", BuildingCatalog.Get(BuildingType.TownHall), new TileCoordinate(7, 7), starting: true), townHallLevel);
        }
    }
}
