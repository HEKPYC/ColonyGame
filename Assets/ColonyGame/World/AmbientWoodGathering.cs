using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyGame.World
{
    public interface IGatheringRandom { double NextDouble(); int Next(int count); }
    public sealed class SeededGatheringRandom : IGatheringRandom
    {
        private readonly Random random;
        public SeededGatheringRandom(int seed=2917) { random=new Random(seed); }
        public double NextDouble()=>random.NextDouble();
        public int Next(int count)=>random.Next(count);
    }

    // Cardinal distance from all actual claims: first exterior cell is depth 1.
    // No bounding-box membership or camera/forest-object dependency.
    public sealed class WildernessTaskArea
    {
        private readonly SettlementState settlement;
        private readonly int maximumDepth;
        private readonly Dictionary<TileCoordinate,int> depths=new Dictionary<TileCoordinate,int>();
        private int claimCount=-1;
        public WildernessTaskArea(SettlementState settlement,int maximumDepth)
        {
            this.settlement=settlement??throw new ArgumentNullException(nameof(settlement));
            if(maximumDepth<1) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            this.maximumDepth=maximumDepth;
            Refresh();
        }
        public void Refresh()
        {
            if(claimCount==settlement.Territory.Count) return;
            claimCount=settlement.Territory.Count;
            depths.Clear();
            var queue=new Queue<TileCoordinate>();
            foreach(var claim in settlement.Territory.ClaimedTiles)
                foreach(var next in CitizenNavigation.Neighbors(claim))
                    if(!settlement.Territory.IsClaimed(next)&&!depths.ContainsKey(next)) { depths.Add(next,1); queue.Enqueue(next); }
            while(queue.Count>0)
            {
                var tile=queue.Dequeue(); int depth=depths[tile];
                if(depth==maximumDepth) continue;
                foreach(var next in CitizenNavigation.Neighbors(tile))
                    if(!settlement.Territory.IsClaimed(next)&&!depths.ContainsKey(next)) { depths.Add(next,depth+1);queue.Enqueue(next); }
            }
        }
        public int Depth(TileCoordinate tile)
        {
            Refresh();
            return settlement.Territory.IsClaimed(tile)?0:depths.TryGetValue(tile,out int depth)?depth:int.MaxValue;
        }
        public bool CanOccupy(CitizenPosition p)
        {
            Refresh(); double r=Citizen.BodyHalfExtent;
            for(int z=(int)Math.Floor(p.Z-r);z<=(int)Math.Floor(p.Z+r);z++)
                for(int x=(int)Math.Floor(p.X-r);x<=(int)Math.Floor(p.X+r);x++)
                {
                    var t=new TileCoordinate(x,z);
                    if(settlement.IsOccupied(t)||(!settlement.Territory.IsClaimed(t)&&!depths.ContainsKey(t))) return false;
                }
            return true;
        }
    }

    public sealed class AmbientWoodGathering
    {
        private sealed class Trip
        {
            public double Cooldown, Pause;
            public int PendingWood;
            public string DeliveryId;
            public bool Rolled;
            public TileCoordinate ReturnAccess;
        }
        private readonly SettlementBootstrap bootstrap;
        private readonly CitizenSimulation simulation;
        private readonly IGatheringRandom random;
        private readonly Dictionary<string,Trip> trips=new Dictionary<string,Trip>();
        public WildernessTaskArea Area { get; }
        public int SuccessRolls { get; private set; }
        public AmbientWoodGathering(SettlementBootstrap bootstrap,IGatheringRandom random=null)
        {
            this.bootstrap=bootstrap??throw new ArgumentNullException(nameof(bootstrap));
            simulation=bootstrap.Simulation;
            this.random=random??new SeededGatheringRandom();
            Area=new WildernessTaskArea(bootstrap.Settlement,bootstrap.Settings.WildernessDepth);
            for(int i=0;i<simulation.Citizens.Count;i++) trips.Add(simulation.Citizens[i].Id,new Trip { Cooldown=bootstrap.Settings.InitialGatherDelay+i*bootstrap.Settings.CitizenGatherStagger });
        }
        public static bool IsEligible(Citizen c)=>c!=null&&c.Active&&c.Age>=18&&c.Profession==CitizenProfession.Unemployed;
        public int PendingWood(Citizen c)=>trips[c.Id].PendingWood;
        public bool HasTrip(Citizen c)=>c.GatheringTask!=CitizenTask.Idle;
        private bool Walkable(TileCoordinate t)=>Area.CanOccupy(CitizenPosition.Center(t));
        private void SetRoute(Citizen c,IReadOnlyList<TileCoordinate> route,CitizenTask task)
        {
            c.Route.Clear(); c.RecoveryTiles.Clear();
            foreach(var tile in route)
            {
                var p=CitizenPosition.Center(tile);
                if(p.DistanceSquared(c.Position)>1e-12) c.Route.Enqueue(p);
            }
            c.Destination=CitizenPosition.Center(route[route.Count-1]);
            c.Movement=c.Route.Count>0?CitizenMovement.Walking:CitizenMovement.Idle;
            c.GatheringTask=task;
        }
        private IReadOnlyList<TileCoordinate> HallRoute(Citizen c)
        {
            var hall=bootstrap.Settlement.TownHall;
            // Prefer the oriented entrance. Other adjacent exterior cells are an outdoor
            // delivery area fallback if another reserved footprint blocks that access.
            var exterior=hall.OccupiedTiles.SelectMany(CitizenNavigation.Neighbors)
                .Where(t=>!bootstrap.Settlement.IsOccupied(t)&&bootstrap.Settlement.Territory.IsClaimed(t)).Distinct()
                .OrderBy(t=>t.Equals(hall.Entrance.ExteriorTile)?0:1)
                .ThenBy(t=>Math.Abs(t.X-hall.Entrance.ExteriorTile.X)+Math.Abs(t.Z-hall.Entrance.ExteriorTile.Z))
                .ThenBy(t=>t.Z).ThenBy(t=>t.X);
            foreach(var access in exterior)
            {
                var route=CitizenNavigation.FindPath(c.Position.Tile,access,Walkable);
                if(route.Count>0) return route;
            }
            return Array.Empty<TileCoordinate>();
        }
        private bool ReturnToHall(Citizen c)
        {
            var route=HallRoute(c);
            if(route.Count==0) return false;
            trips[c.Id].ReturnAccess=route[route.Count-1];
            SetRoute(c,route,CitizenTask.ReturningWood);
            return true;
        }
        // Called by normal safety reconciliation first. A blocked task yields to footprint recovery.
        public bool Reconcile(Citizen c)
        {
            if(!HasTrip(c)||c.GatheringTask==CitizenTask.Recovery) return false;
            if(!Area.CanOccupy(c.Position))
            {
                c.GatheringTask=CitizenTask.Recovery;
                c.Route.Clear(); c.Movement=CitizenMovement.Idle;
                return false;
            }
            bool changedTarget=(c.GatheringTask==CitizenTask.GatheringWoodOutbound||c.GatheringTask==CitizenTask.GatheringWood)&&Area.Depth(c.Destination.Tile)==0;
            if(changedTarget||c.Route.Any(p=>!Area.CanOccupy(p)))
            {
                if(!ReturnToHall(c)) { c.GatheringTask=CitizenTask.Recovery; return false; }
            }
            return true;
        }
        public bool TryBegin(Citizen c)
        {
            if(!simulation.Citizens.Contains(c)||!IsEligible(c)||HasTrip(c)||c.Movement==CitizenMovement.Recovering) return false;
            // Require a real return route before dispatching any task.
            if(HallRoute(c).Count==0) return false;
            var reachable=CitizenNavigation.ReachableTiles(c.Position.Tile,Walkable);
            var candidates=reachable.Where(t=>Area.Depth(t)>0&&Area.Depth(t)<=Math.Min(bootstrap.Settings.PreferredGatherDepth,bootstrap.Settings.WildernessDepth))
                .OrderBy(t=>CitizenPosition.Center(t).DistanceSquared(c.Position)).ThenBy(t=>t.Z).ThenBy(t=>t.X).Take(bootstrap.Settings.DestinationCandidates).ToArray();
            if(candidates.Length==0) return false;
            var target=candidates[random.Next(candidates.Length)];
            var route=CitizenNavigation.FindPath(c.Position.Tile,target,Walkable);
            if(route.Count==0) return false;
            var trip=trips[c.Id]; trip.PendingWood=0; trip.Rolled=false; trip.DeliveryId=Guid.NewGuid().ToString("N");
            SetRoute(c,route,CitizenTask.GatheringWoodOutbound);
            return true;
        }
        private void End(Citizen c,Trip trip)
        {
            c.GatheringTask=CitizenTask.Idle; c.Movement=CitizenMovement.Idle; c.Route.Clear();
            c.Destination=c.Position; c.Pause=1;
            trip.PendingWood=0; trip.Rolled=false;
            trip.Cooldown=bootstrap.Settings.MinimumCooldown+random.NextDouble()*(bootstrap.Settings.MaximumCooldown-bootstrap.Settings.MinimumCooldown);
        }
        // True means this task owns this frame's movement. Ordinary policy remains unchanged.
        public bool TickCitizen(Citizen c,double seconds,double speed)
        {
            var trip=trips[c.Id];
            if(c.Movement==CitizenMovement.Recovering||!c.Active) return false;
            if(c.GatheringTask==CitizenTask.Recovery)
            {
                if(!ReturnToHall(c)) return true; // keep pending delivery, retry after access is restored
            }
            if(!HasTrip(c))
            {
                trip.Cooldown-=seconds;
                if(trip.Cooldown>0||!IsEligible(c)||simulation.Citizens.Count(HasTrip)>=bootstrap.Settings.MaximumGatherers||!TryBegin(c)) return false;
            }
            if(!IsEligible(c)&&c.GatheringTask!=CitizenTask.ReturningWood)
            {
                if(!ReturnToHall(c)) { c.GatheringTask=CitizenTask.Recovery; return true; }
            }
            if(c.GatheringTask==CitizenTask.GatheringWood)
            {
                trip.Pause-=seconds;
                if(trip.Pause>0) return true;
                if(!trip.Rolled)
                {
                    SuccessRolls++; trip.Rolled=true;
                    trip.PendingWood=random.NextDouble()<bootstrap.Settings.SuccessChance?bootstrap.Settings.WoodYield:0;
                }
                if(!ReturnToHall(c)) c.GatheringTask=CitizenTask.Recovery;
                return true;
            }
            double remaining=seconds;
            while(c.Route.Count>0)
            {
                var target=c.Route.Peek(); double dx=target.X-c.Position.X,dz=target.Z-c.Position.Z;
                double distance=Math.Sqrt(dx*dx+dz*dz),amount=Math.Min(distance,remaining*speed);
                var next=distance==0?target:new CitizenPosition(c.Position.X+dx/distance*amount,c.Position.Z+dz/distance*amount);
                if(!Area.CanOccupy(next)) { c.GatheringTask=CitizenTask.Recovery; c.Route.Clear(); c.Movement=CitizenMovement.Idle; return true; }
                c.Position=next; remaining-=amount/speed;
                if(amount<distance) return true;
                c.Route.Dequeue(); if(remaining<=0&&c.Route.Count>0) return true;
            }
            c.Movement=CitizenMovement.Idle;
            if(c.GatheringTask==CitizenTask.GatheringWoodOutbound)
            {
                c.GatheringTask=CitizenTask.GatheringWood; trip.Pause=bootstrap.Settings.GatheringPause;
            }
            else if(c.GatheringTask==CitizenTask.ReturningWood)
            {
                if(c.Position.DistanceSquared(CitizenPosition.Center(trip.ReturnAccess))<1e-10)
                {
                    if(bootstrap.Resources.TryTransaction("ambient-delivery-"+trip.DeliveryId,ResourceType.Wood,trip.PendingWood))
                        End(c,trip);
                }
            }
            return true;
        }
    }
}