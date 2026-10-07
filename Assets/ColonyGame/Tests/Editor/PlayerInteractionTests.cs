using System;
using NUnit.Framework;

namespace ColonyGame.World.Tests
{
    public sealed class PlayerInteractionTests
    {
        private static SettlementBootstrap Bootstrap()
        {
            var state=SettlementState.CreatePrototype(4);
            return new SettlementBootstrap(state,CitizenSimulation.CreatePrototype(state),new BootstrapSettings(playerWorkSeconds:30));
        }
        private static BuildingState Site(SettlementBootstrap b)
        {
            b.Resources.TryTransaction("test-supply",ResourceType.Wood,15);
            Assert.IsTrue(b.TryStartFirstHut(new TileCoordinate(17,8),0,out var site)); return site;
        }
        [Test] public void NoTargetExposesNoInteractionAndPressDoesNotMutateSettlement()
        {
            var b=Bootstrap(); var interaction=new PlayerInteraction(b);
            Assert.AreEqual(PlayerInteractionType.None,interaction.AvailableInteraction);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(true,true,1).Type);
            Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(400,b.Settlement.Territory.Count);
        }
        [Test] public void TownHallUsesDiscretePressRatherThanRepeatedHeldAction()
        {
            var b=Bootstrap(); var interaction=new PlayerInteraction(b); interaction.SelectBuilding(b.Settlement.TownHall);
            Assert.AreEqual(PlayerInteractionType.TownHallManagement,interaction.AvailableInteraction);
            Assert.AreEqual(PlayerInteractionType.TownHallManagement,interaction.Act(true,false,1).Type);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(false,true,10).Type);
            Assert.AreEqual(10,b.Resources.Amount(ResourceType.Wood)); Assert.AreEqual(1,b.Settlement.Buildings.Count);
        }
        [Test] public void IncompleteHutRequiresHoldAndNeverExposesWorkplaceManagement()
        {
            var b=Bootstrap(); var site=Site(b); var interaction=new PlayerInteraction(b); interaction.SelectBuilding(site);
            Assert.AreEqual(PlayerInteractionType.ManualConstruction,interaction.AvailableInteraction);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(true,false,10).Type); Assert.AreEqual(0,site.ConstructionWork);
            Assert.AreEqual(PlayerInteractionType.ManualConstruction,interaction.Act(false,true,15).Type);
            Assert.AreEqual(15,site.ConstructionWork); Assert.AreEqual(400,b.Settlement.Territory.Count);
            interaction.Act(false,false,20); Assert.AreEqual(15,site.ConstructionWork);
        }
        [Test] public void CompletionChangesContextOnceWithoutAutomaticManagementOrAssignment()
        {
            var b=Bootstrap(); var site=Site(b); var interaction=new PlayerInteraction(b); interaction.SelectBuilding(site);
            var result=interaction.Act(true,true,30);
            Assert.IsTrue(result.ConstructionCompleted); Assert.AreEqual(427,b.Settlement.Territory.Count);
            Assert.AreEqual(PlayerInteractionType.WorkplaceManagement,interaction.AvailableInteraction);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(false,true,30).Type);
            Assert.AreEqual(PlayerInteractionType.WorkplaceManagement,interaction.Act(true,false,0).Type);
            Assert.AreEqual(0,b.EmployedCount); Assert.IsNull(b.WorkerAt(site)); Assert.AreEqual(427,b.Settlement.Territory.Count);
        }
        [Test] public void CompletedHutInteractionStillRequiresExplicitCitizenChoiceForAssignment()
        {
            var b=Bootstrap(); var site=Site(b); b.ContributeWork(site,ConstructionActor.Player,30);
            var interaction=new PlayerInteraction(b); interaction.SelectBuilding(site);
            interaction.Act(true,true,10); Assert.AreEqual(0,b.EmployedCount);
            var chosen=b.Roster[2]; Assert.IsTrue(b.TryAssignBuilder(site,chosen.Id)); Assert.AreSame(chosen,b.WorkerAt(site));
            interaction.Act(true,true,10); Assert.AreEqual(1,b.EmployedCount); Assert.AreSame(chosen,b.WorkerAt(site));
        }
        [Test] public void CitizenPressReturnsInformationWithoutMutatingIdentityOrState()
        {
            var b=Bootstrap(); var citizen=b.Roster[1]; var interaction=new PlayerInteraction(b); interaction.SelectCitizen(citizen);
            var attributes=citizen.Attributes; var position=citizen.Position; string id=citizen.Id,name=citizen.FirstName;
            var sex=citizen.Sex; int age=citizen.Age;
            Assert.AreEqual(PlayerInteractionType.CitizenInformation,interaction.Act(true,false,1).Type);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(false,true,10).Type);
            Assert.AreSame(citizen,interaction.CitizenTarget); Assert.AreEqual(id,citizen.Id); Assert.AreEqual(name,citizen.FirstName);
            Assert.AreEqual(sex,citizen.Sex); Assert.AreEqual(age,citizen.Age); Assert.AreSame(attributes,citizen.Attributes);
            Assert.AreEqual(position.X,citizen.Position.X); Assert.AreEqual(position.Z,citizen.Position.Z);
            Assert.AreEqual(CitizenProfession.Unemployed,citizen.Profession); Assert.IsNull(citizen.WorkplaceBuildingId);
        }
        [Test] public void BlockedHelpOrPlacementSuppressesBothPressAndHoldWithoutClearingTarget()
        {
            var b=Bootstrap(); var site=Site(b); var interaction=new PlayerInteraction(b); interaction.SelectBuilding(site);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(true,true,30,blocked:true).Type);
            Assert.AreEqual(0,site.ConstructionWork); Assert.AreSame(site,interaction.BuildingTarget);
            interaction.SelectBuilding(b.Settlement.TownHall);
            Assert.AreEqual(PlayerInteractionType.None,interaction.Act(true,true,1,blocked:true).Type);
            interaction.SelectCitizen(b.Roster[0]); Assert.AreEqual(PlayerInteractionType.None,interaction.Act(true,true,1,blocked:true).Type);
        }
        [Test] public void SelectingContextReplacesOnlyInteractionTargetAndRejectsForeignObjects()
        {
            var b=Bootstrap(); var interaction=new PlayerInteraction(b); var foreign=Bootstrap();
            interaction.SelectBuilding(b.Settlement.TownHall); interaction.SelectCitizen(b.Roster[0]); Assert.IsNull(interaction.BuildingTarget);
            Assert.IsFalse(interaction.SelectBuilding(foreign.Settlement.TownHall)); Assert.AreSame(b.Roster[0],interaction.CitizenTarget);
            Assert.IsFalse(interaction.SelectCitizen(foreign.Roster[0])); Assert.AreSame(b.Roster[0],interaction.CitizenTarget);
            interaction.SelectBuilding(null); Assert.IsNull(interaction.CitizenTarget); Assert.AreEqual(PlayerInteractionType.None,interaction.AvailableInteraction);
            Assert.Throws<ArgumentOutOfRangeException>(()=>interaction.Act(false,true,-1));
        }
    }
}