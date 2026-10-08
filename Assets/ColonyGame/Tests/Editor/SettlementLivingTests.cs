using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class SettlementLivingTests
    {
        private sealed class Rolls : IGatheringRandom
        {
            private readonly double roll;
            public Rolls(double roll=0) {this.roll=roll;}
            public double NextDouble()=>roll;
            public int Next(int count)=>0;
        }
        private static SettlementEconomy Create(int level=1)
        {
            var state=SettlementState.CreatePrototype(level);
            return new SettlementEconomy(new SettlementBootstrap(state,CitizenSimulation.CreatePrototype(state,seed:1147)),new Rolls(1),new Rolls());
        }
        private static void Stock(SettlementEconomy e,ResourceType resource,int amount)
        {Assert.IsTrue(e.Bootstrap.Resources.TryTransaction(Guid.NewGuid().ToString(),resource,amount-e.Bootstrap.Resources.Amount(resource)));}
        private static BuildingState Building(SettlementEconomy e,BuildingType type,int x=1,int z=1,int level=1)
        {
            Assert.IsTrue(e.Bootstrap.Settlement.TryPlacePrototypeBuilding(type,new TileCoordinate(x,z),0,out var b));
            typeof(BuildingState).GetProperty("Level").SetValue(b,level);typeof(BuildingState).GetProperty("TargetLevel").SetValue(b,level);e.Residences.Reconcile();return b;
        }
        private static Citizen Builder(SettlementEconomy e,int level=1)
        {
            var hut=Building(e,BuildingType.BuildersHut,15,15,level);
            var c=e.Bootstrap.Roster[0];Assert.IsTrue(e.Bootstrap.TryAssignBuilder(hut,c.Id));return c;
        }
        private static HashSet<string> Ancestors(Citizen c)=>(HashSet<string>)typeof(Citizen).GetField("Ancestors",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(c);
        private static Citizen Child(SettlementEconomy e,int age=0)
        {
            var c=new Citizen("fixture-child","Robin",CitizenSex.Male,age,new CitizenAttributes(5,5,5,5,5),new CitizenPosition(.5,.5));
            Assert.IsTrue(e.Bootstrap.Simulation.AddCitizen(c));e.Residences.Reconcile();return c;
        }
        [Test] public void EveryResourceStartsEmptyAndAmbientTripsBootstrapTheFirstHut()
        {
            var e=Create();Assert.IsTrue(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().All(r=>e.Bootstrap.Resources.Amount(r)==0));
            e.Bootstrap.Simulation.Gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());
            for(int i=0;i<250&&e.Bootstrap.Resources.Amount(ResourceType.Wood)<25;i++)e.Advance(1);
            Assert.GreaterOrEqual(e.Bootstrap.Resources.Amount(ResourceType.Wood),25);
            int before=e.Bootstrap.Resources.Amount(ResourceType.Wood);
            Assert.IsTrue(e.TryBuild(BuildingType.BuildersHut,new TileCoordinate(1,1),0,out var hut));
            Assert.AreEqual(before,e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.IsTrue(e.Fund(hut));
            Assert.AreEqual(before-25,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            e.Clock.Advance(420);Assert.IsTrue(e.ContributeWork(hut,ConstructionActor.Player,300));
        }
        [Test] public void MultiplePlacedSitesRemainUnfundedUnassignedAndDoNotAutoStart()
        {
            var e=Create();Builder(e);Stock(e,ResourceType.Wood,200);
            Assert.IsTrue(e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var a));
            Assert.IsTrue(e.TryBuild(BuildingType.Farm,new TileCoordinate(1,14),0,out var b));
            e.Advance(200);Assert.AreEqual(200,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.IsTrue(e.Jobs.All(j=>!j.IsFunded&&j.Builder==null));Assert.AreEqual(0,a.ConstructionWork);Assert.AreEqual(0,b.ConstructionWork);
        }
        [Test] public void ReassignmentPausesPreviousJobRetainsProgressAndPaysOnlyOnce()
        {
            var e=Create();var builder=Builder(e);Stock(e,ResourceType.Wood,200);
            e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var a);e.TryBuild(BuildingType.Farm,new TileCoordinate(1,14),0,out var b);
            Assert.IsTrue(e.StartConstruction(a,builder.Id));e.ContributeWork(a,ConstructionActor.Builder,10,builder);double work=a.ConstructionWork;
            Assert.IsFalse(e.StartConstruction(b,builder.Id));Assert.AreEqual(175,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.IsTrue(e.StartConstruction(b,builder.Id,true));Assert.IsNull(e.JobAt(a).Builder);Assert.AreEqual(work,a.ConstructionWork);
            Assert.IsTrue(e.StartConstruction(a,builder.Id,true));Assert.AreEqual(125,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.AreEqual(1,e.Jobs.Count(j=>j.Builder==builder));Assert.IsFalse(e.StartConstruction(a,builder.Id,true));
            Assert.IsFalse(e.Fund(a));Assert.AreEqual(builder.WorkplaceBuildingId,e.Bootstrap.Settlement.Buildings.Single(h=>h.Type==BuildingType.BuildersHut).Id);
        }
        [Test] public void FailedStartDoesNotEvictBusyBuilderOrSpendResources()
        {
            var e=Create();var c=Builder(e);Stock(e,ResourceType.Wood,25);
            e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var a);e.TryBuild(BuildingType.Farm,new TileCoordinate(1,14),0,out var b);
            Assert.IsTrue(e.StartConstruction(a,c.Id));Assert.IsFalse(e.StartConstruction(b,c.Id,true));Assert.AreSame(c,e.JobAt(a).Builder);Assert.IsFalse(e.JobAt(b).IsFunded);
        }
        [Test] public void NightAssignmentIsAllowedButNpcWorkPausesAndHallNeverEmploysAnyone()
        {
            var e=Create(2);var hut=Building(e,BuildingType.BuildersHut,15,15,2);var c=e.Bootstrap.Roster[0];Stock(e,ResourceType.Wood,350);
            Stock(e,ResourceType.Wool,100);e.Clock.Advance(420);e.Advance(60);Assert.IsTrue(e.Bootstrap.TryAssignWorker(hut,c.Id));
            Assert.IsFalse(e.Bootstrap.TryAssignWorker(e.Bootstrap.Settlement.TownHall,e.Bootstrap.Roster[1].Id));
            Assert.IsTrue(e.TryUpgradeTownHall());Assert.IsTrue(e.StartConstruction(e.Bootstrap.Settlement.TownHall,c.Id));
            e.Advance(60);Assert.AreEqual(0,e.Bootstrap.Settlement.TownHall.ConstructionWork);
            Assert.IsNull(e.Bootstrap.WorkerAt(e.Bootstrap.Settlement.TownHall));Assert.AreEqual(hut.Id,c.WorkplaceBuildingId);
        }
        [Test] public void WorkerTransferReplacesOnlyExplicitlyAndUnassignmentPausesJobs()
        {
            var e=Create();var builder=Builder(e);var farm=Building(e,BuildingType.Farm);var other=e.Bootstrap.Roster[1];
            Assert.IsTrue(e.Bootstrap.TryAssignWorker(farm,other.Id));Stock(e,ResourceType.Wood,200);
            e.TryBuild(BuildingType.House,new TileCoordinate(1,14),0,out var house);Assert.IsTrue(e.StartConstruction(house,builder.Id));
            Assert.IsFalse(e.Bootstrap.TransferWorker(farm,builder.Id));
            e.Bootstrap.UnassignWorker(e.Bootstrap.Settlement.Buildings.Single(h=>h.Id==builder.WorkplaceBuildingId));e.Bootstrap.UnassignWorker(farm);
            Assert.IsTrue(e.Bootstrap.TryAssignWorker(farm,builder.Id));Assert.IsNull(e.JobAt(house).Builder);Assert.IsTrue(e.JobAt(house).IsFunded);
            Assert.IsNull(other.WorkplaceBuildingId);Assert.AreEqual(CitizenProfession.Farmer,builder.Profession);Assert.AreSame(builder,e.Bootstrap.WorkerAt(farm));
            Assert.IsTrue(e.Bootstrap.UnassignWorker(farm));Assert.IsNull(e.Bootstrap.WorkerAt(farm));
        }
        [Test] public void ChildrenCannotWorkGatherOrBeVoluntarilyDismissed()
        {
            var e=Create();var child=Child(e);var farm=Building(e,BuildingType.Farm);
            Assert.IsFalse(e.Bootstrap.TryAssignWorker(farm,child.Id));Assert.IsFalse(e.Bootstrap.TransferWorker(farm,child.Id));
            Assert.IsFalse(new AmbientWoodGathering(e.Bootstrap).CanGather(child));Assert.IsFalse(e.CanDismiss(child));
            Assert.AreEqual(.5,LivingRules.DailyNeed(child));Assert.AreEqual(4,e.Bootstrap.UnemployedCount);
        }
        [Test] public void EmployedAmbientGatheringUsesFreePeriodAndNeverInterruptsBuilderJobs()
        {
            var e=Create();var c=Builder(e);var gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());e.Bootstrap.Simulation.Gathering=gathering;
            Assert.IsFalse(gathering.CanGather(c));e.Clock.Advance(300);Assert.IsTrue(gathering.CanGather(c));
            Stock(e,ResourceType.Wood,25);e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var site);e.StartConstruction(site,c.Id);
            Assert.IsFalse(gathering.CanGather(c));e.Clock.Advance(120);Assert.IsFalse(gathering.CanGather(c));
        }
        [Test] public void NutritionValuesAndProductionIntervalsMatchApprovedBalance()
        {
            double[] nutrition={0,0,0,0,1,1,1.5,1.5,2,2.5};
            foreach(ResourceType r in Enum.GetValues(typeof(ResourceType)))Assert.AreEqual(nutrition[(int)r],LivingRules.Nutrition(r));
            Assert.AreEqual(10,ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Wood).Seconds);
            Assert.AreEqual(20,ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Stone).Seconds);
            Assert.AreEqual(10,ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Wool).Seconds);
            Assert.AreEqual(40,ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Iron).Seconds);
        }
        [Test] public void FarmWorkBudgetFeedsFiveAdultsPerLevelWithoutChangingCropsOrQuantities()
        {
            for(int level=1;level<=5;level++)
            {
                double nutrition=ResourceCatalog.Production.Where(p=>p.Building==BuildingType.Farm&&p.UnlockLevel<=level)
                    .Sum(p=>270/LivingRules.CycleSeconds(p,level)*p.Quantity(level)*LivingRules.Nutrition(p.Resource));
                Assert.AreEqual(5*level,nutrition,1e-8);
                Assert.IsTrue(ResourceCatalog.Production.Where(p=>p.Building==BuildingType.Farm&&p.UnlockLevel<=level).All(p=>p.Quantity(level)==level-p.UnlockLevel+1));
            }
        }
        [Test] public void FarmMultiDayActualOutputMatchesNormalRoutineTargetAndUnstaffedFarmProducesNothing()
        {
            for(int level=1;level<=5;level++)
            {
                var e=Create(5);var farm=Building(e,BuildingType.Farm,1,1,level);double produced=0;
                e.ProductionEvent+=(b,r,n,rejected)=>produced+=n*LivingRules.Nutrition(r);
                e.Advance(100);Assert.AreEqual(0,produced);e.Bootstrap.TryAssignWorker(farm,e.Bootstrap.Roster[0].Id);
                // Alternative food keeps the worker healthy; crop storage remains free.
                Stock(e,ResourceType.Meat,200);e.Advance(600*6);
                Assert.That(produced/6,Is.InRange(5*level*.8,5*level*1.15),"Routine travel/breaks must stay close to target average nutrition.");
            }
        }
        [Test] public void DailyMealsAreIdempotentPreserveFractionalCreditAndLeaveMaterialsAlone()
        {
            var e=Create();Child(e);Stock(e,ResourceType.Meat,4);Stock(e,ResourceType.Wood,20);
            Assert.AreEqual(4.5,e.Living.DailyDemand);e.Living.ProcessDay(2);
            Assert.AreEqual(2,e.Bootstrap.Resources.Amount(ResourceType.Meat));Assert.AreEqual(.5,e.Living.NutritionCredit);
            e.Living.ProcessDay(2);Assert.AreEqual(2,e.Bootstrap.Resources.Amount(ResourceType.Meat));
            e.Living.ProcessDay(3);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Meat));Assert.AreEqual(1,e.Living.NutritionCredit);
            Assert.AreEqual(20,e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.IsFalse(Enum.GetNames(typeof(ResourceType)).Contains("Food"));
        }
        [Test] public void PartialDailyFoodIsSharedDeterministicallyWithoutRoundingLoss()
        {
            var e=Create();Stock(e,ResourceType.Fish,1);e.Living.ProcessDay(2);
            Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.Satiety==.5&&c.HungryDays==1&&c.Health==100));Assert.AreEqual(0,e.Living.NutritionCredit);
        }
        [Test] public void FiveFullHungryDaysAreSafeThenDamageIsGradualAndFeedingRecovers()
        {
            var e=Create();e.Living.ProcessDay(6);Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.Health==100&&c.HungryDays==5));
            e.Living.ProcessDay(7);Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.Health==80));
            Stock(e,ResourceType.Beetroot,20);e.Living.ProcessDay(8);Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.Health==90&&c.HungryDays==0));
            e.Living.ProcessDay(9);Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.Health==100));
        }
        [Test] public void StarvationDeathBypassesDismissalMinimumAndReleasesJobsEmploymentAndHomes()
        {
            var e=Create();var c=Builder(e);Stock(e,ResourceType.Wood,25);e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var house);e.StartConstruction(house,c.Id);
            Assert.IsFalse(e.CanDismiss(c));e.Living.ProcessDay(11);
            Assert.AreEqual(0,e.Bootstrap.Roster.Count);Assert.AreEqual(4,e.Living.Deaths);Assert.IsNull(e.JobAt(house).Builder);
            Assert.IsNull(c.WorkplaceBuildingId);Assert.IsNull(c.ResidenceBuildingId);Assert.IsFalse(c.Active);Assert.IsTrue(e.JobAt(house).IsFunded);
        }
        [Test] public void VoluntaryDismissalStopsAtFourAndDeathRemainsIndependent()
        {
            var e=Create();Assert.IsFalse(e.Dismiss(e.Bootstrap.Roster[0]));Child(e,18);var c=e.Bootstrap.Roster[0];
            Assert.IsTrue(e.Dismiss(c));Assert.AreEqual(4,e.Bootstrap.Roster.Count);Assert.IsFalse(e.Dismiss(e.Bootstrap.Roster[0]));
        }
        [Test] public void PartnershipsPreferSharedHomesAreUniqueAndDoNotIncludeChildrenOrRelatives()
        {
            var e=Create();var roster=e.Bootstrap.Roster;Ancestors(roster[2]).Add(roster[0].Id);Child(e);
            Stock(e,ResourceType.Meat,200);e.Living.ProcessDay(2);
            Assert.AreNotEqual(roster[2].Id,roster[0].PartnerId);Assert.IsNull(roster.Last().PartnerId);
            Assert.IsTrue(roster.Where(c=>c.PartnerId!=null).All(c=>roster.Single(p=>p.Id==c.PartnerId).PartnerId==c.Id));
            Assert.AreEqual(roster.Where(c=>c.PartnerId!=null).Count(),roster.Where(c=>c.PartnerId!=null).Select(c=>c.PartnerId).Distinct().Count());
        }
        [Test] public void BirthRequiresHousingFoodAndCooldownThenCreatesRelatedInheritedChild()
        {
            var e=Create(2);Stock(e,ResourceType.Meat,350);e.Living.ProcessDay(2);e.Living.ProcessDay(11);Assert.AreEqual(0,e.Living.Births);
            e.Living.ProcessDay(12);Assert.AreEqual(1,e.Living.Births);var child=e.Bootstrap.Roster.Single(c=>!c.IsAdult);
            Assert.IsNotNull(child.ParentOneId);Assert.IsNotNull(child.ParentTwoId);Assert.AreEqual(0,child.Age);Assert.AreEqual(2,Ancestors(child).Count);
            Assert.AreEqual(child.ResidenceBuildingId,e.Bootstrap.Roster.Single(c=>c.Id==child.ParentOneId).ResidenceBuildingId);
            Assert.That(child.Attributes.Strength,Is.InRange(1,10));Assert.IsFalse(child.CanWork);
            e.Living.ProcessDay(13);Assert.AreEqual(1,e.Living.Births);Assert.AreEqual(1,child.Age);
        }
        [Test] public void NoBirthWithoutFreeHousingOrFoodAndChildBecomesAdultAfterEighteenDays()
        {
            var e=Create();Stock(e,ResourceType.Meat,200);e.Living.ProcessDay(20);Assert.AreEqual(0,e.Living.Births);
            var child=Child(e);Stock(e,ResourceType.Meat,200);e.Living.ProcessDay(38);Assert.AreEqual(18,child.Age);Assert.IsTrue(child.CanWork);
            var farm=Building(e,BuildingType.Farm);Assert.IsTrue(e.Bootstrap.TryAssignWorker(farm,child.Id));
            var hungry=Create(2);hungry.Living.ProcessDay(11);Assert.AreEqual(0,hungry.Living.Births);
        }
        [Test] public void CapacityUsesHallAndOnlyCompletedAdditiveWarehouses()
        {
            int[] capacity={200,350,500,750,1000};
            for(int level=1;level<=5;level++)Assert.AreEqual(capacity[level-1],Create(level).Bootstrap.Resources.Capacity);
            var e=Create(3);Building(e,BuildingType.Warehouse,1,1,2);Building(e,BuildingType.Warehouse,1,14,3);
            Assert.AreEqual(1750,e.Bootstrap.Resources.Capacity);
            e.TryBuild(BuildingType.Warehouse,new TileCoordinate(14,1),0,out var site);Assert.AreEqual(1750,e.Bootstrap.Resources.Capacity);
            Assert.IsFalse(site.IsCompleted);
        }
        [Test] public void DepositsClipPerResourceAndReplayNeverDuplicatesStoredAmount()
        {
            var e=Create();var stock=e.Bootstrap.Resources;Stock(e,ResourceType.Wood,199);
            Assert.AreEqual(1,stock.Store("deposit",ResourceType.Wood,3));Assert.AreEqual(1,stock.Store("deposit",ResourceType.Wood,3));
            Assert.AreEqual(200,stock.Amount(ResourceType.Wood));Assert.AreEqual(0,stock.Store("blocked",ResourceType.Wood,3));
            Assert.AreEqual(3,stock.Store("other",ResourceType.Stone,3));Assert.AreEqual(0,stock.Store("deposit",ResourceType.Stone,3));
        }
        [Test] public void CancellationRefundAtCapacityIsFullAtomicIdempotentAndNotProduction()
        {
            var e=Create();var c=Builder(e);Stock(e,ResourceType.Wood,200);e.TryBuild(BuildingType.Farm,new TileCoordinate(1,1),0,out var farm);e.StartConstruction(farm,c.Id);
            Stock(e,ResourceType.Wood,200);int events=0;e.ProductionEvent+=(b,r,n,rejected)=>events++;
            Assert.IsTrue(e.CancelConstruction(farm));Assert.AreEqual(250,e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.AreEqual(0,events);
            Assert.IsFalse(e.CancelConstruction(farm));Assert.AreEqual(250,e.Bootstrap.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void CancellationRefundAboveCapacityPreservesAllMaterialsAndSpendingResumesDeposits()
        {
            var e=Create(3);var c=Builder(e,3);Stock(e,ResourceType.Wood,500);Stock(e,ResourceType.Wool,500);
            e.TryBuild(BuildingType.Warehouse,new TileCoordinate(1,1),0,out var warehouse);e.StartConstruction(warehouse,c.Id);
            Stock(e,ResourceType.Wood,650);Stock(e,ResourceType.Wool,550);Assert.IsTrue(e.CancelConstruction(warehouse));
            Assert.AreEqual(800,e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.AreEqual(575,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            Assert.AreEqual(0,e.Bootstrap.Resources.Store("overflow",ResourceType.Wood,3));
            Assert.IsTrue(e.Bootstrap.Resources.TryTransaction("spend",ResourceType.Wood,-302));
            Assert.AreEqual(2,e.Bootstrap.Resources.Store("recovery",ResourceType.Wood,3));Assert.AreEqual(500,e.Bootstrap.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void RefundBundleFailureCannotPartiallyApplyAndReplayCannotDuplicate()
        {
            var e=Create();var stock=e.Bootstrap.Resources;Stock(e,ResourceType.Wood,200);Stock(e,ResourceType.Stone,int.MaxValue);
            var refund=new Dictionary<ResourceType,int>{{ResourceType.Wood,50},{ResourceType.Stone,25}};
            Assert.IsFalse(stock.TryBundle("refund",refund));Assert.AreEqual(200,stock.Amount(ResourceType.Wood));
            stock.TryTransaction("spend-stone",ResourceType.Stone,-25);Assert.IsTrue(stock.TryBundle("refund",refund));Assert.IsTrue(stock.TryBundle("refund",refund));
            Assert.AreEqual(250,stock.Amount(ResourceType.Wood));Assert.AreEqual(int.MaxValue,stock.Amount(ResourceType.Stone));
        }
        [Test] public void WarehouseDemolitionPreservesOverflowRefundAndProductionRecoversAfterSpending()
        {
            var e=Create(3);var builder=Builder(e,3);var warehouse=Building(e,BuildingType.Warehouse);typeof(BuildingState).GetMethod("Invest",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(warehouse,new object[]{BuildingEconomyRules.Cost(BuildingType.Warehouse)});
            var mill=Building(e,BuildingType.LumberMill,1,14);e.Bootstrap.TryAssignWorker(mill,e.Bootstrap.Roster[1].Id);
            Stock(e,ResourceType.Wood,750);Assert.IsTrue(e.Demolish(warehouse));Assert.IsTrue(e.StartConstruction(warehouse,builder.Id));
            Assert.IsTrue(e.ContributeWork(warehouse,ConstructionActor.Builder,10000,builder));
            Assert.AreEqual(500,e.Bootstrap.Resources.Capacity);Assert.AreEqual(825,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.AreEqual(12,e.Bootstrap.Resources.Amount(ResourceType.Wool));Assert.IsFalse(e.Demolish(warehouse));
            int stored=0,blocked=0;e.ProductionEvent+=(b,r,n,rejected)=>{if(r==ResourceType.Wood){stored+=n;blocked+=rejected;}};
            e.Advance(150);Assert.AreEqual(0,stored);Assert.Greater(blocked,0);Assert.AreEqual(825,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            e.Bootstrap.Resources.TryTransaction("overflow-spend",ResourceType.Wood,-327);e.Advance(150);
            Assert.AreEqual(2,stored);Assert.AreEqual(500,e.Bootstrap.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void AmbientGatheringCannotDepositAboveCapacityAndRefundDoesNotCountAsGathering()
        {
            var e=Create();var gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());e.Bootstrap.Simulation.Gathering=gathering;
            Stock(e,ResourceType.Wood,230);e.Advance(200);Assert.Greater(gathering.SuccessRolls,0);Assert.AreEqual(230,e.Bootstrap.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void EveryHallUpgradeCostFitsPreviousHallCapacityWithoutRepricing()
        {
            for(int target=2;target<=5;target++)Assert.IsTrue(BuildingEconomyRules.TownHallCost(target).Values.All(v=>v<=Create(target-1).Bootstrap.Resources.Capacity));
            Assert.AreEqual(600,BuildingEconomyRules.TownHallCost(5)[ResourceType.Wood]);
            Assert.AreEqual(25,BuildingEconomyRules.Cost(BuildingType.BuildersHut)[ResourceType.Wood]);
        }
        [Test] public void CompletedBuildingsDoNotCreateCitizensAndUnfundedUpgradesDoNotSpendOrAutoAssign()
        {
            var e=Create(2);Builder(e,2);var house=Building(e,BuildingType.House);Stock(e,ResourceType.Wood,300);Stock(e,ResourceType.Wool,100);
            Assert.AreEqual(4,e.Bootstrap.Roster.Count);Assert.IsTrue(e.TryUpgrade(house));
            e.Advance(100);Assert.IsFalse(e.JobAt(house).IsFunded);Assert.IsNull(e.JobAt(house).Builder);
            Assert.AreEqual(300,e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.AreEqual(1,house.Level);Assert.AreEqual(4,e.Bootstrap.Roster.Count);
        }
        [Test] public void ProductionReportsActualPartialDepositWhileOtherResourcesContinue()
        {
            var e=Create(2);var b=Building(e,BuildingType.Butcher,1,1,2);e.Bootstrap.TryAssignWorker(b,e.Bootstrap.Roster[0].Id);
            Stock(e,ResourceType.Meat,349);Stock(e,ResourceType.Wool,400);int meat=0,wool=0,rejected=0;
            e.ProductionEvent+=(building,r,n,blocked)=>{if(r==ResourceType.Meat)meat+=n;else if(r==ResourceType.Wool)wool+=n;rejected+=blocked;};
            e.Advance(180);Assert.AreEqual(1,meat);Assert.AreEqual(0,wool);Assert.Greater(rejected,0);Assert.AreEqual(400,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            e.Bootstrap.Resources.TryTransaction("spend-wool",ResourceType.Wool,-60);e.Advance(100);Assert.Greater(wool,0);
        }
        [Test] public void DeathClearsSurvivingPartnersWithoutRetainingDeadWorkerReferences()
        {
            var e=Create();Stock(e,ResourceType.Meat,100);e.Living.ProcessDay(2);var victim=e.Bootstrap.Roster.First(c=>c.PartnerId!=null);
            var partner=e.Bootstrap.Roster.Single(c=>c.Id==victim.PartnerId);
            typeof(Citizen).GetProperty("Health").SetValue(victim,1.0);typeof(Citizen).GetProperty("HungryDays").SetValue(victim,5);
            Stock(e,ResourceType.Meat,0);e.Living.ProcessDay(3);Assert.IsFalse(e.Bootstrap.Roster.Contains(victim));Assert.IsNull(partner.PartnerId);Assert.AreEqual(3,e.Bootstrap.Roster.Count);
        }
        [Test] public void BirthSimulationIsDeterministicAndAttributesStayWithinParentalVariation()
        {
            var a=Create(2);var b=Create(2);Stock(a,ResourceType.Meat,350);Stock(b,ResourceType.Meat,350);
            a.Living.ProcessDay(13);b.Living.ProcessDay(13);
            CollectionAssert.AreEqual(a.Bootstrap.Roster.Select(c=>c.Id+":"+c.FirstName+":"+c.Age+":"+c.ParentOneId+":"+c.Attributes.Strength),b.Bootstrap.Roster.Select(c=>c.Id+":"+c.FirstName+":"+c.Age+":"+c.ParentOneId+":"+c.Attributes.Strength));
            var child=a.Bootstrap.Roster.Single(c=>!c.IsAdult);var p=a.Bootstrap.Roster.Single(c=>c.Id==child.ParentOneId);var q=a.Bootstrap.Roster.Single(c=>c.Id==child.ParentTwoId);
            int[] Values(Citizen c)=>new[]{c.Attributes.Strength,c.Attributes.Dexterity,c.Attributes.Intelligence,c.Attributes.Endurance,c.Attributes.Charisma};
            for(int i=0;i<5;i++)Assert.LessOrEqual(Math.Abs(Values(child)[i]-Math.Round((Values(p)[i]+Values(q)[i])/2.0,MidpointRounding.AwayFromZero)),1);
        }
        [Test] public void TavernRecruitmentRequiresFoodAfterPaymentAndNeverReplacesWorkers()
        {
            var state=SettlementState.CreatePrototype(2);var e=new SettlementEconomy(new SettlementBootstrap(state,CitizenSimulation.CreatePrototype(state,seed:1147)),new Rolls(),new Rolls(1));
            var hut=Building(e,BuildingType.BuildersHut,15,15);var worker=e.Bootstrap.Roster[0];e.Bootstrap.TryAssignWorker(hut,worker.Id);Building(e,BuildingType.Tavern);
            e.Recruitment.ProcessDay(2);var candidate=e.Recruitment.Candidates.Single();Stock(e,ResourceType.Wood,350);
            Assert.IsFalse(e.Recruitment.CanRecruit(candidate));Assert.IsFalse(e.Recruitment.Recruit(candidate));Assert.AreEqual(350,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            Stock(e,ResourceType.Meat,100);Assert.IsTrue(e.Recruitment.CanRecruit(candidate));Assert.IsTrue(e.Recruitment.Recruit(candidate));Assert.IsFalse(e.Recruitment.Recruit(candidate));
            Assert.AreSame(worker,e.Bootstrap.WorkerAt(hut));Assert.AreEqual(5,e.Bootstrap.Roster.Count);
        }
        [Test] public void BuilderAndPermanentProducerKeepDistinctValidTargetsAtSharedWorkplace()
        {
            var e=Create(2);var builder=Builder(e,2);var butcher=Building(e,BuildingType.Butcher);var worker=e.Bootstrap.Roster[1];e.Bootstrap.TryAssignWorker(butcher,worker.Id);
            Stock(e,ResourceType.Wood,350);Assert.IsTrue(e.TryUpgrade(butcher,builder.Id));e.Advance(140);
            Assert.AreEqual(CitizenProfession.Builder,builder.Profession);Assert.AreEqual(CitizenProfession.Butcher,worker.Profession);
            Assert.AreEqual(butcher.Id,worker.WorkplaceBuildingId);Assert.AreNotEqual(worker.WorkplaceBuildingId,builder.WorkplaceBuildingId);
            foreach(var c in new[]{builder,worker})if(c.InsideBuildingId==null)Assert.IsTrue(PerimeterNavigation.ClearOfStructures(c.Position,e.Bootstrap.Settlement.Buildings));
            Assert.Greater(builder.Position.DistanceSquared(worker.Position),.01);Assert.Greater(butcher.ConstructionWork,0);
        }

        [Test] public void JobChooserIncludesOnlyQualifiedAvailableBuildersUnlessReassignmentIsExplicit()
        {
            var e=Create(2);var builder=Builder(e,2);var farm=Building(e,BuildingType.Farm);e.Bootstrap.TryAssignWorker(farm,e.Bootstrap.Roster[1].Id);Child(e);
            Stock(e,ResourceType.Wood,350);Stock(e,ResourceType.Wool,100);e.TryUpgradeTownHall();var hall=e.Bootstrap.Settlement.TownHall;
            CollectionAssert.AreEqual(new[]{builder},e.EligibleJobBuilders(hall));Assert.IsNull(e.Bootstrap.WorkerAt(hall));
            e.TryBuild(BuildingType.House,new TileCoordinate(1,14),0,out var site);Assert.IsTrue(e.StartConstruction(site,builder.Id));
            Assert.IsEmpty(e.EligibleJobBuilders(hall));CollectionAssert.AreEqual(new[]{builder},e.EligibleJobBuilders(hall,true));
            Assert.IsFalse(e.StartConstruction(hall,builder.Id));Assert.IsTrue(e.StartConstruction(hall,builder.Id,true));Assert.IsNull(e.JobAt(site).Builder);
        }
        [Test] public void HiringRequiresUnassignmentAndNeverDismissesOrSilentlyReplacesAnyone()
        {
            var e=Create();var farm=Building(e,BuildingType.Farm);var hut=Building(e,BuildingType.BuildersHut,15,15);var c=e.Bootstrap.Roster[0];
            Assert.IsTrue(e.Bootstrap.TryAssignWorker(farm,c.Id));Assert.IsFalse(e.Bootstrap.EligibleWorkers(hut).Contains(c));Assert.IsFalse(e.Bootstrap.TransferWorker(hut,c.Id));
            Assert.AreSame(c,e.Bootstrap.WorkerAt(farm));Assert.IsTrue(e.Bootstrap.UnassignWorker(farm));Assert.IsTrue(e.Bootstrap.TryAssignWorker(hut,c.Id));Assert.AreEqual(4,e.Bootstrap.Roster.Count);
        }
        [Test] public void CancelAtZeroHalfAndNinetyNinePercentRefundsEntireCommittedBundleOnce()
        {
            foreach(double fraction in new[]{0,.5,.99})
            {
                var e=Create(3);var c=Builder(e,3);Stock(e,ResourceType.Wood,500);Stock(e,ResourceType.Wool,500);
                Assert.IsTrue(e.TryBuild(BuildingType.Warehouse,new TileCoordinate(1,1),0,out var b));Assert.IsTrue(e.StartConstruction(b,c.Id));
                double rate=BuildingEconomyRules.BuilderEfficiency(c,3,1);e.ContributeWork(b,ConstructionActor.Builder,b.RequiredConstructionWork*fraction/rate,c);
                Assert.IsFalse(b.IsCompleted);Assert.AreEqual(150,e.CancellationRefund(b)[ResourceType.Wood]);Assert.AreEqual(25,e.CancellationRefund(b)[ResourceType.Wool]);
                Stock(e,ResourceType.Wood,600);Stock(e,ResourceType.Wool,600);Assert.IsTrue(e.CancelConstruction(b));Assert.IsFalse(e.CancelConstruction(b));
                Assert.AreEqual(750,e.Bootstrap.Resources.Amount(ResourceType.Wood));Assert.AreEqual(625,e.Bootstrap.Resources.Amount(ResourceType.Wool));Assert.IsEmpty(e.Jobs);Assert.IsFalse(e.Bootstrap.Settlement.IsOccupied(b.Origin));
            }
            var unfunded=Create();unfunded.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var site);Assert.IsEmpty(unfunded.CancellationRefund(site));
        }
        [Test] public void WoodStoneAndIronAllLevelsKeepIndependentApprovedCyclesAndStorageLimits()
        {
            foreach(var resource in new[]{ResourceType.Wood,ResourceType.Stone,ResourceType.Iron})
            {
                var recipe=ResourceCatalog.Production.Single(p=>p.Resource==resource);Assert.AreEqual(resource==ResourceType.Wood?10:resource==ResourceType.Stone?20:40,recipe.Seconds);
                for(int level=1;level<=5;level++)
                {
                    var e=Create(5);var b=Building(e,recipe.Building,1,1,level);Stock(e,ResourceType.Meat,200);
                    int count=0;e.ProductionEvent+=(building,r,n,blocked)=>{if(r==resource){count+=n;Assert.LessOrEqual(n,recipe.Quantity(level));}};
                    e.Advance(80);Assert.AreEqual(0,count);e.Bootstrap.TryAssignWorker(b,e.Bootstrap.Roster[0].Id);e.Advance(220);
                    if(level<recipe.UnlockLevel)Assert.AreEqual(0,count);else Assert.Greater(count,0);
                    int before=count;e.Clock.Advance(420-e.Clock.Elapsed);e.Advance(180);Assert.AreEqual(before,count);
                }
            }
        }
        [Test] public void BlockingNominalHallDoorRetainsReachableDeliveryAndValidRotatedPerimeters()
        {
            foreach(int rotation in new[]{0,90,180,270})
            {
                var e=Create();e.Bootstrap.Simulation.Gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());
                Assert.IsTrue(e.TryBuild(BuildingType.Farm,new TileCoordinate(8,3),rotation,out var site));e.Bootstrap.Simulation.ReconcileBuildings();
                Assert.Greater(e.Bootstrap.Settlement.ExteriorAccessTiles(e.Bootstrap.Settlement.TownHall).Count(),0);
                Assert.IsTrue(e.Bootstrap.Settlement.ExteriorAccessTiles(site).All(t=>!e.Bootstrap.Settlement.IsOccupied(t)));
                e.Advance(300);Assert.GreaterOrEqual(e.Bootstrap.Resources.Amount(ResourceType.Wood),25);
                foreach(var c in e.Bootstrap.Roster)Assert.IsTrue(e.Bootstrap.Simulation.Gathering.Area.CanOccupy(c.Position));
            }
        }
        [Test] public void DenseHallEnclosureKeepsSharedBoundaryAccessAndDeliversWood()
        {
            var e=Create(2);Building(e,BuildingType.Farm,7,3);Building(e,BuildingType.House,10,4);
            Building(e,BuildingType.BuildersHut,4,7);Building(e,BuildingType.GuardTower,5,10);
            Building(e,BuildingType.BuildersHut,12,7);Building(e,BuildingType.GuardTower,12,10);Building(e,BuildingType.House,7,12);
            Assert.AreEqual(BuildingPlacementValidity.Valid,e.Bootstrap.Settlement.ValidateAccessiblePlacement(BuildingType.House,new TileCoordinate(10,12)));
            Assert.IsTrue(e.TryBuild(BuildingType.House,new TileCoordinate(10,12),0,out var site));
            e.Bootstrap.Simulation.Gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());
            e.Bootstrap.Simulation.ReconcileBuildings();
            for(int i=0;i<300;i++)
            {
                var before=e.Bootstrap.Roster.Select(c=>c.Position).ToArray();e.Advance(1);
                for(int c=0;c<before.Length;c++)
                {Assert.LessOrEqual(before[c].DistanceSquared(e.Bootstrap.Roster[c].Position),.8*.8+1e-8);Assert.IsTrue(e.Bootstrap.Simulation.Gathering.Area.CanOccupy(e.Bootstrap.Roster[c].Position));}
            }
            Assert.GreaterOrEqual(e.Bootstrap.Resources.Amount(ResourceType.Wood),25);
            Assert.IsFalse(site.IsCompleted);
        }
        [Test] public void TouchingBuildingsMergeBoundarySegmentsWithoutReservingExtraTiles()
        {
            var e=Create();var a=Building(e,BuildingType.House,1,1);var b=Building(e,BuildingType.House,4,1);
            var segments=PerimeterNavigation.Segments(new[]{a,b}).ToArray();
            Assert.AreEqual(21,segments.Length);Assert.AreEqual(segments.Length,segments.Distinct().Count());
            Assert.AreEqual(3,segments.Count(s=>s.Start.X==4&&s.End.X==4));
            Assert.AreEqual(9,a.OccupiedTiles.Count());Assert.AreEqual(9,b.OccupiedTiles.Count());
        }
        [Test] public void SharedBoundaryAndCornerRoutesStayClearOfSolidInteriors()
        {
            var e=Create();Building(e,BuildingType.House,1,1);Building(e,BuildingType.House,4,1);
            var policy=new SettlementIdleMovementPolicy(e.Bootstrap.Settlement);
            var shared=new CitizenPosition(4,2.5);var corner=new CitizenPosition(4,4);
            var target=new CitizenPosition(5.5,4);
            Assert.IsTrue(policy.CanOccupy(shared));Assert.IsTrue(policy.CanOccupy(corner));
            var route=PerimeterNavigation.FindPath(shared,target,policy.CanOccupy);Assert.Greater(route.Count,0);
            Assert.IsTrue(route.Any(p=>p.DistanceSquared(corner)<1e-12));
            for(int i=1;i<route.Count;i++)
            {Assert.IsTrue(route[i].X==route[i-1].X||route[i].Z==route[i-1].Z);Assert.IsTrue(PerimeterNavigation.SegmentClear(route[i-1],route[i],policy.CanOccupy));}
            foreach(var b in e.Bootstrap.Settlement.Buildings)
                foreach(var tile in b.OccupiedTiles)Assert.IsFalse(policy.CanOccupy(CitizenPosition.Center(tile)));
            Assert.IsEmpty(PerimeterNavigation.FindPath(shared,new CitizenPosition(2.5,2.5),policy.CanOccupy));
        }
        [Test] public void EveryRotatedBuildingEntranceConnectsToPerimeterWithoutInteriorShortcut()
        {
            foreach(var definition in BuildingCatalog.All.Where(d=>d.Type!=BuildingType.TownHall))
                foreach(int rotation in new[]{0,90,180,270})
                {
                    var state=SettlementState.CreatePrototype(5);
                    Assert.IsTrue(state.TryPlacePrototypeBuilding(definition.Type,new TileCoordinate(1,1),rotation,out var b));
                    var policy=new SettlementIdleMovementPolicy(state);
                    var entrance=PerimeterNavigation.Entrance(b);
                    var route=PerimeterNavigation.FindPath(new CitizenPosition(.5,.5),entrance,policy.CanOccupy);
                    Assert.Greater(route.Count,0,definition.Type+" rotation "+rotation);
                    for(int i=1;i<route.Count;i++)Assert.IsTrue(PerimeterNavigation.SegmentClear(route[i-1],route[i],policy.CanOccupy));
                    Assert.IsTrue(b.OccupiedTiles.All(t=>!policy.CanOccupy(CitizenPosition.Center(t))));
                }
        }
        [Test] public void CitizenActuallyTraversesSharedEdgeAndCornerWithoutTeleporting()
        {
            var e=Create();Building(e,BuildingType.House,1,1);Building(e,BuildingType.House,4,1);
            var policy=new SettlementIdleMovementPolicy(e.Bootstrap.Settlement);
            var citizen=new Citizen("path-walker","Robin",CitizenSex.Male,25,new CitizenAttributes(5,5,5,5,5),new CitizenPosition(4,2.5));
            var simulation=new CitizenSimulation(policy,new[]{citizen});
            var target=new CitizenPosition(5.5,4);
            var travel=typeof(CitizenSimulation).GetMethod("TravelTo",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,
                new[]{typeof(Citizen),typeof(CitizenPosition),typeof(double),typeof(double)},null);
            simulation.ScheduledMovement=(c,seconds,speed)=>{travel.Invoke(simulation,new object[]{c,target,seconds,speed});return true;};
            bool corner=false;
            for(int i=0;i<100;i++)
            {
                var before=citizen.Position;simulation.Tick(.05);
                Assert.LessOrEqual(before.DistanceSquared(citizen.Position),.04*.04+1e-9);
                Assert.IsTrue(policy.CanOccupy(citizen.Position));
                if(before.X==4&&before.Z<4&&citizen.Position.Z==4)corner=true;
            }
            Assert.IsTrue(corner);Assert.Less(citizen.Position.DistanceSquared(target),1e-12);
        }
        [Test] public void SiteAtClaimBoundaryKeepsEdgePathsWithoutGrantingWildernessWandering()
        {
            var e=Create();Assert.IsTrue(e.TryBuild(BuildingType.House,new TileCoordinate(0,1),0,out var site));
            var state=e.Bootstrap.Settlement;var policy=new SettlementIdleMovementPolicy(state);
            Assert.AreEqual(400,state.Territory.Count);Assert.AreEqual(9,site.OccupiedTiles.Count());
            Assert.IsTrue(policy.CanOccupy(new CitizenPosition(0,2.5)));
            Assert.IsFalse(policy.CanOccupy(new CitizenPosition(-.5,2.5)));
            Assert.IsFalse(policy.CanOccupy(new CitizenPosition(.1,10.5)));
            var route=PerimeterNavigation.FindPath(new CitizenPosition(0,2.5),PerimeterNavigation.Entrance(site),policy.CanOccupy);
            Assert.Greater(route.Count,0);for(int i=1;i<route.Count;i++)Assert.IsTrue(PerimeterNavigation.SegmentClear(route[i-1],route[i],policy.CanOccupy));
        }
        [Test] public void StartingConstructionPreservesPendingWoodRouteAndDepositsOnce()
        {
            var e=Create();var hut=Building(e,BuildingType.BuildersHut,15,15);var c=e.Bootstrap.Roster[0];
            var gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());e.Bootstrap.Simulation.Gathering=gathering;
            Assert.IsTrue(gathering.TryBegin(c));gathering.AllowNewTrips=false;
            for(int i=0;i<250&&c.Task!=CitizenTask.ReturningWood;i++)e.Bootstrap.Simulation.Tick(1);
            Assert.AreEqual(CitizenTask.ReturningWood,c.Task);Assert.AreEqual(3,gathering.PendingWood(c));
            Assert.IsTrue(e.Bootstrap.TryAssignBuilder(hut,c.Id));
            Stock(e,ResourceType.Wood,100);Assert.IsTrue(e.TryBuild(BuildingType.Farm,new TileCoordinate(1,14),0,out var site));
            var route=c.PlannedRoute.ToArray();var destination=c.Destination;
            Assert.IsTrue(e.StartConstruction(site,c.Id));CollectionAssert.AreEqual(route,c.PlannedRoute);
            Assert.AreEqual(0,destination.DistanceSquared(c.Destination));
            int paid=e.Bootstrap.Resources.Amount(ResourceType.Wood);gathering.AllowNewTrips=false;
            for(int i=0;i<200&&gathering.HasTrip(c);i++)e.Bootstrap.Simulation.Tick(1);
            Assert.IsFalse(gathering.HasTrip(c));Assert.AreEqual(paid+3,e.Bootstrap.Resources.Amount(ResourceType.Wood));
            e.Bootstrap.Simulation.Tick(30);Assert.AreEqual(paid+3,e.Bootstrap.Resources.Amount(ResourceType.Wood));
        }
        [Test] public void MorningExitWaitsForEntranceClearanceInsteadOfTeleportingToAnotherEdge()
        {
            var e=Create();e.Clock.Advance(420);e.Advance(100);
            Assert.IsTrue(e.Bootstrap.Roster.All(c=>c.InsideBuildingId!=null));
            var before=e.Bootstrap.Roster.Select(c=>c.Position).ToArray();e.Clock.Advance(80);e.Advance(.1);
            for(int i=0;i<before.Length;i++)Assert.LessOrEqual(before[i].DistanceSquared(e.Bootstrap.Roster[i].Position),.08*.08+1e-9);
        }
        [Test] public void FullyReservedClaimsStillConnectHallBoundaryAndWoodDelivery()
        {
            var hall=SettlementState.CreatePrototype().TownHall;
            var territory=new SettlementTerritory(hall.OccupiedTiles);
            var state=(SettlementState)System.Activator.CreateInstance(typeof(SettlementState),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,
                null,new object[]{territory,hall,1},null);
            var policy=new SettlementIdleMovementPolicy(state);var entrance=PerimeterNavigation.Entrance(hall);
            var c=new Citizen("dense-walker","Robin",CitizenSex.Male,25,new CitizenAttributes(5,5,5,5,5),entrance);
            var simulation=new CitizenSimulation(policy,new[]{c});var bootstrap=new SettlementBootstrap(state,simulation);
            Assert.IsTrue(policy.IsConnected(entrance));Assert.AreEqual(0,policy.ConnectedTileCount);
            Assert.IsTrue(hall.OccupiedTiles.All(t=>!policy.CanOccupy(CitizenPosition.Center(t))));
            var gathering=new AmbientWoodGathering(bootstrap,new Rolls());simulation.Gathering=gathering;
            Assert.IsTrue(gathering.TryBegin(c));
            for(int i=0;i<1000&&gathering.HasTrip(c);i++)simulation.Tick(.25);
            Assert.IsFalse(gathering.HasTrip(c));Assert.AreEqual(3,bootstrap.Resources.Amount(ResourceType.Wood));
            Assert.IsTrue(policy.IsConnected(c.Position));Assert.AreEqual(25,territory.Count);
        }
        private static void WaitFor(SettlementEconomy e,Func<bool> ready,string purpose)
        {
            for(int step=0;step<5000&&!ready();step++)e.Advance(10);
            Assert.IsTrue(ready(),purpose+" must be achievable using real deposits and work.");
            Assert.AreEqual(0,e.Living.Deaths,"Establishing food must keep the starting settlement alive.");
        }
        private static BuildingState BuildPlayable(SettlementEconomy e,BuildingType type,int x,int z,Citizen builder)
        {
            Assert.IsTrue(e.TryBuild(type,new TileCoordinate(x,z),0,out var b));
            WaitFor(e,()=>e.Bootstrap.Resources.CanPay(e.JobAt(b).Materials),"Fund "+type);
            Assert.IsTrue(builder==null?e.Fund(b):e.StartConstruction(b,builder.Id));
            if(builder==null)
                while(!b.IsCompleted) {e.ContributeWork(b,ConstructionActor.Player,1);e.Advance(1);}
            else WaitFor(e,()=>b.IsCompleted,"Complete "+type);
            return b;
        }
        private static void UpgradePlayable(SettlementEconomy e,BuildingState b,Citizen builder)
        {
            Assert.IsTrue(e.TryUpgrade(b));WaitFor(e,()=>e.Bootstrap.Resources.CanPay(e.JobAt(b).Materials),"Upgrade materials "+b.Type);
            Assert.IsTrue(e.StartConstruction(b,builder.Id));int target=b.TargetLevel;
            WaitFor(e,()=>!b.IsUpgrading&&b.Level==target,"Upgrade "+b.Type);
        }
        [Test] public void FullPlayableProgressionFromZeroResourcesReachesHallFiveWithoutInjectedMaterials()
        {
            var e=Create();e.Bootstrap.Simulation.Gathering=new AmbientWoodGathering(e.Bootstrap,new Rolls());
            var hut=BuildPlayable(e,BuildingType.BuildersHut,1,1,null);var builder=e.Bootstrap.Roster[0];
            Assert.AreSame(builder,e.Bootstrap.WorkerAt(hut));
            var farm=BuildPlayable(e,BuildingType.Farm,1,14,builder);Assert.AreSame(e.Bootstrap.Roster[1],e.Bootstrap.WorkerAt(farm));
            UpgradePlayable(e,e.Bootstrap.Settlement.TownHall,builder);UpgradePlayable(e,hut,builder);
            var butcher=BuildPlayable(e,BuildingType.Butcher,14,1,builder);Assert.AreSame(e.Bootstrap.Roster[2],e.Bootstrap.WorkerAt(butcher));
            UpgradePlayable(e,butcher,builder);UpgradePlayable(e,e.Bootstrap.Settlement.TownHall,builder);
            var mine=BuildPlayable(e,BuildingType.Mine,14,14,builder);Assert.AreSame(e.Bootstrap.Roster[3],e.Bootstrap.WorkerAt(mine));
            var mill=BuildPlayable(e,BuildingType.LumberMill,1,6,builder);
            Assert.IsFalse(e.Bootstrap.TransferWorker(mill,e.Bootstrap.Roster[1].Id));e.Bootstrap.UnassignWorker(farm);
            Assert.IsTrue(e.Bootstrap.TryAssignWorker(mill,e.Bootstrap.Roster[1].Id));
            UpgradePlayable(e,hut,builder);UpgradePlayable(e,butcher,builder);
            UpgradePlayable(e,mine,builder);UpgradePlayable(e,mine,builder);
            UpgradePlayable(e,e.Bootstrap.Settlement.TownHall,builder);UpgradePlayable(e,hut,builder);UpgradePlayable(e,mine,builder);
            UpgradePlayable(e,e.Bootstrap.Settlement.TownHall,builder);UpgradePlayable(e,hut,builder);
            Assert.AreEqual(5,e.Bootstrap.Settlement.TownHallLevel);Assert.AreEqual(5,hut.Level);
            Assert.AreEqual(4,mine.Level);Assert.Greater(e.Bootstrap.Simulation.Gathering.SuccessRolls,0);
        }

        private static Type PresentationType(string name)=>UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()
            .Select(a=>a.GetType("ColonyGame.Presentation."+name)).First(t=>t!=null);
        private static T Value<T>(object instance,string property)=>(T)instance.GetType().GetProperty(property).GetValue(instance);
        private static object Invoke(object instance,string method,params object[] arguments)=>instance.GetType().GetMethod(method).Invoke(instance,arguments);
        private static object Management(SettlementEconomy e,Action<Citizen> inspect=null)=>Activator.CreateInstance(PresentationType("CitizenManagement"),e.Bootstrap,inspect??(_=>{}));
        private static object Swarms()=>Activator.CreateInstance(PresentationType("FireflySwarmSystem"),1039,null);
        private static object[] SwarmPool(object system)=>((System.Collections.IEnumerable)Value<object>(system,"Swarms")).Cast<object>().ToArray();
        private static void SwarmTick(object system,double seconds,bool day,Func<CitizenPosition?> candidate,Func<CitizenPosition,bool> valid=null)=>
            Invoke(system,"Tick",seconds,day,candidate,valid??(_=>true));
        [Test] public void SwarmsSpawnIndependentlyInAFixedPoolAndExpireInsteadOfFreezing()
        {
            var system=Swarms();var pool=SwarmPool(system);int location=0;
            Func<CitizenPosition?> next=()=>new CitizenPosition((location++)*4,0);
            for(int i=0;i<65;i++) SwarmTick(system,.1,false,next);
            var active=pool.Where(s=>Value<bool>(s,"Active")).ToArray();
            Assert.That(active.Length,Is.InRange(3,6));
            Assert.AreEqual(active.Length,active.Select(s=>Value<CitizenPosition>(s,"Center")).Distinct().Count());
            foreach(var swarm in active)Assert.That(Value<double>(swarm,"Lifetime"),Is.InRange(8,15));
            var generations=active.Select(s=>Value<int>(s,"Generation")).ToArray();
            for(int i=0;i<200;i++) SwarmTick(system,.1,false,next);
            Assert.Greater(Value<int>(system,"Expired"),0);Assert.Greater(Value<int>(system,"Spawned"),6);
            Assert.IsFalse(pool.Any(s=>Value<bool>(s,"Active")&&generations.Contains(Value<int>(s,"Generation"))));
            CollectionAssert.AreEqual(pool,SwarmPool(system));
        }
        [Test] public void FireflyPointsStayLocalStationaryDuringGlowAndCycleInGroupsOfThree()
        {
            var system=Swarms();int location=0;Func<CitizenPosition?> next=()=>new CitizenPosition((location++)*4,0);
            SwarmTick(system,.1,false,next);SwarmTick(system,.6,false,next);
            var swarm=SwarmPool(system).First(s=>Value<bool>(s,"Active"));var center=Value<CitizenPosition>(swarm,"Center");
            var before=Value<CitizenPosition[]>(swarm,"Points").ToArray();var alpha=Value<double[]>(swarm,"Alpha").ToArray();
            Assert.AreEqual(9,before.Length);Assert.AreEqual(3,alpha.Count(a=>a>0));
            Assert.IsTrue(before.All(p=>p.DistanceSquared(center)<=1.1*1.1+1e-9));
            SwarmTick(system,.02,false,next);CollectionAssert.AreEqual(before,Value<CitizenPosition[]>(swarm,"Points"));
            Assert.Greater(Value<double[]>(swarm,"Alpha")[0],alpha[0]);
            SwarmTick(system,1.8,false,next);
            Assert.AreEqual(center,Value<CitizenPosition>(swarm,"Center"));
            Assert.IsFalse(before.SequenceEqual(Value<CitizenPosition[]>(swarm,"Points")));
            var changed=Value<double[]>(swarm,"Alpha");Assert.AreEqual(3,changed.Count(a=>a>0));Assert.AreEqual(0,changed[0]);Assert.Greater(changed[3],0);
        }
        [Test] public void FirefliesFadeSmoothlyAtExpiryAndResetAtDawnAndNextNight()
        {
            var system=Swarms();int location=0;Func<CitizenPosition?> next=()=>new CitizenPosition((location++)*4,0);
            SwarmTick(system,.1,false,next);var swarm=SwarmPool(system).First(s=>Value<bool>(s,"Active"));
            double life=Value<double>(swarm,"Lifetime");
            SwarmTick(system,life-.1,false,next);Assert.IsTrue(Value<bool>(swarm,"Active"));
            Assert.Less(Value<double[]>(swarm,"Alpha").Max(),.04);
            SwarmTick(system,.2,false,next);Assert.IsFalse(Value<bool>(swarm,"Active"));Assert.IsTrue(Value<double[]>(swarm,"Alpha").All(a=>a==0));
            SwarmTick(system,.1,true,next);Assert.IsFalse(SwarmPool(system).Any(s=>Value<bool>(s,"Active")));
            Assert.IsTrue(SwarmPool(system).All(s=>Value<double[]>(s,"Alpha").All(a=>a==0)));
            int spawned=Value<int>(system,"Spawned");SwarmTick(system,20,true,next);Assert.AreEqual(spawned,Value<int>(system,"Spawned"));
            SwarmTick(system,.1,false,next);Assert.AreEqual(spawned+1,Value<int>(system,"Spawned"));
        }
        [Test] public void FirefliesRequireValidLocationsAndClearOffCameraOrClaimedAnchors()
        {
            var system=Swarms();Func<CitizenPosition?> next=()=>new CitizenPosition(4,4);
            SwarmTick(system,1,false,next,_=>false);Assert.AreEqual(0,Value<int>(system,"Spawned"));
            SwarmTick(system,2,false,next);Assert.AreEqual(1,Value<int>(system,"Spawned"));
            SwarmTick(system,.1,false,next,_=>false);Assert.IsFalse(SwarmPool(system).Any(s=>Value<bool>(s,"Active")));
            SwarmTick(system,2,false,()=>null);Assert.AreEqual(1,Value<int>(system,"Spawned"));
        }
        [Test] public void HallRosterUsesOnlyLivingNamesInStableOrderAndSharedInspectionCallback()
        {
            var e=Create();var child=Child(e);Citizen selected=null;var management=Management(e,c=>selected=c);
            var roster=Value<IEnumerable<Citizen>>(management,"LivingRoster").ToArray();CollectionAssert.AreEqual(e.Bootstrap.Roster,roster);
            foreach(var c in roster)Assert.AreEqual(c.FirstName,Invoke(management,"RosterLabel",c));
            Invoke(management,"Select",child);Assert.AreSame(child,selected);
            var dismissed=e.Bootstrap.Roster[0];Assert.IsTrue(e.Dismiss(dismissed));
            Assert.IsFalse(Value<IEnumerable<Citizen>>(management,"LivingRoster").Contains(dismissed));
            selected=null;Invoke(management,"Select",dismissed);Assert.IsNull(selected);
            e.Living.ProcessDay(11);Assert.IsEmpty(Value<IEnumerable<Citizen>>(management,"LivingRoster"));
            var hud=System.IO.File.ReadAllText("Assets/ColonyGame/Scripts/WorldPresentation/PrototypeHud.cs");
            Assert.IsTrue(hud.Contains("citizenManagement.Select(selected)"));Assert.IsTrue(hud.Contains("text=citizenManagement.RosterLabel(selected)"));
        }
        [Test] public void CitizenPanelUnassignmentReleasesJobWithoutDismissingAndLocksOnlyItsWorkplace()
        {
            var e=Create();var builder=Builder(e);var hut=e.Bootstrap.Settlement.Buildings.Single(b=>b.Type==BuildingType.BuildersHut);
            Stock(e,ResourceType.Wood,100);e.TryBuild(BuildingType.House,new TileCoordinate(1,1),0,out var site);Assert.IsTrue(e.StartConstruction(site,builder.Id));
            int population=e.Bootstrap.Roster.Count;var management=Management(e);
            Assert.IsTrue((bool)Invoke(management,"Unassign",builder));Assert.IsNull(e.JobAt(site).Builder);Assert.IsTrue(e.JobAt(site).IsFunded);
            Assert.AreEqual(population,e.Bootstrap.Roster.Count);Assert.IsTrue(builder.Active);Assert.AreEqual(CitizenProfession.Unemployed,builder.Profession);
            Assert.IsNull(builder.WorkplaceBuildingId);Assert.IsTrue(hut.ManualVacancy);e.Bootstrap.FillAutomaticVacancies();e.Advance(1);Assert.IsNull(e.Bootstrap.WorkerAt(hut));
            Assert.IsTrue(e.Bootstrap.TryAssignWorker(hut,builder.Id));Assert.IsFalse(hut.ManualVacancy);
        }
        [Test] public void CitizenPanelMovesToHallReleasesOldSlotAndSurvivesHousePreferenceAndFamilies()
        {
            var e=Create(2);var house=Building(e,BuildingType.House);var c=e.Bootstrap.Roster.First(x=>x.ResidenceBuildingId==house.Id);
            var management=Management(e);int before=e.Residences.Residents(house).Count();
            Assert.IsTrue((bool)Invoke(management,"CanMoveToHall",c));Assert.IsTrue((bool)Invoke(management,"MoveToHall",c));
            Assert.AreEqual(before-1,e.Residences.Residents(house).Count());Assert.AreEqual(e.Bootstrap.Settlement.TownHall.Id,c.PreferredResidenceId);
            Stock(e,ResourceType.Meat,300);for(int day=2;day<15;day++){e.Residences.Reconcile();e.Living.ProcessDay(day);}
            Assert.AreEqual(e.Bootstrap.Settlement.TownHall.Id,c.ResidenceBuildingId);
            Assert.IsTrue(e.Bootstrap.Roster.Where(x=>x!=c&&x.PreferredResidenceId==null).Any(x=>x.ResidenceBuildingId==house.Id));
        }
        [Test] public void HallMovesRejectFullUnfinishedOrRemovedHousingWithoutChangingOldAssignment()
        {
            var e=Create();var house=Building(e,BuildingType.House);var management=Management(e);
            foreach(var c in e.Bootstrap.Roster.ToArray())
                Assert.IsTrue(c.ResidenceBuildingId==e.Bootstrap.Settlement.TownHall.Id?e.Residences.Assign(c,e.Bootstrap.Settlement.TownHall):(bool)Invoke(management,"MoveToHall",c));
            var child=Child(e);Assert.AreEqual(house.Id,child.ResidenceBuildingId);
            Assert.IsFalse((bool)Invoke(management,"CanMoveToHall",child));Assert.IsFalse((bool)Invoke(management,"MoveToHall",child));Assert.AreEqual(house.Id,child.ResidenceBuildingId);
            Assert.IsTrue(e.TryBuild(BuildingType.House,new TileCoordinate(1,14),0,out var unfinished));Assert.IsFalse(e.Residences.Assign(child,unfinished));
            typeof(SettlementState).GetMethod("RemoveBuilding",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(e.Bootstrap.Settlement,new object[]{house});Assert.IsFalse(e.Residences.Assign(child,house));
        }
        [Test] public void CompletedWorkplaceAutomaticallyEmploysFirstAdultButNeverStartsBuilderJobs()
        {
            var e=Create();Stock(e,ResourceType.Wood,100);Assert.IsTrue(e.TryBuild(BuildingType.BuildersHut,new TileCoordinate(15,15),0,out var hut));
            Assert.IsTrue(e.Fund(hut));Assert.IsTrue(e.ContributeWork(hut,ConstructionActor.Player,300));
            Assert.AreSame(e.Bootstrap.Roster[0],e.Bootstrap.WorkerAt(hut));Assert.IsEmpty(e.Jobs);
            Assert.IsTrue(e.TryBuild(BuildingType.Farm,new TileCoordinate(1,1),0,out var farm));Assert.IsNull(e.JobAt(farm).Builder);Assert.IsFalse(e.JobAt(farm).IsFunded);
            Assert.IsTrue(e.StartConstruction(farm,e.Bootstrap.Roster[0].Id));Assert.IsTrue(e.ContributeWork(farm,ConstructionActor.Builder,10000,e.Bootstrap.Roster[0]));
            Assert.AreSame(e.Bootstrap.Roster[1],e.Bootstrap.WorkerAt(farm));Assert.IsEmpty(e.Jobs);Assert.AreEqual(4,e.Bootstrap.Roster.Count);
        }
        [Test] public void AutomaticStaffingSkipsChildrenInactiveAndEmployedAndNeverTransfersWorkers()
        {
            var e=Create(2);var existing=Builder(e);var child=e.Bootstrap.Roster[1];typeof(Citizen).GetProperty("Age").SetValue(child,5);
            var inactive=e.Bootstrap.Roster[2];typeof(Citizen).GetProperty("Active").SetValue(inactive,false);
            var farm=Building(e,BuildingType.Farm);e.Bootstrap.FillAutomaticVacancies();Assert.AreSame(e.Bootstrap.Roster[3],e.Bootstrap.WorkerAt(farm));
            Assert.AreEqual(CitizenProfession.Builder,existing.Profession);Assert.IsFalse(e.Bootstrap.TryAssignWorker(farm,existing.Id));Assert.IsNull(child.WorkplaceBuildingId);Assert.IsNull(inactive.WorkplaceBuildingId);
            var butcher=Building(e,BuildingType.Butcher,14,1);e.Bootstrap.FillAutomaticVacancies();Assert.IsNull(e.Bootstrap.WorkerAt(butcher));
            e.Advance(60);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Meat));Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
        }
        [Test] public void ManualVacancyStaysEmptyWhileOtherUnlockedWorkplacesCanHire()
        {
            var e=Create(2);var builder=Builder(e);var hut=e.Bootstrap.Settlement.Buildings.Single(b=>b.Type==BuildingType.BuildersHut);
            var farm=Building(e,BuildingType.Farm);Assert.IsTrue(e.Bootstrap.UnassignWorker(hut));e.Bootstrap.FillAutomaticVacancies();
            Assert.IsNull(e.Bootstrap.WorkerAt(hut));Assert.AreSame(builder,e.Bootstrap.WorkerAt(farm));
            e.Advance(20);Assert.IsNull(e.Bootstrap.WorkerAt(hut));Assert.AreSame(builder,e.Bootstrap.WorkerAt(farm));
            Assert.IsFalse(e.Bootstrap.TryAssignWorker(hut,builder.Id));Assert.IsTrue(e.Bootstrap.TryAssignWorker(hut,e.Bootstrap.Roster[1].Id));
        }
        [Test] public void AutomaticHiringNeverChangesAnExistingProfessionEvenWithoutAWorkplaceLink()
        {
            var e=Create();var existing=e.Bootstrap.Roster[0];typeof(Citizen).GetProperty("Profession").SetValue(existing,CitizenProfession.Farmer);
            var hut=Building(e,BuildingType.BuildersHut,15,15);e.Bootstrap.FillAutomaticVacancies();
            Assert.AreSame(e.Bootstrap.Roster[1],e.Bootstrap.WorkerAt(hut));Assert.AreEqual(CitizenProfession.Farmer,existing.Profession);
            Assert.IsNull(existing.WorkplaceBuildingId);Assert.IsFalse(e.Bootstrap.TryAssignWorker(hut,existing.Id));
        }
        [Test] public void ExistingCompletedWorkplacesInitializeEmploymentWithoutStaffingNonJobs()
        {
            var state=SettlementState.CreatePrototype(2);state.TryPlacePrototypeBuilding(BuildingType.BuildersHut,new TileCoordinate(15,15),0,out var hut);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(1,1),0,out var house);
            state.TryPlacePrototypeBuilding(BuildingType.Tavern,new TileCoordinate(1,14),0,out var tavern);
            var e=new SettlementEconomy(new SettlementBootstrap(state,CitizenSimulation.CreatePrototype(state,seed:1147)));
            Assert.AreSame(e.Bootstrap.Roster[0],e.Bootstrap.WorkerAt(hut));Assert.IsNull(e.Bootstrap.WorkerAt(state.TownHall));Assert.IsNull(e.Bootstrap.WorkerAt(house));Assert.IsNull(e.Bootstrap.WorkerAt(tavern));Assert.IsEmpty(e.Jobs);
        }
        [Test] public void NewAdulthoodFillsUnlockedVacancyWithoutRefillingManualVacancy()
        {
            var e=Create(2);var hut=Building(e,BuildingType.BuildersHut,15,15);var farm=Building(e,BuildingType.Farm);
            var butcher=Building(e,BuildingType.Butcher,14,1);var guard=Building(e,BuildingType.GuardTower,8,1);e.Bootstrap.FillAutomaticVacancies();
            var fisher=Building(e,BuildingType.FishermansHut,1,9);var child=Child(e,17);Stock(e,ResourceType.Meat,100);
            Assert.IsTrue(e.Bootstrap.UnassignWorker(hut));e.Bootstrap.TryAssignWorker(fisher,e.Bootstrap.Roster[0].Id);
            var vacancy=Building(e,BuildingType.GuardTower,14,10);e.Living.ProcessDay(2);
            Assert.IsTrue(child.IsAdult);Assert.AreSame(child,e.Bootstrap.WorkerAt(vacancy));Assert.IsNull(e.Bootstrap.WorkerAt(hut));
        }
        [Test] public void RecruitedAdultImmediatelyFillsFirstUnlockedVacancy()
        {
            var state=SettlementState.CreatePrototype(2);var e=new SettlementEconomy(new SettlementBootstrap(state,CitizenSimulation.CreatePrototype(state,seed:1147)),new Rolls());
            Building(e,BuildingType.BuildersHut,15,15);Building(e,BuildingType.Farm);Building(e,BuildingType.Butcher,14,1);Building(e,BuildingType.GuardTower,8,1);e.Bootstrap.FillAutomaticVacancies();
            var vacancy=Building(e,BuildingType.FishermansHut,1,9);Building(e,BuildingType.Tavern,6,14);Stock(e,ResourceType.Wood,350);Stock(e,ResourceType.Meat,350);
            e.Recruitment.ProcessDay(2);var candidate=e.Recruitment.Candidates.Single();Assert.IsTrue(e.Recruitment.Recruit(candidate));
            Assert.AreSame(candidate.Citizen,e.Bootstrap.WorkerAt(vacancy));Assert.AreEqual(5,e.Bootstrap.Roster.Count);Assert.IsEmpty(e.Jobs);
        }
        private static void ProduceForTest(SettlementEconomy e,BuildingState building,Citizen worker,double seconds)=>
            typeof(SettlementEconomy).GetMethod("Produce",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(e,new object[]{building,worker,seconds});
        [Test] public void WoolRequiresAssignedLivingAdultButcherAndDoesNotAdvanceAtNight()
        {
            var e=Create(5);var butcher=Building(e,BuildingType.Butcher,1,1,2);var c=e.Bootstrap.Roster[0];
            ProduceForTest(e,butcher,c,10);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            Assert.IsTrue(e.Bootstrap.TryAssignWorker(butcher,c.Id));
            typeof(Citizen).GetProperty("Age").SetValue(c,17);ProduceForTest(e,butcher,c,10);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            typeof(Citizen).GetProperty("Age").SetValue(c,18);typeof(Citizen).GetProperty("Active").SetValue(c,false);
            ProduceForTest(e,butcher,c,10);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            typeof(Citizen).GetProperty("Active").SetValue(c,true);e.Clock.Advance(420);
            ProduceForTest(e,butcher,c,10);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            e.Clock.Advance(180);ProduceForTest(e,butcher,c,9);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            ProduceForTest(e,butcher,c,1);Assert.AreEqual(1,e.Bootstrap.Resources.Amount(ResourceType.Wool));
        }
        [Test] public void WoolAllFiveLevelsUseTenSecondCyclesAndIndependentUnchangedMeatCycles()
        {
            var recipe=ResourceCatalog.Production.Single(p=>p.Resource==ResourceType.Wool);Assert.AreEqual(10,recipe.Seconds);
            for(int level=1;level<=5;level++)
            {
                var e=Create(5);var butcher=Building(e,BuildingType.Butcher,1,1,level);var worker=e.Bootstrap.Roster[0];e.Bootstrap.TryAssignWorker(butcher,worker.Id);
                int deposited=0;e.ProductionEvent+=(b,r,n,blocked)=>{if(r==ResourceType.Wool)deposited+=n;};
                ProduceForTest(e,butcher,worker,9.9);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
                ProduceForTest(e,butcher,worker,.1);Assert.AreEqual(Math.Max(0,level-1),deposited);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Meat));
                ProduceForTest(e,butcher,worker,20);Assert.AreEqual(Math.Max(0,level-1)*3,e.Bootstrap.Resources.Amount(ResourceType.Wool));Assert.AreEqual(level,e.Bootstrap.Resources.Amount(ResourceType.Meat));
            }
        }
        [Test] public void WoolPartialCycleSurvivesNightAndFullStorageReportsOnlyActualDeposits()
        {
            var e=Create(5);var butcher=Building(e,BuildingType.Butcher,1,1,5);var worker=e.Bootstrap.Roster[0];e.Bootstrap.TryAssignWorker(butcher,worker.Id);
            ProduceForTest(e,butcher,worker,6);e.Clock.Advance(420);e.Advance(120);Assert.AreEqual(0,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            e.Clock.Advance(SettlementClock.CycleSeconds-e.Clock.Elapsed%SettlementClock.CycleSeconds);
            ProduceForTest(e,butcher,worker,4);Assert.AreEqual(4,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            Stock(e,ResourceType.Wool,999);int actual=0,rejected=0;e.ProductionEvent+=(b,r,n,blocked)=>{if(r==ResourceType.Wool){actual+=n;rejected+=blocked;}};
            ProduceForTest(e,butcher,worker,10);Assert.AreEqual(1,actual);Assert.AreEqual(3,rejected);Assert.AreEqual(1000,e.Bootstrap.Resources.Amount(ResourceType.Wool));
            ProduceForTest(e,butcher,worker,10);Assert.AreEqual(1,actual);Assert.AreEqual(7,rejected);
            Stock(e,ResourceType.Wool,1100);ProduceForTest(e,butcher,worker,10);Assert.AreEqual(1100,e.Bootstrap.Resources.Amount(ResourceType.Wool));Assert.AreEqual(1,actual);
            Stock(e,ResourceType.Wool,997);ProduceForTest(e,butcher,worker,10);Assert.AreEqual(4,actual);Assert.AreEqual(1000,e.Bootstrap.Resources.Amount(ResourceType.Wool));
        }
    }
}
