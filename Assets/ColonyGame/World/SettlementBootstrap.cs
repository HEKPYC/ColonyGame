using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public enum ResourceType { Wood, Stone, Iron, Wool, Beetroot, Wheat, Potato, Carrot, Fish, Meat }
    public enum ConstructionStage { Planned, UnderConstruction, Completed }
    public enum ConstructionActor { Player, Builder }

    // Central prototype balance, independent of presentation.
    public sealed class BootstrapSettings
    {
        public int StartingWood { get; }
        public int HutWoodCost { get; }
        public int WoodYield { get; }
        public double SuccessChance { get; }
        public int WildernessDepth { get; }
        public double PlayerWorkSeconds { get; }
        public double MinimumCooldown { get; }
        public double MaximumCooldown { get; }
        public double GatheringPause { get; }
        public double WildlifeDetectionRadius { get; }
        public double InitialGatherDelay { get; }
        public double CitizenGatherStagger { get; }
        public int MaximumGatherers { get; }
        public int PreferredGatherDepth { get; }
        public int DestinationCandidates { get; }
        public BootstrapSettings(int startingWood=10, int hutWoodCost=25, int woodYield=3,
            double successChance=.9, int wildernessDepth=10, double playerWorkSeconds=300,
            double minimumCooldown=18, double maximumCooldown=32, double gatheringPause=3,
            double wildlifeDetectionRadius=4, double initialGatherDelay=4, double citizenGatherStagger=6,
            int maximumGatherers=2, int preferredGatherDepth=4, int destinationCandidates=24)
        {
            if(startingWood<0 || hutWoodCost<1 || woodYield<0 || wildernessDepth<1) throw new ArgumentOutOfRangeException(nameof(startingWood));
            if(!Finite(successChance)||successChance<0||successChance>1 || !Finite(playerWorkSeconds)||playerWorkSeconds<=0 ||
                !Finite(minimumCooldown)||minimumCooldown<0 || !Finite(maximumCooldown)||maximumCooldown<minimumCooldown ||
                !Finite(gatheringPause)||gatheringPause<0 || !Finite(wildlifeDetectionRadius)||wildlifeDetectionRadius<=0)
                throw new ArgumentOutOfRangeException(nameof(successChance));
            if(!Finite(initialGatherDelay)||initialGatherDelay<0||!Finite(citizenGatherStagger)||citizenGatherStagger<0||
                maximumGatherers<1||preferredGatherDepth<1||destinationCandidates<1) throw new ArgumentOutOfRangeException(nameof(initialGatherDelay));
            InitialGatherDelay=initialGatherDelay; CitizenGatherStagger=citizenGatherStagger; MaximumGatherers=maximumGatherers;
            PreferredGatherDepth=preferredGatherDepth; DestinationCandidates=destinationCandidates;
            StartingWood=startingWood; HutWoodCost=hutWoodCost; WoodYield=woodYield; SuccessChance=successChance;
            WildernessDepth=wildernessDepth; PlayerWorkSeconds=playerWorkSeconds;
            MinimumCooldown=minimumCooldown; MaximumCooldown=maximumCooldown; GatheringPause=gatheringPause;
            WildlifeDetectionRadius=wildlifeDetectionRadius;
        }
        internal static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    }

    // Transaction identifiers make delivery/debit retries idempotent. No physical containers yet.
    public sealed class ResourceInventory
    {
        private readonly Dictionary<ResourceType,int> amounts=new Dictionary<ResourceType,int>();
        private readonly Dictionary<string,string> transactions=new Dictionary<string,string>();
        public int Amount(ResourceType type)=>amounts.TryGetValue(type,out int n)?n:0;
        public bool CanPay(IReadOnlyDictionary<ResourceType,int> cost)=>cost.All(x=>x.Value>=0&&Amount(x.Key)>=x.Value);
        public bool TryPay(string id,IReadOnlyDictionary<ResourceType,int> cost)
        {
            if(cost.Any(x=>x.Value<0)) throw new ArgumentOutOfRangeException(nameof(cost));
            return TryBundle(id,cost.ToDictionary(x=>x.Key,x=>-x.Value));
        }
        public bool TryTransaction(string id,ResourceType type,int delta)=>TryBundle(id,new Dictionary<ResourceType,int>{{type,delta}});
        public bool TryBundle(string id,IReadOnlyDictionary<ResourceType,int> changes)
        {
            if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Transaction identity is required.",nameof(id));
            if(changes==null) throw new ArgumentNullException(nameof(changes));
            if(changes.Keys.Any(x=>!Enum.IsDefined(typeof(ResourceType),x))) throw new ArgumentOutOfRangeException(nameof(changes));
            string fingerprint=string.Join(";",changes.OrderBy(x=>x.Key).Select(x=>$"{(int)x.Key}:{x.Value}"));
            if(transactions.TryGetValue(id,out var old)) return old==fingerprint;
            if(changes.Any(x=>(long)Amount(x.Key)+x.Value<0||(long)Amount(x.Key)+x.Value>int.MaxValue)) return false;
            foreach(var x in changes) amounts[x.Key]=Amount(x.Key)+x.Value;
            transactions.Add(id,fingerprint);
            return true;
        }
    }
    public sealed class SettlementBootstrap
    {
        public SettlementEconomy Economy { get; internal set; }
        public SettlementState Settlement { get; }
        public CitizenSimulation Simulation { get; }
        public BootstrapSettings Settings { get; }
        public ResourceInventory Resources { get; }=new ResourceInventory();
        public string StorageBuildingId => Settlement.TownHall.Id;
        public IReadOnlyList<Citizen> Roster => Simulation.Citizens;
        public int EmployedCount=>Roster.Count(c=>c.Profession!=CitizenProfession.Unemployed);
        public int UnemployedCount=>Roster.Count(c=>c.Profession==CitizenProfession.Unemployed);
        public SettlementBootstrap(SettlementState settlement,CitizenSimulation simulation,BootstrapSettings settings=null)
        {
            Settlement=settlement??throw new ArgumentNullException(nameof(settlement));
            Simulation=simulation??throw new ArgumentNullException(nameof(simulation));
            Settings=settings??new BootstrapSettings();
            Resources.TryTransaction("initial-wood",ResourceType.Wood,Settings.StartingWood);
        }
        public bool CanStartFirstHut => Resources.Amount(ResourceType.Wood)>=Settings.HutWoodCost;
        public bool TryStartFirstHut(TileCoordinate origin,int orientation,out BuildingState site)
        {
            site=null;
            if(!CanStartFirstHut || Settlement.ValidateBuildingPlacement(BuildingType.BuildersHut,origin,orientation)!=BuildingPlacementValidity.Valid) return false;
            if(!Resources.TryTransaction("hut-materials-"+Guid.NewGuid().ToString("N"),ResourceType.Wood,-Settings.HutWoodCost)) return false;
            site=Settlement.AddConstructionSite(BuildingType.BuildersHut,origin,orientation,Settings.PlayerWorkSeconds);
            site.Invest(new Dictionary<ResourceType,int>{{ResourceType.Wood,Settings.HutWoodCost}});
            return true;
        }
        // The player contributes only to initial Level 1 Hut construction.
        public bool ContributeWork(BuildingState site,ConstructionActor actor,double seconds,Citizen worker=null)
        {
            if(!BootstrapSettings.Finite(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(site==null||!Settlement.Buildings.Contains(site)||site.ConstructionStage!=ConstructionStage.UnderConstruction) return false;
            if(Economy!=null) return Economy.ContributeWork(site,actor,seconds,worker);
            if(actor==ConstructionActor.Player)
            {
                if(site.Type!=BuildingType.BuildersHut || site.IsUpgrading) return false;
            }
            else if(actor==ConstructionActor.Builder)
            {
                if(worker==null||!Roster.Contains(worker)||worker.Profession!=CitizenProfession.Builder||worker.WorkplaceBuildingId==null) return false;
            }
            else return false;
            site.ConstructionWork=Math.Min(site.RequiredConstructionWork,site.ConstructionWork+seconds);
            if(site.ConstructionWork<site.RequiredConstructionWork) return false;
            site.ConstructionStage=ConstructionStage.Completed;
            Settlement.ApplyBuildingInfluence(site);
            return true;
        }
        public Citizen WorkerAt(BuildingState building)=>building==null?null:Roster.FirstOrDefault(c=>c.WorkplaceBuildingId==building.Id);
        public IEnumerable<Citizen> EligibleWorkers(BuildingState building)=>building!=null&&Settlement.Buildings.Contains(building)&&
            BuildingEconomyRules.Profession(building.Type)!=CitizenProfession.Unemployed&&building.IsCompleted&&WorkerAt(building)==null
            ?Roster.Where(AmbientWoodGathering.IsEligible):Enumerable.Empty<Citizen>();
        public bool TryAssignBuilder(BuildingState building,string citizenId)=>building!=null&&building.Type==BuildingType.BuildersHut&&TryAssignWorker(building,citizenId);
        public bool TryAssignWorker(BuildingState building,string citizenId)
        {
            var citizen=EligibleWorkers(building).FirstOrDefault(c=>c.Id==citizenId);
            if(citizen==null) return false;
            citizen.WorkplaceBuildingId=building.Id;
            citizen.Profession=BuildingEconomyRules.Profession(building.Type);
            return true;
        }
        public string WorkplaceName(Citizen citizen)=>Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.WorkplaceBuildingId)?.Definition.DisplayName??"None";
    }
}