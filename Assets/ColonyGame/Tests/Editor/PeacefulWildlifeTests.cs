using System;
using System.Linq;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class PeacefulWildlifeTests
    {
        [Test]
        public void SpawnsOnlyInRepresentedWildernessAndIncludesFourSpecies()
        {
            var state = SettlementState.CreatePrototype();
            var habitat = new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(-6,-6,26,26));
            var system = new PeacefulWildlifeSystem(habitat,2);
            Assert.That(system.Animals.Count,Is.EqualTo(8));
            Assert.That(system.Animals.Select(a=>a.Species).Distinct().Count(),Is.EqualTo(4));
            Assert.That(system.Animals.All(a=>habitat.CanOccupy(a.Position,PeacefulWildlifeState.ClearanceRadius)),Is.True);
            Assert.That(habitat.CanOccupy(new WildlifePosition(10.5,10.5),.48),Is.False);
            Assert.That(habitat.CanOccupy(new WildlifePosition(26.5,0.5),.48),Is.False);
        }
        [Test]
        public void EligibilityRespectsIrregularClaimsAndBodyClearance()
        {
            var territory = new SettlementTerritory(new[]{new TileCoordinate(0,0),new TileCoordinate(1,0),new TileCoordinate(0,1),new TileCoordinate(4,3)});
            var habitat = new PeacefulWildernessHabitat(territory,new WildlifeBounds(-3,-3,8,8));
            Assert.That(habitat.SpawnTiles.Contains(new TileCoordinate(1,1)),Is.True);
            Assert.That(habitat.CanOccupy(new WildlifePosition(1.5,1.5),.48),Is.True);
            Assert.That(habitat.CanOccupy(new WildlifePosition(1.1,1.1),.48),Is.False);
            Assert.That(habitat.SpawnTiles.Contains(new TileCoordinate(4,3)),Is.False);
            Assert.That(habitat.CanOccupy(new WildlifePosition(4.5,3.5),.48),Is.False);
        }
        [Test]
        public void WanderingStaysSafeAndMovesContinuouslyWithoutRapidJumps()
        {
            var state = SettlementState.CreatePrototype();
            var habitat = new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(-6,-6,26,26));
            var system = new PeacefulWildlifeSystem(habitat,3);
            int moves=0,pauses=0;
            for(int step=0;step<1200;step++)
            {
                var before=system.Animals.Select(a=>a.Position).ToArray();
                system.Tick(.05);
                for(int i=0;i<before.Length;i++)
                {
                    var animal=system.Animals[i];
                    Assert.That(habitat.CanOccupy(animal.Position,.48),Is.True);
                    Assert.That(habitat.CanOccupy(animal.Destination,.48),Is.True);
                    Assert.That(animal.Position.DistanceSquared(before[i]),Is.LessThanOrEqualTo(.0325*.0325+.000001));
                    if(animal.Position.DistanceSquared(before[i])>0) moves++; else pauses++;
                }
            }
            Assert.That(moves,Is.GreaterThan(0)); Assert.That(pauses,Is.GreaterThan(0));
        }
        [Test]
        public void NewClaimsStartEvacuationWithoutRelocationOrTerritoryChanges()
        {
            var state=SettlementState.CreatePrototype();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,5,26,14));
            var system=new PeacefulWildlifeSystem(habitat,4);
            var original=system.Animals.Select(a=>a.Position).ToArray();
            Assert.That(habitat.CanOccupy(new WildlifePosition(20.5,8.5),.48),Is.True);
            Assert.That(state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _),Is.True);
            Assert.That(habitat.CanOccupy(new WildlifePosition(20.5,8.5),.48),Is.False);
            Assert.That(original.Any(p=>!habitat.CanOccupy(p,.48)),Is.True);
            system.ReconcileTerritory();
            Assert.That(system.Animals.Count,Is.EqualTo(16));
            for(int i=0;i<original.Length;i++)
            {
                Assert.That(system.Animals[i].Position.DistanceSquared(original[i]),Is.Zero);
                Assert.That(system.Animals[i].Movement,Is.EqualTo(habitat.CanOccupy(original[i],.48)?WildlifeMovement.Wandering:WildlifeMovement.Evacuating));
                Assert.That(habitat.CanOccupy(system.Animals[i].Destination,.48),Is.True);
            }
            Assert.That(system.Animals.All(a=>a.Active),Is.True);
            Assert.That(state.Territory.Count,Is.EqualTo(427));
            for(int i=0;i<100;i++) system.Tick(.1);
            Assert.That(system.Animals.All(a=>habitat.CanOccupy(a.Position,.48)),Is.True);
        }
        [Test]
        public void ClaimedDestinationIsAbandonedEvenWhenCurrentPositionIsStillSafe()
        {
            var state=SettlementState.CreatePrototype();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,5,26,14));
            var system=new PeacefulWildlifeSystem(habitat,10);
            for(int i=0;i<12;i++) system.Tick(.1);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            system.ReconcileTerritory();
            Assert.That(system.Animals.Where(a=>a.Active).All(a=>(habitat.CanOccupy(a.Position,.48)||a.Movement==WildlifeMovement.Evacuating)&&habitat.CanOccupy(a.Destination,.48)),Is.True);
        }
        [Test]
        public void NoWildernessMeansNoSpawnOrSafeDormancyWithoutKilling()
        {
            var state=SettlementState.CreatePrototype();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,5,23,14));
            var system=new PeacefulWildlifeSystem(habitat,2);
            Assert.That(system.Animals.Count,Is.EqualTo(8));
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            system.ReconcileTerritory();
            Assert.That(system.Animals.All(a=>!a.Active),Is.True);
            Assert.That(new PeacefulWildlifeSystem(habitat,2).Animals.Count,Is.Zero);
            habitat.Bounds=new WildlifeBounds(20,5,26,14);
            system.ReconcileTerritory();
            for(int i=0;i<100;i++) system.Tick(.1);
            Assert.That(system.Animals.All(a=>a.Active&&habitat.CanOccupy(a.Position,.48)),Is.True);
        }
        [Test]
        public void AbstractFleeIncreasesThreatDistanceAndPrefersActualBoundaryDepth()
        {
            var territory=new SettlementTerritory(new[]{new TileCoordinate(0,0),new TileCoordinate(1,0),new TileCoordinate(0,1),new TileCoordinate(5,3)});
            var habitat=new PeacefulWildernessHabitat(territory,new WildlifeBounds(-4,-4,10,10));
            var system=new PeacefulWildlifeSystem(habitat,1);
            foreach(var animal in system.Animals)
            {
                var threat=new WildlifePosition(animal.Position.X-.2,animal.Position.Z-.2);
                var candidates=new[]{new WildlifePosition(animal.Position.X+1,animal.Position.Z),new WildlifePosition(animal.Position.X-1,animal.Position.Z),new WildlifePosition(animal.Position.X,animal.Position.Z+1),new WildlifePosition(animal.Position.X,animal.Position.Z-1)}
                    .Where(p=>habitat.CanOccupy(p,.48)&&p.DistanceSquared(threat)>animal.Position.DistanceSquared(threat)).ToArray();
                if(candidates.Length==0)continue;
                Assert.That(system.RequestFlee(animal.Id,threat),Is.True);
                Assert.That(habitat.CanOccupy(animal.Destination,.48),Is.True);
                Assert.That(animal.Destination.DistanceSquared(threat),Is.GreaterThan(animal.Position.DistanceSquared(threat)));
                Assert.That(habitat.SafetyDepth(animal.Destination),Is.EqualTo(candidates.Max(habitat.SafetyDepth)));
            }
            Assert.That(habitat.SafetyDepth(new WildlifePosition(5.5,4.5)),Is.EqualTo(.25));
        }
        [Test]
        public void MultipleAnimalsRunOutwardContinuouslyThenResumeSafeWandering()
        {
            var state=SettlementState.CreatePrototype();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,5,26,14));
            var system=new PeacefulWildlifeSystem(habitat,4,runSpeedMultiplier:2);
            int warnings=0;
            system.DevelopmentWarning+=_=>warnings++;
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            var affected=system.Animals.Where(a=>!habitat.CanOccupy(a.Position,.48)).ToArray();
            Assert.That(affected.Length,Is.GreaterThan(1));
            var starts=affected.Select(a=>a.Position).ToArray();
            system.ReconcileTerritory();
            for(int i=0;i<affected.Length;i++)
            {
                Assert.That(affected[i].Position.DistanceSquared(starts[i]),Is.Zero);
                Assert.That(affected[i].Movement,Is.EqualTo(WildlifeMovement.Evacuating));
                // This bounded test habitat has Wilderness only east of the new protrusion.
                Assert.That(affected[i].Destination.X,Is.GreaterThanOrEqualTo(24.5));
            }
            bool crossed=false,ran=false;
            for(int tick=0;tick<100;tick++)
            {
                var before=affected.Select(a=>a.Position).ToArray();
                var modes=affected.Select(a=>a.Movement).ToArray();
                system.Tick(.05);
                for(int i=0;i<affected.Length;i++)
                {
                    var a=affected[i];
                    double distance=Math.Sqrt(a.Position.DistanceSquared(before[i]));
                    Assert.That(distance,Is.LessThanOrEqualTo(.0650001));
                    if(modes[i]==WildlifeMovement.Evacuating)
                    {
                        Assert.That(a.Position.X,Is.GreaterThanOrEqualTo(before[i].X));
                        if(distance>.0325001) ran=true;
                    }
                    if(habitat.CanOccupy(a.Position,.48))
                    {
                        Assert.That(a.Movement,Is.Not.EqualTo(WildlifeMovement.Evacuating));
                        if(a.Movement==WildlifeMovement.Fleeing) crossed=true;
                    }
                    else Assert.That(a.Movement,Is.EqualTo(WildlifeMovement.Evacuating));
                }
            }
            Assert.That(ran,Is.True); Assert.That(crossed,Is.True);
            Assert.That(affected.All(a=>a.Movement==WildlifeMovement.Wandering),Is.True);
            var safeStarts=affected.Select(a=>a.Position).ToArray();
            for(int tick=0;tick<300;tick++)
            {
                system.Tick(.05);
                Assert.That(system.Animals.All(a=>habitat.CanOccupy(a.Position,.48)&&a.Movement!=WildlifeMovement.Evacuating),Is.True);
            }
            Assert.That(affected.Where((a,i)=>a.Position.DistanceSquared(safeStarts[i])>0).Any(),Is.True);
            Assert.That(warnings,Is.Zero);
            Assert.That(state.Territory.Count,Is.EqualTo(427));
        }
        [Test]
        public void ShortestExitUsesIrregularProtrusionRatherThanSettlementRectangle()
        {
            var state=SettlementState.CreatePrototype();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,3,26,16));
            var system=new PeacefulWildlifeSystem(habitat,20);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            var affected=system.Animals.Where(a=>!habitat.CanOccupy(a.Position,.48)).ToArray();
            var before=affected.Select(a=>a.Position).ToArray();
            system.ReconcileTerritory();
            for(int i=0;i<affected.Length;i++)
            {
                var a=affected[i];
                // Nearest exit is east (x23), south (z4), or north (z14).
                double nearest=Math.Min(23.5-before[i].X,Math.Min(before[i].Z-4.5,14.5-before[i].Z));
                system.Tick(0);
                Assert.That(a.Position.DistanceSquared(before[i]),Is.Zero);
                Assert.That(habitat.CanOccupy(a.Destination,.48),Is.True);
                Assert.That(Math.Abs(a.Destination.X-before[i].X)+Math.Abs(a.Destination.Z-before[i].Z),Is.LessThanOrEqualTo(nearest+1));
            }
        }
        [Test]
        public void FurtherExpansionReplansWithoutTeleportingAnEvacuatingAnimal()
        {
            var state=SettlementState.CreatePrototype();
            var habitat=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,5,30,14));
            var system=new PeacefulWildlifeSystem(habitat,12);
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            system.ReconcileTerritory(); system.Tick(.2);
            var positions=system.Animals.Select(a=>a.Position).ToArray();
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(20,8),0,out _);
            system.ReconcileTerritory();
            for(int i=0;i<positions.Length;i++)
            {
                Assert.That(system.Animals[i].Position.DistanceSquared(positions[i]),Is.Zero);
                Assert.That(habitat.CanOccupy(system.Animals[i].Destination,.48),Is.True);
            }
            for(int i=0;i<100;i++)system.Tick(.1);
            Assert.That(system.Animals.All(a=>habitat.CanOccupy(a.Position,.48)),Is.True);
            Assert.That(state.Territory.Count,Is.EqualTo(454));
        }
        private sealed class BrokenRouteHabitat : IWildlifeHabitat
        {
            public PeacefulWildernessHabitat Inner;
            public bool CanOccupy(WildlifePosition p,double radius)=>Inner.CanOccupy(p,radius);
            public bool CanTraverseEvacuation(WildlifePosition p,double radius)=>false;
            public System.Collections.Generic.IEnumerable<TileCoordinate> SpawnTiles=>Inner.SpawnTiles;
            public double SafetyDepth(WildlifePosition p)=>Inner.SafetyDepth(p);
        }
        [Test]
        public void ImpossibleRouteUsesWarningBearingEmergencyFallbackOnly()
        {
            var state=SettlementState.CreatePrototype();
            var inner=new PeacefulWildernessHabitat(state.Territory,new WildlifeBounds(20,5,26,14));
            var system=new PeacefulWildlifeSystem(new BrokenRouteHabitat{Inner=inner},4);
            string warning=null; system.DevelopmentWarning+=message=>warning=message;
            state.TryPlacePrototypeBuilding(BuildingType.House,new TileCoordinate(17,8),0,out _);
            system.ReconcileTerritory();
            Assert.That(warning,Does.Contain("emergency relocation"));
            Assert.That(system.Animals.All(a=>inner.CanOccupy(a.Position,.48)),Is.True);
        }        [Test]
        public void InvalidTickAndPopulationInputsAreRejected()
        {
            var habitat=new PeacefulWildernessHabitat(new SettlementTerritory(new TileCoordinate[0]),new WildlifeBounds(0,0,3,3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new PeacefulWildlifeSystem(habitat,-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new PeacefulWildlifeSystem(habitat,0,runSpeedMultiplier:1));
            var system=new PeacefulWildlifeSystem(habitat,0);
            Assert.Throws<ArgumentOutOfRangeException>(()=>system.Tick(-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>system.Tick(double.NaN));
            Assert.That(habitat.CanOccupy(new WildlifePosition(double.NaN,1),.48),Is.False);
        }
    }
}
