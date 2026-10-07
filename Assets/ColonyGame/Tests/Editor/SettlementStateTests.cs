using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class SettlementStateTests
    {
        [Test]
        public void InitialTerritoryContainsExactlyTheExpected400Tiles()
        {
            var state = SettlementState.CreatePrototype(4);
            Assert.That(state.Territory.Count, Is.EqualTo(400));
            Assert.That(state.Territory.ClaimedTiles.Distinct().Count(), Is.EqualTo(400));
            for (int z = 0; z < 20; z++)
                for (int x = 0; x < 20; x++)
                    Assert.That(state.Territory.IsClaimed(new TileCoordinate(x, z)), Is.True);
        }

        [Test]
        public void WildernessCoordinatesAreNotClaimed()
        {
            var territory = SettlementState.CreatePrototype(4).Territory;
            for (int z = -6; z < 26; z++)
                for (int x = -6; x < 26; x++)
                    if (x < 0 || x >= 20 || z < 0 || z >= 20)
                        Assert.That(territory.IsClaimed(new TileCoordinate(x, z)), Is.False);
            Assert.That(territory.IsClaimed(new TileCoordinate(1000, -1000)), Is.False);
        }

        [Test]
        public void CompletedTownHallOccupies25ClaimedTilesWithoutExpansion()
        {
            var state = SettlementState.CreatePrototype(4);
            var hall = state.TownHall;
            Assert.That(hall.Id, Is.EqualTo("starting-town-hall"));
            Assert.That(hall.Type, Is.EqualTo(BuildingType.TownHall));
            Assert.That(hall.IsCompleted, Is.True);
            Assert.That(hall.Width, Is.EqualTo(5));
            Assert.That(hall.Depth, Is.EqualTo(5));
            Assert.That(hall.OrientationDegrees, Is.Zero);
            Assert.That(hall.Origin, Is.EqualTo(new TileCoordinate(7, 7)));
            var occupied = hall.OccupiedTiles.ToArray();
            Assert.That(occupied.Distinct().Count(), Is.EqualTo(25));
            for (int z = 7; z <= 11; z++)
                for (int x = 7; x <= 11; x++)
                    Assert.That(occupied.Contains(new TileCoordinate(x, z)), Is.True);
            Assert.That(occupied.All(state.Territory.IsClaimed), Is.True);
            Assert.That(state.Territory.Count, Is.EqualTo(400));
        }

        [Test]
        public void TerritorySupportsIrregularIndividualTilesAndDeduplicatesThem()
        {
            var tiles = new[] { new TileCoordinate(0, 0), new TileCoordinate(1, 0),
                new TileCoordinate(0, 1), new TileCoordinate(-3, 7), new TileCoordinate(0, 0) };
            var territory = new SettlementTerritory(tiles);
            tiles[0] = new TileCoordinate(50, 50);
            Assert.That(territory.Count, Is.EqualTo(4));
            Assert.That(territory.IsClaimed(new TileCoordinate(0, 0)), Is.True);
            Assert.That(territory.IsClaimed(new TileCoordinate(-3, 7)), Is.True);
            Assert.That(territory.IsClaimed(new TileCoordinate(1, 1)), Is.False);
            Assert.That(territory.IsClaimed(new TileCoordinate(50, 50)), Is.False);
        }
    }
}
