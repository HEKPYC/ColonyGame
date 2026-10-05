using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    // The existing test assembly cannot directly reference Unity's predefined Assembly-CSharp.
    // Resolve the small presentation-only state by type name; no UI objects or pixel assertions.
    public sealed class PrototypeHelpStateTests
    {
        private static object Create()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ColonyGame.Presentation.PrototypeHelpState"))
                .FirstOrDefault(t=>t!=null);
            Assert.IsNotNull(type,"Prototype presentation assembly must be loaded.");
            return Activator.CreateInstance(type);
        }
        private static bool Read(object state,string property)=>(bool)state.GetType().GetProperty(property).GetValue(state);
        private static object Call(object state,string method,params object[] arguments)=>state.GetType().GetMethod(method).Invoke(state,arguments);
        [Test] public void HelpInitiallyClosedAndAllowsGameplayShortcuts()
        {
            var state=Create(); Assert.IsFalse(Read(state,"IsOpen")); Assert.IsTrue(Read(state,"AllowsGameplayShortcuts"));
        }
        [Test] public void HelpToggleKeyAndButtonShareStateAndConsumeClosingFrame()
        {
            var state=Create();
            Assert.IsTrue((bool)Call(state,"HandleKeys",true,false)); Assert.IsTrue(Read(state,"IsOpen"));
            Assert.IsFalse(Read(state,"AllowsGameplayShortcuts"));
            Call(state,"Toggle"); Assert.IsFalse(Read(state,"IsOpen"));
            Call(state,"Toggle"); Assert.IsTrue(Read(state,"IsOpen"));
            Assert.IsTrue((bool)Call(state,"HandleKeys",true,false)); Assert.IsFalse(Read(state,"IsOpen"));
        }
        [Test] public void HelpEscapeConsumesOnlyWhenOpenAndDoesNotReopen()
        {
            var state=Create(); Assert.IsFalse((bool)Call(state,"HandleKeys",false,true));
            Call(state,"Toggle"); Assert.IsTrue((bool)Call(state,"HandleKeys",false,true));
            Assert.IsFalse(Read(state,"IsOpen")); Assert.IsFalse((bool)Call(state,"HandleKeys",false,true));
            Call(state,"Close"); Assert.IsFalse(Read(state,"IsOpen"));
        }
        [Test] public void HelpNoKeysPreservesStateAndToggleWinsSimultaneousEscape()
        {
            var state=Create(); Assert.IsFalse((bool)Call(state,"HandleKeys",false,false));
            Assert.IsTrue((bool)Call(state,"HandleKeys",true,true)); Assert.IsTrue(Read(state,"IsOpen"));
            Assert.IsFalse((bool)Call(state,"HandleKeys",false,false)); Assert.IsTrue(Read(state,"IsOpen"));
            Assert.IsTrue((bool)Call(state,"HandleKeys",true,true)); Assert.IsFalse(Read(state,"IsOpen"));
        }
        [Test] public void HelpOperationsDoNotMutateSettlementOrCitizenAndConstructionState()
        {
            var settlement=SettlementState.CreatePrototype(); var simulation=CitizenSimulation.CreatePrototype(settlement);
            var bootstrap=new SettlementBootstrap(settlement,simulation); var citizen=simulation.Citizens[0];
            bootstrap.Resources.TryTransaction("test-supply",ResourceType.Wood,10);
            bootstrap.TryStartFirstHut(new TileCoordinate(17,8),90,out var site);
            bootstrap.ContributeWork(site,ConstructionActor.Player,12);
            var position=citizen.Position; var attributes=citizen.Attributes; var state=Create();
            Call(state,"Toggle"); Call(state,"HandleKeys",false,true); Call(state,"Toggle"); Call(state,"Close");
            Assert.AreEqual(400,settlement.Territory.Count); Assert.AreEqual(2,settlement.Buildings.Count);
            Assert.AreEqual(0,bootstrap.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(12,site.ConstructionWork);
            Assert.AreEqual(90,site.OrientationDegrees); Assert.IsFalse(site.TerritoryInfluenceApplied);
            Assert.AreEqual(position.X,citizen.Position.X); Assert.AreEqual(position.Z,citizen.Position.Z);
            Assert.AreSame(attributes,citizen.Attributes); Assert.AreEqual(CitizenProfession.Unemployed,citizen.Profession);
        }
    }
}