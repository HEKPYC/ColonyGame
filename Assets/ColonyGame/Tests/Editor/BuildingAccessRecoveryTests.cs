using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class BuildingAccessRecoveryTests
    {
        private static Citizen Adult(CitizenPosition position)=>new Citizen("access-test","Robin",CitizenSex.Male,25,new CitizenAttributes(5,5,5,5,5),position);
        private static SettlementState ThreeHuts()
        {
            var state=SettlementState.CreatePrototype(4);
            foreach(var origin in new[]{new TileCoordinate(1,1),new TileCoordinate(4,2),new TileCoordinate(2,5)})
                Assert.That(state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,origin,0,out _),Is.True);
            return state;
        }
        [Test]
        public void EveryBuildingExposesDeterministicEdgeEntranceAndExteriorAccess()
        {
            var origin=new TileCoordinate(4,4);
            foreach(var definition in BuildingCatalog.All)
                foreach(int angle in new[]{0,90,180,270})
                {
                    var one=BuildingEntrance.For(definition,origin,angle);
                    var two=BuildingEntrance.For(definition,origin,angle);
                    var footprint=definition.GetFootprint(angle);
                    Assert.That(one.FootprintTile,Is.EqualTo(two.FootprintTile));
                    Assert.That(one.Facing,Is.EqualTo(two.Facing));
                    Assert.That(one.FootprintTile.X,Is.InRange(origin.X,origin.X+footprint.Width-1));
                    Assert.That(one.FootprintTile.Z,Is.InRange(origin.Z,origin.Z+footprint.Depth-1));
                    Assert.That(Math.Abs(one.Facing.X)+Math.Abs(one.Facing.Z),Is.EqualTo(1));
                    Assert.That(one.ExteriorTile.X<origin.X||one.ExteriorTile.X>=origin.X+footprint.Width||
                        one.ExteriorTile.Z<origin.Z||one.ExteriorTile.Z>=origin.Z+footprint.Depth,Is.True);
                    Assert.That(one.ExteriorTile,Is.EqualTo(new TileCoordinate(one.FootprintTile.X+one.Facing.X,one.FootprintTile.Z+one.Facing.Z)));
                }
        }
        [Test]
        public void RectangularEntranceRotationMatchesOrientedFootprint()
        {
            var definition=BuildingCatalog.Get(BuildingType.Farm);
            var tiles=new[]{new TileCoordinate(5,4),new TileCoordinate(4,5),new TileCoordinate(5,7),new TileCoordinate(7,5)};
            var faces=new[]{new TileCoordinate(0,-1),new TileCoordinate(-1,0),new TileCoordinate(0,1),new TileCoordinate(1,0)};
            var access=new[]{new TileCoordinate(5,3),new TileCoordinate(3,5),new TileCoordinate(5,8),new TileCoordinate(8,5)};
            for(int i=0;i<4;i++)
            {
                var state=SettlementState.CreatePrototype(4);
                Assert.That(state.TryPlacePrototypeBuilding(BuildingType.Farm,new TileCoordinate(4,4),i*90,out var building),Is.True);
                Assert.That(building.Entrance.FootprintTile,Is.EqualTo(tiles[i]));
                Assert.That(building.Entrance.Facing,Is.EqualTo(faces[i]));
                Assert.That(building.Entrance.ExteriorTile,Is.EqualTo(access[i]));
            }
        }
        [Test]
        public void ExteriorAccessIsNotReservedOrRequiredByPlacementValidation()
        {
            var state=SettlementState.CreatePrototype(4);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(1,1),0,out var house);
            Assert.That(house.Entrance.ExteriorTile,Is.EqualTo(new TileCoordinate(2,0)));
            Assert.That(house.OccupiedTiles.Contains(house.Entrance.ExteriorTile),Is.False);
            Assert.That(house.Entrance.IsExteriorAccessible(state),Is.True);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(1,-2),0,out _),Is.True);
            Assert.That(house.Entrance.IsExteriorAccessible(state),Is.False);
            Assert.That(state.Buildings.Count,Is.EqualTo(3));
        }
        [Test]
        public void EntranceAndAllPerimeterTilesRemainOrdinaryNavigationObstacles()
        {
            var state=SettlementState.CreatePrototype(4);
            state.TryPlacePrototypeBuilding(BuildingType.Farm,new TileCoordinate(4,4),90,out var building);
            var policy=new SettlementIdleMovementPolicy(state);
            Assert.That(building.OccupiedTiles.All(t=>!policy.CanOccupy(CitizenPosition.Center(t))),Is.True);
            var path=CitizenNavigation.FindPath(building.Entrance.ExteriorTile,building.Entrance.FootprintTile,t=>policy.CanOccupy(CitizenPosition.Center(t)));
            Assert.That(path.Count,Is.Zero);
            Assert.That(state.IsOccupied(building.Entrance.FootprintTile),Is.True);
        }
        [Test]
        public void SealedBuildersHutPocketStartsContinuousMinimalRecovery()
        {
            var state=ThreeHuts();var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult(new CitizenPosition(2.5,4.5));var simulation=new CitizenSimulation(policy,new[]{c});
            Assert.That(policy.IsConnected(c.Position),Is.True);
            int warnings=0;simulation.DevelopmentWarning+=_=>warnings++;
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(-1,4),0,out _),Is.True);
            var before=c.Position;simulation.ReconcileBuildings();
            Assert.That(policy.CanOccupy(c.Position),Is.True);
            Assert.That(policy.IsConnected(c.Position),Is.False);
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Recovering));
            Assert.That(c.Position.DistanceSquared(before),Is.Zero);
            Assert.That(policy.IsConnected(c.Destination),Is.True);
            Assert.That(c.PlannedRoute.Count(p=>state.IsOccupied(p.Tile)),Is.EqualTo(2));
            bool arrived=false;
            for(int i=0;i<300;i++)
            {
                var last=c.Position;simulation.Tick(.05);
                Assert.That(c.Position.DistanceSquared(last),Is.LessThanOrEqualTo(.04*.04+1e-9));
                Assert.That(state.Territory.IsClaimed(c.Position.Tile),Is.True);
                if(policy.IsConnected(c.Position)) arrived=true;
                if(arrived) Assert.That(policy.CanOccupy(c.Position)&&c.Movement!=CitizenMovement.Recovering,Is.True);
            }
            Assert.That(arrived,Is.True);Assert.That(warnings,Is.Zero);
            Assert.That(c.InsideBuildingId,Is.Null);
        }
        [Test]
        public void OneTilePassageKeepsCitizenConnectedWithoutExceptionalCrossing()
        {
            var state=ThreeHuts();var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult(new CitizenPosition(2.5,4.5));var simulation=new CitizenSimulation(policy,new[]{c});
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(-2,4),0,out _),Is.True);
            simulation.ReconcileBuildings();
            Assert.That(policy.IsConnected(c.Position),Is.True);
            Assert.That(c.Movement,Is.Not.EqualTo(CitizenMovement.Recovering));
            var path=CitizenNavigation.FindPath(c.Position.Tile,policy.NavigationAnchor.Value,t=>policy.CanOccupy(CitizenPosition.Center(t)));
            Assert.That(path.Count,Is.GreaterThan(0));
            Assert.That(path.All(t=>!state.IsOccupied(t)&&state.Territory.IsClaimed(t)),Is.True);
        }
        [Test]
        public void RecoveryAlwaysPrefersAnOrdinaryDetourOverAnOccupiedShortcut()
        {
            bool Claimed(TileCoordinate t)=>t.X>=0&&t.X<8&&t.Z>=0&&t.Z<5;
            bool Occupied(TileCoordinate t)=>t.X>=3&&t.X<=5&&t.Z>=1&&t.Z<=3;
            var target=new TileCoordinate(7,2);
            var path=CitizenNavigation.FindRecoveryPath(new TileCoordinate(0,2),Claimed,t=>t.Equals(target),Occupied,_=>0);
            Assert.That(path.Count,Is.EqualTo(12));
            Assert.That(path.All(t=>!Occupied(t)),Is.True);
        }
        [Test]
        public void RecoveryMinimizesOccupiedTileCountBeforeTravelDistance()
        {
            bool Claimed(TileCoordinate t)=>t.X>=0&&t.X<7&&t.Z>=0&&t.Z<5;
            bool Occupied(TileCoordinate t)=>t.X==2&&t.Z>=1&&t.Z<=3;
            var path=CitizenNavigation.FindRecoveryPath(new TileCoordinate(1,2),Claimed,t=>t.Equals(new TileCoordinate(3,2)),Occupied,_=>0);
            Assert.That(path.All(t=>!Occupied(t)),Is.True);
            Assert.That(path.Count,Is.GreaterThan(3));
            bool FullWall(TileCoordinate t)=>t.X==2;
            path=CitizenNavigation.FindRecoveryPath(new TileCoordinate(1,2),Claimed,t=>t.Equals(new TileCoordinate(3,2)),FullWall,_=>0);
            Assert.That(path.Count(FullWall),Is.EqualTo(1));
        }
        [Test]
        public void DirectFootprintRecoveryPrefersEntranceWhenCrossingCostsTie()
        {
            var state=SettlementState.CreatePrototype(4);var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult(new CitizenPosition(4.5,4.5));var simulation=new CitizenSimulation(policy,new[]{c});
            state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(3,3),0,out var hut);
            simulation.ReconcileBuildings();
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Recovering));
            Assert.That(c.PlannedRoute.Any(p=>p.Tile.Equals(hut.Entrance.FootprintTile)),Is.True);
            Assert.That(c.Destination.Tile,Is.EqualTo(hut.Entrance.ExteriorTile));
            Assert.That(c.Position.DistanceSquared(new CitizenPosition(4.5,4.5)),Is.Zero);
        }
        [Test]
        public void RecoveryRespectsIrregularClaimsRatherThanBoundingRectangle()
        {
            var claimed=new[]{new TileCoordinate(0,0),new TileCoordinate(1,0),new TileCoordinate(2,0),new TileCoordinate(3,0),
                new TileCoordinate(3,1),new TileCoordinate(3,2),new TileCoordinate(3,3)};
            var territory=new SettlementTerritory(claimed);
            var policy=new SettlementIdleMovementPolicy(territory,t=>t.Equals(new TileCoordinate(1,0)));
            var c=Adult(new CitizenPosition(.5,.5));var simulation=new CitizenSimulation(policy,new[]{c});
            simulation.ReconcileBuildings();
            Assert.That(c.Movement,Is.EqualTo(CitizenMovement.Recovering));
            Assert.That(c.PlannedRoute.All(p=>territory.IsClaimed(p.Tile)),Is.True);
            Assert.That(c.Destination.Tile,Is.EqualTo(new TileCoordinate(2,0)));
            for(int i=0;i<100;i++)simulation.Tick(.05);
            Assert.That(policy.IsConnected(c.Position),Is.True);
            Assert.That(policy.CanTraverseRecovery(new CitizenPosition(1.5,1.5),c.Position),Is.False);
        }
        [Test]
        public void BlockedTownHallAccessUsesDeterministicConnectedAnchor()
        {
            var state=SettlementState.CreatePrototype(4);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(8,4),0,out _);
            Assert.That(state.TownHall.Entrance.IsExteriorAccessible(state),Is.False);
            var one=new SettlementIdleMovementPolicy(state);var two=new SettlementIdleMovementPolicy(state);
            one.RefreshConnectivity();two.RefreshConnectivity();
            Assert.That(one.NavigationAnchor,Is.EqualTo(two.NavigationAnchor));
            Assert.That(one.NavigationAnchor.HasValue,Is.True);
            Assert.That(one.CanOccupy(CitizenPosition.Center(one.NavigationAnchor.Value)),Is.True);
            Assert.That(one.ConnectedTileCount,Is.GreaterThan(300));
        }
        [Test]
        public void FurtherConstructionReplansRecoveryWithoutChangingCitizenPosition()
        {
            var state=ThreeHuts();var policy=new SettlementIdleMovementPolicy(state);
            var c=Adult(new CitizenPosition(2.5,4.5));var simulation=new CitizenSimulation(policy,new[]{c});
            int warnings=0;simulation.DevelopmentWarning+=_=>warnings++;
            state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(-1,4),0,out _);
            simulation.ReconcileBuildings();simulation.Tick(.3);
            var before=c.Position;
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(3,8),0,out _),Is.True);
            simulation.ReconcileBuildings();
            Assert.That(c.Position.DistanceSquared(before),Is.Zero);
            Assert.That(policy.IsConnected(c.Destination),Is.True);
            for(int i=0;i<200;i++)simulation.Tick(.05);
            Assert.That(policy.IsConnected(c.Position),Is.True);Assert.That(warnings,Is.Zero);
        }
        private sealed class BrokenRecoveryPolicy : ICitizenMovementPolicy
        {
            public IEnumerable<TileCoordinate> CandidateTiles=>new[]{new TileCoordinate(1,0),new TileCoordinate(2,0)};
            public bool CanOccupy(CitizenPosition p)=>p.X-.28>=1&&p.X+.28<3&&p.Z-.28>=0&&p.Z+.28<1;
            public bool CanTraverseRecovery(CitizenPosition p,CitizenPosition origin)=>false;
        }
        [Test]
        public void BrokenRouteUsesWarningBearingFallbackAndPreservesIdentity()
        {
            var policy=new BrokenRecoveryPolicy();var c=Adult(new CitizenPosition(.5,.5));
            var simulation=new CitizenSimulation(policy,new[]{c});
            string warning=null;simulation.DevelopmentWarning+=message=>warning=message;
            simulation.ReconcileBuildings();
            Assert.That(warning,Does.Contain("emergency relocation"));
            Assert.That(c.Id,Is.EqualTo("access-test"));Assert.That(c.Attributes.Strength,Is.EqualTo(5));
            Assert.That(policy.CanOccupy(c.Position)&&c.Active,Is.True);
        }
        [Test]
        public void EntranceRejectsInvalidOrientationWithoutMutatingCatalog()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>BuildingEntrance.For(BuildingCatalog.Get(BuildingType.House),new TileCoordinate(0,0),45));
            Assert.Throws<ArgumentNullException>(()=>BuildingEntrance.For(null,new TileCoordinate(0,0),0));
            Assert.That(BuildingCatalog.Get(BuildingType.Farm).BaseWidth,Is.EqualTo(3));
            Assert.That(BuildingCatalog.Get(BuildingType.Farm).BaseDepth,Is.EqualTo(4));
        }
    }
}
