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
            var type=UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a=>a.GetType("ColonyGame.Presentation.PrototypeHelpState"))
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
            var settlement=SettlementState.CreatePrototype(4); var simulation=CitizenSimulation.CreatePrototype(settlement,seed:1147);
            var bootstrap=new SettlementBootstrap(settlement,simulation,new BootstrapSettings(startingWood:10)); var citizen=simulation.Citizens[0];
            bootstrap.Resources.TryTransaction("test-supply",ResourceType.Wood,15);
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

        private static object VisualRule(string method,params object[] arguments)
        {
            var type=UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a=>a.GetType("ColonyGame.Presentation.PrototypePresentationRules")).First(t=>t!=null);
            return type.GetMethod(method).Invoke(null,arguments);
        }
        [NUnit.Framework.Test] public void ConstructionPresentationRevealsFoundationWallsThenRoof()
        {
            NUnit.Framework.Assert.IsFalse((bool)VisualRule("ShowConstructionPart",0.0,false,false));
            NUnit.Framework.Assert.IsTrue((bool)VisualRule("ShowConstructionPart",.2,false,false));
            NUnit.Framework.Assert.IsFalse((bool)VisualRule("ShowConstructionPart",.2,false,true));
            NUnit.Framework.Assert.IsTrue((bool)VisualRule("ShowConstructionPart",.5,false,true));
            NUnit.Framework.Assert.IsFalse((bool)VisualRule("ShowConstructionPart",.5,true,false));
            NUnit.Framework.Assert.IsTrue((bool)VisualRule("ShowConstructionPart",.9,true,false));
        }
        [NUnit.Framework.Test] public void ProfessionAccessoriesChangeWithAssignmentAndClearWhenUnemployed()
        {
            NUnit.Framework.Assert.IsTrue((bool)VisualRule("HasHat",ColonyGame.World.CitizenProfession.Builder));
            foreach(var profession in new[]{ColonyGame.World.CitizenProfession.Builder,ColonyGame.World.CitizenProfession.Farmer,ColonyGame.World.CitizenProfession.Fisher,ColonyGame.World.CitizenProfession.Butcher,ColonyGame.World.CitizenProfession.Lumberjack,ColonyGame.World.CitizenProfession.Miner})
                NUnit.Framework.Assert.IsTrue((bool)VisualRule("HasTool",profession));
            NUnit.Framework.Assert.IsFalse((bool)VisualRule("HasHat",ColonyGame.World.CitizenProfession.Unemployed));
            NUnit.Framework.Assert.IsFalse((bool)VisualRule("HasTool",ColonyGame.World.CitizenProfession.Unemployed));
        }
        [Test] public void FireflyRenderingGatesExcludeDayClaimsAndNonForestWhileCandleFlickerNeverGoesDark()
        {
            Assert.IsTrue((bool)VisualRule("FireflyVisible",false,false,true,1.0));
            Assert.IsFalse((bool)VisualRule("FireflyVisible",true,false,true,1.0));Assert.IsFalse((bool)VisualRule("FireflyVisible",false,true,true,1.0));Assert.IsFalse((bool)VisualRule("FireflyVisible",false,false,false,1.0));
            Assert.IsFalse((bool)VisualRule("FireflyVisible",false,false,true,0.0));Assert.IsTrue((bool)VisualRule("WindowLit",false,1));Assert.IsFalse((bool)VisualRule("WindowLit",false,0));Assert.IsFalse((bool)VisualRule("WindowLit",true,1));
            for(int i=0;i<100;i++)Assert.That((double)VisualRule("WindowFlicker",i*.2,1.0),Is.InRange(.42,1.14));
            Assert.AreNotEqual(VisualRule("WindowFlicker",1.0,1.0),VisualRule("WindowFlicker",1.0,2.0));
        }
        [NUnit.Framework.Test] public void FireflyGlowFadesSmoothlyWithSustainedPeak()
        {
            NUnit.Framework.Assert.AreEqual(0,VisualRule("FireflyGlow",0.0));
            NUnit.Framework.Assert.AreEqual(.5,(double)VisualRule("FireflyGlow",.125),1e-9);
            NUnit.Framework.Assert.AreEqual(1,VisualRule("FireflyGlow",.35));
            NUnit.Framework.Assert.AreEqual(.5,(double)VisualRule("FireflyGlow",.625),1e-9);
            NUnit.Framework.Assert.AreEqual(0,VisualRule("FireflyGlow",.85));
        }
        [Test] public void CandleFlickerIsNoticeableSmoothAndDifferentForEachWindow()
        {
            var values=Enumerable.Range(0,500).Select(i=>(double)VisualRule("WindowFlicker",i*.02,1.0)).ToArray();
            Assert.Greater(values.Max()-values.Min(),.45);
            for(int i=1;i<values.Length;i++)Assert.Less(System.Math.Abs(values[i]-values[i-1]),.04);
            Assert.AreNotEqual(VisualRule("WindowFlicker",2.0,1.0),VisualRule("WindowFlicker",2.0,3.399));
            Assert.IsTrue((bool)VisualRule("WindowLit",false,2));Assert.IsFalse((bool)VisualRule("WindowLit",true,2));Assert.IsFalse((bool)VisualRule("WindowLit",false,0));
        }
        [Test] public void ProceduralGeometryHasNoExternalDecorativeFramesOrWidePathStrips()
        {
            var root=System.IO.Directory.GetCurrentDirectory();
            var schematic=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ColonyGame/Scripts/WorldPresentation/SchematicBuildingView.cs"));
            foreach(var name in new[]{"West site edge","East site edge","South site edge","North site edge","Entrance gate post"})Assert.IsFalse(schematic.Contains(name),name);
            Assert.IsTrue(schematic.Contains("Walls"));Assert.IsTrue(schematic.Contains("Roof"));Assert.IsTrue(schematic.Contains("Dark window"));Assert.IsTrue(schematic.Contains("Door"));
            var view=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/ColonyGame/Scripts/WorldPresentation/SettlementPrototypeView.cs"));
            Assert.IsFalse(view.Contains("Outdoor building-edge path"));Assert.IsTrue(view.Contains("PerimeterNavigation.Segments"));
        }
    }
}