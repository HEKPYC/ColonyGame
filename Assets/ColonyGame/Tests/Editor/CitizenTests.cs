using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class CitizenTests
    {
        private static Citizen Adult(string id,CitizenPosition position) =>
            new Citizen(id,"Robin",CitizenSex.Male,25,new CitizenAttributes(5,5,5,5,5),position);

        [Test]
        public void StartingPopulationHasFourUniqueUnemployedAdultsWithTwoOfEachSex()
        {
            var citizens=CitizenSimulation.CreatePrototype(SettlementState.CreatePrototype()).Citizens;
            Assert.That(citizens.Count,Is.EqualTo(4));
            Assert.That(citizens.Count(c=>c.Sex==CitizenSex.Male),Is.EqualTo(2));
            Assert.That(citizens.Count(c=>c.Sex==CitizenSex.Female),Is.EqualTo(2));
            Assert.That(citizens.Select(c=>c.Id).Distinct().Count(),Is.EqualTo(4));
            Assert.That(citizens.All(c=>c.Profession==CitizenProfession.Unemployed&&!string.IsNullOrWhiteSpace(c.FirstName)),Is.True);
        }
        [Test]
        public void StartingAgeAndAttributeGenerationIsReproducibleAndWithinApprovedRanges()
        {
            var ages=new HashSet<int>();var values=new HashSet<int>();
            for(int seed=0;seed<100;seed++)
            {
                var one=CitizenSimulation.CreatePrototype(SettlementState.CreatePrototype(),seed:seed).Citizens;
                var two=CitizenSimulation.CreatePrototype(SettlementState.CreatePrototype(),seed:seed).Citizens;
                for(int i=0;i<4;i++)
                {
                    Assert.That(one[i].Age,Is.InRange(18,45));
                    Assert.That(one[i].Age,Is.EqualTo(two[i].Age));
                    Assert.That(one[i].Id,Is.EqualTo(two[i].Id));
                    Assert.That(one[i].Position.DistanceSquared(two[i].Position),Is.Zero);
                    var a=one[i].Attributes;var b=two[i].Attributes;
                    var attributes=new[]{a.Strength,a.Dexterity,a.Intelligence,a.Endurance,a.Charisma};
                    Assert.That(attributes,Is.EqualTo(new[]{b.Strength,b.Dexterity,b.Intelligence,b.Endurance,b.Charisma}));
                    foreach(int value in attributes) { Assert.That(value,Is.InRange(1,10));values.Add(value); }
                    ages.Add(one[i].Age);
                }
            }
            Assert.That(ages.Contains(18)&&ages.Contains(45),Is.True);
            Assert.That(values.Contains(1)&&values.Contains(10),Is.True);
        }
        [Test]
        public void StartingPositionsAreDistinctClaimedAndOutsideEveryBuilding()
        {
            var state=SettlementState.CreatePrototype();
            var policy=new SettlementIdleMovementPolicy(state);
            var citizens=CitizenSimulation.CreatePrototype(state).Citizens;
            Assert.That(citizens.Select(c=>c.Position.Tile).Distinct().Count(),Is.EqualTo(4));
            Assert.That(citizens.All(c=>state.Territory.IsClaimed(c.Position.Tile)&&!state.IsOccupied(c.Position.Tile)&&policy.CanOccupy(c.Position)),Is.True);
            Assert.That(state.Territory.Count,Is.EqualTo(400));Assert.That(state.Buildings.Count,Is.EqualTo(1));
        }
        [Test]
        public void StartupAlsoAvoidsPreviouslyPlacedFootprints()
        {
            var state=SettlementState.CreatePrototype();
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(4,4),0,out _),Is.True);
            Assert.That(CitizenSimulation.CreatePrototype(state).Citizens.All(c=>!state.IsOccupied(c.Position.Tile)),Is.True);
        }
        [Test]
        public void NavigationRoutesAroundTownHallInsteadOfCrossingIt()
        {
            var state=SettlementState.CreatePrototype();
            var policy=new SettlementIdleMovementPolicy(state);
            var path=CitizenNavigation.FindPath(new TileCoordinate(6,9),new TileCoordinate(12,9),t=>policy.CanOccupy(CitizenPosition.Center(t)));
            Assert.That(path.Count,Is.EqualTo(13));
            Assert.That(path.All(t=>state.Territory.IsClaimed(t)&&!state.IsOccupied(t)),Is.True);
            for(int i=1;i<path.Count;i++) Assert.That(Math.Abs(path[i].X-path[i-1].X)+Math.Abs(path[i].Z-path[i-1].Z),Is.EqualTo(1));
        }
        [Test]
        public void NavigationFollowsIrregularClaimsWithoutCuttingAcrossWilderness()
        {
            var tiles=new[]{new TileCoordinate(0,0),new TileCoordinate(1,0),new TileCoordinate(2,0),new TileCoordinate(3,0),new TileCoordinate(3,1),new TileCoordinate(3,2),new TileCoordinate(3,3)};
            var territory=new SettlementTerritory(tiles);
            var policy=new SettlementIdleMovementPolicy(territory,_=>false);
            var path=CitizenNavigation.FindPath(tiles[0],tiles[6],t=>policy.CanOccupy(CitizenPosition.Center(t)));
            Assert.That(path.Count,Is.EqualTo(7));
            Assert.That(path.All(territory.IsClaimed),Is.True);
            Assert.That(CitizenNavigation.FindPath(tiles[0],new TileCoordinate(1,1),t=>policy.CanOccupy(CitizenPosition.Center(t))).Count,Is.Zero);
        }
        [Test]
        public void BodyClearanceRejectsClaimEdgesAndBuildingEdges()
        {
            var policy=new SettlementIdleMovementPolicy(SettlementState.CreatePrototype());
            Assert.That(policy.CanOccupy(new CitizenPosition(.1,2.5)),Is.False);
            Assert.That(policy.CanOccupy(new CitizenPosition(6.9,9.5)),Is.False);
            Assert.That(policy.CanOccupy(new CitizenPosition(6.5,9.5)),Is.True);
            Assert.That(policy.CanOccupy(new CitizenPosition(20.5,9.5)),Is.False);
        }
        [Test]
        public void IdleWanderingIsContinuousPausesAndKeepsDestinationsWalkable()
        {
            var state=SettlementState.CreatePrototype();
            state.TryPlacePrototypeBuilding(BuildingType.Farm,new TileCoordinate(2,2),90,out _);
            var policy=new SettlementIdleMovementPolicy(state);
            var simulation=CitizenSimulation.CreatePrototype(state);
            int moves=0,pauses=0;
            for(int tick=0;tick<1200;tick++)
            {
                var before=simulation.Citizens.Select(c=>c.Position).ToArray();
                simulation.Tick(.05);
                for(int i=0;i<4;i++)
                {
                    var c=simulation.Citizens[i];
                    Assert.That(policy.CanOccupy(c.Position)&&policy.CanOccupy(c.Destination),Is.True);
                    double distance=c.Position.DistanceSquared(before[i]);
                    Assert.That(distance,Is.LessThanOrEqualTo(.04*.04+1e-9));
                    if(distance>0)moves++;else pauses++;
                }
            }
            Assert.That(moves,Is.GreaterThan(0));Assert.That(pauses,Is.GreaterThan(0));
        }
        [Test]
        public void ExpansionMakesNewClaimedTilesAvailableWithoutOpeningWilderness()
        {
            var state=SettlementState.CreatePrototype();
            var policy=new SettlementIdleMovementPolicy(state);
            Assert.That(policy.CanOccupy(new CitizenPosition(22.5,11.5)),Is.False);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            var paths=CitizenNavigation.NearbyRoutes(new TileCoordinate(19,11),t=>policy.CanOccupy(CitizenPosition.Center(t)),6);
            Assert.That(paths.Any(p=>p.Last().Equals(new TileCoordinate(22,11))),Is.True);
            Assert.That(paths.SelectMany(p=>p).All(state.Territory.IsClaimed),Is.True);
            Assert.That(policy.CanOccupy(new CitizenPosition(20.5,4.5)),Is.False);
        }
        [Test]
        public void BuildingOverCitizenSucceedsAndStartsNearestContinuousRecovery()
        {
            var state=SettlementState.CreatePrototype();
            var simulation=CitizenSimulation.CreatePrototype(state);
            var c=simulation.Citizens[0];var before=c.Position;
            int warnings=0;simulation.DevelopmentWarning+=_=>warnings++;
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(4,4),0,out _),Is.True);
            simulation.ReconcileBuildings();
            var policy=new SettlementIdleMovementPolicy(state);
            Assert.That(c.Position.DistanceSquared(before),Is.Zero);
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Recovering));
            Assert.That(policy.CanOccupy(c.Destination),Is.True);
            Assert.That(Math.Abs(c.Destination.X-before.X)+Math.Abs(c.Destination.Z-before.Z),Is.EqualTo(1));
            bool exited=false;
            for(int tick=0;tick<200;tick++)
            {
                var last=c.Position;simulation.Tick(.05);
                Assert.That(c.Position.DistanceSquared(last),Is.LessThanOrEqualTo(.04*.04+1e-9));
                Assert.That(state.Territory.IsClaimed(c.Position.Tile),Is.True);
                if(policy.CanOccupy(c.Position)) exited=true;
                if(exited) Assert.That(policy.CanOccupy(c.Position)&&c.Movement!=CitizenMovement.Recovering,Is.True);
            }
            Assert.That(exited,Is.True);Assert.That(warnings,Is.Zero);
            Assert.That(state.Buildings.Count,Is.EqualTo(2));Assert.That(state.Territory.Count,Is.EqualTo(400));
            Assert.That(c.Id,Is.EqualTo(simulation.Citizens[0].Id));
        }
        [Test]
        public void FootprintRecoveryWorksForSeveralCitizensIndependently()
        {
            var state=SettlementState.CreatePrototype();
            var policy=new SettlementIdleMovementPolicy(state);
            var citizens=new[]{Adult("a",new CitizenPosition(2.5,2.5)),Adult("b",new CitizenPosition(2.5,3.5))};
            var simulation=new CitizenSimulation(policy,citizens);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(1,1),0,out _);
            simulation.ReconcileBuildings();
            Assert.That(citizens.All(c=>c.Movement==CitizenMovement.Recovering&&policy.CanOccupy(c.Destination)),Is.True);
            Assert.That(citizens[0].Position.X,Is.EqualTo(2.5));
            for(int i=0;i<100;i++) simulation.Tick(.05);
            Assert.That(citizens.All(c=>policy.CanOccupy(c.Position)),Is.True);
        }
        [Test]
        public void NewObstacleCancelsAnUnsafeWalkingRouteWithoutTeleportation()
        {
            var state=SettlementState.CreatePrototype();var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult("walker",new CitizenPosition(.5,.5));
            var simulation=new CitizenSimulation(policy,new[]{c},new CitizenMovementSettings(minimumPause:0,maximumPause:0));
            simulation.Tick(.01);
            var before=c.Position;var destination=c.Destination.Tile;
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House,destination,0,out _),Is.True);
            simulation.ReconcileBuildings();
            Assert.That(c.Position.DistanceSquared(before),Is.Zero);
            Assert.That(policy.CanOccupy(c.Destination),Is.True);
            for(int i=0;i<200;i++)simulation.Tick(.05);
            Assert.That(policy.CanOccupy(c.Position),Is.True);
        }
        [Test]
        public void NoFreeClaimedTilesProducesLoggedDormancyAndLaterRecovery()
        {
            bool blocked=false;
            var policy=new SettlementIdleMovementPolicy(new SettlementTerritory(new[]{new TileCoordinate(0,0)}),_=>blocked);
            var c=Adult("isolated",new CitizenPosition(.5,.5));
            var simulation=new CitizenSimulation(policy,new[]{c});
            string warning=null;simulation.DevelopmentWarning+=message=>warning=message;
            blocked=true;simulation.ReconcileBuildings();
            Assert.That(c.Active,Is.False);Assert.That(warning,Does.Contain("emergency dormancy"));
            blocked=false;simulation.ReconcileBuildings();
            Assert.That(c.Active,Is.True);Assert.That(c.Id,Is.EqualTo("isolated"));Assert.That(policy.CanOccupy(c.Position),Is.True);
        }
        [Test]
        public void IsolatedCitizenPausesSafelyWithoutInvalidDestinations()
        {
            var policy=new SettlementIdleMovementPolicy(new SettlementTerritory(new[]{new TileCoordinate(0,0)}),_=>false);
            var c=Adult("isolated",new CitizenPosition(.5,.5));
            var simulation=new CitizenSimulation(policy,new[]{c});
            for(int i=0;i<50;i++)simulation.Tick(1);
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Idle));
            Assert.That(c.Position.DistanceSquared(new CitizenPosition(.5,.5)),Is.Zero);
            Assert.That(policy.CanOccupy(c.Destination),Is.True);
        }
        [Test]
        public void RecoveryPermissionDoesNotAllowCrossingAnotherBuilding()
        {
            var state=SettlementState.CreatePrototype();
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(1,1),0,out _);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(4,1),0,out _);
            var policy=new SettlementIdleMovementPolicy(state);
            var origin=new CitizenPosition(5.5,2.5);
            var permitted=new HashSet<TileCoordinate>(state.Buildings.Last().OccupiedTiles);
            Assert.That(policy.CanFollowRecovery(origin,permitted),Is.True);
            Assert.That(policy.CanFollowRecovery(new CitizenPosition(3.5,2.5),permitted),Is.False);
            Assert.That(policy.CanFollowRecovery(new CitizenPosition(7.5,2.5),permitted),Is.True);
            Assert.That(policy.CanFollowRecovery(new CitizenPosition(20.5,2.5),permitted),Is.False);
        }        [Test]
        public void SurroundedFootprintUsesContinuousRecoveryToConnectedClaims()
        {
            var state=SettlementState.CreatePrototype();
            foreach(var origin in new[]{new TileCoordinate(1,4),new TileCoordinate(7,4),new TileCoordinate(4,1),new TileCoordinate(4,7)})
                Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House,origin,0,out _),Is.True);
            int claims=state.Territory.Count;
            var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult("trapped",new CitizenPosition(5.5,5.5));
            var simulation=new CitizenSimulation(policy,new[]{c});
            string warning=null;simulation.DevelopmentWarning+=message=>warning=message;
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(4,4),0,out _),Is.True);
            simulation.ReconcileBuildings();
            Assert.That(warning,Is.Null);
            Assert.That(c.Position.DistanceSquared(new CitizenPosition(5.5,5.5)),Is.Zero);
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Recovering));
            Assert.That(policy.IsConnected(c.Destination),Is.True);
            for(int i=0;i<100;i++) simulation.Tick(.1);
            Assert.That(c.Active&&policy.IsConnected(c.Position),Is.True);
            Assert.That(c.Id,Is.EqualTo("trapped"));
            Assert.That(state.Territory.Count,Is.EqualTo(claims));
        }
        [Test]
        public void PlacementDuringWalkingPreservesFractionalPositionAndRecoveryContinuity()
        {
            var state=SettlementState.CreatePrototype();var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult("moving",new CitizenPosition(.5,.5));
            var simulation=new CitizenSimulation(policy,new[]{c},new CitizenMovementSettings(minimumPause:0,maximumPause:0));
            simulation.Tick(.5);
            var before=c.Position;
            Assert.That(before.DistanceSquared(new CitizenPosition(.5,.5)),Is.GreaterThan(0));
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(0,0),0,out _);
            simulation.ReconcileBuildings();
            Assert.That(c.Position.DistanceSquared(before),Is.Zero);
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Recovering));
            for(int tick=0;tick<100;tick++)
            {
                var last=c.Position;simulation.Tick(.1);
                Assert.That(c.Position.DistanceSquared(last),Is.LessThanOrEqualTo(.08*.08+1e-9));
                Assert.That(state.Territory.IsClaimed(c.Position.Tile),Is.True);
            }
            Assert.That(policy.CanOccupy(c.Position),Is.True);
        }        private sealed class TestJobPolicy : ICitizenMovementPolicy
        {
            // A finite test policy outside the original settlement demonstrates policy injection.
            public IEnumerable<TileCoordinate> CandidateTiles=>new[]{new TileCoordinate(20,0),new TileCoordinate(21,0)};
            public bool CanOccupy(CitizenPosition p)=>p.X-.28>=20&&p.X+.28<22&&p.Z-.28>=0&&p.Z+.28<1;
            public bool CanTraverseRecovery(CitizenPosition p,CitizenPosition origin)=>CanOccupy(p);
        }
        [Test]
        public void SimulationEligibilityIsPolicyDrivenRatherThanUniversalWildernessBan()
        {
            var policy=new TestJobPolicy();var c=Adult("future",new CitizenPosition(20.5,.5));
            var simulation=new CitizenSimulation(policy,new[]{c},new CitizenMovementSettings(minimumPause:0,maximumPause:0));
            simulation.Tick(.5);
            Assert.That(c.Position.X,Is.GreaterThan(20.5));Assert.That(c.Destination.X,Is.EqualTo(21.5));
        }
        [Test]
        public void InvalidModelsSettingsAndDuplicateIdentitiesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CitizenAttributes(0,1,1,1,1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CitizenAttributes(1,1,11,1,1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CitizenMovementSettings(speed:0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CitizenMovementSettings(minimumPause:3,maximumPause:2));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CitizenPosition(double.NaN,0));
            var policy=new SettlementIdleMovementPolicy(SettlementState.CreatePrototype());
            var c=Adult("same",new CitizenPosition(.5,.5));
            Assert.Throws<ArgumentException>(()=>new CitizenSimulation(policy,new[]{c,c}));
            var simulation=new CitizenSimulation(policy,new[]{c});
            Assert.Throws<ArgumentOutOfRangeException>(()=>simulation.Tick(-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>simulation.Tick(double.NaN));
        }
    }
}
