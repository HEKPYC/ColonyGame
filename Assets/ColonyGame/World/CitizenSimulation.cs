using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public sealed class CitizenMovementSettings
    {
        public double Speed { get; }
        public double MinimumPause { get; }
        public double MaximumPause { get; }
        public int WanderSteps { get; }
        public CitizenMovementSettings(double speed=.8,double minimumPause=1,double maximumPause=3,int wanderSteps=6)
        {
            if(double.IsNaN(speed)||double.IsInfinity(speed)||speed<=0) throw new ArgumentOutOfRangeException(nameof(speed));
            if(double.IsNaN(minimumPause)||double.IsInfinity(minimumPause)||minimumPause<0 ||
                double.IsNaN(maximumPause)||double.IsInfinity(maximumPause)||maximumPause<minimumPause)
                throw new ArgumentOutOfRangeException(nameof(minimumPause));
            if(wanderSteps<1) throw new ArgumentOutOfRangeException(nameof(wanderSteps));
            Speed=speed; MinimumPause=minimumPause; MaximumPause=maximumPause; WanderSteps=wanderSteps;
        }
    }

    public sealed class CitizenSimulation
    {
        private readonly ICitizenMovementPolicy policy;
        private readonly SettlementIdleMovementPolicy settlementPolicy;
        private readonly Random random;
        private readonly CitizenMovementSettings settings;
        public IReadOnlyList<Citizen> Citizens { get; }
        public event Action<string> DevelopmentWarning;
        public CitizenSimulation(ICitizenMovementPolicy policy,IEnumerable<Citizen> citizens,CitizenMovementSettings settings=null,int seed=1147)
        {
            this.policy=policy ?? throw new ArgumentNullException(nameof(policy));
            settlementPolicy=policy as SettlementIdleMovementPolicy;
            settlementPolicy?.RefreshConnectivity();
            var list=citizens?.ToList() ?? throw new ArgumentNullException(nameof(citizens));
            if(list.Any(c=>c==null)||list.Select(c=>c.Id).Distinct().Count()!=list.Count)
                throw new ArgumentException("Citizen identities must be unique.",nameof(citizens));
            Citizens=list.AsReadOnly();
            this.settings=settings ?? new CitizenMovementSettings();
            random=new Random(seed);
            foreach(var citizen in list) citizen.Pause=PauseDuration();
        }
        public static CitizenSimulation CreatePrototype(SettlementState settlement,CitizenMovementSettings settings=null,int seed=1147)
        {
            var policy=new SettlementIdleMovementPolicy(settlement);
            var random=new Random(seed);
            var hall=settlement.TownHall;
            var preferred=new[]{
                new TileCoordinate(hall.Origin.X-1,hall.Origin.Z-1),
                new TileCoordinate(hall.Origin.X+hall.Width,hall.Origin.Z-1),
                new TileCoordinate(hall.Origin.X-1,hall.Origin.Z+hall.Depth),
                new TileCoordinate(hall.Origin.X+hall.Width,hall.Origin.Z+hall.Depth)};
            var positions=preferred.Concat(policy.CandidateTiles.OrderBy(t=>t.Z).ThenBy(t=>t.X))
                .Distinct().Where(t=>policy.CanOccupy(CitizenPosition.Center(t))).Take(4).ToArray();
            if(positions.Length!=4) throw new InvalidOperationException("Four walkable starting citizen positions are required.");
            var names=new[]{"Edwin","Owen","Clara","Nora"};
            var citizens=new List<Citizen>();
            for(int i=0;i<4;i++)
                citizens.Add(new Citizen("prototype-citizen-"+(i+1),names[i],i<2?CitizenSex.Male:CitizenSex.Female,
                    random.Next(18,46),new CitizenAttributes(random.Next(1,11),random.Next(1,11),random.Next(1,11),random.Next(1,11),random.Next(1,11)),
                    CitizenPosition.Center(positions[i])));
            return new CitizenSimulation(policy,citizens,settings,seed);
        }
        private double PauseDuration()=>settings.MinimumPause+random.NextDouble()*(settings.MaximumPause-settings.MinimumPause);
        private bool Walkable(TileCoordinate t)=>policy.CanOccupy(CitizenPosition.Center(t));
        private bool Connected(CitizenPosition position)=>policy.CanOccupy(position)&&(settlementPolicy==null||settlementPolicy.IsConnected(position));
        private bool RecoveryAllowed(Citizen citizen,CitizenPosition position)=>settlementPolicy!=null
            ?settlementPolicy.CanFollowRecovery(position,citizen.RecoveryTiles)
            :policy.CanTraverseRecovery(position,citizen.RecoveryOrigin);
        private void EndRecovery(Citizen citizen)
        {
            citizen.RecoveryTiles.Clear();
            citizen.Movement=citizen.Route.Count>0?CitizenMovement.Walking:CitizenMovement.Idle;
        }
        private void SetRoute(Citizen citizen,IReadOnlyList<TileCoordinate> route,CitizenMovement movement)
        {
            citizen.Route.Clear();
            citizen.RecoveryTiles.Clear();
            if(movement==CitizenMovement.Recovering)
            {
                foreach(var tile in route) citizen.RecoveryTiles.Add(tile);
                // A fractional starting body may overlap a neighboring newly occupied cell.
                double r=Citizen.BodyHalfExtent;
                for(int z=(int)Math.Floor(citizen.Position.Z-r);z<=(int)Math.Floor(citizen.Position.Z+r);z++)
                    for(int x=(int)Math.Floor(citizen.Position.X-r);x<=(int)Math.Floor(citizen.Position.X+r);x++)
                        citizen.RecoveryTiles.Add(new TileCoordinate(x,z));
            }
            foreach(var tile in route)
            {
                var center=CitizenPosition.Center(tile);
                if(center.DistanceSquared(citizen.Position)>1e-12) citizen.Route.Enqueue(center);
            }
            citizen.Destination=CitizenPosition.Center(route[route.Count-1]);
            citizen.Movement=citizen.Route.Count>0?movement:CitizenMovement.Idle;
            citizen.Pause=0;
        }
        private void EmergencyRecovery(Citizen citizen)
        {
            var safe=policy.CandidateTiles.Where(t=>Connected(CitizenPosition.Center(t))).OrderBy(t=>CitizenPosition.Center(t).DistanceSquared(citizen.Position)).ToArray();
            citizen.Route.Clear(); citizen.RecoveryTiles.Clear(); citizen.Movement=CitizenMovement.Idle;
            citizen.Active=safe.Length>0;
            if(citizen.Active) citizen.Position=citizen.Destination=CitizenPosition.Center(safe[0]);
            citizen.Pause=PauseDuration();
            DevelopmentWarning?.Invoke($"Citizen {citizen.Id}: no footprint recovery route; emergency {(citizen.Active?"relocation":"dormancy")}.");
        }
        public void ReconcileBuildings(bool recoverDormant=true)
        {
            // Placement calls the full reconciliation once. Ticks use the cached connectivity.
            if(recoverDormant) settlementPolicy?.RefreshConnectivity();
            foreach(var citizen in Citizens)
            {
                if(!citizen.Active&&!recoverDormant) continue;
                if(!Connected(citizen.Position))
                {
                    if(citizen.Movement==CitizenMovement.Recovering&&!recoverDormant&&Connected(citizen.Destination)&&
                        citizen.Route.Count>0&&RecoveryAllowed(citizen,citizen.Position)) continue;
                    citizen.RecoveryOrigin=citizen.Position;
                    var route=CitizenNavigation.FindRecoveryPath(citizen.Position.Tile,
                        t=>policy.CanTraverseRecovery(CitizenPosition.Center(t),citizen.RecoveryOrigin),
                        t=>Connected(CitizenPosition.Center(t)),
                        t=>!Walkable(t),t=>settlementPolicy?.EntrancePenalty(t)??0);
                    if(!policy.CanTraverseRecovery(citizen.Position,citizen.RecoveryOrigin)||route.Count==0) EmergencyRecovery(citizen);
                    else { citizen.Active=true; SetRoute(citizen,route,CitizenMovement.Recovering); }
                }
                else
                {
                    citizen.Active=true;
                    if(citizen.Movement==CitizenMovement.Recovering) EndRecovery(citizen);
                    if(!policy.CanOccupy(citizen.Destination)||citizen.Route.Any(p=>!policy.CanOccupy(p)))
                    {
                        citizen.Route.Clear(); citizen.RecoveryTiles.Clear();
                        var center=CitizenPosition.Center(citizen.Position.Tile);
                        citizen.Route.Enqueue(center); citizen.Destination=center;
                        citizen.Movement=CitizenMovement.Walking;
                    }
                }
            }
        }        public void Tick(double seconds)
        {
            if(seconds<0||double.IsNaN(seconds)||double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            ReconcileBuildings(false);
            foreach(var citizen in Citizens)
            {
                if(!citizen.Active) continue;
                if(citizen.Movement==CitizenMovement.Idle)
                {
                    citizen.Pause-=seconds;
                    if(citizen.Pause>0) continue;
                    var choices=CitizenNavigation.NearbyRoutes(citizen.Position.Tile,Walkable,settings.WanderSteps);
                    if(choices.Count==0) { citizen.Pause=PauseDuration(); continue; }
                    SetRoute(citizen,choices[random.Next(choices.Count)],CitizenMovement.Walking);
                }
                double remaining=seconds;
                while(citizen.Route.Count>0)
                {
                    var target=citizen.Route.Peek();
                    double dx=target.X-citizen.Position.X,dz=target.Z-citizen.Position.Z;
                    double distance=Math.Sqrt(dx*dx+dz*dz);
                    double amount=Math.Min(distance,remaining*settings.Speed);
                    var next=distance==0?target:new CitizenPosition(citizen.Position.X+dx/distance*amount,citizen.Position.Z+dz/distance*amount);
                    bool valid=citizen.Movement==CitizenMovement.Recovering?RecoveryAllowed(citizen,next):policy.CanOccupy(next);
                    if(!valid) { citizen.Route.Clear(); citizen.RecoveryTiles.Clear(); citizen.Movement=CitizenMovement.Idle; citizen.Pause=PauseDuration(); break; }
                    citizen.Position=next;
                    if(citizen.Movement==CitizenMovement.Recovering&&Connected(next)) EndRecovery(citizen);
                    remaining-=amount/settings.Speed;
                    if(amount<distance) break;
                    citizen.Route.Dequeue();
                    if(citizen.Route.Count==0) { citizen.RecoveryTiles.Clear(); citizen.Movement=CitizenMovement.Idle; citizen.Pause=PauseDuration(); }
                    if(remaining<=0) break;
                }
            }
        }
    }
}