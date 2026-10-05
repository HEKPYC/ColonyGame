using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    // Explicit values preserve the existing TownHall/House identifiers.
    public enum BuildingType
    {
        TownHall = 0, House = 1, BuildersHut = 2, Farm = 3, Tavern = 4,
        Butcher = 5, FishermansHut = 6, GuardTower = 7, Warehouse = 8,
        LumberMill = 9, Mine = 10, Barracks = 11, Laboratory = 12, MageTower = 13
    }

    public readonly struct BuildingFootprint
    {
        public int Width { get; }
        public int Depth { get; }
        internal BuildingFootprint(int width, int depth) { Width = width; Depth = depth; }
    }

    public sealed class BuildingDefinition
    {
        public BuildingType Type { get; }
        public string DisplayName { get; }
        public int BaseWidth { get; }
        public int BaseDepth { get; }
        public int RequiredTownHallLevel { get; }
        public bool IsPlayerPlaceable { get; }

        internal BuildingDefinition(BuildingType type, string name, int width, int depth, int unlockLevel, bool placeable = true)
        {
            Type = type;
            DisplayName = name;
            BaseWidth = width;
            BaseDepth = depth;
            RequiredTownHallLevel = unlockLevel;
            IsPlayerPlaceable = placeable;
        }

        public BuildingFootprint GetFootprint(int orientationDegrees)
        {
            if (!IsValidOrientation(orientationDegrees)) throw new ArgumentOutOfRangeException(nameof(orientationDegrees));
            return orientationDegrees % 180 == 0
                ? new BuildingFootprint(BaseWidth, BaseDepth)
                : new BuildingFootprint(BaseDepth, BaseWidth);
        }

        public static bool IsValidOrientation(int degrees) => degrees >= 0 && degrees < 360 && degrees % 90 == 0;
    }

    public static class BuildingCatalog
    {
        // The sole source of prototype footprint, name, unlock, and placeability data.
        public static IReadOnlyList<BuildingDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new BuildingDefinition(BuildingType.TownHall, "Town Hall", 5, 5, 0, false),
            new BuildingDefinition(BuildingType.BuildersHut, "Builder's Hut", 3, 3, 1),
            new BuildingDefinition(BuildingType.House, "House", 3, 3, 1),
            new BuildingDefinition(BuildingType.Farm, "Farm", 3, 4, 1),
            new BuildingDefinition(BuildingType.Tavern, "Tavern", 4, 5, 2),
            new BuildingDefinition(BuildingType.Butcher, "Butcher", 3, 6, 2),
            new BuildingDefinition(BuildingType.FishermansHut, "Fisherman's Hut", 3, 5, 2),
            new BuildingDefinition(BuildingType.GuardTower, "Guard Tower", 2, 2, 2),
            new BuildingDefinition(BuildingType.Warehouse, "Warehouse", 4, 4, 3),
            new BuildingDefinition(BuildingType.LumberMill, "Lumber Mill", 5, 5, 3),
            new BuildingDefinition(BuildingType.Mine, "Mine", 4, 4, 3),
            new BuildingDefinition(BuildingType.Barracks, "Barracks", 3, 5, 3),
            new BuildingDefinition(BuildingType.Laboratory, "Laboratory", 6, 6, 4),
            new BuildingDefinition(BuildingType.MageTower, "Mage Tower + Place of Power", 5, 5, 4)
        });
        private static readonly Dictionary<BuildingType, BuildingDefinition> byType = All.ToDictionary(d => d.Type);
        public static BuildingDefinition Get(BuildingType type) => byType[type];
        public static bool TryGet(BuildingType type, out BuildingDefinition definition) => byType.TryGetValue(type, out definition);
        public static IEnumerable<BuildingDefinition> AvailableAt(int townHallLevel) =>
            All.Where(d => d.IsPlayerPlaceable && d.RequiredTownHallLevel <= townHallLevel);
    }
}
