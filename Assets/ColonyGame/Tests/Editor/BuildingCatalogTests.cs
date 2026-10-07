using System;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class BuildingCatalogTests
    {
        [Test]
        public void CatalogHasEveryApprovedTypeExactlyOnceWithApprovedData()
        {
            // These expected values are the approved design, independent of the catalog.
            var expected = new[]
            {
                new { Type = BuildingType.TownHall, Name = "Town Hall", W = 5, D = 5, Level = 0 },
                new { Type = BuildingType.BuildersHut, Name = "Builder's Hut", W = 3, D = 3, Level = 1 },
                new { Type = BuildingType.House, Name = "House", W = 3, D = 3, Level = 1 },
                new { Type = BuildingType.Farm, Name = "Farm", W = 3, D = 4, Level = 1 },
                new { Type = BuildingType.Tavern, Name = "Tavern", W = 4, D = 5, Level = 2 },
                new { Type = BuildingType.Butcher, Name = "Butcher", W = 3, D = 6, Level = 2 },
                new { Type = BuildingType.FishermansHut, Name = "Fisherman's Hut", W = 3, D = 5, Level = 2 },
                new { Type = BuildingType.GuardTower, Name = "Guard Tower", W = 2, D = 2, Level = 2 },
                new { Type = BuildingType.Warehouse, Name = "Warehouse", W = 4, D = 4, Level = 3 },
                new { Type = BuildingType.LumberMill, Name = "Lumber Mill", W = 5, D = 5, Level = 3 },
                new { Type = BuildingType.Mine, Name = "Mine", W = 4, D = 4, Level = 3 },
                new { Type = BuildingType.Barracks, Name = "Barracks", W = 3, D = 5, Level = 3 },
                new { Type = BuildingType.Laboratory, Name = "Laboratory", W = 6, D = 6, Level = 4 },
                new { Type = BuildingType.MageTower, Name = "Mage Tower + Place of Power", W = 5, D = 5, Level = 4 }
            };
            Assert.That(BuildingCatalog.All.Count, Is.EqualTo(14));
            Assert.That(BuildingCatalog.All.Select(d => d.Type).Distinct().Count(), Is.EqualTo(14));
            Assert.That(BuildingCatalog.All.Select(d => d.Type), Is.EquivalentTo(expected.Select(d => d.Type)));
            Assert.That(Enum.GetValues(typeof(BuildingType)).Cast<BuildingType>(), Is.EquivalentTo(expected.Select(d => d.Type)));
            foreach (var row in expected)
            {
                var actual = BuildingCatalog.Get(row.Type);
                Assert.That(actual.DisplayName, Is.EqualTo(row.Name));
                Assert.That(actual.BaseWidth, Is.EqualTo(row.W), row.Name);
                Assert.That(actual.BaseDepth, Is.EqualTo(row.D), row.Name);
                Assert.That(actual.RequiredTownHallLevel, Is.EqualTo(row.Level), row.Name);
                Assert.That(actual.IsPlayerPlaceable, Is.EqualTo(row.Type != BuildingType.TownHall), row.Name);
            }
        }

        [Test]
        public void LevelOneOffersExactlyThreeStartingTypes() => AssertAvailability(1,
            BuildingType.BuildersHut, BuildingType.House, BuildingType.Farm);

        [Test]
        public void LevelTwoOffersSevenCumulativeTypes() => AssertAvailability(2,
            BuildingType.BuildersHut, BuildingType.House, BuildingType.Farm,
            BuildingType.Tavern, BuildingType.Butcher, BuildingType.FishermansHut, BuildingType.GuardTower);

        [Test]
        public void LevelThreeOffersElevenCumulativeTypes() => AssertAvailability(3,
            BuildingType.BuildersHut, BuildingType.House, BuildingType.Farm,
            BuildingType.Tavern, BuildingType.Butcher, BuildingType.FishermansHut, BuildingType.GuardTower,
            BuildingType.Warehouse, BuildingType.LumberMill, BuildingType.Mine, BuildingType.Barracks);

        [Test]
        public void LevelFourOffersAllThirteenPlaceableTypes() => AssertAvailability(4,
            BuildingType.BuildersHut, BuildingType.House, BuildingType.Farm,
            BuildingType.Tavern, BuildingType.Butcher, BuildingType.FishermansHut, BuildingType.GuardTower,
            BuildingType.Warehouse, BuildingType.LumberMill, BuildingType.Mine, BuildingType.Barracks,
            BuildingType.Laboratory, BuildingType.MageTower);

        private static void AssertAvailability(int level, params BuildingType[] expected)
        {
            var state = SettlementState.CreatePrototype(level);
            Assert.That(state.TownHallLevel, Is.EqualTo(level));
            Assert.That(state.AvailableBuildings.Select(d => d.Type), Is.EquivalentTo(expected));
            Assert.That(state.AvailableBuildings.Any(d => d.Type == BuildingType.TownHall), Is.False);
        }

        [Test]
        public void PrototypeStartsAtLevelOneWithUnchangedTerritoryAndHall()
        {
            var state = SettlementState.CreatePrototype();
            Assert.That(state.TownHallLevel, Is.EqualTo(1));
            Assert.That(state.Territory.Count, Is.EqualTo(400));
            Assert.That(state.Buildings.Count, Is.EqualTo(1));
            Assert.That(state.ApplyBuildingInfluence(state.TownHall), Is.Zero);
            Assert.That(state.Territory.Count, Is.EqualTo(400));
            Assert.That(state.TownHall.TerritoryInfluenceApplied, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => SettlementState.CreatePrototype(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SettlementState.CreatePrototype(6));
        }

        [Test]
        public void GenericPathPlacesEveryPlayerTypeWithImmediateCompletion()
        {
            foreach (var definition in BuildingCatalog.AvailableAt(4))
            {
                var state = SettlementState.CreatePrototype(4);
                Assert.That(state.TryPlacePrototypeBuilding(definition.Type, new TileCoordinate(1, 1), 0, out var building), Is.True, definition.DisplayName);
                Assert.That(building.Type, Is.EqualTo(definition.Type));
                Assert.That(building.Definition, Is.SameAs(definition));
                Assert.That(building.OccupiedTiles.Count(), Is.EqualTo(definition.BaseWidth * definition.BaseDepth));
                Assert.That(building.IsCompleted, Is.True);
                Assert.That(building.TerritoryInfluenceApplied, Is.True);
                Assert.That(state.Buildings.Count, Is.EqualTo(2));
                var claimed = state.Territory.ClaimedTiles.ToArray();
                Assert.That(state.ApplyBuildingInfluence(building), Is.Zero);
                Assert.That(state.Territory.ClaimedTiles, Is.EquivalentTo(claimed));
            }
        }

        [Test]
        public void LockedAndNonPlaceableTypesAreRejectedWithoutMutation()
        {
            var state = SettlementState.CreatePrototype(1);
            var origin = new TileCoordinate(1, 1);
            var original = state.Territory.ClaimedTiles.ToArray();
            foreach (var definition in BuildingCatalog.All.Where(d => d.IsPlayerPlaceable && d.RequiredTownHallLevel > 1))
            {
                Assert.That(state.ValidateBuildingPlacement(definition.Type, origin), Is.EqualTo(BuildingPlacementValidity.Locked));
                Assert.That(state.TryPlacePrototypeBuilding(definition.Type, origin, 0, out var rejected), Is.False);
                Assert.That(rejected, Is.Null);
            }
            Assert.That(state.ValidateBuildingPlacement(BuildingType.TownHall, origin), Is.EqualTo(BuildingPlacementValidity.NotPlayerPlaceable));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.TownHall, origin, 0, out _), Is.False);
            Assert.That(state.Buildings.Count, Is.EqualTo(1));
            Assert.That(state.Territory.ClaimedTiles, Is.EquivalentTo(original));
            Assert.That(state.IsOccupied(origin), Is.False);
        }

        [Test]
        public void RectanglesSwapAtQuarterTurnsAndSquaresKeepTheirShape()
        {
            foreach (var definition in BuildingCatalog.All)
            {
                foreach (int orientation in new[] { 0, 90, 180, 270 })
                {
                    var footprint = definition.GetFootprint(orientation);
                    bool swapped = orientation == 90 || orientation == 270;
                    Assert.That(footprint.Width, Is.EqualTo(swapped ? definition.BaseDepth : definition.BaseWidth));
                    Assert.That(footprint.Depth, Is.EqualTo(swapped ? definition.BaseWidth : definition.BaseDepth));
                }
            }
        }

        [Test]
        public void RotatedOccupiedTilesAndStoredOrientationAreAuthoritative()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm, new TileCoordinate(4, 3), 90, out var farm), Is.True);
            Assert.That(farm.OrientationDegrees, Is.EqualTo(90));
            Assert.That(farm.Width, Is.EqualTo(4));
            Assert.That(farm.Depth, Is.EqualTo(3));
            Assert.That(farm.OccupiedTiles.Count(), Is.EqualTo(12));
            for (int z = 3; z <= 5; z++)
                for (int x = 4; x <= 7; x++) Assert.That(state.IsOccupied(new TileCoordinate(x, z)), Is.True);
            Assert.That(state.IsOccupied(new TileCoordinate(4, 6)), Is.False);
            Assert.That(state.ValidateBuildingPlacement(BuildingType.GuardTower, new TileCoordinate(7, 4)), Is.EqualTo(BuildingPlacementValidity.Occupied));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.GuardTower, new TileCoordinate(7, 4), 0, out _), Is.False);
            Assert.That(state.Buildings.Count, Is.EqualTo(2));
        }

        [Test]
        public void RotationChangesBoundaryAndTownHallOverlapValidation()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.ValidateBuildingPlacement(BuildingType.Farm, new TileCoordinate(16, 17), 0), Is.EqualTo(BuildingPlacementValidity.OutsideTerritory));
            Assert.That(state.ValidateBuildingPlacement(BuildingType.Farm, new TileCoordinate(16, 17), 90), Is.EqualTo(BuildingPlacementValidity.Valid));
            Assert.That(state.ValidateBuildingPlacement(BuildingType.Farm, new TileCoordinate(4, 7), 0), Is.EqualTo(BuildingPlacementValidity.Valid));
            Assert.That(state.ValidateBuildingPlacement(BuildingType.Farm, new TileCoordinate(4, 7), 90), Is.EqualTo(BuildingPlacementValidity.Occupied));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm, new TileCoordinate(16, 17), 0, out _), Is.False);
            Assert.That(state.Buildings.Count, Is.EqualTo(1));
            Assert.That(state.Territory.Count, Is.EqualTo(400));
        }

        [Test]
        public void GuardTowerHasEightByEightInfluenceAndTwentyFourTileEdgeExpansion()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.GuardTower, new TileCoordinate(18, 8), 0, out var tower), Is.True);
            AssertInfluence(tower, 15, 5, 8, 8);
            Assert.That(state.Territory.Count, Is.EqualTo(424));
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(20, 4)), Is.False);
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(20, 13)), Is.False);
        }

        [Test]
        public void UnrotatedFarmHasNineByTenInfluenceAndThirtyTileEdgeExpansion()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm, new TileCoordinate(17, 8), 0, out var farm), Is.True);
            AssertInfluence(farm, 14, 5, 9, 10);
            Assert.That(state.Territory.Count, Is.EqualTo(430));
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(22, 14)), Is.True);
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(20, 15)), Is.False);
        }

        [Test]
        public void RotatedFarmHasTenByNineInfluenceAndTwentySevenTileEdgeExpansion()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm, new TileCoordinate(16, 8), 90, out var farm), Is.True);
            AssertInfluence(farm, 13, 5, 10, 9);
            Assert.That(farm.InfluenceTiles.All(state.Territory.IsClaimed), Is.True);
            Assert.That(state.Territory.Count, Is.EqualTo(427));
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(20, 14)), Is.False);
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(23, 8)), Is.False);
        }

        [Test]
        public void LaboratoryHasTwelveByTwelveInfluence()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Laboratory, new TileCoordinate(14, 7), 0, out var lab), Is.True);
            AssertInfluence(lab, 11, 4, 12, 12);
            Assert.That(state.Territory.Count, Is.EqualTo(436));
        }

        [Test]
        public void InvalidTypesAnglesAndExtremeCoordinatesDoNotMutateState()
        {
            var state = SettlementState.CreatePrototype(4);
            var origin = new TileCoordinate(1, 1);
            Assert.That(state.ValidateBuildingPlacement((BuildingType)999, origin), Is.EqualTo(BuildingPlacementValidity.UnknownType));
            Assert.That(state.TryPlacePrototypeBuilding((BuildingType)999, origin, 0, out _), Is.False);
            foreach (int angle in new[] { -90, 45, 360 })
            {
                Assert.That(state.ValidateBuildingPlacement(BuildingType.Farm, origin, angle), Is.EqualTo(BuildingPlacementValidity.InvalidOrientation));
                Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm, origin, angle, out _), Is.False);
            }
            foreach (int coordinate in new[] { int.MinValue, int.MaxValue })
                Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm, new TileCoordinate(coordinate, coordinate), 90, out _), Is.False);
            Assert.That(state.Buildings.Count, Is.EqualTo(1));
            Assert.That(state.Territory.Count, Is.EqualTo(400));
            Assert.That(state.IsOccupied(origin), Is.False);
        }

        private static void AssertInfluence(BuildingState building, int minX, int minZ, int width, int depth)
        {
            var actual = building.InfluenceTiles.ToArray();
            Assert.That(actual.Distinct().Count(), Is.EqualTo(width * depth));
            Assert.That(actual.Min(t => t.X), Is.EqualTo(minX));
            Assert.That(actual.Max(t => t.X), Is.EqualTo(minX + width - 1));
            Assert.That(actual.Min(t => t.Z), Is.EqualTo(minZ));
            Assert.That(actual.Max(t => t.Z), Is.EqualTo(minZ + depth - 1));
            for (int z = minZ; z < minZ + depth; z++)
                for (int x = minX; x < minX + width; x++) Assert.That(actual.Contains(new TileCoordinate(x, z)), Is.True);
        }
    }
}
