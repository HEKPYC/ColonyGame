using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public enum ResourceType { Wood }
    public enum ConstructionStage { Planned, UnderConstruction, Completed }
    public enum ConstructionActor { Player, Builder }

    // Prototype balance, independent of presentation. Final economy remains undecided.
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
        public BootstrapSettings(int startingWood=10, int hutWoodCost=20, int woodYield=2,
            double successChance=.9, int wildernessDepth=10, double playerWorkSeconds=30,
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
        private readonly Dictionary<string,Tuple<ResourceType,int>> transactions=new Dictionary<string,Tuple<ResourceType,int>>();
        public int Amount(ResourceType type)=>amounts.TryGetValue(type,out int n)?n:0;
        public bool TryTransaction(string transactionId, ResourceType type, int delta)
        {
            if(string.IsNullOrWhiteSpace(transactionId)) throw new ArgumentException("Transaction identity is required.",nameof(transactionId));
            if(!Enum.IsDefined(typeof(ResourceType),type)) throw new ArgumentOutOfRangeException(nameof(type));
            if(transactions.TryGetValue(transactionId,out var previous)) return previous.Item1==type&&previous.Item2==delta;
            long next=(long)Amount(type)+delta;
            if(next<0 || next>int.MaxValue) return false;
            amounts[type]=(int)next;
            transactions.Add(transactionId,Tuple.Create(type,delta));
            return true;
        }
    }

    public sealed class SettlementBootstrap
    {
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
        public bool CanStartFirstHut => !Settlement.Buildings.Any(b=>b.Type==BuildingType.BuildersHut)&&Resources.Amount(ResourceType.Wood)>=Settings.HutWoodCost;
        public bool TryStartFirstHut(TileCoordinate origin,int orientation,out BuildingState site)
        {
            site=null;
            if(!CanStartFirstHut || Settlement.ValidateBuildingPlacement(BuildingType.BuildersHut,origin,orientation)!=BuildingPlacementValidity.Valid) return false;
            if(!Resources.TryTransaction("first-hut-materials",ResourceType.Wood,-Settings.HutWoodCost)) return false;
            site=Settlement.AddConstructionSite(BuildingType.BuildersHut,origin,orientation,Settings.PlayerWorkSeconds);
            return true;
        }
        // Both actor kinds share work/progress; only the first Hut has an approved work requirement now.
        public bool ContributeWork(BuildingState site,ConstructionActor actor,double seconds,Citizen worker=null)
        {
            if(!BootstrapSettings.Finite(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(site==null||!Settlement.Buildings.Contains(site)||site.ConstructionStage!=ConstructionStage.UnderConstruction) return false;
            if(actor==ConstructionActor.Player)
            {
                if(site.Type!=BuildingType.BuildersHut || Settlement.Buildings.First(b=>b.Type==BuildingType.BuildersHut)!=site) return false;
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
            building.Type==BuildingType.BuildersHut&&building.IsCompleted&&WorkerAt(building)==null
            ?Roster.Where(AmbientWoodGathering.IsEligible):Enumerable.Empty<Citizen>();
        public bool TryAssignBuilder(BuildingState building,string citizenId)
        {
            var citizen=EligibleWorkers(building).FirstOrDefault(c=>c.Id==citizenId);
            if(citizen==null) return false;
            citizen.WorkplaceBuildingId=building.Id;
            citizen.Profession=CitizenProfession.Builder;
            return true;
        }
        public string WorkplaceName(Citizen citizen)=>Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.WorkplaceBuildingId)?.Definition.DisplayName??"None";
    }
}