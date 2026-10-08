using System;
using System.Linq;

namespace ColonyGame.World
{
    public enum PlayerInteractionType { None, TownHallManagement, WorkplaceManagement, CitizenInformation, ManualConstruction }
    public readonly struct PlayerInteractionResult
    {
        public PlayerInteractionType Type { get; }
        public bool ConstructionCompleted { get; }
        internal PlayerInteractionResult(PlayerInteractionType type,bool completed=false)
        { Type=type; ConstructionCompleted=completed; }
    }

    // One context-sensitive intent path for the prototype selection adapter and a future
    // physical player target resolver. It owns neither keyboard bindings nor UI rendering.
    public sealed class PlayerInteraction
    {
        private readonly SettlementBootstrap bootstrap;
        public BuildingState BuildingTarget { get; private set; }
        public Citizen CitizenTarget { get; private set; }
        public PlayerInteraction(SettlementBootstrap bootstrap)
        { this.bootstrap=bootstrap??throw new ArgumentNullException(nameof(bootstrap)); }
        public bool SelectBuilding(BuildingState building)
        {
            if(building!=null&&!bootstrap.Settlement.Buildings.Contains(building)) return false;
            BuildingTarget=building; CitizenTarget=null; return true;
        }
        public bool SelectCitizen(Citizen citizen)
        {
            if(citizen!=null&&!bootstrap.Roster.Contains(citizen)) return false;
            CitizenTarget=citizen; BuildingTarget=null; return true;
        }
        public PlayerInteractionType AvailableInteraction
        {
            get
            {
                if(CitizenTarget!=null) return bootstrap.Roster.Contains(CitizenTarget)&&CitizenTarget.Active?PlayerInteractionType.CitizenInformation:PlayerInteractionType.None;
                var b=BuildingTarget;
                if(b==null||!bootstrap.Settlement.Buildings.Contains(b)) return PlayerInteractionType.None;
                if(b.Type==BuildingType.TownHall&&b.IsCompleted) return PlayerInteractionType.TownHallManagement;
                if(b.Type!=BuildingType.BuildersHut&&!b.IsCompleted) return PlayerInteractionType.WorkplaceManagement;
                if(b.IsCompleted||bootstrap.Economy?.JobAt(b)?.Builder!=null) return PlayerInteractionType.WorkplaceManagement;
                return b.ConstructionStage==ConstructionStage.UnderConstruction&&
                    !b.IsUpgrading
                    ?PlayerInteractionType.ManualConstruction:PlayerInteractionType.None;
            }
        }
        // Discrete targets consume press edges; continuous targets consume held time.
        // Help/placement/UI adapters suppress intent here, before any work is contributed.
        public PlayerInteractionResult Act(bool pressed,bool held,double seconds,bool blocked=false)
        {
            if(!BootstrapSettings.Finite(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(blocked) return default;
            var type=AvailableInteraction;
            if(type==PlayerInteractionType.ManualConstruction)
                return held?new PlayerInteractionResult(type,bootstrap.ContributeWork(BuildingTarget,ConstructionActor.Player,seconds)):default;
            return pressed?new PlayerInteractionResult(type):default;
        }
    }
}