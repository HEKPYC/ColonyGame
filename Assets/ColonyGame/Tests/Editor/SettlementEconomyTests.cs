using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class SettlementEconomyTests
    {
        private sealed class FixedRandom : IGatheringRandom
        {
            private readonly double value;
            private readonly bool high;
            public FixedRandom(double value=.05,bool high=false) {this.value=value;this.high=high;}
            public double NextDouble()=>value;
            public int Next(int count)=>high?count-1:0;
        }
        private static SettlementEconomy Create(int level=1,IGatheringRandom random=null)
        {
            var state=SettlementState.CreatePrototype(level);
            return new SettlementEconomy(new SettlementBootstrap(state,CitizenSimulation.CreatePrototype(state)),random??new FixedRandom(.9));
        }
        private static void Supply(SettlementEconomy economy,int amount=10000)
        {
            foreach(ResourceType resource in Enum.GetValues(typeof(ResourceType)))
                economy.Bootstrap.Resources.TryTransaction("test-"+resource,resource,amount);
        }
        private static BuildingState Fixture(SettlementEconomy economy,BuildingType type,int x=1,int z=1)
        {
            Assert.IsTrue(economy.Bootstrap.Settlement.TryPlacePrototypeBuilding(type,new TileCoordinate(x,z),0,out var building));
            economy.Residences.Reconcile();return building;
        }
        private static BuildingState Builder(SettlementEconomy economy,int level=1)
        {
            var hut=Fixture(economy,BuildingType.BuildersHut,15,15);
            Assert.IsTrue(economy.Bootstrap.TryAssignBuilder(hut,economy.Bootstrap.Roster[0].Id));
            for(int i=1;i<level;i++) Upgrade(economy,hut);
            return hut;
        }
        private static void Upgrade(SettlementEconomy economy,BuildingState building)
        {
            Assert.IsTrue(economy.TryUpgrade(building));
            var job=economy.Jobs.Single(j=>j.Building==building);
            Assert.IsTrue(economy.ContributeWork(building,ConstructionActor.Builder,100000,job.Builder));
        }
        private static int Cost(BuildingType type,int level,ResourceType resource)=>
            BuildingEconomyRules.Cost(type,level).TryGetValue(resource,out int value)?value:0;
        [Test] public void RuntimeStartsAtTH1WithFourTownHallResidentsAndTenWood()
        {
            var e=Create();
            Assert.AreEqual(1,e.Bootstrap.Settlement.TownHallLevel);
            Assert.AreEqual(10,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.AreEqual(400,e.Bootstrap.Settlement.Territory.Count);
            Assert.AreEqual(4,e.Residences.Capacity);
            Assert.AreEqual(0,e.Residences.Homeless);
            Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.ResidenceBuildingId==e.Bootstrap.Settlement.TownHall.Id));
        }
        [Test] public void TownHallHasFourExactAtomicCostsAndStopsAtFive()
        {
            var e=Create();
            int[] wood={150,300,450,600},wool={0,100,250,350},stone={0,0,150,250},iron={0,0,0,100};
            for(int target=2;target<=5;target++)
            {
                var cost=BuildingEconomyRules.TownHallCost(target);
                Assert.AreEqual(wood[target-2],Cost(BuildingType.TownHall,target,ResourceType.Wood));
                Assert.AreEqual(wool[target-2],Cost(BuildingType.TownHall,target,ResourceType.Wool));
                Assert.AreEqual(stone[target-2],Cost(BuildingType.TownHall,target,ResourceType.Stone));
                Assert.AreEqual(iron[target-2],Cost(BuildingType.TownHall,target,ResourceType.Iron));
                Assert.IsFalse(e.TryUpgradeTownHall());
                foreach(var item in cost)
                    e.Bootstrap.Resources.TryTransaction("supply-"+target+"-"+item.Key,item.Key,item.Value);
                int before=e.Bootstrap.Resources.Amount(ResourceType.Wood);
                Assert.IsTrue(e.TryUpgradeTownHall());Assert.AreEqual(target,e.Bootstrap.Settlement.TownHallLevel);
                Assert.AreEqual(before-wood[target-2],e.Bootstrap.Resources.Amount(ResourceType.Wood));
            }
            Assert.IsFalse(e.TryUpgradeTownHall());Assert.AreEqual(8,e.Residences.Capacity);
        }
        [Test] public void LevelFiveRetainsAllThirteenTypesWithoutNewUnlocks()
        {
            Assert.AreEqual(13,Create(5).Bootstrap.Settlement.AvailableBuildings.Count());
            CollectionAssert.AreEquivalent(BuildingCatalog.AvailableAt(4),BuildingCatalog.AvailableAt(5));
        }
        [Test] public void EveryInitialBuildingRecipeMatchesApproval()
        {
            BuildingType[] types={BuildingType.BuildersHut,BuildingType.House,BuildingType.Farm,BuildingType.Tavern,BuildingType.Butcher,BuildingType.FishermansHut,BuildingType.GuardTower,BuildingType.Warehouse,BuildingType.LumberMill,BuildingType.Mine,BuildingType.Barracks,BuildingType.Laboratory,BuildingType.MageTower};
            int[] wood={25,25,50,100,150,100,75,150,200,250,150,300,250};
            int[] wool={0,0,0,0,0,0,0,25,100,50,100,150,100};
            int[] stone={0,0,0,0,0,0,0,0,0,0,0,100,50};
            int[] iron={0,0,0,0,0,0,0,0,0,0,0,50,100};
            for(int i=0;i<types.Length;i++)
            {
                Assert.AreEqual(wood[i],Cost(types[i],1,ResourceType.Wood),types[i].ToString());
                Assert.AreEqual(wool[i],Cost(types[i],1,ResourceType.Wool));
                Assert.AreEqual(stone[i],Cost(types[i],1,ResourceType.Stone));
                Assert.AreEqual(iron[i],Cost(types[i],1,ResourceType.Iron));
            }
        }
        [Test] public void BuildersHutUsesStandardWoodCurveAndBothDeferredExceptions()
        {
            int[,] expected={{25,0,0,0},{50,0,0,0},{75,50,25,0},{100,75,50,0},{150,100,75,50}};
            var resources=new[]{ResourceType.Wood,ResourceType.Wool,ResourceType.Stone,ResourceType.Iron};
            for(int level=1;level<=5;level++) for(int r=0;r<resources.Length;r++)
                Assert.AreEqual(expected[level-1,r],Cost(BuildingType.BuildersHut,level,resources[r]));
        }
        [Test] public void HouseExactCostTableIntroducesWoolStoneAndIron()
        {
            int[,] expected={{25,0,0,0},{50,25,0,0},{75,50,25,0},{100,75,50,25},{150,100,75,50}};
            var resources=new[]{ResourceType.Wood,ResourceType.Wool,ResourceType.Stone,ResourceType.Iron};
            for(int level=1;level<=5;level++) for(int r=0;r<4;r++) Assert.AreEqual(expected[level-1,r],Cost(BuildingType.House,level,resources[r]));
        }
        [Test] public void WarehouseExactCostTableKeepsExistingWoolCurve()
        {
            int[,] expected={{150,25,0,0},{200,50,0,0},{250,75,25,0},{300,100,50,25},{350,150,75,50}};
            var resources=new[]{ResourceType.Wood,ResourceType.Wool,ResourceType.Stone,ResourceType.Iron};
            for(int level=1;level<=5;level++) for(int r=0;r<4;r++) Assert.AreEqual(expected[level-1,r],Cost(BuildingType.Warehouse,level,resources[r]));
        }
        [Test] public void StandardComponentCurveSwitchesFromTwentyFiveToFiftyAtOneHundred()
        {
            Assert.AreEqual(50,BuildingEconomyRules.NextAmount(25));Assert.AreEqual(75,BuildingEconomyRules.NextAmount(50));
            Assert.AreEqual(100,BuildingEconomyRules.NextAmount(75));Assert.AreEqual(150,BuildingEconomyRules.NextAmount(100));
            Assert.AreEqual(200,BuildingEconomyRules.NextAmount(150));
            Assert.Throws<ArgumentOutOfRangeException>(()=>BuildingEconomyRules.Cost(BuildingType.House,6));
            Assert.Throws<ArgumentOutOfRangeException>(()=>BuildingEconomyRules.TownHallCost(0));
        }
        [Test] public void ButcherDefersWoolOnceWithoutRestartingItsCurve()
        {
            Assert.AreEqual(0,Cost(BuildingType.Butcher,2,ResourceType.Wool));
            Assert.AreEqual(50,Cost(BuildingType.Butcher,3,ResourceType.Wool));
            Assert.AreEqual(75,Cost(BuildingType.Butcher,4,ResourceType.Wool));
            Assert.AreEqual(100,Cost(BuildingType.Butcher,5,ResourceType.Wool));
            var wool=ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Wool);
            Assert.AreEqual(2,wool.UnlockLevel);Assert.AreEqual(45,wool.Seconds);
        }
        [Test] public void MineDefersIronOnceWithoutRestartingItsCurve()
        {
            Assert.AreEqual(0,Cost(BuildingType.Mine,4,ResourceType.Iron));
            Assert.AreEqual(50,Cost(BuildingType.Mine,5,ResourceType.Iron));
            var iron=ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Iron);
            Assert.AreEqual(4,iron.UnlockLevel);Assert.AreEqual(60,iron.Seconds);
        }
        [Test] public void ResourceDependencyPathHasNoProducerDeadlock()
        {
            var obtainable=new HashSet<ResourceType>{ResourceType.Wood};
            Action<BuildingType,int> check=(type,level)=>Assert.IsTrue(BuildingEconomyRules.Cost(type,level).Keys.All(obtainable.Contains),type+" target "+level);
            check(BuildingType.BuildersHut,1);check(BuildingType.TownHall,2);check(BuildingType.BuildersHut,2);
            check(BuildingType.Butcher,1);check(BuildingType.Butcher,2);obtainable.Add(ResourceType.Wool);
            check(BuildingType.TownHall,3);check(BuildingType.Mine,1);obtainable.Add(ResourceType.Stone);
            check(BuildingType.BuildersHut,3);check(BuildingType.Mine,2);check(BuildingType.Mine,3);
            check(BuildingType.TownHall,4);check(BuildingType.BuildersHut,4);check(BuildingType.Mine,4);obtainable.Add(ResourceType.Iron);
            check(BuildingType.TownHall,5);check(BuildingType.BuildersHut,5);
            foreach(var type in BuildingCatalog.All.Where(b=>b.IsPlayerPlaceable).Select(b=>b.Type)) for(int level=1;level<=5;level++) check(type,level);
        }
        [Test] public void AtomicBundleNeverPartiallyPaysAndMatchingRetryChargesOnce()
        {
            var inventory=new ResourceInventory();inventory.TryTransaction("stock",ResourceType.Wood,100);
            var cost=new Dictionary<ResourceType,int>{{ResourceType.Wood,25},{ResourceType.Wool,25}};
            Assert.IsFalse(inventory.TryPay("payment",cost));Assert.AreEqual(100,inventory.Amount(ResourceType.Wood));
            inventory.TryTransaction("wool",ResourceType.Wool,25);
            Assert.IsTrue(inventory.TryPay("payment",cost));Assert.IsTrue(inventory.TryPay("payment",cost));
            Assert.AreEqual(75,inventory.Amount(ResourceType.Wood));Assert.AreEqual(0,inventory.Amount(ResourceType.Wool));
        }
        [Test] public void PlayerMayBuildSeveralLevelOneHutsWithoutAnyBuilder()
        {
            var e=Create();Supply(e);
            Assert.IsTrue(e.TryBuild(BuildingType.BuildersHut,new TileCoordinate(1,1),0,out var first));
            Assert.IsTrue(e.TryBuild(BuildingType.BuildersHut,new TileCoordinate(15,15),0,out var second));
            Assert.AreEqual(300,first.RequiredConstructionWork);
            Assert.IsTrue(e.ContributeWork(first,ConstructionActor.Player,300));
            Assert.IsTrue(e.ContributeWork(second,ConstructionActor.Player,300));
            Assert.IsFalse(e.TryBuild(BuildingType.House,new TileCoordinate(1,15),0,out _));
        }
        [Test] public void OwnHutUpgradeIsAllowedButPlayerUpgradeAndUnderlevelOtherUpgradeAreRejected()
        {
            var e=Create(2);Supply(e);var hut=Builder(e);
            Assert.IsTrue(e.TryUpgrade(hut));Assert.IsFalse(e.ContributeWork(hut,ConstructionActor.Player,10000));
            Assert.IsTrue(e.ContributeWork(hut,ConstructionActor.Builder,10000,e.Bootstrap.WorkerAt(hut)));
            Assert.AreEqual(2,hut.Level);
            var house=Fixture(e,BuildingType.House);Assert.IsTrue(e.TryUpgrade(house));
            Assert.IsFalse(e.TryUpgrade(house));Assert.IsFalse(e.TryUpgrade(hut));
        }
        [Test] public void OtherBuildingUpgradeRequiresBuilderHutAtTargetLevel()
        {
            var e=Create(3);Supply(e);Builder(e);var house=Fixture(e,BuildingType.House);
            Assert.IsFalse(e.TryUpgrade(house));Assert.AreEqual(1,house.Level);
        }
        [Test] public void BuilderIsExclusiveToOneJobAndParallelBuildersHaveSeparateJobs()
        {
            var e=Create(2);Supply(e);Builder(e);
            Assert.IsTrue(e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out _));
            Assert.IsFalse(e.TryBuild(BuildingType.Farm,new TileCoordinate(1,15),0,out _));
            var other=Fixture(e,BuildingType.BuildersHut,15,1);e.Bootstrap.TryAssignBuilder(other,e.Bootstrap.Roster[1].Id);
            Assert.IsTrue(e.TryBuild(BuildingType.Farm,new TileCoordinate(1,15),0,out _));
            Assert.AreEqual(2,e.Jobs.Count);Assert.AreNotSame(e.Jobs[0].Builder,e.Jobs[1].Builder);
        }
        [Test] public void EveryBaseConstructionDurationMatchesApprovedMinutes()
        {
            int[] seconds={0,300,300,360,480,480,420,360,600,720,720,600,900,1080};
            foreach(BuildingType type in Enum.GetValues(typeof(BuildingType))) Assert.AreEqual(seconds[(int)type],BuildingEconomyRules.BaseSeconds(type),type.ToString());
        }
        [Test] public void UpgradeWorkUsesTargetLevelMultiplierWithoutChangingCapacityBeforeCompletion()
        {
            var e=Create(5);Supply(e);var hut=Builder(e,5);var house=Fixture(e,BuildingType.House);
            for(int target=2;target<=5;target++)
            {
                int oldCapacity=BuildingEconomyRules.ResidenceCapacity(house);
                Assert.IsTrue(e.TryUpgrade(house));Assert.AreEqual(300*target,house.RequiredConstructionWork);
                Assert.AreEqual(oldCapacity,BuildingEconomyRules.ResidenceCapacity(house));
                Assert.IsTrue(e.ContributeWork(house,ConstructionActor.Builder,10000,e.Bootstrap.WorkerAt(hut)));
                Assert.AreEqual(target,house.Level);
            }
        }
        [Test] public void BuilderEfficiencyUsesOnlyThreeApprovedAttributesAndLevelAdvantage()
        {
            var citizen=new Citizen("test","Test",CitizenSex.Male,20,new CitizenAttributes(5,5,1,5,10),new CitizenPosition(.5,.5));
            Assert.AreEqual(1,BuildingEconomyRules.BuilderEfficiency(citizen,1,1),1e-9);
            Assert.AreEqual(1.6,BuildingEconomyRules.BuilderEfficiency(citizen,4,1),1e-9);
            var weak=new Citizen("weak","Weak",CitizenSex.Male,20,new CitizenAttributes(1,1,10,1,1),citizen.Position);
            var strong=new Citizen("strong","Strong",CitizenSex.Female,20,new CitizenAttributes(10,10,1,10,1),citizen.Position);
            Assert.AreEqual(.92,BuildingEconomyRules.BuilderEfficiency(weak,1,1),1e-9);
            Assert.AreEqual(1.1,BuildingEconomyRules.BuilderEfficiency(strong,1,1),1e-9);
        }
        [Test] public void BuilderWorkStopsAtNightAndKeepsProgress()
        {
            var e=Create(2);Supply(e);var hut=Builder(e);e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var house);
            e.ContributeWork(house,ConstructionActor.Builder,30,e.Bootstrap.WorkerAt(hut));double before=house.ConstructionWork;
            e.Clock.Advance(420);Assert.IsFalse(e.ContributeWork(house,ConstructionActor.Builder,100,e.Bootstrap.WorkerAt(hut)));
            Assert.AreEqual(before,house.ConstructionWork);e.Clock.Advance(180);
            e.ContributeWork(house,ConstructionActor.Builder,30,e.Bootstrap.WorkerAt(hut));Assert.Greater(house.ConstructionWork,before);
        }
        [Test] public void HousingCapacitiesScaleAcrossAllFiveLevels()
        {
            for(int level=1;level<=5;level++) Assert.AreEqual(level+3,Create(level).Residences.Capacity);
            var e=Create(5);Supply(e);Builder(e,5);var house=Fixture(e,BuildingType.House);var tavern=Fixture(e,BuildingType.Tavern,1,14);
            for(int level=1;level<=5;level++)
            {
                Assert.AreEqual(level+1,BuildingEconomyRules.ResidenceCapacity(house));
                Assert.AreEqual(level+2,BuildingEconomyRules.ResidenceCapacity(tavern));
                if(level<5) {Upgrade(e,house);Upgrade(e,tavern);}
            }
        }
        [Test] public void ResidenceAssignmentChecksCapacityAndPreservesOutdoorPosition()
        {
            var e=Create();var house=Fixture(e,BuildingType.House);
            var position=e.Bootstrap.Roster[0].Position;
            Assert.IsTrue(e.Residences.Assign(e.Bootstrap.Roster[0],house));
            Assert.IsTrue(e.Residences.Assign(e.Bootstrap.Roster[1],house));
            Assert.IsFalse(e.Residences.Assign(e.Bootstrap.Roster[2],house));
            Assert.AreEqual(position.X,e.Bootstrap.Roster[0].Position.X);Assert.AreEqual(position.Z,e.Bootstrap.Roster[0].Position.Z);
        }
        [Test] public void ClockHasSevenMinuteDayThreeMinuteNightAndFiveDebugSpeeds()
        {
            var clock=new SettlementClock();Assert.AreEqual(1,clock.Day);Assert.IsTrue(clock.IsDay);
            clock.Advance(420);Assert.IsFalse(clock.IsDay);Assert.AreEqual(0,clock.PhaseProgress,1e-9);
            clock.Advance(180);Assert.AreEqual(2,clock.Day);Assert.IsTrue(clock.IsDay);
            foreach(int speed in new[]{1,2,4,8,16}) {clock.SetSpeed(speed);Assert.AreEqual(speed,clock.Speed);}
            Assert.Throws<ArgumentOutOfRangeException>(()=>clock.SetSpeed(3));
        }
        [Test] public void SpeedScalesSimulationTimeAndCitizensSleepThenExitThroughFreeAccess()
        {
            var e=Create();e.Clock.SetSpeed(16);e.Advance(1);Assert.AreEqual(16,e.Clock.Elapsed,1e-7);
            e.Clock.Advance(404);e.Advance(10);
            Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.InsideBuildingId==e.Bootstrap.Settlement.TownHall.Id));
            e.Advance(2);
            Assert.IsTrue(e.Clock.IsDay);Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.InsideBuildingId==null));
            var policy=new SettlementIdleMovementPolicy(e.Bootstrap.Settlement);
            Assert.IsTrue(e.Bootstrap.Roster.All(c=>policy.CanOccupy(c.Position)));
        }
        [Test] public void ProductionTableAndQuantitiesMatchEveryApprovedResource()
        {
            var expected=new[]{
                "Farm:Beetroot:1:15","Farm:Wheat:2:30","Farm:Potato:3:45","Farm:Carrot:4:60",
                "Butcher:Meat:1:30","Butcher:Wool:2:45","FishermansHut:Fish:1:30","LumberMill:Wood:1:30",
                "Mine:Stone:1:45","Mine:Iron:4:60"};
            CollectionAssert.AreEquivalent(expected,ResourceCatalog.Production.Select(p=>$"{p.Building}:{p.Resource}:{p.UnlockLevel}:{p.Seconds}"));
            foreach(var recipe in ResourceCatalog.Production) for(int level=1;level<=5;level++)
                Assert.AreEqual(Math.Max(0,level-recipe.UnlockLevel+1),recipe.Quantity(level));
            CollectionAssert.AreEqual(new[]{5,4,3,2},ResourceCatalog.Production.Where(p=>p.Building==BuildingType.Farm).Select(p=>p.Quantity(5)));
        }
        [Test] public void ProductionRequiresAssignedWorkerAndIndependentFarmsKeepSeparateCycles()
        {
            var e=Create();var farm=Fixture(e,BuildingType.Farm);
            e.Advance(31);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Beetroot));
            e.Bootstrap.TryAssignWorker(farm,e.Bootstrap.Roster[0].Id);
            e.Advance(45);Assert.Greater(e.Bootstrap.Resources.Amount(ResourceType.Beetroot),0);
            var second=Fixture(e,BuildingType.Farm,1,14);e.Bootstrap.TryAssignWorker(second,e.Bootstrap.Roster[1].Id);
            e.Advance(50);int before=e.Bootstrap.Resources.Amount(ResourceType.Beetroot);e.Advance(30);
            Assert.AreEqual(4,e.Bootstrap.Resources.Amount(ResourceType.Beetroot)-before);
        }
        [Test] public void ProductionPausesAtNightAndPreservesPartialCycle()
        {
            var state=SettlementState.CreatePrototype();
            state.TryPlacePrototypeBuilding(BuildingType.Farm,new TileCoordinate(1,1),0,out var farm);
            var citizen=new Citizen("farmer","Farmer",CitizenSex.Female,20,new CitizenAttributes(5,5,5,5,5),CitizenPosition.Center(farm.Entrance.ExteriorTile));
            var bootstrap=new SettlementBootstrap(state,new CitizenSimulation(new SettlementIdleMovementPolicy(state),new[]{citizen}));
            var e=new SettlementEconomy(bootstrap);bootstrap.TryAssignWorker(farm,citizen.Id);
            e.Advance(10);Assert.AreEqual(0,bootstrap.Resources.Amount(ResourceType.Beetroot));
            e.Clock.Advance(410);e.Advance(180);Assert.AreEqual(0,bootstrap.Resources.Amount(ResourceType.Beetroot));
            // Travel from Town Hall before resuming the retained 10-second cycle.
            e.Advance(60);Assert.GreaterOrEqual(bootstrap.Resources.Amount(ResourceType.Beetroot),1);
        }
        [Test] public void ResourceAvailabilityRequiresActualProducerAtItsUnlockLevel()
        {
            var e=Create(5);Assert.IsFalse(ResourceCatalog.Obtainable(e.Bootstrap.Settlement,ResourceType.Iron));
            Supply(e);Builder(e,5);var mine=Fixture(e,BuildingType.Mine);
            Assert.IsTrue(ResourceCatalog.Obtainable(e.Bootstrap.Settlement,ResourceType.Stone));
            Assert.IsFalse(ResourceCatalog.Obtainable(e.Bootstrap.Settlement,ResourceType.Iron));
            Upgrade(e,mine);Upgrade(e,mine);Upgrade(e,mine);
            Assert.IsTrue(ResourceCatalog.Obtainable(e.Bootstrap.Settlement,ResourceType.Iron));
        }
        [Test] public void NoTavernMakesNoRollOrCandidate()
        {
            var e=Create(2,new FixedRandom());e.Recruitment.ProcessDay(5);
            Assert.AreEqual(0,e.Recruitment.ArrivalRolls);Assert.IsEmpty(e.Recruitment.Candidates);
        }
        [Test] public void EveryCompletedTavernRollsIndependentlyExactlyOncePerCompletedDay()
        {
            var e=Create(2,new FixedRandom());Fixture(e,BuildingType.Tavern);Fixture(e,BuildingType.Tavern,1,14);
            e.Recruitment.ProcessDay(1);Assert.AreEqual(0,e.Recruitment.ArrivalRolls);
            e.Recruitment.ProcessDay(2);Assert.AreEqual(2,e.Recruitment.ArrivalRolls);Assert.AreEqual(2,e.Recruitment.Candidates.Count);
            e.Recruitment.ProcessDay(2);Assert.AreEqual(2,e.Recruitment.ArrivalRolls);
            Assert.AreEqual(4,e.Bootstrap.Roster.Count);Assert.AreNotEqual(e.Recruitment.Candidates[0].TavernId,e.Recruitment.Candidates[1].TavernId);
        }
        [Test] public void FailedTavernRollDoesNotAttractVisitor()
        {
            var e=Create(2,new FixedRandom(.1));Fixture(e,BuildingType.Tavern);e.Recruitment.ProcessDay(2);
            Assert.AreEqual(.1,TavernRecruitment.ArrivalChance);Assert.IsEmpty(e.Recruitment.Candidates);
        }
        [Test] public void CandidatesExpireAfterThreeFullDaysAndAreNotRetainedInHistory()
        {
            var e=Create(2,new FixedRandom());Fixture(e,BuildingType.Tavern);e.Recruitment.ProcessDay(2);
            var first=e.Recruitment.Candidates.Single();Assert.AreEqual(3,first.RemainingDays(2));
            e.Recruitment.ProcessDay(4);Assert.IsTrue(e.Recruitment.Candidates.Contains(first));Assert.AreEqual(1,first.RemainingDays(4));
            e.Recruitment.ProcessDay(5);Assert.IsFalse(e.Recruitment.Candidates.Contains(first));Assert.AreEqual(3,e.Recruitment.Candidates.Count);
        }
        [Test] public void TavernTierNeverRequestsFutureLockedResourcesEvenWithHighTownHall()
        {
            var e=Create(5,new FixedRandom());Fixture(e,BuildingType.Tavern);Fixture(e,BuildingType.Farm,1,14);
            e.Recruitment.ProcessDay(2);var candidate=e.Recruitment.Candidates.Single();
            Assert.IsTrue(candidate.Request.Keys.All(r=>r==ResourceType.Wood||r==ResourceType.Beetroot));
            Assert.IsFalse(ResourceCatalog.TavernEligible(e.Bootstrap.Settlement,ResourceType.Wool,1));
        }
        [Test] public void CandidateQualityScalesBudgetWithinSensibleTierBounds()
        {
            for(int tier=1;tier<=5;tier++)
            {
                int low=TavernRecruitment.RequestBudget(tier,1),high=TavernRecruitment.RequestBudget(tier,10);
                Assert.Greater(high,low);Assert.GreaterOrEqual(low,(int)(25*tier*.75));Assert.LessOrEqual(high,(int)Math.Ceiling(25*tier*1.25));
            }
        }
        [Test] public void RecruitmentConsumesEntireRequestOnceAndKeepsCandidateIdentity()
        {
            var e=Create(2,new FixedRandom());Fixture(e,BuildingType.Tavern);e.Recruitment.ProcessDay(2);
            var candidate=e.Recruitment.Candidates.Single();Assert.IsFalse(e.Recruitment.CanRecruit(candidate));
            Supply(e);var identity=candidate.Citizen;int before=e.Bootstrap.Resources.Amount(ResourceType.Wood);
            Assert.IsTrue(e.Recruitment.Recruit(candidate));Assert.IsFalse(e.Recruitment.Recruit(candidate));
            Assert.AreSame(identity,e.Bootstrap.Roster.Single(c=>c.Id==identity.Id));Assert.IsNotNull(identity.ResidenceBuildingId);
            Assert.AreEqual(before-candidate.Request[ResourceType.Wood],e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.IsEmpty(e.Recruitment.Candidates);
        }
        [Test] public void FullHousingBlocksRecruitmentWithoutTakingAnyPayment()
        {
            var e=Create(2,new FixedRandom());Fixture(e,BuildingType.Tavern);Supply(e);e.Recruitment.ProcessDay(2);
            var candidate=e.Recruitment.Candidates.Single();
            while(e.Bootstrap.Roster.Count<e.Residences.Capacity)
            {
                var c=new Citizen("extra-"+e.Bootstrap.Roster.Count,"Extra",CitizenSex.Male,20,candidate.Citizen.Attributes,new CitizenPosition(.5,.5));
                e.Bootstrap.Simulation.AddCitizen(c);e.Residences.Reconcile();
            }
            int before=e.Bootstrap.Resources.Amount(ResourceType.Wood);
            Assert.IsFalse(e.Recruitment.Recruit(candidate));Assert.AreEqual(before,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.IsTrue(e.Recruitment.Candidates.Contains(candidate));
        }
        [Test] public void DismissalPermanentlyRemovesRosterAndLinksIncludingActiveBuilderJob()
        {
            var e=Create(2);Supply(e);Builder(e);var citizen=e.Bootstrap.Roster[0];
            e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var house);
            Assert.IsTrue(e.Dismiss(citizen));Assert.AreEqual(3,e.Bootstrap.Roster.Count);
            Assert.IsNull(citizen.WorkplaceBuildingId);Assert.IsNull(citizen.ResidenceBuildingId);Assert.IsFalse(citizen.Active);
            Assert.IsNull(e.Jobs.Single().Builder);Assert.IsFalse(e.Dismiss(citizen));
            e.Advance(10);Assert.IsFalse(e.Bootstrap.Roster.Contains(citizen));Assert.IsEmpty(e.Recruitment.Candidates);
        }
        [Test] public void DemolitionRefundIncludesCompletedUpgradeInvestmentsWithFloorRounding()
        {
            var e=Create(3);Supply(e);Builder(e,3);
            Assert.IsTrue(e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var house));
            e.ContributeWork(house,ConstructionActor.Builder,10000,e.Jobs.Single().Builder);
            Upgrade(e,house);Upgrade(e,house);
            Assert.AreEqual(150,house.Investment[ResourceType.Wood]);Assert.AreEqual(75,house.Investment[ResourceType.Wool]);
            var refund=e.Refund(house);Assert.AreEqual(75,refund[ResourceType.Wood]);Assert.AreEqual(37,refund[ResourceType.Wool]);Assert.AreEqual(12,refund[ResourceType.Stone]);
            int before=e.Bootstrap.Resources.Amount(ResourceType.Wood);var claims=e.Bootstrap.Settlement.Territory.ClaimedTiles.ToArray();
            Assert.IsTrue(e.Demolish(house));Assert.AreEqual(before+75,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.IsFalse(e.Demolish(house));CollectionAssert.AreEquivalent(claims,e.Bootstrap.Settlement.Territory.ClaimedTiles);
            Assert.IsFalse(e.Bootstrap.Settlement.IsOccupied(house.Origin));
        }
        [Test] public void DemolitionClearsWorkersReassignsResidentsAndRemovesTavernCandidates()
        {
            var e=Create(2,new FixedRandom());Supply(e);var hut=Builder(e);var worker=e.Bootstrap.WorkerAt(hut);
            Assert.IsTrue(e.Demolish(hut));Assert.AreEqual(CitizenProfession.Unemployed,worker.Profession);Assert.IsNull(worker.WorkplaceBuildingId);
            var tavern=Fixture(e,BuildingType.Tavern);e.Residences.Assign(worker,tavern);e.Recruitment.ProcessDay(2);
            Assert.IsTrue(e.Demolish(tavern));Assert.IsEmpty(e.Recruitment.Candidates);
            Assert.AreEqual(e.Bootstrap.Settlement.TownHall.Id,worker.ResidenceBuildingId);Assert.AreEqual(0,e.Residences.Homeless);
        }
        [Test] public void HousingLossCreatesHomelessWithoutDeletingCitizens()
        {
            var e=Create();var house=Fixture(e,BuildingType.House);
            var extra=new Citizen("extra","Extra",CitizenSex.Female,20,new CitizenAttributes(5,5,5,5,5),new CitizenPosition(.5,.5));
            e.Bootstrap.Simulation.AddCitizen(extra);e.Residences.Reconcile();Assert.IsNotNull(extra.ResidenceBuildingId);
            Assert.IsTrue(e.Demolish(house));Assert.AreEqual(1,e.Residences.Homeless);Assert.AreEqual(5,e.Bootstrap.Roster.Count);
            e.Clock.Advance(420);e.Advance(90);Assert.IsNull(extra.InsideBuildingId);Assert.IsNull(extra.ResidenceBuildingId);
        }
        [Test] public void RemovingSelectedObjectsMakesCentralInteractionUnavailable()
        {
            var e=Create();var house=Fixture(e,BuildingType.House);var interaction=new PlayerInteraction(e.Bootstrap);
            interaction.SelectBuilding(house);Assert.AreEqual(PlayerInteractionType.WorkplaceManagement,interaction.AvailableInteraction);
            e.Demolish(house);Assert.AreEqual(PlayerInteractionType.None,interaction.AvailableInteraction);
            var citizen=e.Bootstrap.Roster[0];interaction.SelectCitizen(citizen);e.Dismiss(citizen);
            Assert.AreEqual(PlayerInteractionType.None,interaction.AvailableInteraction);
        }
        [Test] public void AllFunctionalProducersRunTheirUnlockedCyclesAtLevelFive()
        {
            foreach(var type in new[]{BuildingType.Farm,BuildingType.Butcher,BuildingType.FishermansHut,BuildingType.LumberMill,BuildingType.Mine})
            {
                var e=Create(5);Supply(e);Builder(e,5);var producer=Fixture(e,type);
                for(int level=2;level<=5;level++) Upgrade(e,producer);
                Assert.IsTrue(e.Bootstrap.TryAssignWorker(producer,e.Bootstrap.Roster[1].Id));
                e.Advance(80);
                var recipes=ResourceCatalog.Production.Where(p=>p.Building==type).ToArray();
                var before=recipes.ToDictionary(p=>p.Resource,p=>e.Bootstrap.Resources.Amount(p.Resource));
                e.Advance(180);
                foreach(var recipe in recipes)
                    Assert.AreEqual((180/recipe.Seconds)*recipe.Quantity(5),e.Bootstrap.Resources.Amount(recipe.Resource)-before[recipe.Resource],type+" "+recipe.Resource);
            }
        }
        [Test] public void ButcherAndMineBeginTheirDeferredResourcesOnlyAfterApprovedUpgradeCompletes()
        {
            var e=Create(5);Supply(e);Builder(e,5);var butcher=Fixture(e,BuildingType.Butcher);
            e.Bootstrap.TryAssignWorker(butcher,e.Bootstrap.Roster[1].Id);
            int wool=e.Bootstrap.Resources.Amount(ResourceType.Wool);e.Advance(60);
            Assert.AreEqual(wool,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            Upgrade(e,butcher);e.Advance(60);Assert.Greater(e.Bootstrap.Resources.Amount(ResourceType.Wool),wool);
            var mine=Fixture(e,BuildingType.Mine,1,14);e.Bootstrap.TryAssignWorker(mine,e.Bootstrap.Roster[2].Id);
            Upgrade(e,mine);Upgrade(e,mine);
            int iron=e.Bootstrap.Resources.Amount(ResourceType.Iron);e.Advance(80);
            Assert.AreEqual(iron,e.Bootstrap.Resources.Amount(ResourceType.Iron));
            Upgrade(e,mine);e.Advance(80);Assert.Greater(e.Bootstrap.Resources.Amount(ResourceType.Iron),iron);
        }
        [Test] public void NewGatheringIsDisabledAtNightAndSleepingCitizensDoNotStartTrips()
        {
            var e=Create();
            e.Bootstrap.Simulation.Gathering=new AmbientWoodGathering(e.Bootstrap,new FixedRandom());
            e.Clock.Advance(420);e.Advance(90);
            Assert.IsFalse(e.Bootstrap.Simulation.Gathering.AllowNewTrips);
            Assert.AreEqual(0,e.Bootstrap.Simulation.Gathering.SuccessRolls);
            Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.Task==CitizenTask.Sleeping));
            Assert.IsFalse(e.Bootstrap.Simulation.Gathering.TryBegin(e.Bootstrap.Roster[0]));
        }
        [Test] public void IncompleteTavernsDoNotRollAndSeededCandidatesRepeatExactly()
        {
            var a=Create(2,new SeededGatheringRandom(23));var b=Create(2,new SeededGatheringRandom(23));
            Fixture(a,BuildingType.Tavern);Fixture(b,BuildingType.Tavern);
            for(int day=2;day<100;day++) {a.Recruitment.ProcessDay(day);b.Recruitment.ProcessDay(day);}
            CollectionAssert.AreEqual(a.Recruitment.Candidates.Select(c=>c.Citizen.Id+":"+c.Citizen.FirstName+":"+c.Quality+":"+BuildingEconomyRules.Describe(c.Request)),
                b.Recruitment.Candidates.Select(c=>c.Citizen.Id+":"+c.Citizen.FirstName+":"+c.Quality+":"+BuildingEconomyRules.Describe(c.Request)));
            var e=Create(2,new FixedRandom());Supply(e);Builder(e);
            e.TryBuild(BuildingType.Tavern,new TileCoordinate(1,1),0,out var site);
            Assert.IsFalse(site.IsCompleted);e.Recruitment.ProcessDay(2);
            Assert.AreEqual(0,e.Recruitment.ArrivalRolls);
        }
        [Test] public void DismissedBuilderJobRetainsProgressAndCanResumeWithReplacement()
        {
            var e=Create(2);Supply(e);var hut=Builder(e);var builder=e.Bootstrap.WorkerAt(hut);
            e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var house);
            e.ContributeWork(house,ConstructionActor.Builder,30,builder);double work=house.ConstructionWork;
            e.Dismiss(builder);Assert.AreEqual(work,house.ConstructionWork);
            Assert.IsTrue(e.Bootstrap.TryAssignBuilder(hut,e.Bootstrap.Roster[0].Id));
            e.Advance(60);Assert.Greater(house.ConstructionWork,work);
            Assert.AreSame(e.Bootstrap.WorkerAt(hut),e.Jobs.Single().Builder);
        }
        private static void Wait(SettlementEconomy economy,Func<bool> ready)
        {
            for(int step=0;step<8000&&!ready();step++) economy.Advance(10);
            Assert.IsTrue(ready(),"Real daytime jobs/production must make progression possible.");
        }
        [Test] public void FullBuilderAndProducerProgressionReachesFiveWithoutSupplyingWoolStoneOrIron()
        {
            var e=Create();
            e.Bootstrap.Resources.TryTransaction("test-wood-only",ResourceType.Wood,100000);
            Assert.IsTrue(e.TryBuild(BuildingType.BuildersHut,new TileCoordinate(15,15),0,out var hut));
            e.ContributeWork(hut,ConstructionActor.Player,300);
            e.Bootstrap.TryAssignBuilder(hut,e.Bootstrap.Roster[0].Id);
            Assert.IsTrue(e.TryUpgradeTownHall());
            Assert.IsTrue(e.TryUpgrade(hut));Wait(e,()=>hut.Level==2);
            Assert.IsTrue(e.TryBuild(BuildingType.Butcher,new TileCoordinate(1,1),0,out var butcher));
            Wait(e,()=>butcher.IsCompleted);e.Bootstrap.TryAssignWorker(butcher,e.Bootstrap.Roster[1].Id);
            Assert.IsTrue(e.TryUpgrade(butcher));Wait(e,()=>butcher.Level==2);
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.TownHallCost(3)));
            Assert.IsTrue(e.TryUpgradeTownHall());
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.Mine)));
            Assert.IsTrue(e.TryBuild(BuildingType.Mine,new TileCoordinate(1,14),0,out var mine));
            Wait(e,()=>mine.IsCompleted);e.Bootstrap.TryAssignWorker(mine,e.Bootstrap.Roster[2].Id);
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.BuildersHut,3)));
            Assert.IsTrue(e.TryUpgrade(hut));Wait(e,()=>hut.Level==3);
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.Butcher,3)));
            Assert.IsTrue(e.TryUpgrade(butcher));Wait(e,()=>butcher.Level==3);
            for(int target=2;target<=3;target++)
            {
                int level=target;
                Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.Mine,level)));
                Assert.IsTrue(e.TryUpgrade(mine));Wait(e,()=>mine.Level==level);
            }
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.TownHallCost(4)));
            Assert.IsTrue(e.TryUpgradeTownHall());
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.BuildersHut,4)));
            Assert.IsTrue(e.TryUpgrade(hut));Wait(e,()=>hut.Level==4);
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.Mine,4)));
            Assert.IsTrue(e.TryUpgrade(mine));Wait(e,()=>mine.Level==4);
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.TownHallCost(5)));
            Assert.IsTrue(e.TryUpgradeTownHall());
            Wait(e,()=>e.Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(BuildingType.BuildersHut,5)));
            Assert.IsTrue(e.TryUpgrade(hut));Wait(e,()=>hut.Level==5);
            Assert.AreEqual(5,e.Bootstrap.Settlement.TownHallLevel);Assert.AreEqual(5,hut.Level);
            Assert.AreEqual(4,mine.Level);Assert.AreEqual(3,butcher.Level);
            Assert.IsTrue(ResourceCatalog.Obtainable(e.Bootstrap.Settlement,ResourceType.Iron));
        }
        [Test] public void NightResidenceReassignmentExitsWithoutTeleportAndWalksToNewHome()
        {
            var e=Create();var house=Fixture(e,BuildingType.House);
            e.Clock.Advance(420);e.Advance(60);var citizen=e.Bootstrap.Roster[0];
            Assert.AreEqual(e.Bootstrap.Settlement.TownHall.Id,citizen.InsideBuildingId);
            var before=citizen.Position;
            Assert.IsTrue(e.Residences.Assign(citizen,house));Assert.IsNull(citizen.InsideBuildingId);
            Assert.AreEqual(before.X,citizen.Position.X);Assert.AreEqual(before.Z,citizen.Position.Z);
            e.Advance(60);Assert.AreEqual(house.Id,citizen.InsideBuildingId);
        }
    }
}