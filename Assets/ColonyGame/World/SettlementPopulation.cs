using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public sealed class ResidenceService
    {
        private readonly SettlementBootstrap bootstrap;
        public ResidenceService(SettlementBootstrap bootstrap) {this.bootstrap=bootstrap;}
        public IEnumerable<BuildingState> Buildings=>bootstrap.Settlement.Buildings.Where(b=>BuildingEconomyRules.ResidenceCapacity(b)>0);
        public int Capacity=>Buildings.Sum(BuildingEconomyRules.ResidenceCapacity);
        public int Homeless=>bootstrap.Roster.Count(c=>c.ResidenceBuildingId==null);
        public IEnumerable<Citizen> Residents(BuildingState building)=>bootstrap.Roster.Where(c=>c.ResidenceBuildingId==building.Id);
        public BuildingState FreeResidence=>Buildings.FirstOrDefault(b=>Residents(b).Count()<BuildingEconomyRules.ResidenceCapacity(b));
        public bool Assign(Citizen citizen,BuildingState building)
        {
            if(!bootstrap.Roster.Contains(citizen)||!Buildings.Contains(building)) return false;
            if(citizen.ResidenceBuildingId==building.Id) return true;
            if(Residents(building).Count()>=BuildingEconomyRules.ResidenceCapacity(building)) return false;
                        citizen.ResidenceBuildingId=building.Id;
            if(citizen.InsideBuildingId!=null&&citizen.InsideBuildingId!=building.Id)
            {
                // Exit at the already stored exterior access position; do not move the citizen.
                citizen.InsideBuildingId=null;citizen.Route.Clear();citizen.Destination=citizen.Position;
                citizen.Movement=CitizenMovement.Idle;citizen.ScheduledTask=CitizenTask.ReturningHome;
            }
            return true;
        }
        public void Reconcile()
        {
            foreach(var citizen in bootstrap.Roster)
                if(!Buildings.Any(b=>b.Id==citizen.ResidenceBuildingId)) citizen.ResidenceBuildingId=null;
            foreach(var building in Buildings)
                foreach(var excess in Residents(building).Skip(BuildingEconomyRules.ResidenceCapacity(building)).ToArray()) excess.ResidenceBuildingId=null;
            foreach(var citizen in bootstrap.Roster.Where(c=>c.ResidenceBuildingId==null))
            {
                var residence=FreeResidence;
                if(residence!=null) citizen.ResidenceBuildingId=residence.Id;
            }
        }
    }

    public sealed class RecruitmentCandidate
    {
        public Citizen Citizen {get;}
        public string TavernId {get;}
        public int TavernLevel {get;}
        public int ArrivalDay {get;}
        public int DepartureDay=>ArrivalDay+3;
        public double Quality=> (Citizen.Attributes.Strength+Citizen.Attributes.Dexterity+Citizen.Attributes.Intelligence+
            Citizen.Attributes.Endurance+Citizen.Attributes.Charisma)/5.0;
        public IReadOnlyDictionary<ResourceType,int> Request {get;}
        internal RecruitmentCandidate(Citizen citizen,BuildingState tavern,int day,IReadOnlyDictionary<ResourceType,int> request)
        {Citizen=citizen;TavernId=tavern.Id;TavernLevel=tavern.Level;ArrivalDay=day;Request=request;}
        public int RemainingDays(int day)=>Math.Max(0,DepartureDay-day);
    }
    public sealed class TavernRecruitment
    {
        private readonly SettlementEconomy economy;
        private readonly IGatheringRandom random;
        private readonly List<RecruitmentCandidate> candidates=new List<RecruitmentCandidate>();
        public IReadOnlyList<RecruitmentCandidate> Candidates=>candidates.AsReadOnly();
        private int processedDay=1;
        private int sequence;
        public int ArrivalRolls {get;private set;}
        public const double ArrivalChance=.1;
        private static readonly string[] MaleNames={"Arthur","Felix","Hugh","Leo","Simon","Tobias"};
        private static readonly string[] FemaleNames={"Alice","Beatrice","Eva","Iris","Mabel","Rose"};
        public TavernRecruitment(SettlementEconomy economy,IGatheringRandom random)
        {this.economy=economy;this.random=random;}
        public static int ResourceValue(ResourceType resource)=>resource==ResourceType.Iron?4:resource==ResourceType.Stone||resource==ResourceType.Wool?2:1;
        public static int RequestBudget(int level,double quality)=>(int)Math.Round(25*level*Math.Max(.75,Math.Min(1.25,.75+quality/20)),MidpointRounding.AwayFromZero);
        private int Attribute(int level)
        {
            // Independent bounded draws retain exceptional low-tier candidates and imperfect high tiers.
            int first=(1+random.Next(10));
            return random.NextDouble()<(level-1)*.15?Math.Max(first,(1+random.Next(10))):first;
        }
        public void ProcessDay(int day)
        {
            if(day<processedDay) throw new ArgumentOutOfRangeException(nameof(day));
            while(processedDay<day)
            {
                processedDay++;
                candidates.RemoveAll(c=>c.DepartureDay<=processedDay);
                foreach(var tavern in economy.Bootstrap.Settlement.Buildings.Where(b=>b.Type==BuildingType.Tavern&&b.IsCompleted).ToArray())
                {
                    ArrivalRolls++;
                    if(random.NextDouble()<ArrivalChance) candidates.Add(Generate(tavern,processedDay));
                }
            }
        }
        private RecruitmentCandidate Generate(BuildingState tavern,int day)
        {
            CitizenSex sex=random.Next(2)==0?CitizenSex.Male:CitizenSex.Female;
            var names=sex==CitizenSex.Male?MaleNames:FemaleNames;
            var citizen=new Citizen("visitor-"+(++sequence),names[random.Next(names.Length)],sex,(18+random.Next(28)),
                new CitizenAttributes(Attribute(tavern.Level),Attribute(tavern.Level),Attribute(tavern.Level),Attribute(tavern.Level),Attribute(tavern.Level)),
                CitizenPosition.Center(tavern.Entrance.ExteriorTile));
            double quality=(citizen.Attributes.Strength+citizen.Attributes.Dexterity+citizen.Attributes.Intelligence+citizen.Attributes.Endurance+citizen.Attributes.Charisma)/5.0;
            int budget=RequestBudget(tavern.Level,quality);
            var eligible=Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().Where(r=>ResourceCatalog.TavernEligible(economy.Bootstrap.Settlement,r,tavern.Level)).ToList();
            var selected=new List<ResourceType>();
            int count=(1+random.Next(Math.Min(3,eligible.Count)));
            while(selected.Count<count) {int index=random.Next(eligible.Count);selected.Add(eligible[index]);eligible.RemoveAt(index);}
            var request=new Dictionary<ResourceType,int>();
            for(int i=0;i<selected.Count;i++)
            {
                int portion=i==selected.Count-1?budget:Math.Max(1,budget/(selected.Count-i));
                int quantity=Math.Max(1,portion/ResourceValue(selected[i]));
                request[selected[i]]=quantity;budget-=quantity*ResourceValue(selected[i]);
            }
            return new RecruitmentCandidate(citizen,tavern,day,new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceType,int>(request));
        }
        public bool CanRecruit(RecruitmentCandidate candidate)=>candidate!=null&&candidates.Contains(candidate)&&economy.Residences.FreeResidence!=null&&
            economy.Bootstrap.Resources.CanPay(candidate.Request)&&economy.Access(economy.Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==candidate.TavernId),candidate.Citizen)!=null;
        public bool Recruit(RecruitmentCandidate candidate)
        {
            if(!CanRecruit(candidate)) return false;
            var tavern=economy.Bootstrap.Settlement.Buildings.First(b=>b.Id==candidate.TavernId);
            var access=economy.Access(tavern,candidate.Citizen);
            if(!economy.Bootstrap.Resources.TryPay("recruit-"+candidate.Citizen.Id,candidate.Request)) return false;
            candidate.Citizen.Position=candidate.Citizen.Destination=CitizenPosition.Center(access.Value);
            economy.Bootstrap.Simulation.AddCitizen(candidate.Citizen);
            economy.Residences.Reconcile();candidates.Remove(candidate);return true;
        }
        public void RemoveTavern(string id)=>candidates.RemoveAll(c=>c.TavernId==id);
    }
}