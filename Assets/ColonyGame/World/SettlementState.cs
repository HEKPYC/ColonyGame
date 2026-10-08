using System;
using System.Collections.Generic;
using System.Linq;

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
        Valid, OutsideTerritory, Occupied, Locked, NotPlayerPlaceable, UnknownType, InvalidOrientation, InsufficientResources, ConstructionDeferred, AccessBlocked
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
        public ConstructionStage ConstructionStage { get; internal set; }
        public bool IsCompleted => ConstructionStage == ConstructionStage.Completed || IsUpgrading;
        public int Level { get; internal set; } = 1;
        public bool ManualVacancy { get; internal set; }
        public bool IsUpgrading { get; internal set; }
        public int TargetLevel { get; internal set; } = 1;
        internal readonly Dictionary<ResourceType,int> investment = new Dictionary<ResourceType,int>();
        public IReadOnlyDictionary<ResourceType,int> Investment => new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceType,int>(investment);
        internal void Invest(IReadOnlyDictionary<ResourceType,int> cost)
        {
            foreach(var item in cost) investment[item.Key]=investment.TryGetValue(item.Key,out int value)?checked(value+item.Value):item.Value;
        }
        public double ConstructionWork { get; internal set; }
        public double RequiredConstructionWork { get; internal set; }
        public double ConstructionProgress => ConstructionStage == ConstructionStage.Completed ? 1 : RequiredConstructionWork <= 0 ? 0 : ConstructionWork / RequiredConstructionWork;
        public bool IsStartingBuilding { get; }
        public bool TerritoryInfluenceApplied { get; private set; }
        public BuildingEntrance Entrance => BuildingEntrance.For(Definition,Origin,OrientationDegrees);
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
            ConstructionStage = ConstructionStage.Completed;
            IsStartingBuilding = starting;
        }
        internal void MarkInfluenceApplied() { TerritoryInfluenceApplied = true; }
    }

    public sealed class SettlementState
    {
        private readonly List<BuildingState> buildings = new List<BuildingState>();
        private readonly HashSet<TileCoordinate> occupied = new HashSet<TileCoordinate>();
        internal CitizenSimulation InitialCitizens;
        public SettlementTerritory Territory { get; }
        public BuildingState TownHall { get; }
        public int TownHallLevel => TownHall.Level;
        public IReadOnlyList<BuildingState> Buildings { get; }
        public IEnumerable<BuildingDefinition> AvailableBuildings => BuildingCatalog.AvailableAt(TownHallLevel);
        private SettlementState(SettlementTerritory territory, BuildingState townHall, int townHallLevel)
        {
            Territory = territory;
            TownHall = townHall;
            TownHall.Level = TownHall.TargetLevel = townHallLevel;
            Buildings = buildings.AsReadOnly();
            AddBuilding(townHall);
        }
        public bool IsOccupied(TileCoordinate tile) => occupied.Contains(tile);
        internal BuildingState AddConstructionSite(BuildingType type, TileCoordinate origin, int orientation, double requiredWork)
        {
            var site = new BuildingState(Guid.NewGuid().ToString("N"), BuildingCatalog.Get(type), origin, orientation)
            { ConstructionStage = ConstructionStage.UnderConstruction, RequiredConstructionWork = requiredWork };
            AddBuilding(site);
            return site;
        }

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

        public IEnumerable<TileCoordinate> ExteriorAccessTiles(BuildingState building)=>building.OccupiedTiles
            .SelectMany(CitizenNavigation.Neighbors).Distinct().Where(t=>Territory.IsClaimed(t)&&!IsOccupied(t))
            .OrderBy(t=>t.Equals(building.Entrance.ExteriorTile)?0:1).ThenBy(t=>t.Z).ThenBy(t=>t.X);

        // Runtime placement preserves one connected outdoor network serving every building
        // and an actual Wilderness boundary. Interiors remain solid; fixtures retain geometric placement.
        public BuildingPlacementValidity ValidateAccessiblePlacement(BuildingType type,TileCoordinate origin,int orientationDegrees=0)
        {
            var validity=ValidateBuildingPlacement(type,origin,orientationDegrees);
            if(validity!=BuildingPlacementValidity.Valid) return validity;
            var proposed=new BuildingState("placement-check",BuildingCatalog.Get(type),origin,orientationDegrees);
            var all=buildings.Concat(new[]{proposed}).ToArray();
            bool Walk(CitizenPosition p)=>PerimeterNavigation.CanOccupy(p,Territory,all);
            foreach(var access in PerimeterNavigation.AccessPoints(TownHall))
            {
                var network=PerimeterNavigation.Reachable(access,Walk);
                if(network.Count==0) continue;
                bool wilderness=Territory.ClaimedTiles.Any(t=>CitizenNavigation.Neighbors(t).Where(n=>!Territory.IsClaimed(n)).Any(n=>
                {
                    var edge=new CitizenPosition(t.X+.5+(n.X-t.X)*.5,t.Z+.5+(n.Z-t.Z)*.5);
                    return Walk(edge)&&PerimeterNavigation.Contains(network,edge)||
                        Walk(CitizenPosition.Center(t))&&PerimeterNavigation.Contains(network,CitizenPosition.Center(t));
                }));
                if(wilderness&&all.All(b=>PerimeterNavigation.AccessPoints(b).Any(p=>Walk(p)&&PerimeterNavigation.Contains(network,p))))
                    return BuildingPlacementValidity.Valid;
                break;
            }
            return BuildingPlacementValidity.AccessBlocked;
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
        internal bool RemoveBuilding(BuildingState building)
        {
            if(building==TownHall||!buildings.Remove(building)) return false;
            occupied.Clear();
            foreach(var remaining in buildings) foreach(var tile in remaining.OccupiedTiles) occupied.Add(tile);
            return true;
        }
        private void AddBuilding(BuildingState building)
        {
            buildings.Add(building);
            foreach (var tile in building.OccupiedTiles) occupied.Add(tile);
        }
        // Explicit higher levels are available for focused domain fixtures.
        // Runtime starts at Town Hall Level 1.
        public static SettlementState CreatePrototype(int townHallLevel = 1)
        {
            if (townHallLevel < 1 || townHallLevel > 5) throw new ArgumentOutOfRangeException(nameof(townHallLevel));
            var tiles = new List<TileCoordinate>();
            for (int z = 0; z < 20; z++)
                for (int x = 0; x < 20; x++) tiles.Add(new TileCoordinate(x, z));
            return new SettlementState(new SettlementTerritory(tiles),
                new BuildingState("starting-town-hall", BuildingCatalog.Get(BuildingType.TownHall), new TileCoordinate(7, 7), starting: true), townHallLevel);
        }
    }
}
