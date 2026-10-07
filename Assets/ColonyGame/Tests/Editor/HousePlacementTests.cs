using System;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class HousePlacementTests
    {
        [Test]
        public void InteriorHouseCompletesWithNineTilesAndNoNewClaims()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.ValidateBuildingPlacement(BuildingType.House, new TileCoordinate(3, 3)), Is.EqualTo(BuildingPlacementValidity.Valid));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(3, 3), 0, out var house), Is.True);
            Assert.That(house.Type, Is.EqualTo(BuildingType.House));
            Assert.That(house.OccupiedTiles.Count(), Is.EqualTo(9));
            Assert.That(house.Width, Is.EqualTo(3));
            Assert.That(house.Depth, Is.EqualTo(3));
            Assert.That(house.OrientationDegrees, Is.Zero);
            Assert.That(house.IsCompleted, Is.True);
            Assert.That(house.TerritoryInfluenceApplied, Is.True);
            Assert.That(state.Buildings.Count, Is.EqualTo(2));
            Assert.That(state.Territory.Count, Is.EqualTo(400));
        }
        [Test]
        public void PartialWildernessPlacementIsRejectedWithoutMutation()
        {
            var state = SettlementState.CreatePrototype(4);
            var original = state.Territory.ClaimedTiles.ToArray();
            Assert.That(state.ValidateBuildingPlacement(BuildingType.House, new TileCoordinate(18, 8)), Is.EqualTo(BuildingPlacementValidity.OutsideTerritory));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(18, 8), 0, out var house), Is.False);
            Assert.That(house, Is.Null);
            Assert.That(state.Buildings.Count, Is.EqualTo(1));
            Assert.That(state.Territory.ClaimedTiles, Is.EquivalentTo(original));
        }
        [Test]
        public void TownHallAndHouseOverlapAreRejected()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.ValidateBuildingPlacement(BuildingType.House, new TileCoordinate(6, 6)), Is.EqualTo(BuildingPlacementValidity.Occupied));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(6, 6), 0, out _), Is.False);
            state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(3, 3), 0, out _);
            Assert.That(state.ValidateBuildingPlacement(BuildingType.House, new TileCoordinate(5, 5)), Is.EqualTo(BuildingPlacementValidity.Occupied));
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(5, 5), 0, out _), Is.False);
            Assert.That(state.Buildings.Count, Is.EqualTo(2));
            Assert.That(state.Territory.Count, Is.EqualTo(400));
        }
        [Test]
        public void RightEdgeCreatesOnlyTwentySevenTilesInThreeByNineProtrusion()
        {
            var state = SettlementState.CreatePrototype(4);
            state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(17, 8), 0, out _);
            Assert.That(state.Territory.Count, Is.EqualTo(427));
            var outside = state.Territory.ClaimedTiles.Where(t => t.X >= 20).ToArray();
            Assert.That(outside.Length, Is.EqualTo(27));
            for (int z = 5; z <= 13; z++)
                for (int x = 20; x <= 22; x++)
                    Assert.That(state.Territory.IsClaimed(new TileCoordinate(x, z)), Is.True);
            for (int z = 0; z < 20; z++)
                if (z < 5 || z > 13)
                    for (int x = 20; x <= 22; x++)
                        Assert.That(state.Territory.IsClaimed(new TileCoordinate(x, z)), Is.False);
            Assert.That(state.Territory.IsClaimed(new TileCoordinate(23, 8)), Is.False);
        }
        [Test]
        public void InfluenceAndValidationAreIdempotentIncludingStartingExemption()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.ApplyBuildingInfluence(state.TownHall), Is.Zero);
            Assert.That(state.Territory.Count, Is.EqualTo(400));
            Assert.That(state.TownHall.TerritoryInfluenceApplied, Is.False);
            state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(17, 8), 0, out var house);
            var original = state.Territory.ClaimedTiles.ToArray();
            Assert.That(state.ApplyBuildingInfluence(house), Is.Zero);
            state.ValidateBuildingPlacement(BuildingType.House, new TileCoordinate(20, 8));
            Assert.That(state.ApplyBuildingInfluence(house), Is.Zero);
            Assert.That(state.Territory.ClaimedTiles, Is.EquivalentTo(original));
        }
        [Test]
        public void SecondHouseCanUseFirstHouseClaimsAndHasDistinctIdentity()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.ValidateBuildingPlacement(BuildingType.House, new TileCoordinate(20, 8)), Is.EqualTo(BuildingPlacementValidity.OutsideTerritory));
            state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(17, 8), 0, out var first);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(20, 8), 0, out var second), Is.True);
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(state.Buildings.Count, Is.EqualTo(3));
            Assert.That(state.Territory.Count, Is.EqualTo(454));
            Assert.That(second.OccupiedTiles.All(state.Territory.IsClaimed), Is.True);
            Assert.That(first.OccupiedTiles.Intersect(second.OccupiedTiles).Count(), Is.Zero);
        }
        [Test]
        public void InfluenceRejectsBuildingsFromOtherSettlements()
        {
            var state = SettlementState.CreatePrototype(4);
            var other = SettlementState.CreatePrototype(4);
            other.TryPlacePrototypeBuilding(BuildingType.House, new TileCoordinate(3, 3), 0, out var house);
            Assert.Throws<ArgumentException>(() => state.ApplyBuildingInfluence(house));
            Assert.That(state.Territory.Count, Is.EqualTo(400));
        }
    }
}
