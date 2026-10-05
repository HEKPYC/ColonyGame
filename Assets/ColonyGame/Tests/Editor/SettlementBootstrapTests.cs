using System;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class SettlementBootstrapTests
    {
        private sealed class FixedRandom : IGatheringRandom
        {
            private readonly double value;
            public FixedRandom(double value=.1) { this.value=value; }
            public double NextDouble()=>value;
            public int Next(int count)=>0;
        }
        private static SettlementBootstrap Create(bool single=false,double result=.1)
        {
            var state=SettlementState.CreatePrototype();
            var simulation=CitizenSimulation.CreatePrototype(state);
            if(single) simulation=new CitizenSimulation(new SettlementIdleMovementPolicy(state),new[]{simulation.Citizens[0]});
            var bootstrap=new SettlementBootstrap(state,simulation,new BootstrapSettings(minimumCooldown:10000,maximumCooldown:10000));
            simulation.Gathering=new AmbientWoodGathering(bootstrap,new FixedRandom(result));
            return bootstrap;
        }
        private static void Fund(SettlementBootstrap b)=>b.Resources.TryTransaction("test-materials",ResourceType.Wood,10);
        private static BuildingState Start(SettlementBootstrap b,int angle=0)
        {
            Fund(b);
            Assert.IsTrue(b.TryStartFirstHut(new TileCoordinate(17,8),angle,out var site));
            return site;
        }
        private static BuildingState Complete(SettlementBootstrap b)
        {
            var site=Start(b);
            Assert.IsTrue(b.ContributeWork(site,ConstructionActor.Player,30));
            return site;
        }
        private static void Until(SettlementBootstrap b,Func<bool> done,int steps=2400)
        {
            for(int i=0;i<steps&&!done();i++) b.Simulation.Tick(.25);
            Assert.IsTrue(done(),"Task must reach expected state within finite movement time.");
        }
        [Test] public void InitialResourcesStorageAndPopulationAreExact()
        {
            var b=Create();
            Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood));
            Assert.AreEqual(b.Settlement.TownHall.Id,b.StorageBuildingId);
            Assert.AreEqual(4,b.Roster.Count); Assert.AreEqual(4,b.UnemployedCount); Assert.AreEqual(0,b.EmployedCount);
            Assert.IsFalse(b.Settlement.Buildings.Any(x=>x.Type==BuildingType.BuildersHut));
        }
        [Test] public void InventoryRejectsNegativeOverflowAndConflictingTransactionRetry()
        {
            var inventory=new ResourceInventory();
            Assert.IsFalse(inventory.TryTransaction("empty",ResourceType.Wood,-1));
            Assert.IsTrue(inventory.TryTransaction("supply",ResourceType.Wood,int.MaxValue));
            Assert.IsFalse(inventory.TryTransaction("overflow",ResourceType.Wood,1));
            Assert.IsTrue(inventory.TryTransaction("supply",ResourceType.Wood,int.MaxValue));
            Assert.IsFalse(inventory.TryTransaction("supply",ResourceType.Wood,2));
            Assert.AreEqual(int.MaxValue,inventory.Amount(ResourceType.Wood));
        }
        [Test] public void InsufficientWoodRejectsPlacementWithoutChargeOrReservation()
        {
            var b=Create();
            Assert.IsFalse(b.TryStartFirstHut(new TileCoordinate(17,8),0,out var site));
            Assert.IsNull(site); Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood));
            Assert.AreEqual(1,b.Settlement.Buildings.Count); Assert.IsFalse(b.Settlement.IsOccupied(new TileCoordinate(17,8)));
        }
        [Test] public void InvalidPlacementCannotConsumeMaterials()
        {
            var b=Create(); Fund(b);
            Assert.IsFalse(b.TryStartFirstHut(new TileCoordinate(7,7),0,out _));
            Assert.IsFalse(b.TryStartFirstHut(new TileCoordinate(19,19),0,out _));
            Assert.IsFalse(b.TryStartFirstHut(new TileCoordinate(17,8),45,out _));
            Assert.AreEqual(20,b.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void FirstHutStartsWithoutBuilderChargesExactlyOnceAndReservesNineTiles()
        {
            var b=Create(); var site=Start(b,90);
            Assert.AreEqual(0,b.EmployedCount); Assert.AreEqual(0,b.Resources.Amount(ResourceType.Wood));
            Assert.AreEqual(9,site.OccupiedTiles.Count()); Assert.IsTrue(site.OccupiedTiles.All(b.Settlement.IsOccupied));
            Assert.IsFalse(site.IsCompleted); Assert.AreEqual(ConstructionStage.UnderConstruction,site.ConstructionStage);
            Assert.AreEqual(400,b.Settlement.Territory.Count); Assert.IsFalse(site.TerritoryInfluenceApplied);
            Assert.AreEqual(new TileCoordinate(-1,0),site.Entrance.Facing);
            Assert.IsFalse(b.TryStartFirstHut(new TileCoordinate(2,2),0,out _));
            Assert.AreEqual(0,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(2,b.Settlement.Buildings.Count);
        }
        [Test] public void CostIsTwentyEvenWithExtraStockAndRepeatedInput()
        {
            var b=Create(); b.Resources.TryTransaction("more",ResourceType.Wood,100);
            Assert.IsTrue(b.TryStartFirstHut(new TileCoordinate(17,8),0,out _));
            for(int i=0;i<30;i++) Assert.IsFalse(b.TryStartFirstHut(new TileCoordinate(2,2),0,out _));
            Assert.AreEqual(90,b.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void WorkOnlyProgressesFromExplicitContributionAndResumesAfterPause()
        {
            var b=Create(); var site=Start(b);
            b.Simulation.Tick(10); Assert.AreEqual(0,site.ConstructionWork);
            Assert.IsFalse(b.ContributeWork(site,ConstructionActor.Player,15));
            Assert.AreEqual(.5,site.ConstructionProgress); b.Simulation.Tick(20);
            Assert.AreEqual(15,site.ConstructionWork);
            Assert.IsTrue(b.ContributeWork(site,ConstructionActor.Player,15)); Assert.IsTrue(site.IsCompleted);
        }
        [Test] public void CompletionAppliesIrregularInfluenceOnceAndCapsWork()
        {
            var b=Create(); var site=Start(b);
            Assert.AreEqual(0,b.Settlement.ApplyBuildingInfluence(site));
            Assert.IsTrue(b.ContributeWork(site,ConstructionActor.Player,100));
            Assert.AreEqual(30,site.ConstructionWork); Assert.AreEqual(427,b.Settlement.Territory.Count);
            Assert.IsTrue(b.Settlement.Territory.IsClaimed(new TileCoordinate(22,8)));
            Assert.IsFalse(b.Settlement.Territory.IsClaimed(new TileCoordinate(20,4)));
            Assert.IsFalse(b.ContributeWork(site,ConstructionActor.Player,30));
            Assert.AreEqual(0,b.Settlement.ApplyBuildingInfluence(site)); Assert.AreEqual(427,b.Settlement.Territory.Count);
        }
        [Test] public void WorkRejectsForeignSiteInvalidActorAndInvalidTime()
        {
            var b=Create(); var site=Start(b); var other=Create(); var foreign=Start(other);
            Assert.IsFalse(b.ContributeWork(foreign,ConstructionActor.Player,30));
            Assert.IsFalse(b.ContributeWork(site,ConstructionActor.Builder,30,b.Roster[0]));
            Assert.IsFalse(b.ContributeWork(site,(ConstructionActor)99,30));
            Assert.Throws<ArgumentOutOfRangeException>(()=>b.ContributeWork(site,ConstructionActor.Player,double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>b.ContributeWork(site,ConstructionActor.Player,-1));
        }
        [Test] public void IncompleteOrForeignWorkplaceCannotAssignCitizen()
        {
            var b=Create(); var site=Start(b);
            Assert.IsEmpty(b.EligibleWorkers(site)); Assert.IsFalse(b.TryAssignBuilder(site,b.Roster[0].Id));
            Assert.IsFalse(b.TryAssignBuilder(b.Settlement.TownHall,b.Roster[0].Id));
            Assert.IsFalse(b.TryAssignBuilder(Complete(Create()),b.Roster[0].Id));
            Assert.AreEqual(0,b.EmployedCount);
        }
        [Test] public void ExplicitAssignmentPreservesIdentityAllAttributesAndUnselectedCitizens()
        {
            var b=Create(); var site=Complete(b); var citizen=b.Roster[2];
            string id=citizen.Id,name=citizen.FirstName; var sex=citizen.Sex; int age=citizen.Age; var attributes=citizen.Attributes;
            Assert.AreEqual(4,b.EligibleWorkers(site).Count()); Assert.IsTrue(b.TryAssignBuilder(site,id));
            Assert.AreSame(citizen,b.WorkerAt(site)); Assert.AreEqual(CitizenProfession.Builder,citizen.Profession);
            Assert.AreEqual(site.Id,citizen.WorkplaceBuildingId); Assert.AreEqual("Builder's Hut",b.WorkplaceName(citizen));
            Assert.AreEqual(id,citizen.Id); Assert.AreEqual(name,citizen.FirstName); Assert.AreEqual(sex,citizen.Sex);
            Assert.AreEqual(age,citizen.Age); Assert.AreSame(attributes,citizen.Attributes);
            Assert.IsTrue(b.Roster.Where(c=>c!=citizen).All(c=>c.Profession==CitizenProfession.Unemployed&&c.WorkplaceBuildingId==null));
        }
        [Test] public void WorkplaceHasOneAuthoritativeWorkerAndRejectsDuplicateOrUnknownAssignment()
        {
            var b=Create(); var site=Complete(b);
            Assert.IsFalse(b.TryAssignBuilder(site,"missing")); Assert.IsTrue(b.TryAssignBuilder(site,b.Roster[1].Id));
            Assert.IsFalse(b.TryAssignBuilder(site,b.Roster[0].Id)); Assert.IsEmpty(b.EligibleWorkers(site));
            Assert.AreEqual(1,b.EmployedCount); Assert.AreEqual(3,b.UnemployedCount);
        }
        [Test] public void RosterUsesActualUniqueDomainCitizensAndReflectsLiveAssignment()
        {
            var b=Create(); Assert.AreSame(b.Simulation.Citizens,b.Roster);
            Assert.AreEqual(4,b.Roster.Select(c=>c.Id).Distinct().Count());
            foreach(var c in b.Roster)
            {
                var actual=b.Simulation.Citizens.Single(x=>x.Id==c.Id);
                Assert.AreSame(actual,c); Assert.AreSame(actual.Attributes,c.Attributes);
                Assert.AreEqual(actual.FirstName,c.FirstName); Assert.AreEqual(actual.Sex,c.Sex); Assert.AreEqual(actual.Age,c.Age);
                Assert.AreEqual(actual.Attributes.Strength,c.Attributes.Strength); Assert.AreEqual(actual.Attributes.Dexterity,c.Attributes.Dexterity);
                Assert.AreEqual(actual.Attributes.Intelligence,c.Attributes.Intelligence); Assert.AreEqual(actual.Attributes.Endurance,c.Attributes.Endurance);
                Assert.AreEqual(actual.Attributes.Charisma,c.Attributes.Charisma);
            }
            var site=Complete(b); b.TryAssignBuilder(site,b.Roster[3].Id);
            Assert.AreEqual(CitizenProfession.Builder,b.Roster[3].Profession); Assert.AreEqual(site.Id,b.Roster[3].WorkplaceBuildingId);
        }
        [Test] public void EligibilityRequiresUnemployedAdultAndBuilderCannotStartTrip()
        {
            var b=Create(); Assert.IsTrue(AmbientWoodGathering.IsEligible(b.Roster[0]));
            var child=new Citizen("child","Future",CitizenSex.Male,17,b.Roster[0].Attributes,b.Roster[0].Position);
            Assert.IsFalse(AmbientWoodGathering.IsEligible(child));
            var site=Complete(b); b.TryAssignBuilder(site,b.Roster[0].Id);
            Assert.IsFalse(AmbientWoodGathering.IsEligible(b.Roster[0])); Assert.IsFalse(b.Simulation.Gathering.TryBegin(b.Roster[0]));
        }
        [Test] public void TaskAreaUsesCurrentIrregularClaimsAndInclusiveTenTileLimit()
        {
            var b=Create(); var area=b.Simulation.Gathering.Area;
            Assert.AreEqual(1,area.Depth(new TileCoordinate(20,8))); Assert.AreEqual(10,area.Depth(new TileCoordinate(29,8)));
            Assert.AreEqual(int.MaxValue,area.Depth(new TileCoordinate(30,8)));
            b.Settlement.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            Assert.AreEqual(1,area.Depth(new TileCoordinate(23,8))); Assert.AreEqual(10,area.Depth(new TileCoordinate(32,8)));
            Assert.AreEqual(1,area.Depth(new TileCoordinate(20,4))); // bounding rectangle would claim this incorrectly
            Assert.AreEqual(int.MaxValue,area.Depth(new TileCoordinate(33,8)));
        }
        [Test] public void GatheringAuthorizationDoesNotChangeOrdinaryPolicy()
        {
            var b=Create(); var p=CitizenPosition.Center(new TileCoordinate(20,8));
            Assert.IsTrue(b.Simulation.Gathering.Area.CanOccupy(p));
            Assert.IsFalse(new SettlementIdleMovementPolicy(b.Settlement).CanOccupy(p));
            Assert.IsFalse(b.Simulation.Gathering.Area.CanOccupy(CitizenPosition.Center(new TileCoordinate(30,8))));
        }
        [Test] public void GatheringChoosesReachableWildernessAndRecordsNoEarlyWood()
        {
            var b=Create(true); var c=b.Roster[0]; Assert.IsTrue(b.Simulation.Gathering.TryBegin(c));
            Assert.AreEqual(CitizenTask.GatheringWoodOutbound,c.Task);
            Assert.IsFalse(b.Settlement.Territory.IsClaimed(c.Destination.Tile));
            Assert.LessOrEqual(b.Simulation.Gathering.Area.Depth(c.Destination.Tile),10);
            Until(b,()=>c.Task==CitizenTask.ReturningWood);
            Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(2,b.Simulation.Gathering.PendingWood(c));
            Assert.AreEqual(1,b.Simulation.Gathering.SuccessRolls);
            Assert.AreEqual(CitizenPosition.Center(b.Settlement.TownHall.Entrance.ExteriorTile).Tile,c.Destination.Tile);
        }
        [Test] public void SuccessfulReturnDepositsExactlyTwoOnceAndRestoresOrdinaryMovement()
        {
            var b=Create(true); var c=b.Roster[0]; Assert.IsTrue(b.Simulation.Gathering.TryBegin(c));
            Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(12,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(1,b.Simulation.Gathering.SuccessRolls);
            Assert.AreEqual(0,b.Simulation.Gathering.PendingWood(c));
            Assert.Less(c.Position.DistanceSquared(CitizenPosition.Center(b.Settlement.TownHall.Entrance.ExteriorTile)),1e-10);
            for(int i=0;i<100;i++) b.Simulation.Tick(.25);
            Assert.AreEqual(12,b.Resources.Amount(ResourceType.Wood));
            Assert.IsTrue(new SettlementIdleMovementPolicy(b.Settlement).CanOccupy(c.Position));
        }
        [Test] public void FailedTripRollsOnceReturnsAndDepositsZero()
        {
            var b=Create(true,.95); var c=b.Roster[0]; b.Simulation.Gathering.TryBegin(c);
            Until(b,()=>c.Task==CitizenTask.ReturningWood);
            Assert.AreEqual(0,b.Simulation.Gathering.PendingWood(c)); Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood));
            Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(1,b.Simulation.Gathering.SuccessRolls);
        }
        [Test] public void InjectedRandomnessProducesRepeatableRoutesAndResults()
        {
            var a=Create(true); var b=Create(true); a.Simulation.Gathering.TryBegin(a.Roster[0]); b.Simulation.Gathering.TryBegin(b.Roster[0]);
            Assert.AreEqual(a.Roster[0].Destination.Tile,b.Roster[0].Destination.Tile);
            for(int i=0;i<400;i++) { a.Simulation.Tick(.25); b.Simulation.Tick(.25); }
            Assert.AreEqual(a.Roster[0].Position.X,b.Roster[0].Position.X); Assert.AreEqual(a.Roster[0].Position.Z,b.Roster[0].Position.Z);
            Assert.AreEqual(a.Resources.Amount(ResourceType.Wood),b.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void AssignmentWhileReturningPreservesPendingDeliveryThenStopsAmbientWork()
        {
            var b=Create(true); var site=Complete(b); var c=b.Roster[0]; b.Simulation.Gathering.TryBegin(c);
            Until(b,()=>c.Task==CitizenTask.ReturningWood); Assert.AreEqual(2,b.Simulation.Gathering.PendingWood(c));
            b.TryAssignBuilder(site,c.Id); Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(2,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(CitizenProfession.Builder,c.Profession);
            for(int i=0;i<200;i++) b.Simulation.Tick(.25);
            Assert.IsFalse(b.Simulation.Gathering.HasTrip(c)); Assert.AreEqual(1,b.Simulation.Gathering.SuccessRolls);
        }
        [Test] public void AssignmentDuringOutboundCancelsUnrolledGatheringAndReturnsSafely()
        {
            var b=Create(true); var site=Complete(b); var c=b.Roster[0]; b.Simulation.Gathering.TryBegin(c);
            b.TryAssignBuilder(site,c.Id); Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(0,b.Simulation.Gathering.SuccessRolls); Assert.AreEqual(0,b.Resources.Amount(ResourceType.Wood));
            Assert.IsTrue(new SettlementIdleMovementPolicy(b.Settlement).CanOccupy(c.Position));
        }
        [Test] public void ConstructionOverTaskCitizenYieldsToContinuousRecovery()
        {
            var b=Create(true); var c=b.Roster[0]; b.Simulation.Gathering.TryBegin(c);
            var position=c.Position;
            Assert.IsTrue(b.Settlement.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(4,4),0,out _));
            b.Simulation.ReconcileBuildings();
            Assert.AreEqual(position.X,c.Position.X); Assert.AreEqual(position.Z,c.Position.Z);
            Assert.AreEqual(CitizenTask.Recovery,c.Task);
            Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void BlockedHallEntranceUsesReachableOutdoorAccessAndStillDeliversOnce()
        {
            var b=Create(true); var c=b.Roster[0];
            Assert.IsTrue(b.Settlement.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(8,4),0,out _));
            Assert.IsFalse(b.Settlement.TownHall.Entrance.IsExteriorAccessible(b.Settlement));
            Assert.IsTrue(b.Simulation.Gathering.TryBegin(c));
            Until(b,()=>c.Task==CitizenTask.ReturningWood);
            Assert.AreNotEqual(b.Settlement.TownHall.Entrance.ExteriorTile,c.Destination.Tile);
            Assert.IsTrue(b.Settlement.Territory.IsClaimed(c.Destination.Tile));
            Assert.IsFalse(b.Settlement.IsOccupied(c.Destination.Tile));
            Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(12,b.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void NewlyClaimedGatheringDestinationCancelsWithoutAwardOrTeleport()
        {
            var b=Create(true); var c=b.Roster[0]; Assert.IsTrue(b.Simulation.Gathering.TryBegin(c));
            var destination=c.Destination.Tile;
            // Grow exactly the chosen Wilderness area using existing fixture placement.
            var origin=new TileCoordinate(destination.X<0?0:destination.X>=20?17:Math.Max(0,Math.Min(17,destination.X)),
                destination.Z<0?0:destination.Z>=20?17:Math.Max(0,Math.Min(17,destination.Z)));
            Assert.IsTrue(b.Settlement.TryPlacePrototypeBuilding(BuildingType.House,origin,0,out _));
            Assert.IsTrue(b.Settlement.Territory.IsClaimed(destination));
            var position=c.Position; b.Simulation.ReconcileBuildings();
            Assert.AreEqual(position.X,c.Position.X); Assert.AreEqual(position.Z,c.Position.Z);
            Assert.AreEqual(CitizenTask.ReturningWood,c.Task);
            Until(b,()=>!b.Simulation.Gathering.HasTrip(c));
            Assert.AreEqual(0,b.Simulation.Gathering.SuccessRolls); Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void CentralBalanceMatchesApprovedValuesAndRejectsInvalidConfiguration()
        {
            var s=new BootstrapSettings(); Assert.AreEqual(10,s.StartingWood); Assert.AreEqual(20,s.HutWoodCost);
            Assert.AreEqual(2,s.WoodYield); Assert.AreEqual(.9,s.SuccessChance); Assert.AreEqual(10,s.WildernessDepth);
            Assert.AreEqual(30,s.PlayerWorkSeconds); Assert.AreEqual(4,s.WildlifeDetectionRadius);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new BootstrapSettings(successChance:1.1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new BootstrapSettings(playerWorkSeconds:double.PositiveInfinity));
        }
    }
}