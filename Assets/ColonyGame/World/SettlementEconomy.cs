using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public enum ConstructionJobKind { Construction, Upgrade, Demolition }

    public sealed class ConstructionJob
    {
        public BuildingState Building { get; }
        public Citizen Builder { get; internal set; }
        public ConstructionJobKind Kind { get; internal set; }
        public bool IsFunded => PaidCost != null;
        public double Progress { get; internal set; }
        public double RequiredWork { get; internal set; }
        public IReadOnlyDictionary<ResourceType,int> Materials => Kind == ConstructionJobKind.Demolition
            ? new Dictionary<ResourceType,int>() : BuildingEconomyRules.Cost(Building.Type, Kind == ConstructionJobKind.Upgrade ? Building.TargetLevel : 1);
        internal IReadOnlyDictionary<ResourceType,int> PaidCost;
        internal ConstructionJob(BuildingState building,Citizen builder,IReadOnlyDictionary<ResourceType,int> cost)
        { Building=building;Builder=builder;PaidCost=cost; }
    }

    // Coordinates existing inventory, buildings and citizens; all movement stays in CitizenSimulation.
    public sealed class SettlementEconomy
    {
        public SettlementBootstrap Bootstrap { get; }
        public SettlementClock Clock { get; } = new SettlementClock();
        public SettlementLiving Living {get;}
        public TavernRecruitment Recruitment { get; }
        public ResidenceService Residences { get; }
        private sealed class WorkerRoutine
        {
            public int PreparedDay, RoutineDay;
            public double Preparation,Work,InsideUntil,FreeUntil;
            public bool OnSite,SupplyTrip;
            public string SiteId;
        }
        private readonly Dictionary<string,WorkerRoutine> routines=new Dictionary<string,WorkerRoutine>();
        private WorkerRoutine Routine(Citizen citizen)
        {if(!routines.TryGetValue(citizen.Id,out var routine)) routines.Add(citizen.Id,routine=new WorkerRoutine());return routine;}
        private readonly HashSet<string> availableAdults=new HashSet<string>();
        // An eligibility transition opens unlocked vacancies; manual locks persist until explicit hiring.
        private void RefreshEmployment()
        {
            var eligible=Bootstrap.Roster.Where(c=>c.CanWork&&c.Profession==CitizenProfession.Unemployed&&c.WorkplaceBuildingId==null).Select(c=>c.Id).ToArray();
            if(eligible.Any(id=>!availableAdults.Contains(id))) Bootstrap.FillAutomaticVacancies();
            availableAdults.Clear();
            foreach(var citizen in Bootstrap.Roster.Where(c=>c.CanWork&&c.Profession==CitizenProfession.Unemployed&&c.WorkplaceBuildingId==null)) availableAdults.Add(citizen.Id);
        }
        private readonly List<ConstructionJob> jobs=new List<ConstructionJob>();
        private readonly Dictionary<string,double> productionProgress=new Dictionary<string,double>();
        public IReadOnlyList<ConstructionJob> Jobs => jobs.AsReadOnly();
        public event Action PresentationChanged;
        public event Action<BuildingState,ResourceType,int,int> ProductionEvent;
        public SettlementEconomy(SettlementBootstrap bootstrap,IGatheringRandom random=null,IGatheringRandom livingRandom=null)
        {
            Bootstrap=bootstrap??throw new ArgumentNullException(nameof(bootstrap));
            if(bootstrap.Economy!=null) throw new InvalidOperationException("The settlement already has an economy.");
            bootstrap.Economy=this;
            Residences=new ResidenceService(bootstrap);
            Residences.Reconcile();
            Living=new SettlementLiving(this,livingRandom);
            Recruitment=new TavernRecruitment(this,random??new SeededGatheringRandom(719));
            bootstrap.Simulation.ScheduledMovement=TickCitizen;
            Bootstrap.FillAutomaticVacancies();RefreshEmployment();
        }
        public bool TryUpgradeTownHall() => TryUpgrade(Bootstrap.Settlement.TownHall);
        public ConstructionJob JobAt(BuildingState building) => jobs.FirstOrDefault(j=>j.Building==building);
        public bool Fund(BuildingState building)
        {
            var job=JobAt(building);
            if(job==null||job.IsFunded||job.Kind==ConstructionJobKind.Demolition||
                job.Builder==null&&!(job.Kind==ConstructionJobKind.Construction&&building.Type==BuildingType.BuildersHut)) return false;
            if(!Bootstrap.Resources.TryPay("fund-"+Guid.NewGuid().ToString("N"),job.Materials)) return false;
            job.PaidCost=job.Materials;PresentationChanged?.Invoke();return true;
        }
        public bool AssignBuilder(BuildingState building,string citizenId)
        {
            var job=JobAt(building);
            if(job==null||job.Builder!=null||!job.IsFunded) return false;
            return StartConstruction(building,citizenId);
        }
        public IEnumerable<Citizen> QualifiedBuilders(BuildingState building)
        {
            var job=JobAt(building);if(job==null) return Enumerable.Empty<Citizen>();
            int level=job.Kind==ConstructionJobKind.Construction?1:job.Kind==ConstructionJobKind.Demolition||building==Bootstrap.Settlement.TownHall?building.Level:building.TargetLevel;
            return Bootstrap.Roster.Where(c=>c.CanWork&&c.Profession==CitizenProfession.Builder&&Bootstrap.Settlement.Buildings.Any(h=>h.Id==c.WorkplaceBuildingId&&h.Type==BuildingType.BuildersHut&&h.IsCompleted&&
                (h.Level>=level||h==building&&job.Kind==ConstructionJobKind.Upgrade&&level==h.Level+1)));
        }
        public IEnumerable<Citizen> EligibleJobBuilders(BuildingState building,bool allowReassignment=false)=>QualifiedBuilders(building)
            .Where(c=>allowReassignment||!jobs.Any(j=>j.Builder==c));
        public IReadOnlyDictionary<ResourceType,int> CancellationRefund(BuildingState building)=>JobAt(building)?.Kind==ConstructionJobKind.Construction&&JobAt(building).IsFunded
            ?JobAt(building).PaidCost:new Dictionary<ResourceType,int>();
        public bool ReleaseBuilder(Citizen citizen)
        {
            var job=jobs.FirstOrDefault(j=>j.Builder==citizen);if(job==null) return false;
            job.Builder=null;var routine=Routine(citizen);routine.OnSite=false;routine.SiteId=null;
            if(Bootstrap.Simulation.Gathering?.HasTrip(citizen)!=true) {citizen.Route.Clear();citizen.Destination=citizen.Position;}
            citizen.ScheduledTask=CitizenTask.Idle;
            PresentationChanged?.Invoke();return true;
        }
        public bool StartConstruction(BuildingState building,string builderId,bool reassign=false)
        {
            var job=JobAt(building);var builder=QualifiedBuilders(building).FirstOrDefault(c=>c.Id==builderId);
            if(job==null||job.Builder!=null||builder==null||!job.IsFunded&&!Bootstrap.Resources.CanPay(job.Materials)) return false;
            var previous=jobs.FirstOrDefault(j=>j.Builder==builder);
            if(previous!=null&&!reassign) return false;
            // Validate everything before releasing an existing job or paying once.
            if(previous!=null) ReleaseBuilder(builder);
            job.Builder=builder;
            if(!job.IsFunded&&!Fund(building)) {job.Builder=null;return false;}
            var routine=Routine(builder);routine.SiteId=null;routine.OnSite=false;
            if(Bootstrap.Simulation.Gathering?.HasTrip(builder)!=true) {builder.Route.Clear();builder.Destination=builder.Position;}
            builder.ScheduledTask=CitizenTask.MovingToWork;
            PresentationChanged?.Invoke();return true;
        }
        public string JobStatus(BuildingState building)
        {
            var job=JobAt(building);
            if(job==null) return building.IsCompleted?"Completed":"Awaiting Resources";
            if(!job.IsFunded) return "Awaiting Resources";
            if(job.Builder==null&&!(job.Kind==ConstructionJobKind.Construction&&building.Type==BuildingType.BuildersHut)) return "Awaiting Builder";
            if(job.Builder==null) return "Under Construction (Player)";
            if(!Clock.IsDay) return "Paused At Night";
            return job.Builder.Task==CitizenTask.Working?"Under Construction":"Builder Travelling";
        }
        public IEnumerable<Citizen> AvailableBuilders(int targetLevel,BuildingState upgrading=null)
        {
            return Bootstrap.Roster.Where(c=>c.CanWork&&c.Profession==CitizenProfession.Builder&&!jobs.Any(j=>j.Builder==c)&&
                Bootstrap.Settlement.Buildings.Any(h=>h.Id==c.WorkplaceBuildingId&&h.Type==BuildingType.BuildersHut&&h.IsCompleted&&
                    (h.Level>=targetLevel||h==upgrading&&targetLevel==h.Level+1)));
        }
        public bool CanBuild(BuildingType type)=>BuildingCatalog.Get(type).RequiredTownHallLevel<=Bootstrap.Settlement.TownHallLevel;
        public bool TryBuild(BuildingType type,TileCoordinate origin,int orientation,out BuildingState site,string builderId=null)
        {
            site=null;
            if(Bootstrap.Settlement.ValidateAccessiblePlacement(type,origin,orientation)!=BuildingPlacementValidity.Valid) return false;
            site=Bootstrap.Settlement.AddConstructionSite(type,origin,orientation,BuildingEconomyRules.BaseSeconds(type));
            var job=new ConstructionJob(site,null,null) {RequiredWork=site.RequiredConstructionWork};
            jobs.Add(job);
            PresentationChanged?.Invoke();return true;
        }
        public bool TryUpgrade(BuildingState building,string builderId=null)
        {
            bool hall=building==Bootstrap.Settlement.TownHall;
            if(building==null||!Bootstrap.Settlement.Buildings.Contains(building)||!building.IsCompleted||JobAt(building)!=null||
                building.Level>=5||!hall&&building.Level>=Bootstrap.Settlement.TownHallLevel) return false;
            building.IsUpgrading=true;building.TargetLevel=building.Level+1;building.ConstructionStage=ConstructionStage.UnderConstruction;
            building.ConstructionWork=0;building.RequiredConstructionWork=BuildingEconomyRules.BaseSeconds(building.Type)*building.TargetLevel;
            var job=new ConstructionJob(building,null,null) {Kind=ConstructionJobKind.Upgrade,RequiredWork=building.RequiredConstructionWork};
            jobs.Add(job);
            if(builderId!=null&&!StartConstruction(building,builderId)) {jobs.Remove(job);building.IsUpgrading=false;building.TargetLevel=building.Level;building.ConstructionStage=ConstructionStage.Completed;return false;}
            PresentationChanged?.Invoke();return true;
        }
        public bool ContributeWork(BuildingState site,ConstructionActor actor,double seconds,Citizen worker=null)
        {
            if(!BootstrapSettings.Finite(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(site==null||!Bootstrap.Settlement.Buildings.Contains(site)||site.ConstructionStage!=ConstructionStage.UnderConstruction&&JobAt(site)?.Kind!=ConstructionJobKind.Demolition) return false;
            var job=jobs.FirstOrDefault(j=>j.Building==site);
            if(actor==ConstructionActor.Player)
            {
                if(site.Type!=BuildingType.BuildersHut||site.IsUpgrading||job!=null&&(!job.IsFunded||job.Builder!=null||job.Kind!=ConstructionJobKind.Construction)) return false;
            }
            else if(actor==ConstructionActor.Builder)
            {
                if(!Clock.IsDay||job==null||!job.IsFunded||job.Builder!=worker||!Bootstrap.Roster.Contains(worker)||worker.Profession!=CitizenProfession.Builder||!worker.CanWork) return false;
                var hut=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==worker.WorkplaceBuildingId);
                int requiredLevel=job.Kind==ConstructionJobKind.Construction?1:
                    job.Kind==ConstructionJobKind.Demolition||site==Bootstrap.Settlement.TownHall?site.Level:site.TargetLevel;
                if(hut==null||!hut.IsCompleted||hut.Type!=BuildingType.BuildersHut||
                    hut.Level<requiredLevel&&!(hut==site&&job.Kind==ConstructionJobKind.Upgrade&&requiredLevel==hut.Level+1)) return false;
                seconds*=BuildingEconomyRules.BuilderEfficiency(worker,hut.Level,site.Level);
            }
            else return false;
            if(job?.Kind==ConstructionJobKind.Demolition)
            {
                job.Progress=Math.Min(job.RequiredWork,job.Progress+seconds);
                if(job.Progress<job.RequiredWork) return false;
                if(!CompleteDemolition(site)) return false;
                jobs.Remove(job);return true;
            }
            site.ConstructionWork=Math.Min(site.RequiredConstructionWork,site.ConstructionWork+seconds);
            if(site.ConstructionWork<site.RequiredConstructionWork) return false;
            if(job!=null) {site.Invest(job.PaidCost);jobs.Remove(job);}
            bool newWorkplace=!site.IsUpgrading;
            site.Level=site.TargetLevel;site.IsUpgrading=false;site.ConstructionStage=ConstructionStage.Completed;
            Bootstrap.Settlement.ApplyBuildingInfluence(site);
            if(newWorkplace) Bootstrap.FillAutomaticVacancies();
            Residences.Reconcile();PresentationChanged?.Invoke();return true;
        }
        internal CitizenPosition? Access(BuildingState building,Citizen citizen)
        {
            if(building==null) return null;
            var state=Bootstrap.Settlement;
            bool Walk(CitizenPosition p)=>PerimeterNavigation.CanOccupy(p,state.Territory,state.Buildings);
            bool Free(CitizenPosition p)=>!Bootstrap.Roster.Any(c=>c!=citizen&&c.Active&&c.InsideBuildingId==null&&
                (c.Position.DistanceSquared(p)<.16||c.Destination.DistanceSquared(p)<.16&&c.ScheduledTask!=CitizenTask.Idle));
            var points=PerimeterNavigation.AccessPoints(building).Distinct().ToArray();
            if(points.Any(p=>p.DistanceSquared(citizen.Destination)<1e-9)&&Walk(citizen.Destination)&&Free(citizen.Destination)) return citizen.Destination;
            foreach(var point in points)
                if(Walk(point)&&Free(point)&&PerimeterNavigation.FindPath(citizen.Position,point,Walk).Count>0) return point;
            return null;
        }
        private bool TickCitizen(Citizen citizen,double seconds,double speed)
        {
            var routine=Routine(citizen);
            if(routine.RoutineDay!=Clock.Day) {routine.RoutineDay=Clock.Day;routine.OnSite=false;routine.Work=0;}
            if(citizen.InsideBuildingId!=null)
            {
                if(Clock.IsDay&&routine.InsideUntil>Clock.Elapsed) {citizen.ScheduledTask=CitizenTask.Working;return true;}
                if(!Clock.IsDay) {citizen.ScheduledTask=CitizenTask.Sleeping;return true;}
                var home=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.InsideBuildingId);
                // Entrance may have been blocked overnight; defer exit until a valid location exists.
                var exit=Access(home,citizen);
                if(exit==null||citizen.Position.DistanceSquared(exit.Value)>1e-9) return true;
                // Leave through the same boundary point used on entry; wait for clearance.
                citizen.Destination=citizen.Position;
                citizen.InsideBuildingId=null;citizen.Route.Clear();citizen.Movement=CitizenMovement.Idle;
            }
            if(!Clock.IsDay)
            {
                var home=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.ResidenceBuildingId);
                var access=Access(home??Bootstrap.Settlement.TownHall,citizen);
                citizen.ScheduledTask=CitizenTask.ReturningHome;
                if(access!=null&&Bootstrap.Simulation.TravelTo(citizen,access.Value,seconds,speed)&&home!=null)
                {citizen.InsideBuildingId=home.Id;citizen.ScheduledTask=CitizenTask.Sleeping;citizen.Route.Clear();}
                return true;
            }
            if(!citizen.CanWork) {citizen.ScheduledTask=CitizenTask.Idle;return false;}
            var job=jobs.FirstOrDefault(j=>j.Builder==citizen);
            var permanent=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.WorkplaceBuildingId);
            if(permanent==null) {routine.OnSite=false;citizen.ScheduledTask=CitizenTask.Idle;return false;}
            double phase=Clock.Elapsed%SettlementClock.CycleSeconds;
            if(job==null&&(phase>=300||phase%100>=90||Clock.Elapsed<routine.FreeUntil))
            {routine.OnSite=false;citizen.ScheduledTask=CitizenTask.Idle;return false;}
            if(citizen.Profession==CitizenProfession.Builder&&routine.PreparedDay!=Clock.Day)
            {
                routine.OnSite=false;
                var hutAccess=Access(permanent,citizen);citizen.ScheduledTask=CitizenTask.MovingToWork;
                if(hutAccess!=null&&Bootstrap.Simulation.TravelTo(citizen,hutAccess.Value,seconds,speed))
                {
                    citizen.ScheduledTask=CitizenTask.Working;routine.Preparation+=seconds;
                    if(routine.Preparation>=3) {routine.PreparedDay=Clock.Day;routine.Preparation=0;}
                }
                return true;
            }
            var workplace=job?.Building??permanent;
            if(routine.SiteId!=workplace.Id) {routine.SiteId=workplace.Id;routine.OnSite=false;routine.Work=0;routine.SupplyTrip=false;}
            if(job!=null&&routine.Work>=180) {routine.SupplyTrip=true;routine.Work=0;routine.OnSite=false;}
            if(routine.SupplyTrip)
            {
                var supplier=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Type==BuildingType.Warehouse&&b.IsCompleted)??permanent;
                var access=Access(supplier,citizen);citizen.ScheduledTask=CitizenTask.MovingToWork;
                if(access!=null&&Bootstrap.Simulation.TravelTo(citizen,access.Value,seconds,speed)) routine.SupplyTrip=false;
                return true;
            }
            CitizenPosition? target=routine.OnSite?null:Access(workplace,citizen);
            // Once on site, local movement remains work; trips away from the site do not contribute.
            if(routine.OnSite)
            {
                var anchor=workplace.Entrance.ExteriorTile;
                if(citizen.Profession==CitizenProfession.Fisher)
                {
                    int u=-1,v=(int)(workplace.Definition.BaseDepth*.7),w=workplace.Definition.BaseWidth,d=workplace.Definition.BaseDepth;
                    switch(workplace.OrientationDegrees)
                    {
                        case 0:anchor=new TileCoordinate(workplace.Origin.X+u,workplace.Origin.Z+v);break;
                        case 90:anchor=new TileCoordinate(workplace.Origin.X+v,workplace.Origin.Z+w-1-u);break;
                        case 180:anchor=new TileCoordinate(workplace.Origin.X+w-1-u,workplace.Origin.Z+d-1-v);break;
                        default:anchor=new TileCoordinate(workplace.Origin.X+d-1-v,workplace.Origin.Z+u);break;
                    }
                }
                var candidates=PerimeterNavigation.AccessPoints(workplace).Distinct()
                    .Where(p=>PerimeterNavigation.CanOccupy(p,Bootstrap.Settlement.Territory,Bootstrap.Settlement.Buildings)&&
                        !Bootstrap.Roster.Any(c=>c!=citizen&&c.InsideBuildingId==null&&c.Position.DistanceSquared(p)<.16))
                    .OrderBy(p=>p.DistanceSquared(CitizenPosition.Center(anchor))).Take(3).ToArray();
                if(candidates.Length>0) target=candidates[((int)(routine.Work/20)+Bootstrap.Roster.ToList().IndexOf(citizen))%candidates.Length];
            }
            citizen.ScheduledTask=CitizenTask.MovingToWork;
            if(target!=null&&Bootstrap.Simulation.TravelTo(citizen,target.Value,seconds,speed)) routine.OnSite=true;
            if(routine.OnSite)
            {
                citizen.ScheduledTask=CitizenTask.Working;routine.Work+=seconds;
                if(job!=null&&job.IsFunded) ContributeWork(job.Building,ConstructionActor.Builder,seconds,citizen);
                else if(Bootstrap.WorkerAt(workplace)==citizen)
                {
                    Produce(workplace,citizen,seconds);
                    if((citizen.Profession==CitizenProfession.Miner||citizen.Profession==CitizenProfession.Butcher||citizen.Profession==CitizenProfession.Builder)&&routine.Work>=30)
                    {
                        routine.Work=0;routine.OnSite=false;
                        // Logical entrance transition only after reaching an exterior access; no position teleport.
                        if(citizen.Movement==CitizenMovement.Idle)
                        {citizen.InsideBuildingId=workplace.Id;routine.InsideUntil=Clock.Elapsed+3;routine.FreeUntil=Clock.Elapsed+12;}
                    }
                }
            }
            return true;
        }
        private void Produce(BuildingState building,Citizen citizen,double seconds)
        {
            if(!Clock.IsDay||!building.IsCompleted||!citizen.CanWork||Bootstrap.WorkerAt(building)!=citizen||
                BuildingEconomyRules.Profession(building.Type)!=citizen.Profession) return;
            foreach(var recipe in ResourceCatalog.Production.Where(p=>p.Building==building.Type&&p.UnlockLevel<=building.Level))
            {
                string key=building.Id+":"+recipe.Resource;
                productionProgress.TryGetValue(key,out double elapsed);elapsed+=seconds;
                double interval=LivingRules.CycleSeconds(recipe,building.Level);
                while(elapsed+1e-9>=interval)
                {
                    int requested=recipe.Quantity(building.Level);
                    int stored=Bootstrap.Resources.Store("production-"+Guid.NewGuid().ToString("N"),recipe.Resource,requested);
                    ProductionEvent?.Invoke(building,recipe.Resource,stored,requested-stored);
                    elapsed=Math.Max(0,elapsed-interval);
                }
                productionProgress[key]=elapsed;
            }
        }
        public void Advance(double realSeconds,Action<double,bool> presentationTick=null)
        {
            if(!BootstrapSettings.Finite(realSeconds)||realSeconds<0) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            RefreshEmployment();
            double remaining=realSeconds*Clock.Speed;
            while(remaining>1e-9)
            {
                double cycle=Clock.Elapsed%SettlementClock.CycleSeconds;
                double boundary=Clock.IsDay?SettlementClock.DaySeconds-cycle:SettlementClock.CycleSeconds-cycle;
                double step=Math.Min(remaining,Math.Min(.1,boundary));
                bool day=Clock.IsDay;
                if(Bootstrap.Simulation.Gathering!=null) Bootstrap.Simulation.Gathering.AllowNewTrips=day;
                Bootstrap.Simulation.Tick(step);
                presentationTick?.Invoke(step,day);
                int previousDay=Clock.Day;
                Clock.Advance(step);remaining-=step;
                Living.ProcessDay(Clock.Day);Recruitment.ProcessDay(Clock.Day);
                if(Clock.Day!=previousDay) RefreshEmployment();
            }
        }
        public IReadOnlyDictionary<ResourceType,int> Refund(BuildingState building)=>building.Investment.ToDictionary(x=>x.Key,x=>x.Value/2);
        public bool CancelConstruction(BuildingState building)
        {
            var job=JobAt(building);
            if(building==null||building.IsCompleted||job==null||job.Kind!=ConstructionJobKind.Construction) return false;
            if(job.IsFunded&&!Bootstrap.Resources.TryBundle("cancel-"+building.Id,job.PaidCost)) return false;
            jobs.Remove(job);Bootstrap.Settlement.RemoveBuilding(building);
            Bootstrap.Simulation.ReconcileBuildings();PresentationChanged?.Invoke();return true;
        }
        public bool Demolish(BuildingState building)
        {
            if(building==null||building==Bootstrap.Settlement.TownHall||!Bootstrap.Settlement.Buildings.Contains(building)||!building.IsCompleted||JobAt(building)!=null) return false;
            jobs.Add(new ConstructionJob(building,null,new Dictionary<ResourceType,int>())
                {Kind=ConstructionJobKind.Demolition,RequiredWork=BuildingEconomyRules.BaseSeconds(building.Type)*.5});
            PresentationChanged?.Invoke();return true;
        }
        private bool CompleteDemolition(BuildingState building)
        {
            if(!Bootstrap.Resources.TryBundle("demolish-"+building.Id,Refund(building))) return false;
            foreach(var job in jobs.Where(j=>j.Builder!=null&&j.Builder.WorkplaceBuildingId==building.Id).ToArray())
            {
                job.Builder=null;
            }
            foreach(var c in Bootstrap.Roster)
            {
                if(c.WorkplaceBuildingId==building.Id) {c.WorkplaceBuildingId=null;c.Profession=CitizenProfession.Unemployed;}
                if(c.ResidenceBuildingId==building.Id) c.ResidenceBuildingId=null;
                if(c.InsideBuildingId==building.Id) {c.InsideBuildingId=null;c.ScheduledTask=CitizenTask.Idle;}
            }
            Recruitment.RemoveTavern(building.Id);
            foreach(var key in productionProgress.Keys.Where(k=>k.StartsWith(building.Id+":",StringComparison.Ordinal)).ToArray()) productionProgress.Remove(key);
            Bootstrap.Settlement.RemoveBuilding(building);Residences.Reconcile();
            Bootstrap.Simulation.ReconcileBuildings();PresentationChanged?.Invoke();return true;
        }
        public bool CanDismiss(Citizen citizen)=>citizen!=null&&citizen.IsAdult&&Bootstrap.Roster.Contains(citizen)&&Bootstrap.Roster.Count>4;
        public bool Dismiss(Citizen citizen)=>CanDismiss(citizen)&&RemoveCitizen(citizen);
        internal bool RemoveCitizen(Citizen citizen)
        {
            if(citizen==null||!Bootstrap.Roster.Contains(citizen)) return false;
            var partner=Bootstrap.Roster.FirstOrDefault(c=>c.Id==citizen.PartnerId);
            if(partner!=null) partner.PartnerId=null;citizen.PartnerId=null;
            foreach(var job in jobs.Where(j=>j.Builder==citizen)) job.Builder=null;
            if(!Bootstrap.Simulation.RemoveCitizen(citizen)) return false;
            routines.Remove(citizen.Id);
            Residences.Reconcile();PresentationChanged?.Invoke();return true;
        }
    }
}
