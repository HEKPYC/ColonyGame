using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public sealed class ConstructionJob
    {
        public BuildingState Building { get; }
        public Citizen Builder { get; internal set; }
        internal IReadOnlyDictionary<ResourceType,int> PaidCost;
        internal ConstructionJob(BuildingState building,Citizen builder,IReadOnlyDictionary<ResourceType,int> cost)
        { Building=building;Builder=builder;PaidCost=cost; }
    }

    // Coordinates existing inventory, buildings and citizens; all movement stays in CitizenSimulation.
    public sealed class SettlementEconomy
    {
        public SettlementBootstrap Bootstrap { get; }
        public SettlementClock Clock { get; } = new SettlementClock();
        public TavernRecruitment Recruitment { get; }
        public ResidenceService Residences { get; }
        private readonly List<ConstructionJob> jobs=new List<ConstructionJob>();
        private readonly Dictionary<string,double> productionProgress=new Dictionary<string,double>();
        public IReadOnlyList<ConstructionJob> Jobs => jobs.AsReadOnly();
        public event Action PresentationChanged;
        public SettlementEconomy(SettlementBootstrap bootstrap,IGatheringRandom random=null)
        {
            Bootstrap=bootstrap??throw new ArgumentNullException(nameof(bootstrap));
            if(bootstrap.Economy!=null) throw new InvalidOperationException("The settlement already has an economy.");
            bootstrap.Economy=this;
            Residences=new ResidenceService(bootstrap);
            Residences.Reconcile();
            Recruitment=new TavernRecruitment(this,random??new SeededGatheringRandom(719));
            bootstrap.Simulation.ScheduledMovement=TickCitizen;
        }
        public bool TryUpgradeTownHall()
        {
            var hall=Bootstrap.Settlement.TownHall;
            if(hall.Level>=5) return false;
            var cost=BuildingEconomyRules.TownHallCost(hall.Level+1);
            if(!Bootstrap.Resources.TryPay("hall-upgrade-"+(hall.Level+1),cost)) return false;
            hall.Invest(cost);hall.Level++;hall.TargetLevel=hall.Level;
            Residences.Reconcile();PresentationChanged?.Invoke();return true;
        }
        public IEnumerable<Citizen> AvailableBuilders(int targetLevel,BuildingState upgrading=null)
        {
            return Bootstrap.Roster.Where(c=>c.Active&&c.Profession==CitizenProfession.Builder&&!jobs.Any(j=>j.Builder==c)&&
                Bootstrap.Settlement.Buildings.Any(h=>h.Id==c.WorkplaceBuildingId&&h.Type==BuildingType.BuildersHut&&h.IsCompleted&&
                    (h.Level>=targetLevel||h==upgrading&&targetLevel==h.Level+1)));
        }
        public bool CanBuild(BuildingType type)=>Bootstrap.Resources.CanPay(BuildingEconomyRules.Cost(type))&&
            (type==BuildingType.BuildersHut||AvailableBuilders(1).Any());
        public bool TryBuild(BuildingType type,TileCoordinate origin,int orientation,out BuildingState site,string builderId=null)
        {
            site=null;
            if(Bootstrap.Settlement.ValidateBuildingPlacement(type,origin,orientation)!=BuildingPlacementValidity.Valid) return false;
            if(type==BuildingType.BuildersHut&&builderId==null)
            {
                if(!Bootstrap.TryStartFirstHut(origin,orientation,out site)) return false;
                PresentationChanged?.Invoke();return true;
            }
            var builder=AvailableBuilders(1).FirstOrDefault(c=>builderId==null||c.Id==builderId);
            if(builder==null) return false;
            var cost=BuildingEconomyRules.Cost(type);
            if(!Bootstrap.Resources.TryPay("build-"+Guid.NewGuid().ToString("N"),cost)) return false;
            site=Bootstrap.Settlement.AddConstructionSite(type,origin,orientation,BuildingEconomyRules.BaseSeconds(type));
            jobs.Add(new ConstructionJob(site,builder,cost));PresentationChanged?.Invoke();return true;
        }
        public bool TryUpgrade(BuildingState building,string builderId=null)
        {
            if(building==null||!Bootstrap.Settlement.Buildings.Contains(building)||building==Bootstrap.Settlement.TownHall||
                !building.IsCompleted||building.IsUpgrading||building.Level>=Bootstrap.Settlement.TownHallLevel||building.Level>=5) return false;
            var builder=AvailableBuilders(building.Level+1,building).FirstOrDefault(c=>builderId==null||c.Id==builderId);
            if(builder==null) return false;
            var cost=BuildingEconomyRules.Cost(building.Type,building.Level+1);
            if(!Bootstrap.Resources.TryPay("upgrade-"+building.Id+"-"+(building.Level+1),cost)) return false;
            building.IsUpgrading=true;building.TargetLevel=building.Level+1;building.ConstructionStage=ConstructionStage.UnderConstruction;
            building.ConstructionWork=0;building.RequiredConstructionWork=BuildingEconomyRules.BaseSeconds(building.Type)*building.TargetLevel;
            jobs.Add(new ConstructionJob(building,builder,cost));PresentationChanged?.Invoke();return true;
        }
        public bool ContributeWork(BuildingState site,ConstructionActor actor,double seconds,Citizen worker=null)
        {
            if(!BootstrapSettings.Finite(seconds)||seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!Clock.IsDay||site==null||!Bootstrap.Settlement.Buildings.Contains(site)||site.ConstructionStage!=ConstructionStage.UnderConstruction) return false;
            var job=jobs.FirstOrDefault(j=>j.Building==site);
            if(actor==ConstructionActor.Player)
            {
                if(site.Type!=BuildingType.BuildersHut||site.IsUpgrading||job!=null) return false;
            }
            else if(actor==ConstructionActor.Builder)
            {
                if(job==null||job.Builder!=worker||!Bootstrap.Roster.Contains(worker)||worker.Profession!=CitizenProfession.Builder) return false;
                var hut=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==worker.WorkplaceBuildingId);
                if(hut==null) return false;
                seconds*=BuildingEconomyRules.BuilderEfficiency(worker,hut.Level,site.Level);
            }
            else return false;
            site.ConstructionWork=Math.Min(site.RequiredConstructionWork,site.ConstructionWork+seconds);
            if(site.ConstructionWork<site.RequiredConstructionWork) return false;
            if(job!=null) {site.Invest(job.PaidCost);jobs.Remove(job);}
            site.Level=site.TargetLevel;site.IsUpgrading=false;site.ConstructionStage=ConstructionStage.Completed;
            Bootstrap.Settlement.ApplyBuildingInfluence(site);
            Residences.Reconcile();PresentationChanged?.Invoke();return true;
        }
        internal TileCoordinate? Access(BuildingState building,Citizen citizen)
        {
            if(building==null) return null;
            var state=Bootstrap.Settlement;
            bool Walkable(TileCoordinate tile)=>state.Territory.IsClaimed(tile)&&!state.IsOccupied(tile);
                        var preferred=building.Entrance.ExteriorTile;
            var existing=citizen.Destination.Tile;
            bool OnPerimeter(TileCoordinate tile)=>tile.X>=building.Origin.X-1&&tile.X<=building.Origin.X+building.Width&&
                tile.Z>=building.Origin.Z-1&&tile.Z<=building.Origin.Z+building.Depth&&
                (tile.X==building.Origin.X-1||tile.X==building.Origin.X+building.Width||tile.Z==building.Origin.Z-1||tile.Z==building.Origin.Z+building.Depth);
            if(OnPerimeter(existing)&&Walkable(existing)&&(citizen.Route.Count>0||citizen.Position.DistanceSquared(CitizenPosition.Center(existing))<1e-9)) return existing;
            var perimeter=new List<TileCoordinate>();
            for(int x=building.Origin.X-1;x<=building.Origin.X+building.Width;x++)
            {perimeter.Add(new TileCoordinate(x,building.Origin.Z-1));perimeter.Add(new TileCoordinate(x,building.Origin.Z+building.Depth));}
            for(int z=building.Origin.Z;z<building.Origin.Z+building.Depth;z++)
            {perimeter.Add(new TileCoordinate(building.Origin.X-1,z));perimeter.Add(new TileCoordinate(building.Origin.X+building.Width,z));}
            foreach(var tile in new[]{preferred}.Concat(perimeter.OrderBy(t=>CitizenPosition.Center(t).DistanceSquared(citizen.Position))).Distinct())
                if(Walkable(tile)&&CitizenNavigation.FindPath(citizen.Position.Tile,tile,Walkable).Count>0) return tile;
            return null;
        }
        private bool TickCitizen(Citizen citizen,double seconds,double speed)
        {
            if(citizen.InsideBuildingId!=null)
            {
                if(!Clock.IsDay) {citizen.ScheduledTask=CitizenTask.Sleeping;return true;}
                var home=Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.InsideBuildingId);
                // Entrance may have been blocked overnight; defer exit until a valid location exists.
                var exit=Access(home,citizen);
                if(exit==null) return true;
                citizen.Position=citizen.Destination=CitizenPosition.Center(exit.Value);
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
            var job=jobs.FirstOrDefault(j=>j.Builder==citizen);
            var workplace=job?.Building??Bootstrap.Settlement.Buildings.FirstOrDefault(b=>b.Id==citizen.WorkplaceBuildingId);
            if(workplace==null) {citizen.ScheduledTask=CitizenTask.Idle;return false;}
            var target=Access(workplace,citizen);
            citizen.ScheduledTask=CitizenTask.MovingToWork;
            if(target!=null&&Bootstrap.Simulation.TravelTo(citizen,target.Value,seconds,speed))
            {
                citizen.ScheduledTask=CitizenTask.Working;
                if(job!=null) ContributeWork(job.Building,ConstructionActor.Builder,seconds,citizen);
                else if(workplace.IsCompleted) Produce(workplace,citizen,seconds);
            }
            return true;
        }
        private void Produce(BuildingState building,Citizen citizen,double seconds)
        {
            if(BuildingEconomyRules.Profession(building.Type)!=citizen.Profession) return;
            foreach(var recipe in ResourceCatalog.Production.Where(p=>p.Building==building.Type&&p.UnlockLevel<=building.Level))
            {
                string key=building.Id+":"+recipe.Resource;
                productionProgress.TryGetValue(key,out double elapsed);elapsed+=seconds;
                while(elapsed+1e-9>=recipe.Seconds)
                {
                    if(!Bootstrap.Resources.TryTransaction("production-"+Guid.NewGuid().ToString("N"),recipe.Resource,recipe.Quantity(building.Level))) break;
                    elapsed=Math.Max(0,elapsed-recipe.Seconds);
                }
                productionProgress[key]=elapsed;
            }
        }
        public void Advance(double realSeconds,Action<double,bool> presentationTick=null)
        {
            if(!BootstrapSettings.Finite(realSeconds)||realSeconds<0) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            double remaining=realSeconds*Clock.Speed;
            while(remaining>1e-9)
            {
                double cycle=Clock.Elapsed%SettlementClock.CycleSeconds;
                double boundary=Clock.IsDay?SettlementClock.DaySeconds-cycle:SettlementClock.CycleSeconds-cycle;
                double step=Math.Min(remaining,Math.Min(.1,boundary));
                bool day=Clock.IsDay;
                if(Bootstrap.Simulation.Gathering!=null) Bootstrap.Simulation.Gathering.AllowNewTrips=day;
                foreach(var job in jobs.Where(j=>j.Builder==null))
                    job.Builder=AvailableBuilders(job.Building.TargetLevel,job.Building.IsUpgrading?job.Building:null).FirstOrDefault();
                Bootstrap.Simulation.Tick(step);
                presentationTick?.Invoke(step,day);
                Clock.Advance(step);remaining-=step;
                Recruitment.ProcessDay(Clock.Day);
            }
        }
        public IReadOnlyDictionary<ResourceType,int> Refund(BuildingState building)=>building.Investment.ToDictionary(x=>x.Key,x=>x.Value/2);
        public bool Demolish(BuildingState building)
        {
            if(building==null||building==Bootstrap.Settlement.TownHall||!Bootstrap.Settlement.Buildings.Contains(building)||!building.IsCompleted||building.IsUpgrading) return false;
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
        public bool Dismiss(Citizen citizen)
        {
            if(citizen==null||!Bootstrap.Roster.Contains(citizen)) return false;
            foreach(var job in jobs.Where(j=>j.Builder==citizen)) job.Builder=null;
            if(!Bootstrap.Simulation.RemoveCitizen(citizen)) return false;
            Residences.Reconcile();PresentationChanged?.Invoke();return true;
        }
    }
}