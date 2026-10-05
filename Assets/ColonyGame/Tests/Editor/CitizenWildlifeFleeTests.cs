using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class CitizenWildlifeFleeTests
    {
        private static void Set(PeacefulWildlifeState animal,string name,object value)
        {
            var property=typeof(PeacefulWildlifeState).GetProperty(name);
            if(property!=null) property.SetValue(animal,value);
            else typeof(PeacefulWildlifeState).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(animal,value);
        }
        private static bool HasThreat(PeacefulWildlifeState animal)=>(bool)typeof(PeacefulWildlifeState)
            .GetField("CitizenThreat",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(animal);
        private static PeacefulWildlifeSystem Create(SettlementState state)=>new PeacefulWildlifeSystem(
            new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(-12,-12,32,32)),1);
        private static Citizen Threat(double x=19.5,double z=10.5)=>new Citizen("threat","Edwin",CitizenSex.Male,24,
            new CitizenAttributes(1,2,3,4,5),new CitizenPosition(x,z));
        private static PeacefulWildlifeState Animal(PeacefulWildlifeSystem system)
        {
            var a=system.Animals[0]; Set(a,"Position",new WildlifePosition(20.5,10.5)); Set(a,"Destination",a.Position); Set(a,"Moving",false); Set(a,"Pause",10d);
            return a;
        }
        [Test] public void NearbyCitizenInterruptsPauseAndFleesAwayIntoDeeperWilderness()
        {
            var state=SettlementState.CreatePrototype(); var system=Create(state); var animal=Animal(system); var c=Threat();
            var threat=new WildlifePosition(c.Position.X,c.Position.Z); double before=animal.Position.DistanceSquared(threat);
            system.ReactToCitizens(new[]{c},4);
            Assert.AreEqual(WildlifeMovement.Fleeing,animal.Movement); Assert.IsTrue(animal.Moving);
            Assert.Greater(animal.Destination.X,animal.Position.X); Assert.Greater(animal.Destination.DistanceSquared(threat),before);
            system.Tick(.25); Assert.Greater(animal.Position.DistanceSquared(threat),before);
        }
        [Test] public void CitizenInterruptsActiveWanderingAndSelectsNearestThreat()
        {
            var system=Create(SettlementState.CreatePrototype()); var animal=Animal(system); var c=Threat();
            Set(animal,"Moving",true); Set(animal,"Destination",new WildlifePosition(20.5,11.5));
            system.ReactToCitizens(new[]{Threat(27.5,10.5),c},4);
            Assert.AreEqual(WildlifeMovement.Fleeing,animal.Movement); Assert.AreEqual(21.5,animal.Destination.X);
        }
        [Test] public void DistantCitizensDoNotTriggerFlee()
        {
            var system=Create(SettlementState.CreatePrototype()); var animal=Animal(system);
            system.ReactToCitizens(new[]{Threat(10.5,10.5)},4);
            Assert.AreEqual(WildlifeMovement.Wandering,animal.Movement); Assert.IsFalse(animal.Moving);
        }
        [Test] public void AnimalStaysUnclaimedLeavesFleeAtSafetyAndResumesWandering()
        {
            var state=SettlementState.CreatePrototype(); var system=Create(state); var animal=Animal(system); var c=Threat();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(-12,-12,32,32));
            for(int i=0;i<300;i++)
            {
                system.ReactToCitizens(new[]{c},4); system.Tick(.1);
                Assert.IsTrue(habitat.CanOccupy(animal.Position,PeacefulWildlifeState.ClearanceRadius));
                if(animal.Position.X>=25.5&&!HasThreat(animal)) break;
            }
            Assert.IsFalse(HasThreat(animal)); Assert.AreEqual(WildlifeMovement.Wandering,animal.Movement);
            var p=animal.Position;
            for(int i=0;i<100;i++) system.Tick(.1);
            Assert.Greater(animal.Position.DistanceSquared(p),0);
        }
        [Test] public void ExpansionEvacuationOverridesCitizenFleeWithoutPositionJump()
        {
            var state=SettlementState.CreatePrototype(); var system=Create(state); var animal=Animal(system); var c=Threat();
            system.ReactToCitizens(new[]{c},4); var p=animal.Position;
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            system.ReconcileTerritory(); system.ReactToCitizens(new[]{c},4);
            Assert.AreEqual(p.X,animal.Position.X); Assert.AreEqual(p.Z,animal.Position.Z);
            Assert.AreEqual(WildlifeMovement.Evacuating,animal.Movement);
            for(int i=0;i<300;i++) { system.ReactToCitizens(new[]{c},4); system.Tick(.1); }
            Assert.IsFalse(state.Territory.IsClaimed(new TileCoordinate((int)animal.Position.X,(int)animal.Position.Z)));
        }
        [Test] public void EveryPeacefulSpeciesUsesTheSameCitizenTriggerAndConfiguredRunSpeed()
        {
            var system=Create(SettlementState.CreatePrototype()); var c=Threat();
            foreach(var a in system.Animals) { Set(a,"Position",new WildlifePosition(20.5,10.5)); Set(a,"Destination",a.Position); Set(a,"Pause",20d); Set(a,"Moving",false); }
            system.ReactToCitizens(new[]{c},4); Assert.IsTrue(system.Animals.All(a=>a.Movement==WildlifeMovement.Fleeing));
            system.Tick(.25);
            foreach(var a in system.Animals) Assert.AreEqual(20.5+PeacefulWildlifeSystem.WalkSpeed*1.8*.25,a.Position.X,1e-9);
        }
    }
}